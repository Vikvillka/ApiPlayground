using System.Xml.Linq;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.Extensions.DependencyInjection;
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
    public void Domain_has_no_persistence_or_host_dependencies()
    {
        var domainRoot = Path.Combine(BackendRoot, "src", "ApiPlayground.Domain");
        var packageReferences = XDocument.Load(Directory.EnumerateFiles(domainRoot, "*.csproj").Single())
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();
        Assert.Empty(packageReferences);

        var forbiddenNamespaces = new[] { "Microsoft.EntityFrameworkCore", "Npgsql", "ApiPlayground.Infrastructure", "ApiPlayground.Api" };
        var violations = Directory.EnumerateFiles(domainRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .SelectMany(path => forbiddenNamespaces
                .Where(name => File.ReadAllText(path).Contains(name, StringComparison.Ordinal))
                .Select(name => $"{Path.GetRelativePath(BackendRoot, path)} references {name}"))
            .ToArray();

        Assert.True(violations.Length == 0, "Domain must not depend on persistence or host code: " + string.Join("; ", violations));
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

    [Fact]
    public async Task Production_composition_uses_one_fixed_public_only_guard_without_test_switches()
    {
        var services = new ServiceCollection();
        services.AddGuardedOutbound(OutboundPolicy.PublicInternetOnly());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IOutboundRequestExecutor));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<GuardedTargetHttpClient>(provider.GetRequiredService<IOutboundRequestExecutor>());
        var policy = provider.GetRequiredService<OutboundPolicy>();
        Assert.False(policy.AllowsAddress(new Uri("http://127.0.0.1/"), System.Net.IPAddress.Loopback));

        var program = await File.ReadAllTextAsync(Path.Combine(BackendRoot, "src", "ApiPlayground.Api", "Program.cs"));
        Assert.Contains("AddGuardedOutbound(OutboundPolicy.PublicInternetOnly())", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ForTestOnlyLoopbackEndpoint", program, StringComparison.Ordinal);
        Assert.DoesNotContain("OutboundPolicy__", program, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateClient(", program, StringComparison.Ordinal);
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
            .Select(element => Path.GetFileNameWithoutExtension(
                ((string?)element.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
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
