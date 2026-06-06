using Tinadec.Contracts.Models;
using TinadecCore.Services;

namespace TinadecCore.Tests;

public sealed class ModelProviderCatalogTests
{
    [Fact]
    public void OpenAiCompatibleTemplateExposesCapabilityMetadata()
    {
        var template = GetTemplate("openai-compatible");

        Assert.Equal("openai-compatible", template.ProviderFamily);
        Assert.Equal("openai-compatible", template.Driver);
        Assert.Equal("http", template.ConnectionKind);
        Assert.Equal("api_key", template.CredentialKind);
        Assert.True(template.Capabilities.SupportsStreaming);
        Assert.True(template.Capabilities.SupportsTools);
        Assert.True(template.Capabilities.SupportsJsonMode);
        Assert.True(template.Capabilities.SupportsSystemPrompt);
        Assert.False(template.Capabilities.RequiresWorkspace);
    }

    [Fact]
    public void AnthropicTemplateIsDistinctFromOpenAiCompatible()
    {
        var template = GetTemplate("anthropic");

        Assert.Equal("anthropic", template.ProviderFamily);
        Assert.Equal("anthropic", template.Driver);
        Assert.NotEqual("openai-compatible", template.ProviderFamily);
        Assert.Equal("api_key", template.CredentialKind);
        Assert.True(template.Capabilities.SupportsStreaming);
        Assert.True(template.Capabilities.SupportsTools);
        Assert.True(template.Capabilities.SupportsJsonMode);
        Assert.True(template.Capabilities.SupportsSystemPrompt);
        Assert.False(template.Capabilities.RequiresWorkspace);
    }

    [Fact]
    public void LocalHttpTemplateExposesLocalConnectionCapabilities()
    {
        var template = GetTemplate("local-http");

        Assert.Equal("local-http", template.ProviderFamily);
        Assert.Equal("local-http", template.Driver);
        Assert.Equal("http", template.ConnectionKind);
        Assert.Equal("none", template.CredentialKind);
        Assert.True(template.Capabilities.SupportsStreaming);
        Assert.False(template.Capabilities.SupportsTools);
        Assert.False(template.Capabilities.SupportsJsonMode);
        Assert.True(template.Capabilities.SupportsSystemPrompt);
        Assert.False(template.Capabilities.RequiresWorkspace);
    }

    [Fact]
    public void CodexCliTemplateRequiresWorkspaceAndSupportsTools()
    {
        var template = GetTemplate("codex-cli");

        Assert.Equal("codex-cli", template.ProviderFamily);
        Assert.Equal("codex-cli", template.Driver);
        Assert.Equal("cli", template.ConnectionKind);
        Assert.Equal("cli", template.CredentialKind);
        Assert.False(template.Capabilities.SupportsStreaming);
        Assert.True(template.Capabilities.SupportsTools);
        Assert.False(template.Capabilities.SupportsJsonMode);
        Assert.True(template.Capabilities.SupportsSystemPrompt);
        Assert.True(template.Capabilities.RequiresWorkspace);
    }

    [Fact]
    public void CatalogTemplatesExposeUniqueDrivers()
    {
        var drivers = ModelProviderCatalog.ListTemplates().Select(template => template.Driver).ToArray();

        Assert.Equal(drivers.Length, drivers.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static ModelProviderTemplateDto GetTemplate(string driver)
    {
        return Assert.Single(ModelProviderCatalog.ListTemplates(), template => template.Driver.Equals(driver, StringComparison.OrdinalIgnoreCase));
    }
}
