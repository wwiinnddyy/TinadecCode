using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace TinadecCore.Tracing;

/// <summary>
/// Analyzes trace data to produce diagnostic reports:
/// failure patterns, slow spans, and aggregated summaries.
/// Mirrors the Codex rollout-trace TraceDiagnostics model.
/// </summary>
public sealed class TraceDiagnosticService
{
    private readonly TracingOptions _options;

    // In-memory store of recent spans for diagnostics
    private readonly ConcurrentQueue<SpanSummary> _recentSpans = new();
    private readonly ConcurrentQueue<RecentFailure> _recentFailures = new();
    private const int MaxRecentSpans = 500;
    private const int MaxRecentFailures = 100;

    public TraceDiagnosticService(AgentTracing tracing)
    {
        _options = tracing.Options;
    }

    /// <summary>
    /// Record a completed span for diagnostics.
    /// Called by the ActivityProcessor when a span ends.
    /// </summary>
    public void RecordSpan(SpanSummary span)
    {
        _recentSpans.Enqueue(span);
        TrimQueue(_recentSpans, MaxRecentSpans);

        if (span.FailureCount > 0)
        {
            _recentFailures.Enqueue(new RecentFailure
            {
                Name = span.Name,
                DurationMs = span.AverageDurationMs,
                EndedAt = DateTime.UtcNow.ToString("O"),
                Cause = "Error",
                TraceId = "",
                SpanId = ""
            });
            TrimQueue(_recentFailures, MaxRecentFailures);
        }
    }

    /// <summary>
    /// Generate a diagnostic report from the current trace data.
    /// </summary>
    public TraceDiagnosticsReport GenerateReport()
    {
        var spans = _recentSpans.ToArray();
        var failures = _recentFailures.ToArray();
        var slowThresholdMs = 5000; // 5 seconds

        var topSpansByCount = spans
            .GroupBy(s => s.Name)
            .Select(g => new SpanSummary
            {
                Name = g.Key,
                Count = g.Count(),
                FailureCount = g.Count(s => s.FailureCount > 0),
                TotalDurationMs = g.Sum(s => s.TotalDurationMs),
                AverageDurationMs = g.Average(s => s.AverageDurationMs),
                MaxDurationMs = g.Max(s => s.MaxDurationMs)
            })
            .OrderByDescending(s => s.Count)
            .Take(20)
            .ToArray();

        var slowestSpans = spans
            .Where(s => s.MaxDurationMs > slowThresholdMs)
            .OrderByDescending(s => s.MaxDurationMs)
            .Take(10)
            .Select(s => new SlowSpanEntry
            {
                Name = s.Name,
                DurationMs = s.MaxDurationMs,
                EndedAt = DateTime.UtcNow.ToString("O"),
                TraceId = "",
                SpanId = ""
            })
            .ToArray();

        var commonFailures = failures
            .GroupBy(f => new { f.Name, f.Cause })
            .Select(g => new FailureCluster
            {
                Name = g.Key.Name,
                Cause = g.Key.Cause,
                Count = g.Count(),
                LastSeenAt = g.Max(f => f.EndedAt) ?? "",
                TraceId = "",
                SpanId = ""
            })
            .OrderByDescending(f => f.Count)
            .Take(10)
            .ToArray();

        // Read NDJSON file stats
        var recordCount = 0;
        var parseErrorCount = 0;
        try
        {
            if (File.Exists(_options.TraceFilePath))
            {
                var lines = File.ReadLines(_options.TraceFilePath);
                recordCount = lines.Count();
            }
        }
        catch
        {
            parseErrorCount = 1;
        }

        return new TraceDiagnosticsReport
        {
            GeneratedAt = DateTime.UtcNow.ToString("O"),
            TraceFilePath = _options.TraceFilePath,
            RecordCount = recordCount,
            ParseErrorCount = parseErrorCount,
            FailureCount = failures.Length,
            InterruptionCount = 0,
            SlowSpanThresholdMs = slowThresholdMs,
            SlowSpanCount = slowestSpans.Length,
            TopSpansByCount = topSpansByCount,
            SlowestSpans = slowestSpans,
            CommonFailures = commonFailures,
            LatestFailures = failures.TakeLast(20).ToArray(),
            LatestWarningsAndErrors = []
        };
    }

    private static void TrimQueue<T>(ConcurrentQueue<T> queue, int maxSize)
    {
        while (queue.Count > maxSize)
        {
            queue.TryDequeue(out _);
        }
    }
}

// --- Diagnostic Report Models ---

public sealed class TraceDiagnosticsReport
{
    public string GeneratedAt { get; set; } = "";
    public string TraceFilePath { get; set; } = "";
    public int RecordCount { get; set; }
    public int ParseErrorCount { get; set; }
    public int FailureCount { get; set; }
    public int InterruptionCount { get; set; }
    public int SlowSpanThresholdMs { get; set; }
    public int SlowSpanCount { get; set; }
    public SpanSummary[] TopSpansByCount { get; set; } = [];
    public SlowSpanEntry[] SlowestSpans { get; set; } = [];
    public FailureCluster[] CommonFailures { get; set; } = [];
    public RecentFailure[] LatestFailures { get; set; } = [];
    public LogEvent[] LatestWarningsAndErrors { get; set; } = [];
}

public sealed class SpanSummary
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public int FailureCount { get; set; }
    public double TotalDurationMs { get; set; }
    public double AverageDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
}

public sealed class SlowSpanEntry
{
    public string Name { get; set; } = "";
    public double DurationMs { get; set; }
    public string EndedAt { get; set; } = "";
    public string TraceId { get; set; } = "";
    public string SpanId { get; set; } = "";
}

public sealed class FailureCluster
{
    public string Name { get; set; } = "";
    public string Cause { get; set; } = "";
    public int Count { get; set; }
    public string LastSeenAt { get; set; } = "";
    public string TraceId { get; set; } = "";
    public string SpanId { get; set; } = "";
}

public sealed class RecentFailure
{
    public string Name { get; set; } = "";
    public double DurationMs { get; set; }
    public string EndedAt { get; set; } = "";
    public string Cause { get; set; } = "";
    public string TraceId { get; set; } = "";
    public string SpanId { get; set; } = "";
}

public sealed class LogEvent
{
    public string SpanName { get; set; } = "";
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public string SeenAt { get; set; } = "";
    public string TraceId { get; set; } = "";
    public string SpanId { get; set; } = "";
}
