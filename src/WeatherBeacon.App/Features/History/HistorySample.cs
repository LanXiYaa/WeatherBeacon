using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Features.History;
/// <summary>Deterministic, season-shaped sample year for previewing the weather record page (developer mode only, never saved).</summary>
public static class HistorySample
{
 public static IReadOnlyDictionary<DateOnly,DayRecord> Year(DateOnly end,string city)
 {
  var random=new Random(20261024);var result=new Dictionary<DateOnly,DayRecord>();
  for(var date=end.AddDays(-371);date<=end;date=date.AddDays(1))
  {
   if(random.NextDouble()<.07)continue; // days the app was not running
   var month=date.Month;bool summer=month is >=5 and <=9,winter=month is 12 or 1 or 2;
   var roll=random.NextDouble();
   var kind=winter
    ?roll switch{<.34=>WeatherKind.Clear,<.58=>WeatherKind.Cloudy,<.74=>WeatherKind.Overcast,<.86=>WeatherKind.Fog,<.93=>WeatherKind.Snow,_=>WeatherKind.Rain}
    :summer
     ?roll switch{<.22=>WeatherKind.Clear,<.44=>WeatherKind.Cloudy,<.54=>WeatherKind.Overcast,<.82=>WeatherKind.Rain,_=>WeatherKind.Thunderstorm}
     :roll switch{<.30=>WeatherKind.Clear,<.55=>WeatherKind.Cloudy,<.70=>WeatherKind.Overcast,<.76=>WeatherKind.Fog,<.95=>WeatherKind.Rain,_=>WeatherKind.Thunderstorm};
   var high=Math.Round(16+13*Math.Sin((date.DayOfYear-105)/365.0*2*Math.PI)+random.NextDouble()*4-2);
   var description=kind switch{WeatherKind.Rain=>random.NextDouble()<.5?"小雨":"多云转阵雨",WeatherKind.Thunderstorm=>"雷阵雨",WeatherKind.Snow=>"小雪",WeatherKind.Fog=>"雾",WeatherKind.Overcast=>"阴",WeatherKind.Cloudy=>"多云",_=>"晴"};
   result[date]=new DayRecord(date,kind,description,high-8-random.Next(0,3),high,city,new DateTimeOffset(date.ToDateTime(new TimeOnly(20,0)),TimeSpan.FromHours(8)));
  }
  return result;
 }
}
