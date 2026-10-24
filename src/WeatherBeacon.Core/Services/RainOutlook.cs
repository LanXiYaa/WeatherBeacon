using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Core.Services;
/// <summary>How urgent a precipitation reminder is; drives the widget's accent colour.</summary>
public enum RainLevel { None, Later, Soon, Now }
public sealed record RainReminder(RainLevel Level, string Text);
/// <summary>Turns the hourly forecast into one short sentence such as "今晚 20 时起有小雨，记得带伞".</summary>
public static class RainOutlook
{
    /// <summary>How far ahead the reminder looks.</summary>
    public const int WindowHours = 24;
    public static bool IsWet(WeatherKind kind) => kind is WeatherKind.Rain or WeatherKind.Thunderstorm;
    public static RainReminder Describe(IReadOnlyList<HourlyWeather> hours, WeatherKind current, DateTimeOffset now)
    {
        // Each slot covers [Time, Time+1h); keep the one that contains "now" and the next 24 hours.
        var upcoming = hours.Where(h => h.Time.AddHours(1) > now && h.Time < now.AddHours(WindowHours)).OrderBy(h => h.Time).ToList();
        if (upcoming.Count == 0) return new(RainLevel.None, "");
        bool wetNow = IsWet(current) || (upcoming[0].Time <= now && IsWet(upcoming[0].Kind));
        if (wetNow)
        {
            var dry = upcoming.FirstOrDefault(h => h.Time > now && !IsWet(h.Kind));
            if (dry is null) return new(RainLevel.Now, "正在下雨，未来 24 小时仍有雨，出门带伞");
            return new(RainLevel.Now, $"正在下雨，预计{When(dry.Time, now)}停，出门带伞");
        }
        var first = upcoming.FirstOrDefault(h => IsWet(h.Kind));
        if (first is null) return new(RainLevel.None, "未来 24 小时无降雨");
        var what = Precipitation(first);
        var minutes = (first.Time - now).TotalMinutes;
        if (minutes <= 120)
        {
            var lead = minutes <= 60 ? "1 小时内" : "约 2 小时后";
            return new(RainLevel.Soon, $"{lead}有{what}，出门记得带伞");
        }
        return new(RainLevel.Later, $"{When(first.Time, now)}起有{what}，记得带伞");
    }
    private static string Precipitation(HourlyWeather hour)
    {
        var label = hour.Description;
        if (string.IsNullOrWhiteSpace(label) || label.Contains('/')) return hour.Kind == WeatherKind.Thunderstorm ? "雷阵雨" : "雨";
        return label;
    }
    /// <summary>"今晚 20 时" / "明天 9 时", in the forecast's own time zone (the city's clock, not the PC's).</summary>
    public static string When(DateTimeOffset time, DateTimeOffset now)
    {
        var local = now.ToOffset(time.Offset);
        int days = time.Date.Subtract(local.Date).Days;
        int hour = time.Hour;
        string day = days switch
        {
            0 => hour >= 18 ? "今晚" : "今天",
            // Small hours right after an evening still read as "tonight".
            1 when hour < 6 && local.Hour >= 18 => "今夜",
            1 => hour >= 18 ? "明晚" : "明天",
            _ => time.ToString("M月d日")
        };
        return $"{day} {hour} 时";
    }
}
