namespace WeatherBeacon.Core.Models;
public enum WeatherKind { Unknown, Clear, Cloudy, Overcast, Rain, Snow, Fog, Thunderstorm }
public enum AlertLevel { Blue, Yellow, Orange, Red }
public record HourlyWeather(DateTimeOffset Time, double? Temperature, WeatherKind Kind);
public record DailyWeather(DateOnly Date, double? Low, double? High, WeatherKind Kind, string Description="");
public record WeatherAlert(string Id, string Title, string Description, AlertLevel Level, DateTimeOffset? Expires, string Type="", DateTimeOffset? PublishedAt=null);
public record WeatherSnapshot(string City, DateTimeOffset UpdatedAt, double? Temperature, string Description, WeatherKind Kind, IReadOnlyList<HourlyWeather> Hours, IReadOnlyList<DailyWeather> Days, IReadOnlyList<WeatherAlert> Alerts, bool AlertsComplete=false, string AlertStatus="预警数据尚未读取");
public record WeatherLocation(string Name, string ProviderCityId, double? Latitude, double? Longitude);


