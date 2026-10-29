using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using ApiPlayground.Application.Outbound;
using ApiPlayground.Infrastructure;
using ApiPlayground.Infrastructure.Outbound;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ApiPlayground.Tests;

public sealed class StartupConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_postgres_connection_string_fails_startup_with_a_fixed_message(string? value)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Postgres", value));
        using var owned = factory;

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        var text = exception.ToString();
        Assert.Contains("OptionsValidationException", text);
        Assert.Contains(PostgresOptions.ValidationMessage, text);
    }

    [Fact]
    public async Task Captured_logs_do_not_contain_authorization_or_sensitive_query_values()
    {
        const string headerSecret = "sentinel-authorization-7f3a";
        const string querySecret = "sentinel-query-9c2b";
        var capture = new CapturingLoggerProvider();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .ConfigureLogging(logging => logging.AddProvider(capture))
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IOutboundRequestExecutor>();
                services.AddSingleton<IOutboundRequestExecutor>(new ThrowingExecutor(
                    new InvalidOperationException("upstream rejected " + querySecret)));
                services.RemoveAll<IRequestHistoryRepository>();
                services.AddSingleton<IRequestHistoryRepository>(new NullHistoryRepository());
            }));
        await using (factory)
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/tracer/get")
        {
            Content = new StringContent("{\"url\":\"https://example.test/?access_token=" + querySecret + "\"}",
                Encoding.UTF8, "application/json"),
        })
        {
            request.Headers.Host = "localhost:5000";
            request.Headers.Add("Origin", "http://127.0.0.1:5173");
            request.Headers.Add("X-ApiPlayground-Request", "1");
            request.Headers.Add("Authorization", "Bearer " + headerSecret);

            using var response = await factory.CreateClient().SendAsync(request);
            _ = await response.Content.ReadAsStringAsync();
        }

        var entries = capture.Entries.ToArray();
        Assert.NotEmpty(entries);
        // Proves the request reached the failing executor, so the sensitive values were actually in play.
        Assert.Contains(entries, entry => entry.Contains(nameof(InvalidOperationException)));
        Assert.All(entries, entry =>
        {
            Assert.DoesNotContain(headerSecret, entry);
            Assert.DoesNotContain(querySecret, entry);
        });
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                provider.Entries.Enqueue(formatter(state, exception) + " " + exception);
            }
        }
    }

    private sealed class ThrowingExecutor(Exception exception) : IOutboundRequestExecutor
    {
        public Task<OutboundResult> ExecuteAsync(OutboundRequest request, CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class NullHistoryRepository : IRequestHistoryRepository
    {
        public Task SaveAsync(RequestHistorySnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<RequestHistorySnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestHistorySnapshot>>([]);
        public Task<RequestHistorySnapshot?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestHistorySnapshot?>(null);
    }
}
