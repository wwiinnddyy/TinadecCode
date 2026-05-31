using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;

namespace TinadecCore.Services;

public sealed class CodexRuntimeKernelAdapter : IRuntimeKernelAdapter
{
    public string Id => "codex-rust";
    public string DisplayName => "Codex Rust Kernel";
    public IReadOnlyList<string> Capabilities { get; } =
    [
        "file.search",
        "file.glob",
        "file.read",
        "directory.list",
        "file.grep",
        "patch.apply",
        "shell.approved",
        "review.format"
    ];
}

public sealed class CodexToolInvocationAdapter(ICodeToolClient codeToolClient) : IToolInvocationAdapter
{
    public string Id => "codex-rust";

    public bool CanInvoke(ToolDescriptorDto tool)
    {
        return string.Equals(tool.Source, Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tool.Source, "code", StringComparison.OrdinalIgnoreCase);
    }

    public Task<CodeToolExecuteResultDto> InvokeAsync(
        ToolDescriptorDto tool,
        CodeToolExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        return codeToolClient.ExecuteAsync(tool, request, cancellationToken);
    }
}
