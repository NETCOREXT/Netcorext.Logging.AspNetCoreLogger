namespace Netcorext.Logging.AspNetCoreLogger;

internal static class LoggerEventIds
{
    public static readonly EventId RequestInfo = new(10000, "RequestInfo");
    public static readonly EventId ResponseInfo = new(10001, "ResponseInfo");
}
