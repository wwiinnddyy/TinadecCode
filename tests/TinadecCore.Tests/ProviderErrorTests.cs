using Tinadec.Contracts.Models;
using TinadecCore.Services;

namespace TinadecCore.Tests;

public sealed class ProviderErrorTests
{
    [Theory]
    [InlineData(401, ProviderErrorCategory.AuthenticationFailed)]
    [InlineData(429, ProviderErrorCategory.RateLimited)]
    [InlineData(503, ProviderErrorCategory.ProviderUnavailable)]
    [InlineData(408, ProviderErrorCategory.Timeout)]
    [InlineData(422, ProviderErrorCategory.InvalidRequest)]
    public void MapsHttpStatusCodesToExpectedCategories(int statusCode, ProviderErrorCategory expected)
    {
        var details = ProviderErrorMapper.FromHttpStatus("provider-openai", statusCode);

        Assert.Equal(expected, details.Category);
        Assert.Equal(statusCode, details.StatusCode);
        Assert.Null(details.ExitCode);
        Assert.Equal("provider-openai", details.ProviderId);
    }

    [Fact]
    public void TimeoutCategoryIsRetryable()
    {
        Assert.True(ProviderErrorMapper.IsRetryable(ProviderErrorCategory.Timeout));
    }

    [Fact]
    public void ProviderUnavailableIsRetryable()
    {
        var details = ProviderErrorMapper.FromHttpStatus("provider-openai", 503);

        Assert.Equal(ProviderErrorCategory.ProviderUnavailable, details.Category);
        Assert.True(details.Retryable);
    }

    [Fact]
    public void RateLimitedIsRetryableAndAuthenticationFailedIsNotRetryable()
    {
        Assert.True(ProviderErrorMapper.IsRetryable(ProviderErrorCategory.RateLimited));
        Assert.False(ProviderErrorMapper.IsRetryable(ProviderErrorCategory.AuthenticationFailed));
    }

    [Fact]
    public void MapsCliExitCodeToAuthenticationFailure()
    {
        var details = ProviderErrorMapper.FromCliExitCode("provider-cli", 77);

        Assert.Equal(ProviderErrorCategory.AuthenticationFailed, details.Category);
        Assert.False(details.Retryable);
        Assert.Equal(77, details.ExitCode);
        Assert.Null(details.StatusCode);
    }
}
