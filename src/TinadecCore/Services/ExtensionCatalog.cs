using System.Text.Json;
using System.Text.Json.Nodes;
using Tinadec.Contracts.Models;

namespace Tinadec.AgentCore.Services;

public sealed record ExtensionDescriptor(
    string ExtensionId,
    string Kind,
    string Version,
    string Publisher,
    string DisplayName,
    string Description,
    string SourceKind,
    string SourceLocation,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Permissions,
    string ManifestJson);

public static class ExtensionCatalog
{
    public const string BuiltinSourceId = "source_builtin";

    public static ExtensionSourceDto BuiltinSource(DateTimeOffset now) => new(
        BuiltinSourceId,
        "Tinadec Curated",
        "builtin",
        "tinadec://marketplace/curated",
        true,
        now,
        now);

    public static IReadOnlyList<ExtensionDescriptor> BuiltinDescriptors { get; } =
    [
        new(
            "skill-agent-market-research",
            "skill",
            "0.1.0",
            "Tinadec",
            "Agent Market Research",
            "A planning-layer skill for comparing agent tools, model providers, and extension ecosystems.",
            "builtin",
            "tinadec://skills/agent-market-research",
            ["skill", "planning-layer", "market"],
            ["context.read"],
            ManifestFrom(new JsonObject
            {
                ["id"] = "skill-agent-market-research",
                ["kind"] = "skill",
                ["version"] = "0.1.0",
                ["publisher"] = "Tinadec",
                ["display_name"] = "Agent Market Research",
                ["description"] = "A planning-layer skill for comparing agent tools, model providers, and extension ecosystems.",
                ["entrypoints"] = new JsonObject { ["skill"] = "SKILL.md" },
                ["capabilities"] = new JsonArray("skill", "planning-layer", "market"),
                ["permissions"] = new JsonArray("context.read")
            })),
        new(
            "mcp-filesystem-stdio",
            "mcp-server",
            "0.1.0",
            "Tinadec",
            "Filesystem MCP Bridge",
            "Declarative MCP stdio server template for exposing selected filesystem tools through Core policy.",
            "builtin",
            "tinadec://mcp/filesystem-stdio",
            ["mcp", "stdio", "tools/list"],
            ["process.spawn", "file.read", "file.write"],
            ManifestFrom(new JsonObject
            {
                ["id"] = "mcp-filesystem-stdio",
                ["kind"] = "mcp-server",
                ["version"] = "0.1.0",
                ["publisher"] = "Tinadec",
                ["display_name"] = "Filesystem MCP Bridge",
                ["description"] = "Declarative MCP stdio server template for exposing selected filesystem tools through Core policy.",
                ["entrypoints"] = new JsonObject
                {
                    ["mcp"] = new JsonObject
                    {
                        ["transport"] = "stdio",
                        ["command"] = "npx",
                        ["args"] = new JsonArray("-y", "@modelcontextprotocol/server-filesystem")
                    }
                },
                ["capabilities"] = new JsonArray("mcp", "stdio", "tools/list"),
                ["permissions"] = new JsonArray("process.spawn", "file.read", "file.write")
            })),
        new(
            "acp-cursor-adapter",
            "acp-adapter",
            "0.1.0",
            "Tinadec",
            "Cursor ACP Adapter",
            "ACP adapter template for launching a Cursor-compatible agent subprocess and routing events through Core.",
            "builtin",
            "tinadec://acp/cursor",
            ["acp", "agent", "json-rpc", "terminal"],
            ["process.spawn", "file.read", "file.write", "terminal.create"],
            ManifestFrom(new JsonObject
            {
                ["id"] = "acp-cursor-adapter",
                ["kind"] = "acp-adapter",
                ["version"] = "0.1.0",
                ["publisher"] = "Tinadec",
                ["display_name"] = "Cursor ACP Adapter",
                ["description"] = "ACP adapter template for launching a Cursor-compatible agent subprocess and routing events through Core.",
                ["entrypoints"] = new JsonObject
                {
                    ["acp"] = new JsonObject
                    {
                        ["command"] = "agent",
                        ["args"] = new JsonArray("acp")
                    }
                },
                ["capabilities"] = new JsonArray("acp", "agent", "json-rpc", "terminal"),
                ["permissions"] = new JsonArray("process.spawn", "file.read", "file.write", "terminal.create")
            }))
    ];

    public static ExtensionDescriptor DescriptorFromRequest(InstallExtensionPreviewRequest request, MarketCatalogItemDto? catalogItem)
    {
        if (catalogItem is not null)
        {
            return new ExtensionDescriptor(
                catalogItem.ExtensionId,
                catalogItem.Kind,
                catalogItem.Version,
                catalogItem.Publisher,
                catalogItem.DisplayName,
                catalogItem.Description,
                catalogItem.SourceKind,
                catalogItem.SourceLocation,
                catalogItem.Capabilities,
                catalogItem.Permissions,
                ManifestFromCatalog(catalogItem));
        }

        if (!string.IsNullOrWhiteSpace(request.ManifestJson))
        {
            return DescriptorFromManifest(request.ManifestJson, request.SourceKind, request.SourceLocation);
        }

        var sourceKind = NormalizeKind(request.SourceKind, "local-directory");
        var sourceLocation = request.SourceLocation?.Trim() ?? string.Empty;
        if (sourceKind == "local-directory")
        {
            var manifestPath = Path.Combine(sourceLocation, "tinadec.extension.json");
            if (File.Exists(manifestPath))
            {
                return DescriptorFromManifest(File.ReadAllText(manifestPath), sourceKind, sourceLocation);
            }

            var skillPath = Path.Combine(sourceLocation, "SKILL.md");
            if (File.Exists(skillPath))
            {
                var name = NormalizeId(Path.GetFileName(sourceLocation));
                return new ExtensionDescriptor(
                    name,
                    "skill",
                    "0.1.0",
                    "Local",
                    Humanize(name),
                    ReadSkillDescription(skillPath),
                    sourceKind,
                    sourceLocation,
                    ["skill"],
                    ["context.read"],
                    ManifestFrom(new JsonObject
                    {
                        ["id"] = name,
                        ["kind"] = "skill",
                        ["version"] = "0.1.0",
                        ["publisher"] = "Local",
                        ["display_name"] = Humanize(name),
                        ["description"] = ReadSkillDescription(skillPath),
                        ["entrypoints"] = new JsonObject { ["skill"] = "SKILL.md" },
                        ["capabilities"] = new JsonArray("skill"),
                        ["permissions"] = new JsonArray("context.read")
                    }));
            }
        }

        var fallbackId = NormalizeId(Path.GetFileNameWithoutExtension(sourceLocation));
        if (string.IsNullOrWhiteSpace(fallbackId))
        {
            fallbackId = $"extension-{Guid.NewGuid():N}"[..24];
        }

        return new ExtensionDescriptor(
            fallbackId,
            InferKind(sourceKind, sourceLocation),
            "0.1.0",
            "Unknown",
            Humanize(fallbackId),
            "Extension resolved from source. Add tinadec.extension.json for richer metadata.",
            sourceKind,
            sourceLocation,
            InferCapabilities(sourceKind, sourceLocation),
            InferPermissions(sourceKind, sourceLocation),
            ManifestFrom(new JsonObject
            {
                ["id"] = fallbackId,
                ["kind"] = InferKind(sourceKind, sourceLocation),
                ["version"] = "0.1.0",
                ["publisher"] = "Unknown",
                ["display_name"] = Humanize(fallbackId),
                ["description"] = "Extension resolved from source. Add tinadec.extension.json for richer metadata.",
                ["source"] = new JsonObject { ["kind"] = sourceKind, ["location"] = sourceLocation },
                ["capabilities"] = ToJsonArray(InferCapabilities(sourceKind, sourceLocation)),
                ["permissions"] = ToJsonArray(InferPermissions(sourceKind, sourceLocation))
            }));
    }

    public static ExtensionInstallPreviewDto BuildPreview(ExtensionDescriptor descriptor)
    {
        var risks = new List<string>();
        if (IsRemoteSource(descriptor.SourceKind))
        {
            risks.Add("Downloads extension metadata or package content from an external network host.");
        }

        if (descriptor.Permissions.Any(p => p.Contains("process", StringComparison.OrdinalIgnoreCase)))
        {
            risks.Add("May launch a local child process when enabled.");
        }

        if (descriptor.Permissions.Any(p => p.Contains("file.write", StringComparison.OrdinalIgnoreCase)))
        {
            risks.Add("May request write access through Core approval policy.");
        }

        if (descriptor.Kind == "mcp-server")
        {
            risks.Add("Adds external MCP tools to the execution-layer tool catalog after enablement.");
        }

        if (descriptor.Kind == "acp-adapter")
        {
            risks.Add("Adds an external agent runtime that can request file, terminal, and permission operations.");
        }

        if (risks.Count == 0)
        {
            risks.Add("Installs metadata and keeps the extension disabled until you enable it.");
        }

        return new ExtensionInstallPreviewDto(
            descriptor.ExtensionId,
            descriptor.Kind,
            descriptor.Version,
            descriptor.Publisher,
            descriptor.DisplayName,
            descriptor.Description,
            descriptor.SourceKind,
            descriptor.SourceLocation,
            descriptor.Capabilities,
            descriptor.Permissions,
            risks,
            true,
            $"Install {descriptor.DisplayName} ({descriptor.Kind}) from {descriptor.SourceKind}");
    }

    public static string NormalizeId(string? value)
    {
        var normalized = new string((value ?? string.Empty).Trim().ToLowerInvariant().Select(ch =>
            char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('-', '_');
        return normalized.Length > 80 ? normalized[..80] : normalized;
    }

    public static bool IsRemoteSource(string sourceKind)
    {
        return sourceKind is "github" or "git" or "https-archive" or "marketplace-url" or "mcpb" or "dxt";
    }

    private static ExtensionDescriptor DescriptorFromManifest(string manifestJson, string? sourceKind, string? sourceLocation)
    {
        var node = JsonNode.Parse(manifestJson)?.AsObject()
            ?? throw new InvalidOperationException("Extension manifest must be a JSON object.");
        var id = NormalizeId(GetString(node, "id") ?? GetString(node, "name"));
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Extension manifest requires an id.");
        }

        var kind = NormalizeKind(GetString(node, "kind"), "skill");
        var capabilities = ReadStringArray(node, "capabilities", InferCapabilities(sourceKind, sourceLocation));
        var permissions = ReadStringArray(node, "permissions", InferPermissions(sourceKind, sourceLocation));
        var manifestSource = node["source"] as JsonObject;
        var resolvedSourceKind = NormalizeKind(sourceKind ?? GetString(manifestSource, "kind"), "local-directory");
        var resolvedSourceLocation = sourceLocation ?? GetString(manifestSource, "location") ?? string.Empty;

        return new ExtensionDescriptor(
            id,
            kind,
            GetString(node, "version") ?? "0.1.0",
            GetString(node, "publisher") ?? "Unknown",
            GetString(node, "display_name") ?? GetString(node, "displayName") ?? Humanize(id),
            GetString(node, "description") ?? "Tinadec extension",
            resolvedSourceKind,
            resolvedSourceLocation,
            capabilities,
            permissions,
            node.ToJsonString(TinadecJson.Options));
    }

    private static string ManifestFromCatalog(MarketCatalogItemDto item)
    {
        return ManifestFrom(new JsonObject
        {
            ["id"] = item.ExtensionId,
            ["kind"] = item.Kind,
            ["version"] = item.Version,
            ["publisher"] = item.Publisher,
            ["display_name"] = item.DisplayName,
            ["description"] = item.Description,
            ["source"] = new JsonObject { ["kind"] = item.SourceKind, ["location"] = item.SourceLocation },
            ["capabilities"] = ToJsonArray(item.Capabilities),
            ["permissions"] = ToJsonArray(item.Permissions)
        });
    }

    private static string ManifestFrom(JsonObject obj) => obj.ToJsonString(TinadecJson.Options);

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static string? GetString(JsonObject? node, string name)
    {
        return node is not null && node.TryGetPropertyValue(name, out var value)
            ? value?.GetValue<string>()
            : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject node, string name, IReadOnlyList<string> fallback)
    {
        if (node.TryGetPropertyValue(name, out var value) && value is JsonArray array)
        {
            return array.Select(item => item?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return fallback;
    }

    private static string NormalizeKind(string? value, string fallback)
    {
        var normalized = NormalizeId(value);
        return normalized is "skill" or "mcp-server" or "acp-adapter" or "tool-pack" or
            "local-directory" or "local-archive" or "github" or "git" or "https-archive" or "marketplace-url" or "mcpb" or "dxt" or "builtin"
            ? normalized
            : fallback;
    }

    private static string InferKind(string? sourceKind, string? sourceLocation)
    {
        var source = $"{sourceKind} {sourceLocation}".ToLowerInvariant();
        if (source.Contains("acp", StringComparison.Ordinal)) return "acp-adapter";
        if (source.Contains("mcp", StringComparison.Ordinal) || source.EndsWith(".mcpb", StringComparison.Ordinal) || source.EndsWith(".dxt", StringComparison.Ordinal)) return "mcp-server";
        return "skill";
    }

    private static IReadOnlyList<string> InferCapabilities(string? sourceKind, string? sourceLocation)
    {
        return InferKind(sourceKind, sourceLocation) switch
        {
            "mcp-server" => ["mcp", "tools/list"],
            "acp-adapter" => ["acp", "agent"],
            _ => ["skill"]
        };
    }

    private static IReadOnlyList<string> InferPermissions(string? sourceKind, string? sourceLocation)
    {
        return InferKind(sourceKind, sourceLocation) switch
        {
            "mcp-server" => ["process.spawn", "network.remote"],
            "acp-adapter" => ["process.spawn", "file.read", "file.write", "terminal.create"],
            _ => ["context.read"]
        };
    }

    private static string Humanize(string id)
    {
        return string.Join(' ', id.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string ReadSkillDescription(string skillPath)
    {
        var lines = File.ReadLines(skillPath).Take(40).ToArray();
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Split(':', 2)[1].Trim().Trim('"', '\'');
            }
        }

        return "Local SKILL.md extension.";
    }
}
