using Tinadec.AgentCore.Services;

namespace Tinadec.AgentCore.Tests;

public sealed class SecretProtectorTests
{
    [Fact]
    public void RoundTripsSecretsWithoutLoggingPlaintext()
    {
        var protector = new SecretProtector();

        var protectedSecret = protector.Protect("sk-test-secret");

        Assert.NotEqual("sk-test-secret", protectedSecret);
        Assert.Equal("sk-test-secret", protector.Unprotect(protectedSecret));
    }
}
