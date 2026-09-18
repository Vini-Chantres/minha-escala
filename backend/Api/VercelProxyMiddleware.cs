using System.Net;

// Ative somente em contêiner com ingresso privado gerenciado pela Vercel.
// A plataforma define estes headers; servidores públicos independentes não devem confiar neles.
public sealed class VercelProxyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (configuration.GetValue<bool>("VERCEL_PROXY"))
        {
            var scheme = context.Request.Headers["x-forwarded-proto"].ToString();
            if (scheme is "https" or "http") context.Request.Scheme = scheme;
            if (IPAddress.TryParse(context.Request.Headers["x-vercel-forwarded-for"].ToString(), out var ip))
                context.Connection.RemoteIpAddress = ip;
        }
        return next(context);
    }
}
