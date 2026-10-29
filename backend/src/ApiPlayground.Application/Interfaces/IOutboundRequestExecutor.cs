namespace ApiPlayground.Application.Outbound;

public interface IOutboundRequestExecutor
{
    Task<OutboundResult> ExecuteAsync(OutboundRequest request, CancellationToken cancellationToken = default);
}
