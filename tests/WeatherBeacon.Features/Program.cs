using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Core.Services;
using WeatherBeacon.Features.Settings;
using WeatherBeacon.Features.Widget;
using WeatherBeacon.Platform;
// Checks for the logging/diagnostics, rain reminder and widget layout features. Usage: WeatherBeacon.Features <output folder>
internal static class Program
{
 static int passed;
 [STAThread] static void Main(string[] args)
 {
  var output=Path.GetFullPath(args.Length>0?args[0]:"features-check");Directory.CreateDirectory(output);
  Rain();
  Logging(output);
  _=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  Widget(output);
  History(output);
  Console.WriteLine($"PASS features: {passed} checks (rain reminder, logging, diagnostics consent, log package, widget scale/font/compact, weather history).");
 }
 static void Rain()
 {
  var tz=TimeSpan.FromHours(8);
  DateTimeOffset At(int day,int hour)=>new(2026,10,24+day,hour,0,0,tz);
  List<HourlyWeather> Hours(DateTimeOffset start,Func<int,WeatherKind> kind,string label="小雨")=>Enumerable.Range(0,24).Select(i=>new HourlyWeather(start.AddHours(i),20,kind(i),kind(i)==WeatherKind.Rain?label:"多云")).ToList();
  var now=At(0,14).AddMinutes(20);
  var evening=RainOutlook.Describe(Hours(At(0,14),i=>i==6?WeatherKind.Rain:WeatherKind.Cloudy),WeatherKind.Cloudy,now);
  Check(evening.Level==RainLevel.Later&&evening.Text=="今晚 20 时起有小雨，记得带伞","evening rain: "+evening.Text);
  var soon=RainOutlook.Describe(Hours(At(0,14),i=>i==1?WeatherKind.Rain:WeatherKind.Cloudy,"阵雨"),WeatherKind.Cloudy,now);
  Check(soon.Level==RainLevel.Soon&&soon.Text.StartsWith("1 小时内有阵雨"),"soon: "+soon.Text);
  var raining=RainOutlook.Describe(Hours(At(0,14),i=>i<3?WeatherKind.Rain:WeatherKind.Cloudy),WeatherKind.Rain,now);
  Check(raining.Level==RainLevel.Now&&raining.Text.Contains("今天 17 时停"),"raining stops: "+raining.Text);
  var dry=RainOutlook.Describe(Hours(At(0,14),_=>WeatherKind.Clear),WeatherKind.Clear,now);
  Check(dry.Level==RainLevel.None&&dry.Text=="未来 24 小时无降雨","dry: "+dry.Text);
  var tomorrow=RainOutlook.Describe(Hours(At(0,14),i=>i==19?WeatherKind.Rain:WeatherKind.Cloudy,"中雨"),WeatherKind.Cloudy,now);
  Check(tomorrow.Text=="明天 9 时起有中雨，记得带伞","tomorrow: "+tomorrow.Text);
  var night=RainOutlook.Describe(Hours(At(0,21),i=>i==5?WeatherKind.Rain:WeatherKind.Cloudy),WeatherKind.Cloudy,At(0,21));
  Check(night.Text=="今夜 2 时起有小雨，记得带伞","small hours: "+night.Text);
  var stale=RainOutlook.Describe(Hours(At(-2,0),_=>WeatherKind.Rain),WeatherKind.Cloudy,now);
  Check(stale.Level==RainLevel.None&&stale.Text=="","expired forecast gives no reminder");
  var thunder=RainOutlook.Describe([new HourlyWeather(At(0,18),20,WeatherKind.Thunderstorm,"雷阵雨")],WeatherKind.Clear,now);
  Check(thunder.Text=="今晚 18 时起有雷阵雨，记得带伞","thunder: "+thunder.Text);
 }
 static void Logging(string output)
 {
  var folder=Path.Combine(output,"logs");if(Directory.Exists(folder))Directory.Delete(folder,true);
  AppLog.UseFolder(folder);
  AppLog.Info("Test","第一条记录");
  AppLog.Warn("Test","带异常的记录",new InvalidOperationException("模拟错误 "+Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
  var text=File.ReadAllText(AppLog.CurrentFile);
  Check(text.Contains("[INFO] [Test] 第一条记录")&&text.Contains("[WARN] [Test] 带异常的记录")&&text.Contains("InvalidOperationException"),"log lines written");
  Check(!text.Contains(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),StringComparison.OrdinalIgnoreCase)&&text.Contains("%USERPROFILE%"),"user profile path redacted");
  var old=Path.Combine(folder,"weatherbeacon-20000101.log");File.WriteAllText(old,"old");File.SetLastWriteTime(old,DateTime.Now.AddDays(-30));
  AppLog.Prune();Check(!File.Exists(old)&&File.Exists(AppLog.CurrentFile),"old logs pruned, current kept");
  var model=new SettingsModel{City=new("兰州 · 甘肃","1",36,103),SystemInfoConsent=false};
  var withheld=Diagnostics.BuildReport(model,"小米天气 · 更新于 10:00");
  Check(withheld.Contains("系统信息（未授权")&&withheld.Contains("CPU：null")&&withheld.Contains("显卡：null")&&withheld.Contains("内存：null")&&withheld.Contains("系统版本：null"),"no consent => system fields null");
  Check(withheld.Contains("兰州 · 甘肃")&&withheld.Contains("第一条记录")&&withheld.Contains("小米天气 · 更新于 10:00"),"report has settings, status and log tail");
  var details=Diagnostics.ReadSystem(true);
  Check(details.Processor is not null&&details.Memory is not null&&details.WindowsVersion is not null,"consent => system details readable");
  model.SystemInfoConsent=true;var granted=Diagnostics.BuildReport(model);
  Check(granted.Contains("系统信息（已授权")&&granted.Contains("CPU："+details.Processor),"consent => details in report");
  File.WriteAllText(Path.Combine(output,"diagnostics-sample.txt"),withheld);
  var zip=Path.Combine(output,"logs-package.zip");if(File.Exists(zip))File.Delete(zip);
  Diagnostics.PackageLogs(model,zip,"测试");
  using(var archive=ZipFile.OpenRead(zip))
  {
   var names=archive.Entries.Select(e=>e.FullName).ToArray();
   Check(names.Contains("diagnostics.txt")&&names.Any(n=>n.StartsWith("logs/weatherbeacon-")),"zip has report and logs: "+string.Join(",",names));
  }
  Check(!File.Exists(zip+".partial"),"no partial file left");
 }
 static void Widget(string output)
 {
  var widget=new WidgetWindow();var shell=(FrameworkElement)widget.Content;
  var vm=new
  {
   City="福州 · 福建",Temperature="26°C",Description="多云",Kind=WeatherKind.Cloudy,Status="小米天气 · 更新于 14:00",IsDebugging=false,
   RainHint="今晚 20 时起有小雨，记得带伞",RainLevel=RainLevel.Later,ShowRain=true,
   Hours=Enumerable.Range(0,8).Select(i=>new ForecastItem((WeatherKind)(i%7+1),$"{14+i:00}:00",$"{26-i}°C")).ToArray(),
   Days=Enumerable.Range(0,5).Select(i=>new ForecastItem((WeatherKind)(i%7+1),$"10.{24+i}","19° / 27°",i==0,"多云转小雨")).ToArray()
  };
  shell.DataContext=vm;
  var normal=Render(widget,shell,1,1,false,Path.Combine(output,"widget-standard.png"));
  var big=Render(widget,shell,1.25,1.2,false,Path.Combine(output,"widget-125-font120.png"));
  Check(Math.Abs(big.Width-normal.Width*1.25)<2,$"scale grows width ({normal.Width}->{big.Width})");
  var compact=Render(widget,shell,1,1,true,Path.Combine(output,"widget-compact.png"));
  Check(compact.Width<normal.Width&&compact.Height<normal.Height*.6,$"compact is smaller ({compact.Width}x{compact.Height} vs {normal.Width}x{normal.Height})");
  var compactLarge=Render(widget,shell,1,1.3,true,Path.Combine(output,"widget-compact-font130.png"));
  Check(compactLarge.Height>compact.Height,"font scale grows text in compact mode");
  var raining=new{vm.City,vm.Temperature,Description="小雨",Kind=WeatherKind.Rain,vm.Status,vm.IsDebugging,RainHint="正在下雨，预计今天 17 时停，出门带伞",RainLevel=RainLevel.Now,ShowRain=true,vm.Hours,vm.Days};
  shell.DataContext=raining;Render(widget,shell,1,1,false,Path.Combine(output,"widget-raining.png"));
  var hidden=new{vm.City,vm.Temperature,vm.Description,vm.Kind,vm.Status,vm.IsDebugging,RainHint="",RainLevel=RainLevel.None,ShowRain=false,vm.Hours,vm.Days};
  shell.DataContext=hidden;var off=Render(widget,shell,1,1,false,Path.Combine(output,"widget-rain-off.png"));
  Check(off.Height<normal.Height,"rain banner collapses when off");
 }
 static void History(string output)
 {
  // Store: one record per city date, unchanged refreshes do not rewrite, reload round-trips.
  var file=Path.Combine(output,"history-test.json");if(File.Exists(file))File.Delete(file);
  var store=new WeatherBeacon.Features.History.WeatherHistory(file);int changes=0;store.Changed+=()=>changes++;
  var tz=TimeSpan.FromHours(8);var today=new DateOnly(2026,10,24);
  WeatherSnapshot Snap(int hour,WeatherKind kind,string text)=>new("兰州 · 甘肃",new DateTimeOffset(2026,10,24,hour,0,0,tz),18,"多云",WeatherKind.Cloudy,[],[new DailyWeather(today,9,19,kind,text),new DailyWeather(today.AddDays(1),8,17,WeatherKind.Clear,"晴")],[]);
  store.Record(Snap(9,WeatherKind.Cloudy,"多云"));store.Record(Snap(10,WeatherKind.Cloudy,"多云"));store.Record(Snap(20,WeatherKind.Rain,"多云转小雨"));
  Check(changes==2&&store.Days.Count==1&&store.Days[today].Kind==WeatherKind.Rain&&store.Days[today].Description=="多云转小雨","history keeps last weather of the day, skips identical refreshes");
  var reloaded=new WeatherBeacon.Features.History.WeatherHistory(file);
  Check(reloaded.Days.TryGetValue(today,out var day)&&day.High==19&&day.City=="兰州 · 甘肃","history reloads from disk");
  // Page: sample year, legend focus, reveal; rendered for review.
  var model=new SettingsModel();
  var window=new SettingsWindow(model,new WeatherBeacon.Infrastructure.Weather.XiaomiWeatherProvider(),()=>{},()=>{});
  window.Show();Pump(300);
  model.DeveloperMode=true;Pump(100);
  ((Button)window.FindName("HistoryPreviewButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  ((ListBox)window.FindName("Navigation")).SelectedIndex=4;Pump(1100);
  var heatmap=(WeatherBeacon.Controls.WeatherHeatmap)window.FindName("Heatmap");
  Check(((FrameworkElement)window.FindName("HistoryPage")).Visibility==Visibility.Visible&&heatmap.Reveal>.99&&heatmap.ActualWidth>500,"history page shows a revealed heatmap ("+heatmap.ActualWidth+")");
  Check(((TextBlock)window.FindName("HistorySummary")).Text.StartsWith("已记录"),"summary counts sample days");
  var content=(FrameworkElement)window.Content;
  RenderWindow(content,Path.Combine(output,"history-page.png"));
  heatmap.FocusOn(WeatherKind.Rain);Pump(400);RenderWindow(content,Path.Combine(output,"history-focus-rain.png"));
  heatmap.FocusOn(null);Pump(400);
  Check(heatmap.FocusLevel<.01,"legend focus fades back");
  window.Close();
 }
 static void RenderWindow(FrameworkElement content,string path)
 {
  content.UpdateLayout();var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(content);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
  Console.WriteLine("RENDER "+Path.GetFileName(path));
 }
 static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static Size Render(WidgetWindow widget,FrameworkElement shell,double scale,double font,bool compact,string path)
 {
  widget.ApplyLayout(scale,font,compact);
  // Item templates are generated during the first pass; repeat until the size settles, as SizeToContent does in the app.
  var size=Size.Empty;
  for(int pass=0;pass<4;pass++)
  {
   shell.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));var next=shell.DesiredSize;
   shell.Arrange(new Rect(next));shell.UpdateLayout();
   if(next==size)break;size=next;
  }
  var bitmap=new RenderTargetBitmap((int)Math.Ceiling(size.Width),(int)Math.Ceiling(size.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(shell);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
  Console.WriteLine($"RENDER {Path.GetFileName(path)} {size.Width:F0}x{size.Height:F0}");
  return size;
 }
 static void Check(bool condition,string name){if(!condition)throw new Exception("FAILED: "+name);passed++;}
}
