namespace Tinadec.AgentCore.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void TinadecCoreIsAPlainLibraryWithoutUiOrBffDependencies()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "src", "TinadecCore", "TinadecCore.csproj"));

        Assert.Contains("Microsoft.NET.Sdk", project);
        Assert.DoesNotContain("Microsoft.NET.Sdk.Web", project);
        Assert.DoesNotContain("Elysia", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Electron", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Vue", project, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentCoreRuntimeDependsOnTinadecCore()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "src", "Tinadec.AgentCore", "Tinadec.AgentCore.csproj"));

        Assert.Contains(@"..\TinadecCore\TinadecCore.csproj", project);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TinadecCode.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find TinadecCode repository root.");
    }
}
