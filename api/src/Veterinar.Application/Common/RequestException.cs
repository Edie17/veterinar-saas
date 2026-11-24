namespace Veterinar.Application.Common;

/// <summary>Expected use-case failure with a safe, user-facing message.</summary>
public class RequestException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
