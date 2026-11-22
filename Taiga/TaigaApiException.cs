using System.Net;

namespace TaigaMcp.Taiga;

/// <summary>
/// Thrown when the Taiga API returns an error status code (4xx/5xx).
/// </summary>
public sealed class TaigaApiException : Exception
{
    /// <summary>HTTP status of the Taiga response.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Response body (usually JSON describing the error).</summary>
    public string ResponseBody { get; }

    public TaigaApiException(HttpStatusCode statusCode, string responseBody)
        : base($"Taiga API returned error {(int)statusCode} ({statusCode}). {responseBody}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
