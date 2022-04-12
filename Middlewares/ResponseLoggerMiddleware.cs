using System.Text;
using Microsoft.IO;

namespace Netcorext.Logging.AspNetCoreLogger;

public class ResponseLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

    public static class EventIds
    {
        public static readonly EventId RequestInfo = new EventId(10000, "RequestInfo");
        public static readonly EventId ResponseInfo = new EventId(10001, "ResponseInfo");
    }

    public ResponseLoggerMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _logger = loggerFactory.CreateLogger<ResponseLoggerMiddleware>();
        _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
    }

    public async Task Invoke(HttpContext context)
    {
        if (!_logger.IsEnabled(LogLevel.Trace))
        {
            await _next(context);

            return;
        }

        var content = $"Content is empty or Content-Type ({context.Response.ContentType}) is not support log.";

        if (IsGrpc(context))
        {
            await _next(context);

            goto logContent;
        }

        var originalBodyStream = context.Response.Body;
        var responseStream = _recyclableMemoryStreamManager.GetStream();

        context.Response.Body = responseStream;

        await _next(context);

        if (context.Response.ContentType != null
         && (context.Response.ContentType.Contains("text/")
          || context.Response.ContentType.Contains("application/json")
          || context.Response.ContentType.Contains("application/xml")
          || context.Response.ContentType.Contains("application/x-www-form-urlencoded")))
        {
            content = await ReadStreamAsync(responseStream, false);
        }

        await responseStream.CopyToAsync(originalBodyStream);

        context.Response.Body = originalBodyStream;

        logContent:

        _logger.LogTrace(EventIds.ResponseInfo,
                         $"Response Information:{Environment.NewLine}" +
                         $"Headers:{Environment.NewLine}{GetHeaders(context.Response.Headers)}" +
                         $"Content:{Environment.NewLine}{content}");
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