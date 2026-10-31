namespace AcaiPos.Api.Services;

public sealed class RecusaApiException : Exception
{
    public RecusaApiException(string message, int statusCode = StatusCodes.Status400BadRequest) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
