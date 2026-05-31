using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;

namespace TinadecCore.Services;

public sealed class CodexCapabilityProvider : ICapabilityProvider
{
    public string Id => "codex-rust";

    private static readonly IReadOnlyList<ToolDescriptorDto> CodexTools =
    [
        new(
            "search_files",
            "Search Files",
            "programming",
            "codex-rust",
            "read-only",
            false,
            "/api/v1/code/tools/search_files/execute",
            ["file.search", "workspace.read", "codex-rust.future"]),
        new(
            "glob_search",
            "Glob Search",
            "programming",
            "codex-rust",
            "read-only",
            false,
            "/api/v1/code/tools/glob_search/execute",
            ["file.glob", "workspace.read", "codex-rust.future"]),
        new(
            "read_file",
            "Read File",
            "programming",
            "codex-rust",
            "read-only",
            false,
            "/api/v1/code/tools/read_file/execute",
            ["file.read", "workspace.read", "codex-rust.active"]),
        new(
            "list_directory",
            "List Directory",
            "programming",
            "codex-rust",
            "read-only",
            false,
            "/api/v1/code/tools/list_directory/execute",
            ["directory.list", "workspace.read", "codex-rust.active"]),
        new(
            "grep_content",
            "Grep Content",
            "programming",
            "codex-rust",
            "read-only",
            false,
            "/api/v1/code/tools/grep_content/execute",
            ["file.grep", "workspace.read", "codex-rust.active"]),
        new(
            "sandbox_exec",
            "Sandbox Exec",
            "programming",
            "codex-rust",
            "shell",
            true,
            "/api/v1/code/tools/sandbox_exec/execute",
            ["shell.approved", "test.run", "codex-rust.future"]),
        new(
            "apply_patch",
            "Apply Patch",
            "programming",
            "codex-rust",
            "workspace-write",
            true,
            "/api/v1/code/tools/apply_patch/execute",
            ["file.write.approved", "patch.apply", "codex-rust.active"]),
        new(
            "review_format",
            "Review Format",
            "programming",
            "codex-rust",
            "read-only",
            false,
            "/api/v1/code/tools/review_format/execute",
            ["review.format", "workspace.read", "codex-rust.active"])
    ];

    public IReadOnlyList<ToolDescriptorDto> ListCapabilities() => CodexTools;
}

public sealed class CodeCapabilityProvider : ICapabilityProvider
{
    public string Id => "code";

    private static readonly string[] RuntimeCapabilities =
    [
        "runtime.nodejs",
        "runtime.bun",
        "runtime.golang",
        "runtime.flutter",
        "runtime.python",
        "runtime.rust",
        "runtime.zig",
        "runtime.nim",
        "runtime.csharp",
        "runtime.java"
    ];

    private static readonly IReadOnlyList<ToolDescriptorDto> CodeTools =
    [
        new(
            "project_templates",
            "Project Templates",
            "programming",
            "code",
            "read-only",
            false,
            "/api/v1/code/tools/project_templates/execute",
            ["project.template", "project.preview", "tool-layer.code-suite", .. RuntimeCapabilities]),
        new(
            "project_template_scaffold",
            "Project Template Scaffold",
            "programming",
            "code",
            "workspace-write",
            true,
            "/api/v1/code/tools/project_template_scaffold/execute",
            ["project.scaffold", "file.write.approved", "tool-layer.code-suite", .. RuntimeCapabilities]),
        new(
            "language_runtime_probe",
            "Language Runtime Probe",
            "programming",
            "code",
            "read-only",
            false,
            "/api/v1/code/tools/language_runtime_probe/execute",
            ["runtime.probe", "tool-layer.code-suite", .. RuntimeCapabilities]),
        new(
            "bash_environment",
            "Bash-like Environment",
            "programming",
            "code",
            "shell",
            true,
            "/api/v1/code/tools/bash_environment/execute",
            ["shell.approved", "process.exec", "env.vars", "tool-layer.code-suite"]),
        new(
            "debug_session",
            "Built-in Debug Session",
            "programming",
            "code",
            "shell",
            true,
            "/api/v1/code/tools/debug_session/execute",
            ["debug.run", "debug.breakpoint", "trace.capture", "tool-layer.code-suite"]),
        new(
            "code_editor",
            "Built-in Code Editor",
            "programming",
            "code",
            "workspace-write",
            true,
            "/api/v1/code/tools/code_editor/execute",
            ["editor.open", "editor.diff", "file.write.approved", "tool-layer.code-suite"]),
        new(
            "git_worktree_manager",
            "Git Worktree Manager",
            "programming",
            "code",
            "git-write",
            true,
            "/api/v1/code/tools/git_worktree_manager/execute",
            ["git.worktree", "git.branch", "workspace.isolation", "tool-layer.code-suite"])
    ];

    public IReadOnlyList<ToolDescriptorDto> ListCapabilities() => CodeTools;
}

public sealed class ToolRegistryService : IToolRegistry
{
    private readonly IReadOnlyList<ICapabilityProvider> _providers;

    public ToolRegistryService()
        : this([new CodexCapabilityProvider(), new CodeCapabilityProvider()])
    {
    }

    public ToolRegistryService(IEnumerable<ICapabilityProvider> providers)
    {
        _providers = providers.ToArray();
    }

    public IReadOnlyList<ToolDescriptorDto> ListTools(string? domain = null)
    {
        var tools = _providers.SelectMany(provider => provider.ListCapabilities()).ToArray();
        if (string.IsNullOrWhiteSpace(domain))
        {
            return tools;
        }

        return tools
            .Where(tool => string.Equals(tool.Domain, domain, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public ToolDescriptorDto? Resolve(string toolId)
    {
        return ListTools().FirstOrDefault(tool =>
            string.Equals(tool.Id, toolId, StringComparison.OrdinalIgnoreCase));
    }
}
