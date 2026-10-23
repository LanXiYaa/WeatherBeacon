using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WeatherBeacon.Features.Widget;
using WeatherBeacon.Features.Settings;
using WeatherBeacon.Infrastructure.Weather;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Controls;
internal static class Program
{
 [STAThread]static void Main(string[] args)
 {
  var app=new Application();Directory.CreateDirectory(args[0]);
  var widget=new WidgetWindow();var content=(FrameworkElement)widget.Content;
  content.DataContext=new{City="北京市 · 中国",Temperature="19°",Description="晴",Kind=WeatherKind.Clear,Status="小米天气 · 更新于 19:00",Hours=Enumerable.Range(0,8).Select(i=>new ForecastItem((WeatherKind)(i%6+1),$"{19+i:00}:00","19°")).ToArray(),Days=Enumerable.Range(0,5).Select(i=>new ForecastItem((WeatherKind)(i%6+1),$"10.{23+i}","12° / 21°",i==0)).ToArray()};
  Render(content,420,430,Path.Combine(args[0],"widget.png"));
  var settings=new SettingsWindow(new SettingsModel(),new XiaomiWeatherProvider(),()=>{});
  Render((FrameworkElement)settings.Content,944,700,Path.Combine(args[0],"settings.png"));
  var panel=new System.Windows.Controls.WrapPanel{Width=560,Background=new SolidColorBrush(Color.FromRgb(17,34,49))};
  foreach(var kind in Enum.GetValues<WeatherKind>())panel.Children.Add(new WeatherIcon{Kind=kind,Width=80,Height=80});
  Render(panel,560,80,Path.Combine(args[0],"icons.png"));
  Console.WriteLine("PASS: widget, settings and all 7 weather icon states rendered.");
 }
 static void Render(FrameworkElement element,int width,int height,string path)
 {
  element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();
  var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bmp.Render(element);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(path);encoder.Save(stream);
 }
}

