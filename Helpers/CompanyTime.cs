namespace HRMS_BACKEND.Helpers;

public static class CompanyTime
{
    private static TimeZoneInfo? _cachedZone;
    private static readonly object _lock = new();

    public static TimeZoneInfo GetZone(IConfiguration config)
    {
        if (_cachedZone is not null) return _cachedZone;
        lock (_lock)
        {
            if (_cachedZone is not null) return _cachedZone;
            var tzId = config["AppSettings:TimeZoneId"] ?? "UTC";
            try
            {
                _cachedZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch (TimeZoneNotFoundException)
            {
                _cachedZone = TimeZoneInfo.Utc;
            }
            return _cachedZone;
        }
    }

    public static DateTime Now(IConfiguration config)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetZone(config));

    public static DateOnly Today(IConfiguration config)
        => DateOnly.FromDateTime(Now(config));

    public static DateTime FromUtc(IConfiguration config, DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(utc, GetZone(config));

    public static DateTime ToUtc(IConfiguration config, DateTime companyLocal)
        => TimeZoneInfo.ConvertTimeToUtc(companyLocal, GetZone(config));
}
