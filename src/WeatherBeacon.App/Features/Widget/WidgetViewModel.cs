using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using WeatherBeacon.Core.Models;
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
 public string RainHint=>"暂无分钟级降水预报";
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
 public async Task RefreshAsync(XiaomiWeatherProvider provider,WeatherLocation location,CancellationToken ct)
 {
  Status="正在更新…";Changed(nameof(Status));LastRefreshSucceeded=false;
  try{var data=await provider.GetWeatherAsync(location,ct);Apply(data);last=data;LastRefreshSucceeded=true;Status=$"小米天气 · 更新于 {data.UpdatedAt:HH:mm}";}
  catch(OperationCanceledException)when(ct.IsCancellationRequested){}
  catch{Status=last==null?"暂时无法获取天气，将自动重试":$"更新失败 · 保留 {last.City} {last.UpdatedAt:MM.dd HH:mm} 数据，将自动重试";}
  Changed(nameof(Status));
 }
 private void Apply(WeatherSnapshot data)
 {
  Temperature=Temp(data.Temperature);Description=weatherOverride?.Name??data.Description;City=data.City;Kind=weatherOverride?.Kind??data.Kind;
  last=data;AlignHours(DateTimeOffset.Now);Days.Clear();
  foreach(var d in data.Days.Take(5))Days.Add(new(d.Kind,d.Date.ToString("MM.dd"),$"{Temp(d.Low)} / {Temp(d.High)}",d.Date==DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)),string.IsNullOrEmpty(d.Description)?XiaomiWeatherProvider.Label(d.Kind):d.Description));
  Changed(null);
 }
 public void AlignHours(DateTimeOffset now)
 {
  var items=(last?.Hours??[]).Where(h=>h.Time.AddHours(1)>now).Select(h=>new ForecastItem(h.Kind,h.Time.ToString("HH:mm"),Temp(h.Temperature))).ToArray();
  if(Hours.SequenceEqual(items))return;Hours.Clear();foreach(var item in items)Hours.Add(item);
 }
 public static string Glyph(WeatherKind k)=>k switch{WeatherKind.Clear=>"☀",WeatherKind.Cloudy or WeatherKind.Overcast=>"☁",WeatherKind.Rain=>"☂",WeatherKind.Snow=>"❄",WeatherKind.Fog=>"≋",WeatherKind.Thunderstorm=>"ϟ",_=>"?"};
 private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
