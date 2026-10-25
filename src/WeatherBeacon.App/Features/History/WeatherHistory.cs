using System.IO;
using System.Text.Json;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Infrastructure.Weather;
namespace WeatherBeacon.Features.History;
/// <summary>One calendar day as WeatherBeacon saw it: the day's forecast weather for the chosen city, last updated during that day.</summary>
public sealed record DayRecord(DateOnly Date,WeatherKind Kind,string Description,double? Low,double? High,string City,DateTimeOffset UpdatedAt);
/// <summary>
/// Keeps one record per day in weather-history.json (about a year, local only). A day is written on every successful refresh
/// during that day, so the last refresh of the day wins. Days when the app was not running stay empty.
/// </summary>
public sealed class WeatherHistory
{
 public const int KeepDays=400;
 private readonly string path;private readonly object gate=new();
 private Dictionary<DateOnly,DayRecord> days=[];
 public event Action? Changed;
 public WeatherHistory(string path){this.path=path;Load();}
 public static WeatherHistory Default{get;}=new(Path.Combine(Settings.SettingsModel.Folder,"weather-history.json"));
 public IReadOnlyDictionary<DateOnly,DayRecord> Days{get{lock(gate)return new Dictionary<DateOnly,DayRecord>(days);}}
 /// <summary>Records today's weather from a fresh snapshot. "Today" is the city's date (the forecast's own clock).</summary>
 public void Record(WeatherSnapshot snapshot)
 {
  var today=DateOnly.FromDateTime(snapshot.UpdatedAt.DateTime);
  var day=snapshot.Days.FirstOrDefault(d=>d.Date==today);
  var record=day is null
   ?new DayRecord(today,snapshot.Kind,snapshot.Description,null,null,snapshot.City,snapshot.UpdatedAt)
   :new DayRecord(today,day.Kind,string.IsNullOrEmpty(day.Description)?XiaomiWeatherProvider.Label(day.Kind):day.Description,day.Low,day.High,snapshot.City,snapshot.UpdatedAt);
  lock(gate)
  {
   if(days.TryGetValue(today,out var existing)&&existing with{UpdatedAt=record.UpdatedAt}==record)return;
   days[today]=record;
   foreach(var old in days.Keys.Where(d=>d<today.AddDays(-KeepDays)).ToArray())days.Remove(old);
   Save();
  }
  Changed?.Invoke();
 }
 private void Load()
 {
  try
  {
   if(!File.Exists(path))return;
   var list=JsonSerializer.Deserialize<List<DayRecord>>(File.ReadAllText(path))??[];
   days=list.GroupBy(d=>d.Date).ToDictionary(g=>g.Key,g=>g.OrderBy(d=>d.UpdatedAt).Last());
  }
  catch(Exception ex){Platform.AppLog.Warn("History","天气记录无法读取，将从空记录开始",ex);days=[];}
 }
 private void Save()
 {
  try
  {
   Directory.CreateDirectory(Path.GetDirectoryName(path)!);
   File.WriteAllText(path+".tmp",JsonSerializer.Serialize(days.Values.OrderBy(d=>d.Date)));File.Move(path+".tmp",path,true);
  }
  catch(Exception ex){Platform.AppLog.Warn("History","天气记录保存失败",ex);}
 }
}
