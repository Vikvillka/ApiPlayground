using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Layouts;
using NLog.Targets;

namespace ApiPlayground.Api;

public static class NLogConsoleExtensions
{
    // Code-only NLog configuration: one console target with an explicit JSON attribute allowlist.
    // Request headers, query strings, and bodies are never rendered, and exceptions are rendered as type names only.
    public static ILoggingBuilder AddNLogConsole(this ILoggingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddNLog(CreateConfiguration());
        return builder;
    }

    private static LoggingConfiguration CreateConfiguration()
    {
        var layout = new JsonLayout { SuppressSpaces = true };
        layout.Attributes.Add(new JsonAttribute("time", "${longdate}"));
        layout.Attributes.Add(new JsonAttribute("level", "${level:uppercase=true}"));
        layout.Attributes.Add(new JsonAttribute("logger", "${logger}"));
        layout.Attributes.Add(new JsonAttribute("message", "${message}"));
        layout.Attributes.Add(new JsonAttribute("exception", "${exception:format=Type}"));

        var console = new ConsoleTarget("console") { Layout = layout };
        var configuration = new LoggingConfiguration();
        configuration.AddTarget(console);
        configuration.LoggingRules.Add(new LoggingRule("Microsoft.Hosting.Lifetime", NLog.LogLevel.Info, NLog.LogLevel.Fatal, console) { Final = true });
        configuration.LoggingRules.Add(new LoggingRule("Microsoft.*", NLog.LogLevel.Warn, NLog.LogLevel.Fatal, console) { Final = true });
        configuration.LoggingRules.Add(new LoggingRule("System.*", NLog.LogLevel.Warn, NLog.LogLevel.Fatal, console) { Final = true });
        configuration.LoggingRules.Add(new LoggingRule("Npgsql.*", NLog.LogLevel.Warn, NLog.LogLevel.Fatal, console) { Final = true });
        configuration.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, NLog.LogLevel.Fatal, console));
        return configuration;
    }
}
