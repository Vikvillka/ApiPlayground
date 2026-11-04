namespace ApiPlayground.Api.Contracts;

public sealed record AuthCredentialsRequest(string? Email, string? Password);
