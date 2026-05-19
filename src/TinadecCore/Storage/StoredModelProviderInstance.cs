using Tinadec.Contracts.Models;

namespace Tinadec.AgentCore.Storage;

public sealed record StoredModelProviderInstance(
    string Id,
    string Driver,
    string DisplayName,
    string ConnectionKind,
    string? BaseUrl,
    string? Model,
    string? EncryptedApiKey,
    string? BinaryPath,
    string? HomePath,
    string? ServerUrl,
    string? LaunchArgs,
    IReadOnlyList<string> Capabilities,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool HasApiKey => !string.IsNullOrWhiteSpace(EncryptedApiKey);

    public ModelProviderInstanceDto ToDto()
    {
        var (status, message) = ResolveStatus();
        return new ModelProviderInstanceDto(
            Id,
            Driver,
            DisplayName,
            ConnectionKind,
            BaseUrl,
            Model,
            HasApiKey,
            BinaryPath,
            HomePath,
            ServerUrl,
            LaunchArgs,
            Capabilities,
            Enabled,
            status,
            message,
            CreatedAt,
            UpdatedAt);
    }

    public StoredModelSettings ToModelSettings(string? routeModel = null)
    {
        return new StoredModelSettings(BaseUrl ?? string.Empty, routeModel ?? Model ?? string.Empty, EncryptedApiKey, UpdatedAt);
    }

    private (string Status, string Message) ResolveStatus()
    {
        if (!Enabled)
        {
            return ("disabled", "Provider is disabled.");
        }

        if (ConnectionKind.Equals("cli", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(BinaryPath)
                ? ("not_configured", "CLI binary path is required.")
                : ("ready", "CLI provider is configured.");
        }

        if (string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrWhiteSpace(Model))
        {
            return ("not_configured", "Base URL and model are required.");
        }

        if (ConnectionKind.Equals("api-key", StringComparison.OrdinalIgnoreCase) && !HasApiKey)
        {
            return ("needs_key", "API key is not set.");
        }

        return ("ready", "Provider is ready.");
    }
}
