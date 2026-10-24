using System.Drawing;
namespace WeatherBeacon.Platform;
internal static class TrayArtwork
{
 public static Icon Create()
 {
  var resource=System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/WeatherBeacon;component/Assets/WeatherBeacon.ico"))
   ?? throw new InvalidOperationException("WeatherBeacon icon resource is missing.");
  using var stream=resource.Stream;
  using var icon=new Icon(stream,16,16);
  return (Icon)icon.Clone();
 }
}