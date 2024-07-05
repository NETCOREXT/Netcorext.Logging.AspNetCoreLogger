using System.Text;
using Microsoft.IO;

namespace Netcorext.Logging.AspNetCoreLogger;

public class ResponseLoggerMiddleware
{
    private const string RESPONSE_INFO_FORMAT = "Response Information {Protocol} {Method} {Scheme}://{Host}{PathBase}{Path}{QueryString} - {StatusCode} {ContentLength} {ContentType}\nHeaders:\n{Headers}\nContent:\n{Content}";

    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

    public ResponseLoggerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _logger = loggerFactory.CreateLogger($"Microsoft.AspNetCore.Hosting.Diagnostics.ResponseLogger");
        _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
    }

    public async Task Invoke(HttpContext context)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            await _next(context);

            return;
        }

        var content = $"Content is empty or Content-Type ({context.Response.ContentType}) is not support log.";

        if (context.Response.ContentType != null
         && (context.Response.ContentType.Contains("text/")
          || context.Response.ContentType.Contains("application/json")
          || context.Response.ContentType.Contains("application/xml")
          || context.Response.ContentType.Contains("application/x-www-form-urlencoded")))
        {
            var responseStream = _recyclableMemoryStreamManager.GetStream();

            await context.Response.Body.CopyToAsync(responseStream);

            content = await ReadStreamAsync(responseStream, false);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
        }

        _logger.Log(LogLevel.Information,
                    LoggerEventIds.ResponseInfo,
                    RESPONSE_INFO_FORMAT,
                    context.Request.Protocol,
                    context.Request.Method,
                    context.Request.Scheme,
                    context.Request.Host,
                    context.Request.PathBase,
                    context.Request.Path,
                    context.Request.QueryString,
                    context.Response.StatusCode,
                    context.Response.ContentLength,
                    context.Response.ContentType,
                    GetHeaders(context.Response.Headers),
                    content);

        await _next(context);
    }

    private static async Task<string> ReadStreamAsync(Stream stream, bool enableDispose = true)
    {
        stream.Seek(0, SeekOrigin.Begin);

        if (!enableDispose)
        {
            return await new StreamReader(stream).ReadToEndAsync();
        }

        using var streamReader = new StreamReader(stream);

        return await streamReader.ReadToEndAsync();
    }

    private static string GetHeaders(IHeaderDictionary headers)
    {
        var builder = new StringBuilder();

        foreach (var header in headers)
        {
            builder.Append(header.Key);
            builder.Append(": ");

            foreach (var value in (IEnumerable<object>)header.Value)
            {
                builder.Append(value);
                builder.Append(", ");
            }

            // Remove the extra ', '
            builder.Remove(builder.Length - 2, 2);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static bool IsGrpc(HttpContext context)
    {
        return context.Request.Protocol == "HTTP/2" && context.Request.Headers.ContentType == "application/grpc";
    }
}
