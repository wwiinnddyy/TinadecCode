using Tinadec.Contracts.Security;

namespace Tinadec.Contracts.Tests;

public sealed class PermissionPolicyTests
{
    [Fact]
    public void ApprovalModeRequiresApprovalForShell()
    {
        var result = PermissionPolicy.Evaluate(PermissionMode.Approval, ToolRisk.Shell);

        Assert.True(result.Required);
        Assert.Contains("Approval mode", result.Reason);
    }

    [Fact]
    public void ReadOnlyToolsDoNotRequireApprovalByDefault()
    {
        var result = PermissionPolicy.Evaluate(PermissionMode.Approval, ToolRisk.ReadOnly);

        Assert.False(result.Required);
    }
}
