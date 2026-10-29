using System.Runtime.CompilerServices;

namespace ApiPlayground.Tests;

// Test hosts do not load user secrets or the root .env file, so startup validation of ConnectionStrings:Postgres
// would stop every WebApplicationFactory<Program> host. This supplies a non-empty placeholder when no value exists.
// The placeholder points at an unused port; tests that touch persistence must supply their own store.
internal static class TestConfigurationDefaults
{
    private const string PostgresKey = "ConnectionStrings__Postgres";
    private const string Placeholder = "Host=localhost;Port=1;Database=apiplayground_test_placeholder;Username=placeholder;Password=placeholder";

    [ModuleInitializer]
    internal static void EnsurePostgresPlaceholder()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresKey)))
            Environment.SetEnvironmentVariable(PostgresKey, Placeholder);
    }
}
