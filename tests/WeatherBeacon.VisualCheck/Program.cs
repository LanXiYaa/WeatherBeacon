using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WeatherBeacon.Features.Widget;
using WeatherBeacon.Features.Settings;
using WeatherBeacon.Infrastructure.Weather;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Controls;
using WeatherBeacon.Platform;
internal static class Program
{
 [STAThread]static void Main(string[] args)
 {
  _=new Application();
  var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
  var widget=new WidgetWindow();var content=(FrameworkElement)widget.Content;
  SetWidgetData(content,WeatherKind.Clear,true);
  Render(content,420,430,Path.Combine(output,"widget-clear-day.png"));
  SetWidgetData(content,WeatherKind.Clear,false);
  Render(content,420,430,Path.Combine(output,"widget-clear-night.png"));
  SetWidgetData(content,WeatherKind.Rain,false);
  Render(content,420,430,Path.Combine(output,"widget-rain-night.png"));

  var panel=new WrapPanel{Width=720,Background=new SolidColorBrush(Color.FromRgb(17,34,49))};
  foreach(var kind in Enum.GetValues<WeatherKind>())panel.Children.Add(new WeatherIcon{Kind=kind,Width=80,Height=80});
  Render(panel,720,160,Path.Combine(output,"icons.png"));

  foreach(var kind in Enum.GetValues<WeatherKind>())
  {
   var scene=new WeatherScene{Kind=kind,IsDaytime=true,Animated=false};
   Render(scene,390,390,Path.Combine(output,$"scene-{kind.ToString().ToLowerInvariant()}-day.png"));
  }
  Render(new WeatherScene{Kind=WeatherKind.Clear,IsDaytime=false,Animated=false},390,390,Path.Combine(output,"scene-clear-night.png"));

  var work=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
  var animationScene=new WeatherScene{Kind=WeatherKind.Rain,IsDaytime=false,Animated=true};
  var animationWindow=new Window{Width=420,Height=430,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Left=work.Left+60,Top=work.Top+60,Content=new Grid{Children={animationScene}}};
  animationWindow.Show();animationWindow.Activate();Pump(TimeSpan.FromMilliseconds(600));
  Render(animationScene,420,430,Path.Combine(output,"animation-rain-t1.png"));
  var firstFrame=animationScene.RenderedFrames;var firstHash=Hash(Path.Combine(output,"animation-rain-t1.png"));
  Pump(TimeSpan.FromMilliseconds(850));
  Render(animationScene,420,430,Path.Combine(output,"animation-rain-t2.png"));
  var secondFrame=animationScene.RenderedFrames;var secondHash=Hash(Path.Combine(output,"animation-rain-t2.png"));
  if(secondFrame<=firstFrame)throw new Exception("Animation timer did not render new frames");
  if(firstHash==secondHash)throw new Exception("Animated frame capture did not change");

  animationScene.Animated=false;Pump(TimeSpan.FromMilliseconds(150));
  var pausedFrames=animationScene.RenderedFrames;Pump(TimeSpan.FromMilliseconds(500));
  if(animationScene.RenderedFrames!=pausedFrames)throw new Exception("Animation did not stop when disabled");
  animationScene.Animated=true;animationScene.SetWindowActive(false);Pump(TimeSpan.FromMilliseconds(150));
  pausedFrames=animationScene.RenderedFrames;Pump(TimeSpan.FromMilliseconds(500));
  if(animationScene.RenderedFrames!=pausedFrames)throw new Exception("Animation did not stop when inactive");

  animationScene.SetWindowActive(true);animationScene.Animated=true;Pump(TimeSpan.FromMilliseconds(600));
  var process=Process.GetCurrentProcess();
  var elapsed=Stopwatch.StartNew();var cpuBefore=process.TotalProcessorTime;
  Pump(TimeSpan.FromSeconds(4));process.Refresh();
  var cpuOn=process.TotalProcessorTime-cpuBefore;var wallOn=elapsed.Elapsed.TotalSeconds;
  var workingSetOn=process.WorkingSet64;var privateOn=process.PrivateMemorySize64;
  animationScene.Animated=false;Pump(TimeSpan.FromMilliseconds(300));
  elapsed.Restart();cpuBefore=process.TotalProcessorTime;
  Pump(TimeSpan.FromSeconds(4));process.Refresh();
  var cpuOff=process.TotalProcessorTime-cpuBefore;var wallOff=elapsed.Elapsed.TotalSeconds;
  var workingSetOff=process.WorkingSet64;var privateOff=process.PrivateMemorySize64;
  var performance=new
  {
   cores=Environment.ProcessorCount,
   animationOn=new{cpuCorePercent=cpuOn.TotalSeconds/wallOn*100,cpuSystemPercent=cpuOn.TotalSeconds/wallOn*100/Environment.ProcessorCount,workingSetMiB=workingSetOn/1048576d,privateMiB=privateOn/1048576d},
   animationOff=new{cpuCorePercent=cpuOff.TotalSeconds/wallOff*100,cpuSystemPercent=cpuOff.TotalSeconds/wallOff*100/Environment.ProcessorCount,workingSetMiB=workingSetOff/1048576d,privateMiB=privateOff/1048576d},
   note="Process-wide measurement of this WPF visual-check process; on/off samples each run for 4 seconds."
  };
  File.WriteAllText(Path.Combine(output,"performance.json"),JsonSerializer.Serialize(performance,new JsonSerializerOptions{WriteIndented=true}));
  animationWindow.Close();

  var settingsModel=new SettingsModel();
  var settings=new SettingsWindow(settingsModel,new XiaomiWeatherProvider(),()=>{},()=>{});
  settings.Topmost=true;settings.Show();settings.Activate();Pump(TimeSpan.FromMilliseconds(350));
  var navigation=(ListBox)settings.FindName("Navigation");
  navigation.SelectedIndex=1;navigation.SelectedIndex=2;navigation.SelectedIndex=0;
  Pump(TimeSpan.FromMilliseconds(400));
  var appearance=(FrameworkElement)settings.FindName("AppearancePage");
  var weather=(FrameworkElement)settings.FindName("WeatherPage");
  var about=(FrameworkElement)settings.FindName("AboutPage");
  if(appearance.Visibility!=Visibility.Visible||weather.Visibility!=Visibility.Collapsed||about.Visibility!=Visibility.Collapsed)throw new Exception("Rapid navigation ended on the wrong page");
  if(!appearance.IsHitTestVisible||appearance.Opacity<.99)throw new Exception("Rapid navigation left the target page incomplete");
  var checkBoxes=Descendants(settings).OfType<CheckBox>().Count();
  if(checkBoxes!=0)throw new Exception($"Settings still contains {checkBoxes} CheckBox controls");
  if(settingsModel.MaterialStatus.StartsWith("正在检测",StringComparison.Ordinal))throw new Exception("Material availability was not resolved");
  foreach(var material in new[]{0,1,2,0})
  {
   settingsModel.Material=material;Pump(TimeSpan.FromMilliseconds(80));
   if(settingsModel.MaterialStatus.Contains("不可用",StringComparison.Ordinal))throw new Exception($"Supported material {material} unexpectedly unavailable: {settingsModel.MaterialStatus}");
  }
  var animationToggle=(ToggleButton)settings.FindName("AnimationToggle");
  animationToggle.IsChecked=false;Pump(TimeSpan.FromMilliseconds(120));
  if(settingsModel.Animations)throw new Exception("Animation toggle did not update the setting");
  animationToggle.IsChecked=true;Pump(TimeSpan.FromMilliseconds(120));
  Render((FrameworkElement)settings.Content,944,700,Path.Combine(output,"settings.png"));
  settings.Close();

  TestPlacementAndOcclusion();
  Console.WriteLine($"PASS: widget, settings, 8 weather icons, 9 scenes; frames {firstFrame}->{secondFrame}; pause works; 0 CheckBox; nav settled; placement/occlusion passed.");
  Console.WriteLine($"PERF: on CPU core {performance.animationOn.cpuCorePercent:F2}% / system {performance.animationOn.cpuSystemPercent:F3}%, WS {performance.animationOn.workingSetMiB:F1} MiB; off CPU core {performance.animationOff.cpuCorePercent:F2}% / system {performance.animationOff.cpuSystemPercent:F3}%, WS {performance.animationOff.workingSetMiB:F1} MiB.");
 }
 static void SetWidgetData(FrameworkElement content,WeatherKind kind,bool day)
 {
  var scene=(WeatherScene)content.FindName("Scene");
  scene.IsDaytime=day;
  content.DataContext=new{City="北京市 · 中国",Temperature=kind==WeatherKind.Rain?"18°":"19°",Description=XiaomiWeatherProvider.Label(kind),Kind=kind,Status="小米天气 · 更新于 19:00",Hours=Enumerable.Range(0,8).Select(i=>new ForecastItem((WeatherKind)(i%7+1),$"{19+i:00}:00","19°")).ToArray(),Days=Enumerable.Range(0,5).Select(i=>new ForecastItem((WeatherKind)(i%7+1),$"10.{23+i}","12° / 21°",i==0)).ToArray()};
 }
 static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
 static IEnumerable<DependencyObject> Descendants(DependencyObject root)
 {
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
  {
   var child=VisualTreeHelper.GetChild(root,i);yield return child;
   foreach(var descendant in Descendants(child))yield return descendant;
  }
 }
 static void TestPlacementAndOcclusion()
 {
  var work=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
  var window=new Window{Width=420,Height=430,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Left=work.Left+80,Top=work.Top+80,Background=Brushes.DarkSlateBlue};
  window.Show();window.Activate();Pump(TimeSpan.FromMilliseconds(250));
  var host=new DesktopHost();host.StartOrdinary(window);
  if(host.IsCompletelyCovered())throw new Exception("Visible placement test window was reported as covered");
  var disconnected=new WidgetPlacement{MonitorDeviceName="__DISCONNECTED__",MonitorLeft=90000,MonitorTop=90000,MonitorWidth=100,MonitorHeight=100,RelativeX=.5,RelativeY=.5,Left=90000,Top=90000};
  host.ApplyPlacement(window,disconnected);AssertInsideAnyWorkArea(host.WindowBounds);
  var negative=System.Windows.Forms.Screen.AllScreens.Where(s=>s.WorkingArea.Left<0).OrderBy(s=>s.WorkingArea.Left).FirstOrDefault();
  if(negative is not null)
  {
   var saved=new WidgetPlacement{MonitorDeviceName=negative.DeviceName,MonitorLeft=negative.Bounds.Left,MonitorTop=negative.Bounds.Top,MonitorWidth=negative.Bounds.Width,MonitorHeight=negative.Bounds.Height,RelativeX=0,RelativeY=1,Left=negative.WorkingArea.Left,Top=negative.WorkingArea.Bottom-430};
   host.ApplyPlacement(window,saved);AssertInsideAnyWorkArea(host.WindowBounds);
  }
  host.MoveToScreen(work.Left+260,work.Top+230);var placement=host.CapturePlacement(window);
  host.MoveToScreen(work.Right-500,work.Bottom-500);var restored=host.ApplyPlacement(window,placement);
  if(Math.Abs(restored.Left-placement.Left)>2||Math.Abs(restored.Top-placement.Top)>2)throw new Exception("Saved placement did not round-trip");
  var cover=new Window{Width=420,Height=430,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Topmost=true,Left=window.Left,Top=window.Top,Background=Brushes.Black};
  cover.Show();cover.Activate();Pump(TimeSpan.FromMilliseconds(300));
  if(!host.IsCompletelyCovered())throw new Exception("Full window cover was not detected");
  cover.Left+=220;Pump(TimeSpan.FromMilliseconds(150));
  if(host.IsCompletelyCovered())throw new Exception("Partially visible window was reported as fully covered");
  cover.Close();host.Dispose();window.Close();
 }
 static void AssertInsideAnyWorkArea(System.Drawing.Rectangle bounds)
 {
  var inside=System.Windows.Forms.Screen.AllScreens.Any(s=>bounds.Left>=s.WorkingArea.Left&&bounds.Top>=s.WorkingArea.Top&&bounds.Right<=s.WorkingArea.Right&&bounds.Bottom<=s.WorkingArea.Bottom);
  if(!inside)throw new Exception($"Placement was not constrained inside any work area: {bounds}");
 }
 static void Pump(TimeSpan duration)
 {
  var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=duration};
  timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
 }
 static void Render(FrameworkElement element,int width,int height,string path)
 {
  element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();
  var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bmp.Render(element);
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(path);encoder.Save(stream);
 }
}

