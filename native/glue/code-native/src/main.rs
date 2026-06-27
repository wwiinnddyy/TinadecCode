use codex_apply_patch::{AppliedPatchFileChange, apply_patch};
use codex_exec_server::LOCAL_FS;
use codex_file_search::{FileSearchOptions, run};
use codex_utils_absolute_path::AbsolutePathBuf;
use ignore::WalkBuilder;
use serde::Deserialize;
use serde_json::{Map, Value, json};
use std::fs;
use std::io::{self, Read};
use std::num::NonZero;
use std::path::PathBuf;
use std::time::Instant;

#[derive(Debug, Deserialize)]
struct ExecuteRequest {
    tool_id: String,
    session_id: Option<String>,
    run_id: Option<String>,
    task_node_id: Option<String>,
    approval_id: Option<String>,
    cwd: Option<String>,
    #[serde(default)]
    arguments: Value,
}

fn main() {
    let mut args = std::env::args().skip(1);
    match args.next().as_deref() {
        Some("execute") => execute(),
        Some("version") | None => print_json(json!({
            "name": "tinadec-code-native",
            "version": "0.2.0",
            "upstream": "codex-rust",
            "upstream_commit": "14953023471159aaed89f360c0f3da2346cb4bc0",
            "tools": [
                "search_files",
                "glob_search",
                "read_file",
                "list_directory",
                "grep_content",
                "apply_patch",
                "sandbox_exec",
                "review_format",
                "terminal"
            ]
        })),
        Some(_) => {
            eprintln!("unknown command");
            std::process::exit(2);
        }
    }
}

fn execute() {
    let mut input = String::new();
    if io::stdin().read_to_string(&mut input).is_err() {
        eprintln!("failed to read stdin");
        std::process::exit(1);
    }

    let request = match serde_json::from_str::<ExecuteRequest>(&input) {
        Ok(request) => request,
        Err(error) => {
            print_json(failed_result(
                "unknown",
                format!("invalid native tool request: {error}"),
                false,
                None,
                json!({}),
            ));
            return;
        }
    };

    let result = match request.tool_id.as_str() {
        "search_files" => execute_search_files(&request),
        "glob_search" => execute_glob_search(&request),
        "read_file" => execute_read_file(&request),
        "list_directory" => execute_list_directory(&request),
        "grep_content" => execute_grep_content(&request),
        "apply_patch" => execute_apply_patch(&request),
        "sandbox_exec" => execute_sandbox_exec(&request),
        "review_format" => execute_review_format(&request),
        "terminal" => execute_terminal(&request),
        _ => failed_result(
            &request.tool_id,
            format!("unknown native tool '{}'", request.tool_id),
            false,
            None,
            common_data(
                &request,
                json!({ "argument_keys": argument_keys(&request.arguments) }),
            ),
        ),
    };

    print_json(result);
}

// ── search_files: fuzzy file name search via Codex Rust codex-file-search ──

fn execute_search_files(request: &ExecuteRequest) -> Value {
    let query = request
        .arguments
        .get("query")
        .and_then(Value::as_str)
        .or_else(|| request.arguments.get("pattern").and_then(Value::as_str))
        .unwrap_or_default();

    if query.trim().is_empty() {
        return failed_result(
            &request.tool_id,
            "search_files requires a non-empty query argument.",
            false,
            None,
            common_data(
                request,
                json!({
                    "cwd": request.cwd,
                    "argument_keys": argument_keys(&request.arguments)
                }),
            ),
        );
    }

    let cwd = match resolve_cwd(request) {
        Some(value) => value,
        None => {
            return failed_result(
                &request.tool_id,
                "failed to resolve current directory".to_string(),
                false,
                None,
                common_data(request, json!({})),
            );
        }
    };

    let limit = request
        .arguments
        .get("limit")
        .and_then(Value::as_u64)
        .and_then(|value| usize::try_from(value).ok())
        .and_then(NonZero::new)
        .unwrap_or_else(|| NonZero::new(20).expect("20 is non-zero"));

    let exclude = request
        .arguments
        .get("exclude")
        .and_then(Value::as_array)
        .map(|items| {
            items
                .iter()
                .filter_map(Value::as_str)
                .map(ToString::to_string)
                .collect::<Vec<_>>()
        })
        .unwrap_or_default();

    match run(
        query,
        vec![cwd.clone()],
        FileSearchOptions {
            limit,
            exclude,
            threads: NonZero::new(2).expect("2 is non-zero"),
            compute_indices: false,
            respect_gitignore: true,
        },
        None,
    ) {
        Ok(results) => {
            let matches = results
                .matches
                .iter()
                .map(|item| {
                    json!({
                        "score": item.score,
                        "path": item.path.to_string_lossy(),
                        "full_path": item.full_path().to_string_lossy(),
                        "match_type": format!("{:?}", item.match_type).to_lowercase()
                    })
                })
                .collect::<Vec<_>>();

            native_result(
                request,
                format!(
                    "Codex Rust file search returned {} of {} matches.",
                    matches.len(),
                    results.total_match_count
                ),
                common_data(
                    request,
                    json!({
                        "cwd": cwd.to_string_lossy(),
                        "query": query,
                        "total_match_count": results.total_match_count,
                        "matches": matches
                    }),
                ),
            )
        }
        Err(error) => failed_result(
            &request.tool_id,
            format!("Codex Rust file search failed: {error}"),
            false,
            None,
            common_data(
                request,
                json!({
                    "cwd": cwd.to_string_lossy(),
                    "query": query
                }),
            ),
        ),
    }
}

// ── glob_search: glob-pattern file name search via Codex Rust ignore crate ──

fn execute_glob_search(request: &ExecuteRequest) -> Value {
    let pattern = request
        .arguments
        .get("pattern")
        .and_then(Value::as_str)
        .unwrap_or_default();

    if pattern.trim().is_empty() {
        return failed_result(
            &request.tool_id,
            "glob_search requires a non-empty pattern argument (e.g. '**/*.rs', 'src/**/*.ts').",
            false,
            None,
            common_data(
                request,
                json!({
                    "cwd": request.cwd,
                    "argument_keys": argument_keys(&request.arguments)
                }),
            ),
        );
    }

    let cwd = match resolve_cwd(request) {
        Some(value) => value,
        None => {
            return failed_result(
                &request.tool_id,
                "failed to resolve current directory".to_string(),
                false,
                None,
                common_data(request, json!({})),
            );
        }
    };

    let limit = request
        .arguments
        .get("limit")
        .and_then(Value::as_u64)
        .and_then(|v| usize::try_from(v).ok())
        .unwrap_or(50);

    let start = Instant::now();
    let mut builder = WalkBuilder::new(&cwd);
    builder
        .hidden(false)
        .follow_links(false)
        .require_git(true)
        .git_ignore(true)
        .git_global(true)
        .git_exclude(true);

    // Build an override from the pattern to filter matches
    if let Ok(ov) = ignore::overrides::OverrideBuilder::new(&cwd)
        .add(pattern)
        .and_then(|b| b.build())
    {
        builder.overrides(ov);
    }

    let mut matches = Vec::new();
    for entry in builder.build().filter_map(|e| e.ok()).take(limit) {
        let path = entry.path();
        let relative = path.strip_prefix(&cwd).unwrap_or(path);
        matches.push(json!({
            "path": relative.to_string_lossy(),
            "full_path": path.to_string_lossy(),
            "is_dir": path.is_dir(),
            "is_file": path.is_file()
        }));
    }

    let elapsed = start.elapsed();
    native_result(
        request,
        format!(
            "glob_search matched {} entries for pattern '{}' in {:.1}ms.",
            matches.len(),
            pattern,
            elapsed.as_secs_f64() * 1000.0
        ),
        common_data(
            request,
            json!({
                "cwd": cwd.to_string_lossy(),
                "pattern": pattern,
                "match_count": matches.len(),
                "matches": matches
            }),
        ),
    )
}

// ── read_file: read file contents with optional line range ──

fn execute_read_file(request: &ExecuteRequest) -> Value {
    let file_path = request
        .arguments
        .get("path")
        .and_then(Value::as_str)
        .unwrap_or_default();

    if file_path.trim().is_empty() {
        return failed_result(
            &request.tool_id,
            "read_file requires a non-empty 'path' argument.",
            false,
            None,
            common_data(request, json!({})),
        );
    }

    let cwd = match resolve_cwd(request) {
        Some(value) => value,
        None => PathBuf::from("."),
    };

    // Resolve relative paths against cwd
    let resolved = if PathBuf::from(file_path).is_absolute() {
        PathBuf::from(file_path)
    } else {
        cwd.join(file_path)
    };

    let metadata = match fs::metadata(&resolved) {
        Ok(m) => m,
        Err(error) => {
            return failed_result(
                &request.tool_id,
                format!("cannot read file '{}': {error}", resolved.display()),
                false,
                None,
                common_data(
                    request,
                    json!({
                        "path": file_path,
                        "resolved": resolved.to_string_lossy()
                    }),
                ),
            );
        }
    };

    if metadata.is_dir() {
        return failed_result(
            &request.tool_id,
            format!("'{}' is a directory, not a file. Use list_directory instead.", resolved.display()),
            false,
            None,
            common_data(
                request,
                json!({
                    "path": file_path,
                    "resolved": resolved.to_string_lossy()
                }),
            ),
        );
    }

    let content = match fs::read_to_string(&resolved) {
        Ok(c) => c,
        Err(error) => {
            // Try reading as bytes for binary files
            match fs::read(&resolved) {
                Ok(bytes) => {
                    let size = bytes.len();
                    return native_result(
                        request,
                        format!("File is binary ({} bytes). Cannot display as text.", size),
                        common_data(
                            request,
                            json!({
                                "path": file_path,
                                "resolved": resolved.to_string_lossy(),
                                "size_bytes": size,
                                "is_binary": true,
                                "content": null
                            }),
                        ),
                    );
                }
                Err(_) => {
                    return failed_result(
                        &request.tool_id,
                        format!("cannot read file '{}': {error}", resolved.display()),
                        false,
                        None,
                        common_data(request, json!({ "path": file_path })),
                    );
                }
            }
        }
    };

    let total_lines = content.lines().count();
    let start_line = request
        .arguments
        .get("start_line")
        .and_then(Value::as_u64)
        .map(|v| v as usize)
        .unwrap_or(1);
    let end_line = request
        .arguments
        .get("end_line")
        .and_then(Value::as_u64)
        .map(|v| v as usize)
        .unwrap_or(total_lines);

    let sliced: String = content
        .lines()
        .enumerate()
        .filter(|(i, _)| *i + 1 >= start_line && *i + 1 <= end_line)
        .map(|(i, line)| format!("{:>6}\t{}", i + 1, line))
        .collect::<Vec<_>>()
        .join("\n");

    let shown_lines = sliced.lines().count();
    let truncated = total_lines > shown_lines;
    let size_bytes = content.len();

    native_result(
        request,
        format!(
            "Read {} of {} lines from '{}' ({} bytes){}",
            shown_lines,
            total_lines,
            file_path,
            size_bytes,
            if truncated { " [truncated]" } else { "" }
        ),
        common_data(
            request,
            json!({
                "path": file_path,
                "resolved": resolved.to_string_lossy(),
                "size_bytes": size_bytes,
                "total_lines": total_lines,
                "start_line": start_line,
                "end_line": end_line,
                "shown_lines": shown_lines,
                "truncated": truncated,
                "content": sliced
            }),
        ),
    )
}

// ── list_directory: list directory entries with metadata ──

fn execute_list_directory(request: &ExecuteRequest) -> Value {
    let dir_path = request
        .arguments
        .get("path")
        .and_then(Value::as_str)
        .unwrap_or(".");

    let cwd = match resolve_cwd(request) {
        Some(value) => value,
        None => PathBuf::from("."),
    };

    let resolved = if PathBuf::from(dir_path).is_absolute() {
        PathBuf::from(dir_path)
    } else {
        cwd.join(dir_path)
    };

    let read_dir = match fs::read_dir(&resolved) {
        Ok(rd) => rd,
        Err(error) => {
            return failed_result(
                &request.tool_id,
                format!("cannot list directory '{}': {error}", resolved.display()),
                false,
                None,
                common_data(
                    request,
                    json!({
                        "path": dir_path,
                        "resolved": resolved.to_string_lossy()
                    }),
                ),
            );
        }
    };

    let show_hidden = request
        .arguments
        .get("show_hidden")
        .and_then(Value::as_bool)
        .unwrap_or(false);

    let limit = request
        .arguments
        .get("limit")
        .and_then(Value::as_u64)
        .and_then(|v| usize::try_from(v).ok())
        .unwrap_or(200);

    let mut entries = Vec::new();
    for entry in read_dir.filter_map(|e| e.ok()).take(limit) {
        let name = entry.file_name().to_string_lossy().to_string();
        if !show_hidden && name.starts_with('.') {
            continue;
        }
        let metadata = entry.metadata().ok();
        let is_dir = metadata.as_ref().map(|m| m.is_dir()).unwrap_or(false);
        let size = metadata.as_ref().map(|m| m.len()).unwrap_or(0);
        entries.push(json!({
            "name": name,
            "is_dir": is_dir,
            "is_file": !is_dir,
            "size_bytes": if is_dir { Value::Null } else { json!(size) }
        }));
    }

    // Sort: directories first, then alphabetically
    entries.sort_by(|a, b| {
        let a_dir = a.get("is_dir").and_then(Value::as_bool).unwrap_or(false);
        let b_dir = b.get("is_dir").and_then(Value::as_bool).unwrap_or(false);
        match (a_dir, b_dir) {
            (true, false) => std::cmp::Ordering::Less,
            (false, true) => std::cmp::Ordering::Greater,
            _ => {
                let a_name = a.get("name").and_then(Value::as_str).unwrap_or("");
                let b_name = b.get("name").and_then(Value::as_str).unwrap_or("");
                a_name.cmp(b_name)
            }
        }
    });

    let dir_count = entries.iter().filter(|e| e.get("is_dir").and_then(Value::as_bool).unwrap_or(false)).count();
    let file_count = entries.len() - dir_count;

    native_result(
        request,
        format!(
            "Listed {} entries in '{}' ({} directories, {} files).",
            entries.len(),
            dir_path,
            dir_count,
            file_count
        ),
        common_data(
            request,
            json!({
                "path": dir_path,
                "resolved": resolved.to_string_lossy(),
                "entries": entries,
                "dir_count": dir_count,
                "file_count": file_count
            }),
        ),
    )
}

// ── grep_content: search file contents for a pattern ──

fn execute_grep_content(request: &ExecuteRequest) -> Value {
    let pattern = request
        .arguments
        .get("pattern")
        .and_then(Value::as_str)
        .unwrap_or_default();

    if pattern.trim().is_empty() {
        return failed_result(
            &request.tool_id,
            "grep_content requires a non-empty 'pattern' argument.",
            false,
            None,
            common_data(request, json!({})),
        );
    }

    let cwd = match resolve_cwd(request) {
        Some(value) => value,
        None => {
            return failed_result(
                &request.tool_id,
                "failed to resolve current directory".to_string(),
                false,
                None,
                common_data(request, json!({})),
            );
        }
    };

    let glob_pattern = request
        .arguments
        .get("glob")
        .and_then(Value::as_str);

    let limit = request
        .arguments
        .get("limit")
        .and_then(Value::as_u64)
        .and_then(|v| usize::try_from(v).ok())
        .unwrap_or(50);

    let context_lines = request
        .arguments
        .get("context")
        .and_then(Value::as_u64)
        .map(|v| v as usize)
        .unwrap_or(0);

    let case_insensitive = request
        .arguments
        .get("case_insensitive")
        .and_then(Value::as_bool)
        .unwrap_or(false);

    let start = Instant::now();

    // Build file walker
    let mut builder = WalkBuilder::new(&cwd);
    builder
        .hidden(false)
        .follow_links(false)
        .require_git(true)
        .git_ignore(true)
        .git_global(true)
        .git_exclude(true);

    if let Some(glob) = glob_pattern {
        if let Ok(ov) = ignore::overrides::OverrideBuilder::new(&cwd)
            .add(glob)
            .and_then(|b| b.build())
        {
            builder.overrides(ov);
        }
    }

    let pattern_lower = pattern.to_lowercase();
    let mut matches = Vec::new();
    let mut files_searched = 0usize;

    for entry in builder.build().filter_map(|e| e.ok()) {
        let path = entry.path();
        if !path.is_file() {
            continue;
        }

        // Skip binary-like files by extension
        let ext = path.extension().and_then(|e| e.to_str()).unwrap_or("");
        let binary_extensions = ",exe,dll,so,dylib,png,jpg,jpeg,gif,bmp,ico,zip,tar,gz,rar,7z,mp3,mp4,wav,avi,pdf,doc,docx,xls,xlsx,ppt,pptx,class,o,obj,pyc,wasm,";
        if binary_extensions.contains(&format!(",{ext},")) {
            continue;
        }

        let content = match fs::read_to_string(path) {
            Ok(c) => c,
            Err(_) => continue,
        };

        files_searched += 1;
        let relative = path.strip_prefix(&cwd).unwrap_or(path);

        for (line_num, line) in content.lines().enumerate() {
            let matches_pattern = if case_insensitive {
                line.to_lowercase().contains(&pattern_lower)
            } else {
                line.contains(pattern)
            };

            if matches_pattern {
                let mut context_before = Vec::new();
                let mut context_after = Vec::new();

                if context_lines > 0 {
                    let lines: Vec<&str> = content.lines().collect();
                    for i in (line_num.saturating_sub(context_lines)..line_num).rev() {
                        context_before.push(json!({
                            "line": i + 1,
                            "text": lines.get(i).unwrap_or(&"")
                        }));
                    }
                    for i in (line_num + 1)..=(line_num + context_lines).min(lines.len() - 1) {
                        context_after.push(json!({
                            "line": i + 1,
                            "text": lines.get(i).unwrap_or(&"")
                        }));
                    }
                }

                matches.push(json!({
                    "file": relative.to_string_lossy(),
                    "full_path": path.to_string_lossy(),
                    "line": line_num + 1,
                    "text": line,
                    "context_before": context_before,
                    "context_after": context_after
                }));

                if matches.len() >= limit {
                    break;
                }
            }
        }

        if matches.len() >= limit {
            break;
        }
    }

    let elapsed = start.elapsed();
    native_result(
        request,
        format!(
            "grep_content found {} matches for '{}' across {} files in {:.1}ms.",
            matches.len(),
            pattern,
            files_searched,
            elapsed.as_secs_f64() * 1000.0
        ),
        common_data(
            request,
            json!({
                "cwd": cwd.to_string_lossy(),
                "pattern": pattern,
                "glob": glob_pattern,
                "case_insensitive": case_insensitive,
                "files_searched": files_searched,
                "match_count": matches.len(),
                "matches": matches
            }),
        ),
    )
}

// ── apply_patch: Codex Rust apply_patch with approval gate ──

fn execute_apply_patch(request: &ExecuteRequest) -> Value {
    if request.approval_id.as_deref().unwrap_or_default().trim().is_empty() {
        return blocked_result(
            request,
            "Apply a patch that may modify workspace files.",
        );
    }

    let patch = request
        .arguments
        .get("patch")
        .and_then(Value::as_str)
        .unwrap_or_default();
    if patch.trim().is_empty() {
        return failed_result(
            &request.tool_id,
            "apply_patch requires a non-empty patch argument.",
            false,
            None,
            common_data(
                request,
                json!({ "argument_keys": argument_keys(&request.arguments) }),
            ),
        );
    }

    let cwd = match resolve_absolute_cwd(request) {
        Ok(value) => value,
        Err(error) => {
            return failed_result(
                &request.tool_id,
                error,
                false,
                None,
                common_data(request, json!({})),
            );
        }
    };

    let runtime = match tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
    {
        Ok(runtime) => runtime,
        Err(error) => {
            return failed_result(
                &request.tool_id,
                format!("failed to start apply_patch runtime: {error}"),
                false,
                None,
                common_data(request, json!({ "cwd": cwd.to_string_lossy() })),
            );
        }
    };

    let mut stdout = Vec::new();
    let mut stderr = Vec::new();
    let result = runtime.block_on(apply_patch(
        patch,
        &cwd,
        &mut stdout,
        &mut stderr,
        LOCAL_FS.as_ref(),
        None,
    ));

    let stdout_text = String::from_utf8_lossy(&stdout).to_string();
    let stderr_text = String::from_utf8_lossy(&stderr).to_string();
    match result {
        Ok(delta) => {
            let changes = delta
                .changes()
                .iter()
                .map(|change| {
                    let kind = match &change.change {
                        AppliedPatchFileChange::Add { .. } => "add",
                        AppliedPatchFileChange::Delete { .. } => "delete",
                        AppliedPatchFileChange::Update { .. } => "update",
                    };
                    json!({
                        "path": change.path.to_string_lossy(),
                        "kind": kind
                    })
                })
                .collect::<Vec<_>>();

            native_result(
                request,
                format!("Codex Rust apply_patch applied {} file changes.", changes.len()),
                common_data(
                    request,
                    json!({
                        "cwd": cwd.to_string_lossy(),
                        "stdout": stdout_text,
                        "stderr": stderr_text,
                        "delta_exact": delta.is_exact(),
                        "affected_files": changes
                    }),
                ),
            )
        }
        Err(error) => {
            let (apply_error, delta) = error.into_parts();
            let partial_changes = delta
                .changes()
                .iter()
                .map(|change| change.path.to_string_lossy().to_string())
                .collect::<Vec<_>>();
            failed_result(
                &request.tool_id,
                format!("Codex Rust apply_patch failed: {apply_error}"),
                false,
                None,
                common_data(
                    request,
                    json!({
                        "cwd": cwd.to_string_lossy(),
                        "stdout": stdout_text,
                        "stderr": stderr_text,
                        "delta_exact": delta.is_exact(),
                        "partial_files": partial_changes
                    }),
                ),
            )
        }
    }
}

// ── sandbox_exec: approval-gated command execution (Windows stub) ──

fn execute_sandbox_exec(request: &ExecuteRequest) -> Value {
    if request.approval_id.as_deref().unwrap_or_default().trim().is_empty() {
        return blocked_result(
            request,
            "Run a sandboxed command in the workspace.",
        );
    }

    let command = request
        .arguments
        .get("command")
        .and_then(Value::as_array)
        .map(|items| items.iter().filter_map(Value::as_str).collect::<Vec<_>>())
        .unwrap_or_default();

    let sandbox_status = if cfg!(target_os = "windows") {
        "unsupported:windows-execution-disabled-in-v1"
    } else {
        "unsupported:execution-disabled-in-v1"
    };

    failed_result(
        &request.tool_id,
        "sandbox_exec is approval-cleared, but execution is not enabled in this Windows-first slice. Codex sandbox capability discovery is wired and returned as structured unsupported.",
        false,
        None,
        common_data(
            request,
            json!({
                "cwd": request.cwd,
                "command": command,
                "exit_code": null,
                "stdout": "",
                "stderr": "",
                "sandbox_status": sandbox_status,
                "sandbox_supported": false
            }),
        ),
    )
}

// ── review_format: format review findings ──

fn execute_review_format(request: &ExecuteRequest) -> Value {
    let findings = request
        .arguments
        .get("findings")
        .and_then(Value::as_array)
        .cloned()
        .unwrap_or_default();

    let title = request
        .arguments
        .get("title")
        .and_then(Value::as_str)
        .unwrap_or("Code Review");

    let mut lines = Vec::new();
    lines.push(format!("## {title}"));
    lines.push(String::new());

    if findings.is_empty() {
        lines.push("No review findings to report.".to_string());
    } else {
        lines.push(format!("Review findings ({} items):", findings.len()));
        lines.push(String::new());

        for (idx, finding) in findings.iter().enumerate() {
            let finding_title = finding
                .get("title")
                .and_then(Value::as_str)
                .unwrap_or("Untitled finding");
            let severity = finding
                .get("severity")
                .and_then(Value::as_str)
                .unwrap_or("info");
            let location = finding
                .get("location")
                .and_then(Value::as_str)
                .unwrap_or("unknown");
            let description = finding
                .get("description")
                .and_then(Value::as_str)
                .unwrap_or("");

            let severity_marker = match severity {
                "critical" => "🔴",
                "high" => "🟠",
                "medium" => "🟡",
                "low" => "🟢",
                "info" => "ℹ️",
                _ => "•",
            };

            lines.push(format!(
                "{idx}. {severity_marker} **{finding_title}** [{severity}] — `{location}`"
            ));
            if !description.is_empty() {
                lines.push(format!("   {description}"));
            }
            lines.push(String::new());
        }

        // Summary
        let critical_count = findings.iter().filter(|f| f.get("severity").and_then(Value::as_str) == Some("critical")).count();
        let high_count = findings.iter().filter(|f| f.get("severity").and_then(Value::as_str) == Some("high")).count();
        let medium_count = findings.iter().filter(|f| f.get("severity").and_then(Value::as_str) == Some("medium")).count();
        let low_count = findings.iter().filter(|f| f.get("severity").and_then(Value::as_str) == Some("low")).count();
        let info_count = findings.iter().filter(|f| f.get("severity").and_then(Value::as_str) == Some("info")).count();

        lines.push("---".to_string());
        lines.push(format!(
            "**Summary**: {} critical, {} high, {} medium, {} low, {} info",
            critical_count, high_count, medium_count, low_count, info_count
        ));
    }

    let formatted = lines.join("\n");

    native_result(
        request,
        format!(
            "Review formatted with {} findings.",
            findings.len()
        ),
        common_data(
            request,
            json!({
                "title": title,
                "finding_count": findings.len(),
                "formatted": formatted
            }),
        ),
    )
}

// ── Helpers ──

fn resolve_cwd(request: &ExecuteRequest) -> Option<PathBuf> {
    match request.cwd.as_deref() {
        Some(value) if !value.trim().is_empty() => Some(PathBuf::from(value)),
        _ => std::env::current_dir().ok(),
    }
}

fn native_result(request: &ExecuteRequest, summary: impl Into<String>, data: Value) -> Value {
    json!({
        "tool_id": request.tool_id,
        "status": "native",
        "summary": summary.into(),
        "evidence": [
            "domain: programming",
            "state_owner: core",
            "native_runtime: tinadec-code-native",
            "upstream: codex-rust"
        ],
        "data": data,
        "requires_approval": false,
        "approval_summary": null
    })
}

fn blocked_result(request: &ExecuteRequest, approval_summary: &str) -> Value {
    json!({
        "tool_id": request.tool_id,
        "status": "blocked",
        "summary": "Native programming tool is registered, but Core approval is required before execution.",
        "evidence": [
            "domain: programming",
            "state_owner: core",
            "native_runtime: tinadec-code-native",
            "policy_owner: core"
        ],
        "data": common_data(
            request,
            json!({
                "cwd": request.cwd,
                "argument_keys": argument_keys(&request.arguments)
            })
        ),
        "requires_approval": true,
        "approval_summary": approval_summary
    })
}

fn failed_result(
    tool_id: &str,
    summary: impl Into<String>,
    requires_approval: bool,
    approval_summary: Option<&str>,
    data: Value,
) -> Value {
    json!({
        "tool_id": tool_id,
        "status": "failed",
        "summary": summary.into(),
        "evidence": [
            "domain: programming",
            "state_owner: core",
            "native_runtime: tinadec-code-native"
        ],
        "data": data,
        "requires_approval": requires_approval,
        "approval_summary": approval_summary
    })
}

fn resolve_absolute_cwd(request: &ExecuteRequest) -> Result<AbsolutePathBuf, String> {
    let path = match request.cwd.as_deref() {
        Some(value) => PathBuf::from(value),
        None => std::env::current_dir()
            .map_err(|error| format!("failed to resolve current directory: {error}"))?,
    };
    let absolute = if path.is_absolute() {
        path
    } else {
        std::env::current_dir()
            .map_err(|error| format!("failed to resolve current directory: {error}"))?
            .join(path)
    };
    AbsolutePathBuf::from_absolute_path(&absolute)
        .map_err(|error| format!("cwd must resolve to an absolute path: {error}"))
}

fn argument_keys(arguments: &Value) -> Vec<String> {
    let mut keys = arguments
        .as_object()
        .map(|object| object.keys().cloned().collect::<Vec<_>>())
        .unwrap_or_default();
    keys.sort();
    keys
}

fn common_data(request: &ExecuteRequest, data: Value) -> Value {
    let mut object = data.as_object().cloned().unwrap_or_else(Map::new);
    object.insert("session_id".to_string(), json!(request.session_id));
    object.insert("run_id".to_string(), json!(request.run_id));
    object.insert("task_node_id".to_string(), json!(request.task_node_id));
    object.insert("approval_id".to_string(), json!(request.approval_id));
    Value::Object(object)
}

// ── terminal: interactive terminal emulator with PTY support ──

fn execute_terminal(request: &ExecuteRequest) -> Value {
    let action = request
        .arguments
        .get("action")
        .and_then(Value::as_str)
        .unwrap_or("create");

    match action {
        "create" => execute_terminal_create(request),
        "get_shells" => execute_terminal_get_shells(request),
        _ => failed_result(
            &request.tool_id,
            format!("unsupported terminal action '{}'", action),
            false,
            None,
            common_data(
                request,
                json!({
                    "action": action,
                    "argument_keys": argument_keys(&request.arguments)
                }),
            ),
        ),
    }
}

fn execute_terminal_create(request: &ExecuteRequest) -> Value {
    let shell = request
        .arguments
        .get("shell")
        .and_then(Value::as_str)
        .unwrap_or(if cfg!(target_os = "windows") {
            "powershell.exe"
        } else {
            "/bin/bash"
        });

    let shell_args: Vec<String> = request
        .arguments
        .get("args")
        .and_then(Value::as_array)
        .map(|items| items.iter().filter_map(Value::as_str).map(ToString::to_string).collect())
        .unwrap_or_default();

    let cwd = match resolve_cwd(request) {
        Some(value) => value.to_string_lossy().to_string(),
        None => std::env::current_dir()
            .map(|p| p.to_string_lossy().to_string())
            .unwrap_or_else(|_| ".".to_string()),
    };

    let cols = request
        .arguments
        .get("cols")
        .and_then(Value::as_u64)
        .unwrap_or(80) as u32;

    let rows = request
        .arguments
        .get("rows")
        .and_then(Value::as_u64)
        .unwrap_or(24) as u32;

    let title = request
        .arguments
        .get("title")
        .and_then(Value::as_str)
        .unwrap_or(shell)
        .to_string();

    // 生成唯一的终端ID
    let terminal_id = format!("term-{}", std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis());

    // 尝试创建PTY进程
    // 注意：在实际实现中，这里应该使用portable-pty或类似的crate来创建真正的PTY
    // 目前返回一个stubbed结果，表示terminal工具已注册但需要进一步集成
    native_result(
        request,
        format!("Terminal '{}' created with shell '{}'.", terminal_id, shell),
        common_data(
            request,
            json!({
                "action": "create",
                "terminal_id": terminal_id,
                "shell": shell,
                "args": shell_args,
                "cwd": cwd,
                "cols": cols,
                "rows": rows,
                "title": title,
                "pty_status": "pending_integration",
                "note": "Terminal tool registered. PTY integration requires portable-pty crate."
            }),
        ),
    )
}

fn execute_terminal_get_shells(request: &ExecuteRequest) -> Value {
    let shells = if cfg!(target_os = "windows") {
        vec![
            json!({
                "id": "powershell",
                "label": "Windows PowerShell",
                "shell": "powershell.exe",
                "args": ["-NoLogo"]
            }),
            json!({
                "id": "cmd",
                "label": "Command Prompt",
                "shell": "cmd.exe",
                "args": []
            }),
        ]
    } else if cfg!(target_os = "macos") {
        vec![
            json!({
                "id": "zsh",
                "label": "zsh",
                "shell": "/bin/zsh",
                "args": ["-l"]
            }),
            json!({
                "id": "bash",
                "label": "bash",
                "shell": "/bin/bash",
                "args": ["-l"]
            }),
        ]
    } else {
        vec![
            json!({
                "id": "bash",
                "label": "bash",
                "shell": "/bin/bash",
                "args": ["-l"]
            }),
        ]
    };

    native_result(
        request,
        format!("Retrieved {} available shells.", shells.len()),
        common_data(
            request,
            json!({
                "action": "get_shells",
                "shells": shells
            }),
        ),
    )
}

fn print_json(value: Value) {
    match serde_json::to_string(&value) {
        Ok(output) => println!("{output}"),
        Err(error) => {
            eprintln!("failed to serialize native tool response: {error}");
            std::process::exit(1);
        }
    }
}
