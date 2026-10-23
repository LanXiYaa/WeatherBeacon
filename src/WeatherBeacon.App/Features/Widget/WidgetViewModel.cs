using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Infrastructure.Weather;
namespace WeatherBeacon.Features.Widget;
public sealed record ForecastItem(WeatherKind Kind, string Time, string Temperature, bool IsToday = false);
public sealed class WidgetViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string temperature="—°", description="请选择城市", city="WEATHERBEACON", status="右键打开城市选择";
    public string Temperature { get=>temperature; private set {temperature=value; Changed();} }
    public string Description { get=>description; private set {description=value; Changed();} }
    public string City { get=>city; private set {city=value; Changed();} }
    public string Status { get=>status; private set {status=value; Changed();} }
    public string Icon { get; private set; } = "☀";
    public WeatherKind Kind { get; private set; }
    private IReadOnlyList<HourlyWeather> hourly=[];
    public ObservableCollection<ForecastItem> Hours { get; } = [];
    public ObservableCollection<ForecastItem> Days { get; } = [];
    public async Task RefreshAsync(XiaomiWeatherProvider provider, WeatherLocation location, CancellationToken ct)
    {
        Status="正在更新…";
        try
        {
            var data=await provider.GetWeatherAsync(location,ct);
            Temperature=data.Temperature is double t ? $"{t:0}°" : "—°";
            Description=data.Description; City=data.City; Kind=data.Kind; Icon=Glyph(data.Kind); Changed(nameof(Icon)); Changed(nameof(Kind));
            hourly=data.Hours; AlignHours(DateTimeOffset.Now);
            Days.Clear(); foreach(var d in data.Days.Take(5)) Days.Add(new(d.Kind,d.Date.ToString("MM.dd"),$"{d.Low:0}° / {d.High:0}°",d.Date==DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8))));
            Status=$"小米天气 · 更新于 {data.UpdatedAt:HH:mm}";
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
        catch { Status="更新失败，已保留原数据 · 右键重试"; }
    }
    public void AlignHours(DateTimeOffset now)
    {
        var upcoming=hourly.Where(h=>h.Time.AddHours(1)>now).ToArray();
        var items=upcoming.Select(h=>new ForecastItem(h.Kind,h.Time.ToString("HH:mm"),$"{h.Temperature:0}°")).ToArray();
        if(Hours.SequenceEqual(items))return;
        Hours.Clear();foreach(var item in items)Hours.Add(item);
    }
    public static string Glyph(WeatherKind k)=>k switch {WeatherKind.Clear=>"☀",WeatherKind.Cloudy=>"☁",WeatherKind.Overcast=>"☁",WeatherKind.Rain=>"☂",WeatherKind.Snow=>"❄",WeatherKind.Fog=>"≋",WeatherKind.Thunderstorm=>"ϟ",_=>"?"};
    private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}


