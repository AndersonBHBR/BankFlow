using Microsoft.AspNetCore.Http;

namespace BankFlow.ServiceDefaults;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers.Append("Referrer-Policy", "no-referrer");
        headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
        return next(context);
    }
}
