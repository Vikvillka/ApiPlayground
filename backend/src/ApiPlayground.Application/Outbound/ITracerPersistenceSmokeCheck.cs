namespace ApiPlayground.Application.Outbound;

public interface ITracerPersistenceSmokeCheck
{
    Task<bool> VerifyRoundTripAsync(CancellationToken cancellationToken = default);
}
