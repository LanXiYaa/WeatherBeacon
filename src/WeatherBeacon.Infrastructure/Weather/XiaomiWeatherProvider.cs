using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using WeatherBeacon.Core.Interfaces;
using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Infrastructure.Weather;
public sealed class XiaomiWeatherProvider : IWeatherProvider
{
    private readonly HttpClient Client;
    public XiaomiWeatherProvider(HttpClient? client=null){Client=client??new HttpClient{Timeout=TimeSpan.FromSeconds(15)};}
    private const string Root = "https://weatherapi.market.xiaomi.com/wtr-v3/";
    public string Id => "xiaomi";
    private static string S(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.ToString() : "";
    private static double? N(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
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
            for (int i=0; i<codes.GetArrayLength(); i++) hours.Add(new(start.AddHours(i), i < temperatures.GetArrayLength() ? N(temperatures[i].ToString()) : null, Kind(codes[i].ToString()), CodeLabel(codes[i].ToString())));
        }
        List<DailyWeather> days = [];
        if (root.TryGetProperty("forecastDaily", out var d))
        {
            var dailyCodes = d.GetProperty("weather").GetProperty("value");
            var temps = d.GetProperty("temperature").GetProperty("value");
            var first = DateOnly.FromDateTime(DateTimeOffset.Parse(S(d,"pubTime"), CultureInfo.InvariantCulture).DateTime);
            for (int i=0; i<Math.Min(dailyCodes.GetArrayLength(), temps.GetArrayLength()); i++) days.Add(new(first.AddDays(i), N(S(temps[i],"to")), N(S(temps[i],"from")), Kind(S(dailyCodes[i],"from")), DailyLabel(S(dailyCodes[i],"from"),S(dailyCodes[i],"to"))));
        }
        var alerts=ParseAlerts(root);
        return new(city, updated, N(S(c.GetProperty("temperature"),"value")), CodeLabel(S(c,"weather")), kind, hours, days, alerts.Items, alerts.Complete, alerts.Status);
    }
    public static (IReadOnlyList<WeatherAlert> Items,bool Complete,string Status) ParseAlerts(JsonElement root)
    {
        if(!root.TryGetProperty("alerts",out var array)||array.ValueKind!=JsonValueKind.Array)
            return ([],false,"小米未返回可识别的预警数组；无法确认当前是否有预警");
        List<WeatherAlert> items=[];int skipped=0;
        foreach(var a in array.EnumerateArray())
        {
            if(a.ValueKind!=JsonValueKind.Object){skipped++;continue;}
            var level=S(a,"level") switch{"蓝色"=>AlertLevel.Blue,"黄色"=>AlertLevel.Yellow,"橙色"=>AlertLevel.Orange,"红色"=>AlertLevel.Red,_=>(AlertLevel?)null};
            var id=S(a,"alertId");var type=S(a,"type");var title=S(a,"title");var detail=S(a,"detail");
            if(level is null||string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(type)||string.IsNullOrWhiteSpace(title)||string.IsNullOrWhiteSpace(detail)||!DateTimeOffset.TryParse(S(a,"pubTime"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var published))
            {skipped++;continue;}
            items.Add(new(id,title,detail,level.Value,null,type,published));
        }
        return (items,skipped==0,skipped>0?$"读取 {items.Count} 条预警；{skipped} 条字段不支持，已跳过（不会据此判断解除）":items.Count==0?"小米当前返回空预警列表":$"小米当前返回 {items.Count} 条预警");
    }
    public async Task<IReadOnlyList<WeatherLocation>> LocateAsync(CancellationToken ct)
    {
        // IP lookup is only called after the user clicks the locate button.
        using var doc=JsonDocument.Parse(await Client.GetStringAsync("https://ipwho.is/",ct));
        var r=doc.RootElement;if(r.TryGetProperty("success",out var ok)&&!ok.GetBoolean())throw new HttpRequestException("IP lookup unavailable");
        var lat=r.GetProperty("latitude").ToString();var lon=r.GetProperty("longitude").ToString();
        using var cities=await GetAsync($"location/city/geo?latitude={Uri.EscapeDataString(lat)}&longitude={Uri.EscapeDataString(lon)}&locale=zh_cn",ct);
        var root=cities.RootElement;var list=root.ValueKind==JsonValueKind.Array?root.EnumerateArray().ToArray():new[]{root};
        return list.Select(e=>new WeatherLocation(S(e,"name")+" · "+S(e,"affiliation"),S(e,"locationKey"),N(S(e,"latitude")),N(S(e,"longitude")))).Where(c=>!string.IsNullOrEmpty(c.ProviderCityId)).ToArray();
    }
    public static string DailyLabel(string from,string to)=>from==to?CodeLabel(from):CodeLabel(from)+"转"+CodeLabel(to);
    public static string CodeLabel(string code)=>code switch{"3"=>"阵雨","4"=>"雷阵雨","5"=>"雷阵雨伴冰雹","6"=>"雨夹雪","7"=>"小雨","8"=>"中雨","9"=>"大雨","10"=>"暴雨","11"=>"大暴雨","12"=>"特大暴雨","13"=>"阵雪","14"=>"小雪","15"=>"中雪","16"=>"大雪","17"=>"暴雪","18"=>"雾","19"=>"冻雨","20"=>"沙尘暴","21"=>"小到中雨","22"=>"中到大雨","23"=>"大到暴雨","24"=>"暴雨到大暴雨","25"=>"大暴雨到特大暴雨","53"=>"霾",_=>Label(Kind(code))};
    public static WeatherKind Kind(string code) => code switch
    {
        "0" => WeatherKind.Clear, "1" => WeatherKind.Cloudy, "2" => WeatherKind.Overcast,
        "4" or "5" => WeatherKind.Thunderstorm,
        "13" or "14" or "15" or "16" or "17" or "26" or "27" or "28" => WeatherKind.Snow,
        "18" or "20" or "29" or "30" or "31" or "53" => WeatherKind.Fog,
        "3" or "6" or "7" or "8" or "9" or "10" or "11" or "12" or "19" or "21" or "22" or "23" or "24" or "25" => WeatherKind.Rain,
        _ => WeatherKind.Unknown
    };
    public static string Label(WeatherKind kind) => kind switch { WeatherKind.Clear=>"晴", WeatherKind.Cloudy=>"多云", WeatherKind.Overcast=>"阴", WeatherKind.Rain=>"雨", WeatherKind.Snow=>"雪", WeatherKind.Fog=>"雾 / 霾", WeatherKind.Thunderstorm=>"雷暴", _=>"未知天气" };
}



