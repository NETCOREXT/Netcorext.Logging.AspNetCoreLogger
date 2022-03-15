namespace Netcorext.Logging.AspNetCoreLogger;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseAspNetCoreLogger(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggerMiddleware>()
                      .UseMiddleware<ResponseLoggerMiddleware>();
    }

    public static IApplicationBuilder UseRequestLogger(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggerMiddleware>();
    }

    public static IApplicationBuilder UseResponseLogger(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ResponseLoggerMiddleware>();
    }
}