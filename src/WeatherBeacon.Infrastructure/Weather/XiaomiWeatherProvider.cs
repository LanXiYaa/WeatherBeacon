using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using WeatherBeacon.Core.Interfaces;
using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Infrastructure.Weather;
public sealed class XiaomiWeatherProvider : IWeatherProvider
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string Root = "https://weatherapi.market.xiaomi.com/wtr-v3/";
    public string Id => "xiaomi";
    private static string S(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.ToString() : "";
    private static double? N(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    private static async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return JsonDocument.Parse(await Client.GetStringAsync(Root + path, ct)); }
            catch (Exception ex) when (attempt < 2 && !ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException)
            { await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct); }
        }
    }
    public async Task<IReadOnlyList<WeatherLocation>> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        using var doc = await GetAsync("location/city/search?name=" + Uri.EscapeDataString(query.Trim()) + "&locale=zh_cn", ct);
        return doc.RootElement.EnumerateArray().Select(e => new WeatherLocation(S(e,"name") + " · " + S(e,"affiliation"), S(e,"locationKey"), N(S(e,"latitude")), N(S(e,"longitude")))).ToArray();
    }
    public async Task<WeatherSnapshot> GetWeatherAsync(WeatherLocation location, CancellationToken ct)
    {
        var lat = location.Latitude?.ToString(CultureInfo.InvariantCulture);
        var lon = location.Longitude?.ToString(CultureInfo.InvariantCulture);
        using var doc = await GetAsync($"weather/all?latitude={lat}&longitude={lon}&locationKey={Uri.EscapeDataString(location.ProviderCityId)}&days=15&appKey=weather20151024&sign=zUFJoAR2ZVrDy1vF3D07&isGlobal=false&locale=zh_cn", ct);
        return Parse(doc.RootElement, location.Name);
    }
    public static WeatherSnapshot Parse(JsonElement root, string city)
    {
        var c = root.GetProperty("current");
        var updated = DateTimeOffset.Parse(S(c,"pubTime"), CultureInfo.InvariantCulture);
        var kind = Kind(S(c,"weather"));
        List<HourlyWeather> hours = [];
        if (root.TryGetProperty("forecastHourly", out var h) && h.TryGetProperty("weather", out var hw) && hw.TryGetProperty("value", out var codes))
        {
            var start = DateTimeOffset.Parse(S(hw,"pubTime"), CultureInfo.InvariantCulture);
            var temperatures = h.GetProperty("temperature").GetProperty("value");
            for (int i=0; i<codes.GetArrayLength(); i++) hours.Add(new(start.AddHours(i), i < temperatures.GetArrayLength() ? N(temperatures[i].ToString()) : null, Kind(codes[i].ToString())));
        }
        List<DailyWeather> days = [];
        if (root.TryGetProperty("forecastDaily", out var d))
        {
            var dailyCodes = d.GetProperty("weather").GetProperty("value");
            var temps = d.GetProperty("temperature").GetProperty("value");
            var first = DateOnly.FromDateTime(DateTimeOffset.Parse(S(d,"pubTime"), CultureInfo.InvariantCulture).DateTime);
            for (int i=0; i<Math.Min(dailyCodes.GetArrayLength(), temps.GetArrayLength()); i++) days.Add(new(first.AddDays(i), N(S(temps[i],"to")), N(S(temps[i],"from")), Kind(S(dailyCodes[i],"from"))));
        }
        return new(city, updated, N(S(c.GetProperty("temperature"),"value")), Label(kind), kind, hours, days, []);
    }
    public static WeatherKind Kind(string code) => code switch
    {
        "0" => WeatherKind.Clear, "1" or "2" => WeatherKind.Cloudy,
        "4" or "5" => WeatherKind.Thunderstorm,
        "13" or "14" or "15" or "16" or "17" or "26" or "27" or "28" => WeatherKind.Snow,
        "18" or "20" or "29" or "30" or "31" or "53" => WeatherKind.Fog,
        "3" or "6" or "7" or "8" or "9" or "10" or "11" or "12" or "19" or "21" or "22" or "23" or "24" or "25" => WeatherKind.Rain,
        _ => WeatherKind.Unknown
    };
    public static string Label(WeatherKind kind) => kind switch { WeatherKind.Clear=>"晴", WeatherKind.Cloudy=>"多云 / 阴", WeatherKind.Rain=>"雨", WeatherKind.Snow=>"雪", WeatherKind.Fog=>"雾 / 霾", WeatherKind.Thunderstorm=>"雷暴", _=>"未知天气" };
}

