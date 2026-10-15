namespace Relewise.BlazorApps.Components;

// Date picker formats copied from the Hub, without its unrelated formatting helpers.
public static class DateFormatExtensions
{
    public static string? GetFormat(this DateFormat? format) => format switch
    {
        DateFormat.Date => "dd. MMM yyyy",
        DateFormat.DateAndTime => "dd. MMM yyyy @ HH:mm",
        DateFormat.DateAndTimeWithoutYear => "dd. MMM @ HH:mm",
        DateFormat.DateAndTimeUtc => "dd. MMM yyyy @ HH:mm (UTC)",
        DateFormat.DateWithoutYear => "dd. MMM",
        DateFormat.DateLongMonthYear => "dd. MMMM yyyy",
        DateFormat.DateLongMonthNoYear => "dd. MMMM",
        DateFormat.DateWithDayOfWeek => "ddd. dd. MMM yyyy",
        DateFormat.DateAndTimeWithDayOfWeek => "ddd. dd. MMM yyyy @ HH:mm",
        DateFormat.MonthYear => "MMM yyyy",
        DateFormat.LongMonthYear => "MMMM yyyy",
        DateFormat.Time => "HH:mm",
        null => null,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };
}

public enum DateFormat
{
    Date,
    DateAndTime,
    DateAndTimeWithoutYear,
    DateAndTimeUtc,
    DateWithoutYear,
    DateLongMonthYear,
    DateLongMonthNoYear,
    DateWithDayOfWeek,
    DateAndTimeWithDayOfWeek,
    MonthYear,
    LongMonthYear,
    Time
}
