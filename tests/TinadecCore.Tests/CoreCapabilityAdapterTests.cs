using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;
using TinadecCore.Services;

namespace TinadecCore.Tests;

public sealed class CoreCapabilityAdapterTests
{
    [Fact]
    public void CodexCapabilityProviderRegistersKernelBackedCapabilities()
    {
        var provider = new CodexCapabilityProvider();

        var capabilities = provider.ListCapabilities();

        Assert.Contains(capabilities, tool => tool.Id == "search_files" && tool.Source == "codex-rust");
        Assert.Contains(capabilities, tool => tool.Id == "read_file" && tool.Capabilities.Contains("codex-rust.active"));
        Assert.Contains(capabilities, tool => tool.Id == "grep_content" && tool.Risk == "read-only");
        Assert.Contains(capabilities, tool => tool.Id == "apply_patch" && tool.RequiresApproval);
        Assert.Contains(capabilities, tool => tool.Id == "sandbox_exec" && tool.RequiresApproval);
    }

    [Fact]
    public void CapabilityPolicyKeepsReadOnlyAutomaticAndMutatingApprovalGated()
    {
        var policy = new CapabilityPolicyService();
        var provider = new CodexCapabilityProvider();
        var readFile = provider.ListCapabilities().Single(tool => tool.Id == "read_file");
        var applyPatch = provider.ListCapabilities().Single(tool => tool.Id == "apply_patch");

        Assert.False(policy.Evaluate("approval", readFile).Required);
        Assert.True(policy.IsReadOnly(readFile));
        Assert.True(policy.Evaluate("approval", applyPatch).Required);
        Assert.False(policy.IsReadOnly(applyPatch));
    }

    [Fact]
    public async Task CodexInvocationAdapterTranslatesCoreInvocationToCodeClient()
    {
        var client = new RecordingCodeToolClient();
        var adapter = new CodexToolInvocationAdapter(client);
        var tool = new CodexCapabilityProvider().ListCapabilities().Single(item => item.Id == "read_file");
        var request = new CodeToolExecuteRequest("sess_1", "run_1", "node_1", null, "D:\\repo", new Dictionary<string, object?>());

        var result = await adapter.InvokeAsync(tool, request);

        Assert.True(adapter.CanInvoke(tool));
        Assert.Equal("native", result.Status);
        Assert.Equal(tool.Id, client.ToolId);
        Assert.Equal(request.RunId, client.Request?.RunId);
    }

    [Fact]
    public async Task CodeInvocationAdapterAcceptsCodeSuiteTools()
    {
        var client = new RecordingCodeToolClient();
        var adapter = new CodexToolInvocationAdapter(client);
        var tool = new CodeCapabilityProvider().ListCapabilities().Single(item => item.Id == "project_template_scaffold");
        var request = new CodeToolExecuteRequest("sess_1", "run_1", "node_1", null, "D:\\repo", new Dictionary<string, object?>());

        var result = await adapter.InvokeAsync(tool, request);

        Assert.True(adapter.CanInvoke(tool));
        Assert.Equal("native", result.Status);
        Assert.Equal("project_template_scaffold", client.ToolId);
    }

    private sealed class RecordingCodeToolClient : ICodeToolClient
    {
        public string? ToolId { get; private set; }
        public CodeToolExecuteRequest? Request { get; private set; }

        public Task<CodeToolExecuteResultDto> ExecuteAsync(
            ToolDescriptorDto tool,
            CodeToolExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            ToolId = tool.Id;
            Request = request;
            return Task.FromResult(new CodeToolExecuteResultDto(
                tool.Id,
                "native",
                "Recorded Codex invocation.",
                ["adapter:codex-rust"],
                new Dictionary<string, object?>(),
                false,
                null));
        }
    }
}
