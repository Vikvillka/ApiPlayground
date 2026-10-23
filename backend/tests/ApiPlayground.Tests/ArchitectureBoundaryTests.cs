using System.Xml.Linq;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string BackendRoot = FindBackendRoot();

    [Fact]
    public void Runtime_project_references_match_the_approved_dependency_direction()
    {
        AssertReferences("ApiPlayground.Domain", []);
        AssertReferences("ApiPlayground.Application", ["ApiPlayground.Domain"]);
        AssertReferences("ApiPlayground.Infrastructure", ["ApiPlayground.Application", "ApiPlayground.Domain"]);
        AssertReferences("ApiPlayground.Api", ["ApiPlayground.Application", "ApiPlayground.Infrastructure"]);

        var runtimeProjects = RuntimeProjects();
        Assert.DoesNotContain(runtimeProjects, project => ProjectReferences(project).Contains("ApiPlayground.Tests"));
        Assert.Equal("ApiPlayground.Api.Data.PlatformDbContext", typeof(ApiPlayground.Api.Data.PlatformDbContext).FullName);
    }

    [Fact]
    public void Runtime_source_does_not_add_direct_target_dispatch()
    {
        var forbiddenPatterns = new[]
        {
            "new HttpClient",
            "HttpClientHandler",
            "SocketsHttpHandler",
            "WebRequest.Create",
            "SendAsync(",
            "GetByteArrayAsync(",
            "GetStringAsync(",
            "GetStreamAsync(",
            "GetFromJsonAsync(",
            "GetFromJsonAsAsyncEnumerable(",
            "PostAsync(",
            "PostAsJsonAsync(",
            "PutAsync(",
            "PutAsJsonAsync(",
            "PatchAsync(",
            "PatchAsJsonAsync(",
            "DeleteAsync(",
            ".Send(",
            ".Get(",
            ".Post(",
            ".Put(",
            ".Patch(",
            ".Delete(",
        };

        var violations = RuntimeProjects()
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(BackendRoot, "src", project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(path => !string.Equals(
                Path.GetRelativePath(BackendRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                "src/ApiPlayground.Infrastructure/Outbound/GuardedTargetHttpClient.cs",
                StringComparison.Ordinal))
            .SelectMany(path => forbiddenPatterns
                .Where(pattern => File.ReadAllText(path).Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(BackendRoot, path)} contains {pattern}"))
            .ToArray();

        Assert.True(violations.Length == 0, "Direct target-dispatch APIs are not permitted in runtime projects: " + string.Join("; ", violations));
    }

    private static void AssertReferences(string projectName, string[] expected)
    {
        var actual = ProjectReferences(projectName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
    }

    private static string[] ProjectReferences(string projectName)
    {
        var projectFile = Directory.EnumerateFiles(Path.Combine(BackendRoot, "src", projectName), "*.csproj").Single();
        var document = XDocument.Load(projectFile);
        return document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? string.Empty))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] RuntimeProjects() =>
    ["ApiPlayground.Domain", "ApiPlayground.Application", "ApiPlayground.Infrastructure", "ApiPlayground.Api"];

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApiPlayground.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate backend/ApiPlayground.sln from the test output directory.");
    }
}
