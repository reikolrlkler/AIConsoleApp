using System.Net;

namespace AIConsoleApp.Providers;

public sealed class ProviderRequestException : Exception
{
    public ProviderRequestException(string message, bool shouldTryNextKey, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ShouldTryNextKey = shouldTryNextKey;
        StatusCode = statusCode;
    }

    public bool ShouldTryNextKey { get; }

    public HttpStatusCode? StatusCode { get; }
}
