using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Core.Services;
using WeatherBeacon.Infrastructure.Weather;
namespace WeatherBeacon.Features.Widget;
public sealed record ForecastItem(WeatherKind Kind,string Time,string Temperature,bool IsToday=false,string Description="");
public sealed class WidgetViewModel : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;
 public string Temperature{get;private set;}="—°";
 public string Description{get;private set;}="请选择城市";
 public string City{get;private set;}="WEATHERBEACON";
 public string Status{get;private set;}="右键打开设置";
 public string RainHint{get;private set;}="";
 public RainLevel RainLevel{get;private set;}
 /// <summary>Shown on the widget only when the user keeps the reminder on and there is a forecast to describe.</summary>
 public bool ShowRain=>rainEnabled&&!string.IsNullOrEmpty(RainHint);
 private bool rainEnabled=true;
 public void SetRainEnabled(bool value){if(rainEnabled==value)return;rainEnabled=value;Changed(nameof(ShowRain));}
 public string Icon=>Glyph(Kind);
 public WeatherKind Kind{get;private set;}
 public WeatherSnapshot? LastSnapshot=>last;
 private WeatherBeacon.Features.Developer.WeatherOverride? weatherOverride;
 public bool IsDebugging=>weatherOverride is not null;
 public void SetOverride(WeatherBeacon.Features.Developer.WeatherOverride? value)
 {
  weatherOverride=value;
  if(last is not null)Apply(last);
  else{Kind=value?.Kind??WeatherKind.Unknown;Description=value?.Name??"请选择城市";Changed(null);}
 }
 private WeatherSnapshot? last;private bool fahrenheit;
 public bool LastRefreshSucceeded{get;private set;}
 public ObservableCollection<ForecastItem> Hours{get;}=[];
 public ObservableCollection<ForecastItem> Days{get;}=[];
 public void SetUnit(bool value){fahrenheit=value;if(last!=null)Apply(last);}
 private string Temp(double? value)=>value is double t?$"{(fahrenheit?t*9/5+32:t):0}°{(fahrenheit?"F":"C")}":"—";
 private string Short(double? value)=>value is double t?$"{(fahrenheit?t*9/5+32:t):0}°":"—";
 public async Task RefreshAsync(XiaomiWeatherProvider provider,WeatherLocation location,CancellationToken ct)
 {
  Status="正在更新…";Changed(nameof(Status));LastRefreshSucceeded=false;
  try{var data=await provider.GetWeatherAsync(location,ct);Apply(data);last=data;LastRefreshSucceeded=true;Status=$"小米天气 · 更新于 {data.UpdatedAt:HH:mm}";WeatherBeacon.Platform.AppLog.Info("Weather",$"刷新成功：{data.City}，{data.Description} {data.Temperature}°，逐小时 {data.Hours.Count} 条，预警 {data.Alerts.Count} 条");}
  catch(OperationCanceledException)when(ct.IsCancellationRequested){}
  catch(Exception ex){WeatherBeacon.Platform.AppLog.Warn("Weather",$"刷新失败：{location.Name}",ex);Status=last==null?"暂时无法获取天气，将自动重试":$"更新失败 · 保留 {last.City} {last.UpdatedAt:MM.dd HH:mm} 数据，将自动重试";}
  Changed(nameof(Status));
 }
 private void Apply(WeatherSnapshot data)
 {
  Temperature=Temp(data.Temperature);Description=weatherOverride?.Name??data.Description;City=data.City;Kind=weatherOverride?.Kind??data.Kind;
  last=data;AlignHours(DateTimeOffset.Now);Days.Clear();
  foreach(var d in data.Days.Take(5))Days.Add(new(d.Kind,d.Date.ToString("MM.dd"),$"{Short(d.Low)} / {Short(d.High)}",d.Date==DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)),string.IsNullOrEmpty(d.Description)?XiaomiWeatherProvider.Label(d.Kind):d.Description));
  Changed(null);
 }
 public void AlignHours(DateTimeOffset now)
 {
  var items=(last?.Hours??[]).Where(h=>h.Time.AddHours(1)>now).Select(h=>new ForecastItem(h.Kind,h.Time.ToString("HH:mm"),Temp(h.Temperature))).ToArray();
  UpdateRain(now);
  if(Hours.SequenceEqual(items))return;Hours.Clear();foreach(var item in items)Hours.Add(item);
 }
 private void UpdateRain(DateTimeOffset now)
 {
  // The developer weather preview only changes visuals; the reminder always reflects the real forecast.
  var reminder=last is null?new RainReminder(RainLevel.None,""):RainOutlook.Describe(last.Hours,last.Kind,now);
  if(reminder.Text==RainHint&&reminder.Level==RainLevel)return;
  RainHint=reminder.Text;RainLevel=reminder.Level;Changed(nameof(RainHint));Changed(nameof(RainLevel));Changed(nameof(ShowRain));
 }
 public static string Glyph(WeatherKind k)=>k switch{WeatherKind.Clear=>"☀",WeatherKind.Cloudy or WeatherKind.Overcast=>"☁",WeatherKind.Rain=>"☂",WeatherKind.Snow=>"❄",WeatherKind.Fog=>"≋",WeatherKind.Thunderstorm=>"ϟ",_=>"?"};
 private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
