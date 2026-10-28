using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ApiPlayground.Application.Outbound;

namespace ApiPlayground.Infrastructure.Outbound;

public sealed class GuardedTargetHttpClient : IOutboundRequestExecutor
{
    private readonly OutboundPolicy _policy;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;
    private readonly Func<OutboundPolicy, bool> _policyValidator;
    private readonly Func<IPEndPoint, CancellationToken, Task<Stream>> _connect;

    public GuardedTargetHttpClient(OutboundPolicy policy)
        : this(policy, static (host, token) => Dns.GetHostAddressesAsync(host, token), null, ConnectSocketAsync)
    {
    }

    internal GuardedTargetHttpClient(
        OutboundPolicy policy,
        Func<string, CancellationToken, Task<IPAddress[]>> resolver,
        Func<OutboundPolicy, bool>? policyValidator = null,
        Func<IPEndPoint, CancellationToken, Task<Stream>>? connector = null)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _resolve = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _policyValidator = policyValidator ?? (candidate => candidate?.IsValid == true);
        _connect = connector ?? ConnectSocketAsync;
    }

    public async Task<OutboundResult> ExecuteAsync(OutboundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        if (!_policyValidator(_policy))
            return Failed(OutboundFailure.PolicyRejected, stopwatch, "The configured outbound policy is invalid.");
        if (request.Uri is null)
            return Failed(OutboundFailure.PolicyRejected, stopwatch, "A destination URI is required.");
        if (_policy.ClaimsTestHost(request.Uri) && !_policy.IsTestEndpoint(request.Uri))
            return Failed(OutboundFailure.PolicyRejected, stopwatch, "The test destination does not match the exact approved scheme and port.");
        if (!IsValidRequest(request, out var rejection))
            return Failed(OutboundFailure.PolicyRejected, stopwatch, rejection);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(OutboundPolicy.TotalTimeout);
        var (pinnedAddress, resolutionFailure) = await ResolveAndSelectAddressAsync(request.Uri, stopwatch, deadline.Token, cancellationToken).ConfigureAwait(false);
        if (resolutionFailure is not null)
            return resolutionFailure;

        using var connectDeadline = new CancellationTokenSource(OutboundPolicy.ConnectTimeout);
        var connectionDeadlineExpired = false;
        using var handler = CreateGuardedHandler(
            request.Uri,
            pinnedAddress!,
            deadline.Token,
            cancellationToken,
            connectDeadline.Token,
            () => connectionDeadlineExpired = true);
        using var client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        if (!TryCreateRequestMessage(request, out var createdMessage))
            return Failed(OutboundFailure.PolicyRejected, stopwatch, "A request header or content type is invalid.");
        using var message = createdMessage!;

        return await DispatchAndReadResponseAsync(
            client,
            message,
            stopwatch,
            deadline.Token,
            cancellationToken,
            () => connectionDeadlineExpired).ConfigureAwait(false);
    }

    private async Task<(IPAddress? Address, OutboundResult? Failure)> ResolveAndSelectAddressAsync(
        Uri uri,
        Stopwatch stopwatch,
        CancellationToken deadlineToken,
        CancellationToken callerCancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            if (IPAddress.TryParse(uri.Host, out var literal))
                addresses = [literal];
            else
                addresses = await _resolve(uri.DnsSafeHost, deadlineToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
        {
            return (null, Failed(OutboundFailure.Cancelled, stopwatch, "The request was cancelled."));
        }
        catch (OperationCanceledException)
        {
            return (null, Failed(OutboundFailure.Timeout, stopwatch, "The total request deadline expired during DNS resolution."));
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or InvalidOperationException or ArgumentException or FormatException)
        {
            var diagnostic = ex is ArgumentException or FormatException
                ? "Destination DNS resolution failed."
                : "The outbound request failed: " + ex.GetType().Name;
            return (null, Failed(OutboundFailure.TransportFailure, stopwatch, diagnostic));
        }

        if (addresses.Length == 0 || addresses.Any(address => !_policy.AllowsAddress(uri, address)))
            return (null, Failed(OutboundFailure.PolicyRejected, stopwatch, "At least one destination address is not permitted."));

        var pinnedAddress = _policy.IsTestEndpoint(uri)
            ? _policy.TestAddress
            : IPAddress.TryParse(uri.Host, out var requestedAddress)
                ? addresses.FirstOrDefault(address => address.AddressFamily == requestedAddress.AddressFamily)
                : addresses.FirstOrDefault();
        return pinnedAddress is null
            ? (null, Failed(OutboundFailure.PolicyRejected, stopwatch, "No destination address is compatible with the requested URI."))
            : (pinnedAddress, null);
    }

    private SocketsHttpHandler CreateGuardedHandler(
        Uri requestedUri,
        IPAddress pinnedAddress,
        CancellationToken totalDeadlineToken,
        CancellationToken callerCancellationToken,
        CancellationToken connectDeadlineToken,
        Action markConnectionDeadlineExpired) => new()
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectTimeout = OutboundPolicy.ConnectTimeout,
            PooledConnectionLifetime = TimeSpan.Zero,
            ConnectCallback = async (context, token) =>
            {
                if (!string.Equals(context.DnsEndPoint.Host, requestedUri.DnsSafeHost, StringComparison.OrdinalIgnoreCase) ||
                    context.DnsEndPoint.Port != requestedUri.Port)
                    throw new HttpRequestException("Unexpected destination endpoint.");

                using var linkedConnectDeadline = CancellationTokenSource.CreateLinkedTokenSource(token, connectDeadlineToken);
                try
                {
                    return await _connect(new IPEndPoint(pinnedAddress, context.DnsEndPoint.Port), linkedConnectDeadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!totalDeadlineToken.IsCancellationRequested && !callerCancellationToken.IsCancellationRequested)
                {
                    markConnectionDeadlineExpired();
                    throw new TimeoutException("The outbound connection deadline expired.");
                }
            }
        };

    private static async Task<OutboundResult> DispatchAndReadResponseAsync(
        HttpClient client,
        HttpRequestMessage message,
        Stopwatch stopwatch,
        CancellationToken totalDeadlineToken,
        CancellationToken callerCancellationToken,
        Func<bool> connectionDeadlineExpired)
    {
        var dispatchStarted = false;
        try
        {
            using var linkedDeadline = CancellationTokenSource.CreateLinkedTokenSource(totalDeadlineToken, callerCancellationToken);
            // Crossing this boundary makes the attempt History-eligible; it does not prove remote receipt.
            dispatchStarted = true;
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linkedDeadline.Token).ConfigureAwait(false);
            var headers = response.Headers.Concat(response.Content.Headers)
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.SelectMany(pair => pair.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
            if (response.Content.Headers.ContentLength is long contentLength && contentLength > OutboundPolicy.MaximumResponseBytes)
                return Failed(OutboundFailure.ResponseTooLarge, stopwatch, "The response exceeds the configured body limit.", (int)response.StatusCode, headers, dispatchStarted: true);

            await using var stream = await response.Content.ReadAsStreamAsync(linkedDeadline.Token).ConfigureAwait(false);
            using var body = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, linkedDeadline.Token).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (body.Length + read > OutboundPolicy.MaximumResponseBytes)
                    return Failed(OutboundFailure.ResponseTooLarge, stopwatch, "The response exceeds the configured body limit.", (int)response.StatusCode, headers, dispatchStarted: true);
                await body.WriteAsync(buffer.AsMemory(0, read), linkedDeadline.Token).ConfigureAwait(false);
            }

            stopwatch.Stop();
            return new OutboundResult(OutboundFailure.None, (int)response.StatusCode, stopwatch.Elapsed,
                new ReadOnlyDictionary<string, IReadOnlyList<string>>(headers), body.ToArray(), DispatchStarted: true);
        }
        catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
        {
            return Failed(OutboundFailure.Cancelled, stopwatch, "The request was cancelled.", dispatchStarted: dispatchStarted);
        }
        catch (OperationCanceledException) when (connectionDeadlineExpired())
        {
            return Failed(OutboundFailure.Timeout, stopwatch, "The outbound connection deadline expired.", dispatchStarted: dispatchStarted);
        }
        catch (OperationCanceledException)
        {
            return Failed(OutboundFailure.Timeout, stopwatch, "The total request deadline expired.", dispatchStarted: dispatchStarted);
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or InvalidOperationException or TimeoutException)
        {
            if (connectionDeadlineExpired() || ex is TimeoutException)
                return Failed(OutboundFailure.Timeout, stopwatch, "The outbound connection deadline expired.", dispatchStarted: dispatchStarted);
            return Failed(OutboundFailure.TransportFailure, stopwatch, "The outbound request failed: " + ex.GetType().Name, dispatchStarted: dispatchStarted);
        }
    }

    private bool IsValidRequest(OutboundRequest request, out string reason)
    {
        if (_policy.ClaimsTestHost(request.Uri) && !_policy.IsTestEndpoint(request.Uri))
        {
            reason = "The test destination does not match the exact approved scheme and port.";
            return false;
        }
        if (!request.Uri.IsAbsoluteUri || request.Uri.UserInfo.Length != 0 || request.Uri.Fragment.Length != 0 ||
            request.Uri.Scheme is not ("http" or "https"))
        {
            reason = "Only absolute HTTP(S) URIs without credentials or fragments are allowed.";
            return false;
        }
        if (request.Method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE"))
        {
            reason = "Only GET, POST, PUT, PATCH, and DELETE methods are allowed.";
            return false;
        }
        if (_policy.IsTestEndpoint(request.Uri))
        {
            reason = string.Empty;
            return true;
        }
        if (IPAddress.TryParse(request.Uri.Host, out var literal))
        {
            if (_policy.AllowsAddress(request.Uri, literal))
            {
                reason = string.Empty;
                return true;
            }
            reason = "The destination address is not public.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool TryCreateRequestMessage(OutboundRequest request, out HttpRequestMessage? message)
    {
        message = null;
        HttpRequestMessage? candidate = null;
        try
        {
            candidate = new HttpRequestMessage(new HttpMethod(request.Method), request.Uri);
            if (request.Body.Length > 0 || request.ContentType is not null)
            {
                candidate.Content = new ByteArrayContent(request.Body.ToArray());
                if (request.ContentType is not null)
                    candidate.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(request.ContentType);
            }

            if (request.Headers is not null)
            {
                foreach (var (name, value) in request.Headers)
                {
                    if (!candidate.Headers.TryAddWithoutValidation(name, value) &&
                        (candidate.Content is null || !candidate.Content.Headers.TryAddWithoutValidation(name, value)))
                    {
                        candidate.Dispose();
                        return false;
                    }
                }
            }

            message = candidate;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            candidate?.Dispose();
            return false;
        }
    }

    private static async Task<Stream> ConnectSocketAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static OutboundResult Failed(
        OutboundFailure failure,
        Stopwatch stopwatch,
        string diagnostic,
        int? statusCode = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
        bool dispatchStarted = false)
    {
        stopwatch.Stop();
        return new OutboundResult(failure, statusCode, stopwatch.Elapsed,
            headers ?? new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>()),
            ReadOnlyMemory<byte>.Empty, diagnostic, dispatchStarted);
    }
}
