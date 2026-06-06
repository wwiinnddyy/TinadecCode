using System.Diagnostics;
using Tinadec.Contracts.Models;
using TinadecCore.Services;

namespace TinadecCore.Tests;

public sealed class CliProviderRuntimeTests
{
    [Fact]
    public async Task HappyPathReturnsSuccessfulTextResult()
    {
        using var fixture = CliFixture.Create("""
            input=$(cat)
            case "$input" in
              *\"session_id\":\"sess_cli\"*) ;;
              *) echo "missing session" >&2; exit 64 ;;
            esac
            case " $* " in
              *" --run-id "*) ;;
              *) echo "missing run" >&2; exit 64 ;;
            esac
            echo "Hello from Codex CLI"
            """);
        var runtime = new CliProviderRuntime(TimeSpan.FromSeconds(10));

        var result = await runtime.GenerateAsync(
            CreateContext(fixture),
            null,
            CreateMessages(),
            CancellationToken.None);

        Assert.Equal("executed", result.Status);
        Assert.Equal("Hello from Codex CLI", result.Content);
        Assert.Equal("cli-provider", result.RuntimeId);
        Assert.False(result.UsedStubResponse);
        Assert.Null(result.ErrorCategory);
    }

    [Fact]
    public async Task NonzeroExitMapsToNormalizedErrorWithExitCode()
    {
        using var fixture = CliFixture.Create("""
            cat >/dev/null
            echo "temporary outage" >&2
            exit 75
            """);
        var runtime = new CliProviderRuntime(TimeSpan.FromSeconds(10));

        var result = await runtime.GenerateAsync(
            CreateContext(fixture),
            null,
            CreateMessages(),
            CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.ProviderUnavailable, result.ErrorCategory);
        Assert.True(result.IsRetryable);
        Assert.Equal(75, result.ProviderExitCode);
        Assert.Equal("provider-cli", result.ErrorProviderId);
        Assert.Contains("temporary outage", result.Content);
    }

    [Fact]
    public async Task MissingWorkspaceFailsBeforeProcessStart()
    {
        using var fixture = CliFixture.Create("""
            touch SHOULD_NOT_EXIST
            echo "started"
            """);
        var invalidWorkspace = Path.Combine(Path.GetTempPath(), $"tinadec-missing-{Guid.NewGuid():N}");
        var runtime = new CliProviderRuntime(TimeSpan.FromSeconds(10));

        var result = await runtime.GenerateAsync(
            CreateContext(fixture, homePath: invalidWorkspace),
            null,
            CreateMessages(),
            CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.InvalidRequest, result.ErrorCategory);
        Assert.Equal(2, result.ProviderExitCode);
        Assert.Contains("workspace path does not exist", result.Content);
        Assert.False(File.Exists(Path.Combine(fixture.Workspace, "SHOULD_NOT_EXIST")));
    }

    [Fact]
    public async Task CancellationTerminatesProcessAndReturnsCancelled()
    {
        using var fixture = CliFixture.Create("""
            cat >/dev/null
            sleep 30
            echo "too late"
            """);
        var runtime = new CliProviderRuntime(TimeSpan.FromMinutes(1));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var stopwatch = Stopwatch.StartNew();
        var result = await runtime.GenerateAsync(
            CreateContext(fixture),
            null,
            CreateMessages(),
            cts.Token);
        stopwatch.Stop();

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.Cancelled, result.ErrorCategory);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task StderrSecretsAreRedactedFromErrorOutput()
    {
        using var fixture = CliFixture.Create("""
            cat >/dev/null
            echo "token=sk-test-secret bearer abc123 password=hunter2" >&2
            exit 77
            """);
        var runtime = new CliProviderRuntime(TimeSpan.FromSeconds(10));

        var result = await runtime.GenerateAsync(
            CreateContext(fixture),
            "sk-test-secret",
            CreateMessages(),
            CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal(ProviderErrorCategory.AuthenticationFailed, result.ErrorCategory);
        Assert.DoesNotContain("sk-test-secret", result.Content);
        Assert.DoesNotContain("abc123", result.Content);
        Assert.DoesNotContain("hunter2", result.Content);
        Assert.Contains("[REDACTED]", result.Content);
    }

    private static MessageDto[] CreateMessages()
    {
        return
        [
            new MessageDto("msg_cli", "sess_cli", "user", "Hello", DateTimeOffset.UtcNow)
        ];
    }

    private static ResolvedModelInvocationContextDto CreateContext(CliFixture fixture, string? homePath = null)
    {
        var provider = new ModelProviderInstanceDto(
            "provider-cli",
            "codex-cli",
            "Codex CLI",
            "cli",
            null,
            "gpt-test",
            false,
            fixture.Executable,
            homePath ?? fixture.Workspace,
            null,
            fixture.LaunchArgs,
            ["tools"],
            true,
            "ready",
            "Ready",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        return new ResolvedModelInvocationContextDto(
            "planner",
            null,
            provider,
            string.Empty,
            "gpt-test",
            null,
            "codex-cli",
            "cli",
            "provider-cli",
            false);
    }

    private sealed class CliFixture : IDisposable
    {
        private CliFixture(string root, string workspace, string executable, string launchArgs)
        {
            Root = root;
            Workspace = workspace;
            Executable = executable;
            LaunchArgs = launchArgs;
        }

        public string Root { get; }

        public string Workspace { get; }

        public string Executable { get; }

        public string LaunchArgs { get; }

        public static CliFixture Create(string unixScript)
        {
            var root = Path.Combine(Path.GetTempPath(), $"tinadec-cli-runtime-{Guid.NewGuid():N}");
            var workspace = Path.Combine(root, "workspace");
            Directory.CreateDirectory(workspace);

            if (OperatingSystem.IsWindows())
            {
                var scriptPath = Path.Combine(root, "fake-cli.cmd");
                File.WriteAllText(scriptPath, ConvertToWindowsScript(unixScript));
                return new CliFixture(root, workspace, Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", $"/c \"{scriptPath}\"");
            }

            var shellScriptPath = Path.Combine(root, "fake-cli.sh");
            File.WriteAllText(shellScriptPath, "#!/bin/sh\n" + unixScript + "\n");
            return new CliFixture(root, workspace, "/bin/sh", shellScriptPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static string ConvertToWindowsScript(string unixScript)
        {
            if (unixScript.Contains("Hello from Codex CLI", StringComparison.Ordinal))
            {
                return "@echo off\r\nfindstr /c:\"session_id\":\"sess_cli\" >nul || (echo missing session 1>&2 & exit /b 64)\r\necho Hello from Codex CLI\r\n";
            }

            if (unixScript.Contains("temporary outage", StringComparison.Ordinal))
            {
                return "@echo off\r\necho temporary outage 1>&2\r\nexit /b 75\r\n";
            }

            if (unixScript.Contains("SHOULD_NOT_EXIST", StringComparison.Ordinal))
            {
                return "@echo off\r\ntype nul > SHOULD_NOT_EXIST\r\necho started\r\n";
            }

            if (unixScript.Contains("sleep 30", StringComparison.Ordinal))
            {
                return "@echo off\r\ntimeout /t 30 /nobreak >nul\r\necho too late\r\n";
            }

            return "@echo off\r\necho token=sk-test-secret bearer abc123 password=hunter2 1>&2\r\nexit /b 77\r\n";
        }
    }
}
