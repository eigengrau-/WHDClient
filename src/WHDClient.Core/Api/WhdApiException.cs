using System.Net;

namespace WHDClient.Core.Api;

public class WhdApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public WhdApiException(HttpStatusCode statusCode, string? responseBody, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

public class WhdAuthenticationException : WhdApiException
{
    public WhdAuthenticationException(string? body)
        : base(HttpStatusCode.Unauthorized, body, "Authentication failed (HTTP 401). Check the API key.") { }
}

public class WhdPermissionException : WhdApiException
{
    public WhdPermissionException(string? body)
        : base(HttpStatusCode.Forbidden, body, "Permission denied (HTTP 403). Your tech account is not allowed to do this.") { }
}
