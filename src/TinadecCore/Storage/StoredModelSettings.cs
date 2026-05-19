using Tinadec.Contracts.Models;

namespace Tinadec.AgentCore.Storage;

public sealed record StoredModelSettings(
    string BaseUrl,
    string Model,
    string? EncryptedApiKey,
    DateTimeOffset UpdatedAt)
{
    public ModelSettingsDto ToDto()
    {
        return new ModelSettingsDto(BaseUrl, Model, !string.IsNullOrWhiteSpace(EncryptedApiKey), UpdatedAt);
    }
}
