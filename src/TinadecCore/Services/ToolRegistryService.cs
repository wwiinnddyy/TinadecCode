using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;

namespace Tinadec.AgentCore.Services;

public sealed class ToolRegistryService : IToolRegistry
{
    private static readonly IReadOnlyList<ToolDescriptorDto> BuiltinTools =
    [
        new(
            "search_files",
            "Search Files",
            "programming",
            "code",
            "read-only",
            false,
            "/api/v1/code/tools/search_files/execute",
            ["file.search", "workspace.read", "codex-rust.future"]),
        new(
            "sandbox_exec",
            "Sandbox Exec",
            "programming",
            "code",
            "shell",
            true,
            "/api/v1/code/tools/sandbox_exec/execute",
            ["shell.approved", "test.run", "codex-rust.future"]),
        new(
            "apply_patch",
            "Apply Patch",
            "programming",
            "code",
            "workspace-write",
            true,
            "/api/v1/code/tools/apply_patch/execute",
            ["file.write.approved", "patch.apply", "codex-rust.future"]),
        new(
            "review_format",
            "Review Format",
            "programming",
            "code",
            "read-only",
            false,
            "/api/v1/code/tools/review_format/execute",
            ["review.format", "workspace.read", "codex-rust.future"])
    ];

    public IReadOnlyList<ToolDescriptorDto> ListTools(string? domain = null)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return BuiltinTools;
        }

        return BuiltinTools
            .Where(tool => string.Equals(tool.Domain, domain, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public ToolDescriptorDto? Resolve(string toolId)
    {
        return BuiltinTools.FirstOrDefault(tool =>
            string.Equals(tool.Id, toolId, StringComparison.OrdinalIgnoreCase));
    }
}
