using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;
using TinadecCore.Storage;
using TinadecCore.Tracing;

namespace TinadecCore.Services;

public sealed class ModelRouteResolver(CoreStore store) : IModelRouteResolver
{
    public ResolvedModelInvocationContextDto Resolve(string purpose)
    {
        using var activity = TinadecActivitySource.Instance.StartActivity(SpanNames.ModelRouteSelection);
        activity?.SetTag(SpanAttrs.RoutePurpose, purpose);

        var route = store.GetModelRoute(purpose);
        var provider = ResolveProvider(purpose);
        activity?
            .SetTag(SpanAttrs.ProviderId, provider?.Id)
            .SetTag(SpanAttrs.ProviderInstanceId, provider?.Id);

        if (provider is null)
        {
            activity?.SetTag(SpanAttrs.Status, "failed");
            activity?.SetError("No enabled model provider can serve route purpose.");
            throw new InvalidOperationException($"No enabled model provider can serve route purpose '{purpose}'.");
        }

        var effective = provider.ToModelSettings(route?.ProviderInstanceId == provider.Id ? route.Model : null);
        var providerDto = provider.ToDto();
        activity?
            .SetTag(SpanAttrs.Model, effective.Model)
            .SetTag(SpanAttrs.ConnectionKind, provider.ConnectionKind)
            .SetTag(SpanAttrs.Driver, provider.Driver)
            .SetTag(SpanAttrs.HealthStatus, ResolveHealthStatus(provider).ToString())
            .SetTag(SpanAttrs.Status, "selected");

        return new ResolvedModelInvocationContextDto(
            purpose,
            route,
            providerDto,
            effective.BaseUrl,
            effective.Model,
            effective.EncryptedApiKey,
            provider.Driver,
            provider.ConnectionKind,
            provider.Id,
            route is null);
    }

    private StoredModelProviderInstance? ResolveProvider(string purpose)
    {
        var route = store.GetModelRoute(purpose);
        var providers = store.ListModelProviderInstances()
            .Select((provider, index) => new ProviderRouteCandidate(
                store.GetStoredModelProviderInstance(provider.Id),
                index,
                null))
            .Where(candidate => candidate.Provider is not null)
            .Select(candidate => candidate with { Priority = ResolvePriority(candidate.Provider!) })
            .ToArray();
        var now = ResolveClock(providers) ?? DateTimeOffset.UtcNow;

        if (route is not null)
        {
            var routedProvider = providers
                .FirstOrDefault(candidate => candidate.Provider!.Id.Equals(route.ProviderInstanceId, StringComparison.OrdinalIgnoreCase)
                    && CanServe(candidate.Provider!, purpose, now))
                ?.Provider;
            if (routedProvider is not null)
            {
                return routedProvider;
            }
        }

        return providers
            .Where(candidate => CanServe(candidate.Provider!, purpose, now))
            .OrderBy(candidate => candidate.Priority is null ? 1 : 0)
            .ThenBy(candidate => candidate.Priority ?? int.MaxValue)
            .ThenBy(candidate => candidate.RouteOrder)
            .ThenBy(candidate => candidate.Provider!.Id, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Provider)
            .FirstOrDefault();
    }

    private static bool CanServe(StoredModelProviderInstance provider, string purpose, DateTimeOffset now)
    {
        return provider.Enabled
            && provider.Capabilities.Any(capability => capability.Equals("chat", StringComparison.OrdinalIgnoreCase))
            && AllowsPurpose(provider, purpose)
            && IsAvailable(provider, now);
    }

    private static bool AllowsPurpose(StoredModelProviderInstance provider, string purpose)
    {
        var routeCapabilities = provider.Capabilities
            .Where(capability => capability.StartsWith("route:", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return routeCapabilities.Length == 0
            || routeCapabilities.Any(capability => capability["route:".Length..].Equals(purpose, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAvailable(StoredModelProviderInstance provider, DateTimeOffset now)
    {
        var health = ResolveHealthStatus(provider);
        if (health is ProviderHealthStatus.Disabled)
        {
            return false;
        }

        if (health is ProviderHealthStatus.Unhealthy)
        {
            return !IsCooldownActive(provider, now);
        }

        if (health is ProviderHealthStatus.Cooldown)
        {
            return !IsCooldownActive(provider, now);
        }

        return true;
    }

    private static bool IsCooldownActive(StoredModelProviderInstance provider, DateTimeOffset now)
    {
        var cooldownUntil = ResolveCapabilityTime(provider, "cooldown_until") ?? provider.CooldownUntil;
        return cooldownUntil is not null && cooldownUntil > now;
    }

    private static int? ResolvePriority(StoredModelProviderInstance provider)
    {
        var value = ResolveCapabilityValue(provider, "priority");
        return int.TryParse(value, out var priority) ? priority : null;
    }

    private static ProviderHealthStatus ResolveHealthStatus(StoredModelProviderInstance provider)
    {
        var health = ResolveCapabilityValue(provider, "health");
        if (health is null)
        {
            return provider.HealthStatus;
        }

        return health.Trim().ToLowerInvariant() switch
        {
            "unhealthy" => ProviderHealthStatus.Unhealthy,
            "unknown" => ProviderHealthStatus.Unknown,
            "disabled" => ProviderHealthStatus.Disabled,
            "cooldown" => ProviderHealthStatus.Cooldown,
            _ => ProviderHealthStatus.Healthy
        };
    }

    private static DateTimeOffset? ResolveClock(IEnumerable<ProviderRouteCandidate> providers)
    {
        return providers
            .Select(candidate => candidate.Provider is null ? null : ResolveCapabilityTime(candidate.Provider, "clock"))
            .FirstOrDefault(value => value is not null);
    }

    private static DateTimeOffset? ResolveCapabilityTime(StoredModelProviderInstance provider, string key)
    {
        var value = ResolveCapabilityValue(provider, key);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? ResolveCapabilityValue(StoredModelProviderInstance provider, string key)
    {
        var prefix = key + ":";
        return provider.Capabilities
            .FirstOrDefault(capability => capability.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
    }

    private sealed record ProviderRouteCandidate(StoredModelProviderInstance? Provider, int RouteOrder, int? Priority);
}
