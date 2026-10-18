namespace ApiPlayground.Tests;

internal sealed class InProcessControlledTarget : HttpMessageHandler
{
    public InProcessControlledTarget(string? targetName = null)
    {
        TargetName = string.IsNullOrWhiteSpace(targetName) ? null : targetName.Trim();
    }

    public string? TargetName { get; }

    public bool IsReady => TargetName is not null;

    public int DispatchCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        DispatchCount++;
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}