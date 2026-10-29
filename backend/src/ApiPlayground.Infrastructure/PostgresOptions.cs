namespace ApiPlayground.Infrastructure;

// Bound from the ConnectionStrings:Postgres configuration value. The value is secret and must never be logged or echoed.
public sealed class PostgresOptions
{
    public const string ValidationMessage =
        "ConnectionStrings:Postgres is required. Set it with user secrets, an environment variable, or configuration.";

    public string ConnectionString { get; set; } = string.Empty;
}
