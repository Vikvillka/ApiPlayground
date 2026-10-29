namespace ApiPlayground.Api;

public sealed record TracerRequest(string Url, string Method = "GET", string? Body = null, Dictionary<string, string>? Headers = null);
