using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Features.Developer;
public sealed record WeatherOverride(string Name,WeatherKind Kind,bool Daytime,double RainIntensity=1);
public sealed record WeatherPreset(string Name,WeatherKind Kind,double RainIntensity=1)
{
 public override string ToString()=>Name;
 public static WeatherPreset[] All {get;}=[new("晴",WeatherKind.Clear),new("多云",WeatherKind.Cloudy),new("阴",WeatherKind.Overcast),new("小雨",WeatherKind.Rain,.45),new("中雨",WeatherKind.Rain),new("大雨",WeatherKind.Rain,1.65),new("雷暴",WeatherKind.Thunderstorm),new("雪",WeatherKind.Snow),new("雾",WeatherKind.Fog),new("未知天气",WeatherKind.Unknown)];
}
