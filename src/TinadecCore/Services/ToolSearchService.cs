using Tinadec.Contracts.Models;
using TinadecCore.Abstractions;

namespace TinadecCore.Services;

public sealed class ToolSearchService(IToolRegistry tools)
{
    private const int DefaultLimit = 12;
    private const int MaxLimit = 50;

    public IReadOnlyList<ToolSearchResultDto> Search(
        string? query,
        string? domain = null,
        string? source = null,
        string? risk = null,
        int? limit = null)
    {
        var tokens = Tokenize(query);
        var hasQuery = tokens.Length > 0;
        var take = Math.Clamp(limit.GetValueOrDefault(DefaultLimit), 1, MaxLimit);

        return tools.ListTools()
            .Where(tool => MatchesFilter(tool.Domain, domain))
            .Where(tool => MatchesFilter(tool.Source, source))
            .Where(tool => MatchesFilter(tool.Risk, risk))
            .Select(tool => BuildResult(tool, tokens, hasQuery))
            .Where(result => !hasQuery || result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => SourceSortKey(result.Tool.Source))
            .ThenBy(result => result.Tool.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Tool.Id, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToArray();
    }

    private static ToolSearchResultDto BuildResult(
        ToolDescriptorDto tool,
        IReadOnlyList<string> tokens,
        bool hasQuery)
    {
        var matchedFields = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var score = hasQuery ? ScoreTool(tool, tokens, matchedFields) : BaselineScore(tool, matchedFields);
        var requiresCheckpoint = RequiresHumanCheckpoint(tool);

        return new ToolSearchResultDto(
            tool,
            score,
            matchedFields.ToArray(),
            SourceLayer(tool.Source),
            requiresCheckpoint,
            ApprovalSummary(tool, requiresCheckpoint));
    }

    private static int ScoreTool(
        ToolDescriptorDto tool,
        IReadOnlyList<string> tokens,
        ISet<string> matchedFields)
    {
        var score = 0;
        foreach (var token in tokens)
        {
            score += ScoreText(tool.Id, token, "id", matchedFields, exactScore: 180, containsScore: 120);
            score += ScoreText(tool.Id.Replace('_', ' '), token, "id", matchedFields, exactScore: 140, containsScore: 90);
            score += ScoreText(tool.DisplayName, token, "display_name", matchedFields, exactScore: 160, containsScore: 110);
            score += ScoreText(tool.Domain, token, "domain", matchedFields, exactScore: 80, containsScore: 45);
            score += ScoreText(tool.Source, token, "source", matchedFields, exactScore: 90, containsScore: 50);
            score += ScoreText(tool.Risk, token, "risk", matchedFields, exactScore: 90, containsScore: 60);
            score += ScoreText(tool.ExecuteEndpoint, token, "execute_endpoint", matchedFields, exactScore: 35, containsScore: 20);

            foreach (var capability in tool.Capabilities)
            {
                score += ScoreText(capability, token, "capabilities", matchedFields, exactScore: 110, containsScore: 75);
                score += ScoreText(capability.Replace('.', ' '), token, "capabilities", matchedFields, exactScore: 85, containsScore: 55);
            }
        }

        if (tokens.Count > 1)
        {
            var phrase = string.Join(' ', tokens);
            score += ScoreText(tool.Id.Replace('_', ' '), phrase, "id", matchedFields, exactScore: 300, containsScore: 220);
            score += ScoreText(tool.DisplayName, phrase, "display_name", matchedFields, exactScore: 280, containsScore: 210);
        }

        return score;
    }

    private static int ScoreText(
        string value,
        string token,
        string field,
        ISet<string> matchedFields,
        int exactScore,
        int containsScore)
    {
        var text = Normalize(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (text.Equals(token, StringComparison.OrdinalIgnoreCase))
        {
            matchedFields.Add(field);
            return exactScore;
        }

        if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            matchedFields.Add(field);
            return containsScore;
        }

        return 0;
    }

    private static int BaselineScore(ToolDescriptorDto tool, ISet<string> matchedFields)
    {
        matchedFields.Add("catalog");
        var activeBonus = tool.Capabilities.Any(capability => capability.EndsWith(".active", StringComparison.OrdinalIgnoreCase)) ? 10 : 0;
        var readOnlyBonus = RequiresHumanCheckpoint(tool) ? 0 : 5;
        return 1 + activeBonus + readOnlyBonus;
    }

    private static string[] Tokenize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return query
            .Split([' ', '\t', '\r', '\n', '_', '.', '/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static bool MatchesFilter(string value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || value.Equals(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresHumanCheckpoint(ToolDescriptorDto tool)
    {
        return tool.RequiresApproval || !tool.Risk.Equals("read-only", StringComparison.OrdinalIgnoreCase);
    }

    private static string ApprovalSummary(ToolDescriptorDto tool, bool requiresCheckpoint)
    {
        if (!requiresCheckpoint)
        {
            return "Auto-dispatchable when an agent has this tool in scope.";
        }

        return tool.RequiresApproval
            ? "Requires Core approval before dispatch."
            : "Requires Core human checkpoint because the risk is not read-only.";
    }

    private static string SourceLayer(string source)
    {
        return source.ToLowerInvariant() switch
        {
            "core" => "core",
            "code" => "tool-layer",
            "codex-rust" => "native-glue",
            _ => "extension"
        };
    }

    private static int SourceSortKey(string source)
    {
        return source.ToLowerInvariant() switch
        {
            "core" => 0,
            "code" => 1,
            "codex-rust" => 2,
            _ => 3
        };
    }
}
