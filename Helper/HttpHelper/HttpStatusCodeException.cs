using System.Net;

namespace DynamicForm.Helper;

/// <summary>
/// Represents an HTTP error with an associated status code.
/// </summary>
public class HttpStatusCodeException : Exception
{
    /// <summary>
    /// The HTTP status code to return to the client.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Create a new exception with a specific HTTP status code and message.
    /// </summary>
    public HttpStatusCodeException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
