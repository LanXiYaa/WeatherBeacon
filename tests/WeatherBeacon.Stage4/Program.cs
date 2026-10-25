using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Infrastructure.Weather;
using WeatherBeacon.Features.Notifications;
using WeatherBeacon.Features.Settings;
using WeatherBeacon.Features.Widget;
using WeatherBeacon.Features.Developer;

internal static class Program
{
 [STAThread] static void Main(string[] args)
 {
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
  if(args[0]=="--activate-notification")
  {
#pragma warning disable CS0618
   var type=Type.GetTypeFromCLSID(new Guid(WeatherBeacon.Platform.ToastRegistration.ActivatorId),true)!;
   var callback=(CommunityToolkit.WinUI.Notifications.NotificationActivator.INotificationActivationCallback)Activator.CreateInstance(type)!;
   try{callback.Activate(WeatherBeacon.Platform.ToastRegistration.AppId,"open-settings",[],0);Console.WriteLine("PASS actual COM toast activation callback.");}
   finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(callback);}
#pragma warning restore CS0618
   return;
  }
  if(args[0]=="--native-lifecycle")
  {
   var service=new WeatherToastService(()=>{if(!File.Exists(WeatherBeacon.Platform.ToastRegistration.ShortcutPath))throw new Exception("Register the actual EXE first");});
   var folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
   var delivery=new AlertDelivery(Path.Combine(folder,"native-"+Guid.NewGuid()+".json"));
   var testAlert=new WeatherAlert("test-a","验证预警","这是自动测试，不是真实天气预警。",AlertLevel.Yellow,null,"验证大风",DateTimeOffset.Now);
   var fixture=new WeatherSnapshot("[调试] 测试城市",DateTimeOffset.Now,20,"晴",WeatherKind.Clear,[],[],[testAlert],true);
   const string city="notification-integration-test";
   var tag=WeatherToastService.TagFor(city,AlertDelivery.Slot(testAlert));
   int sent=0;
   bool NativeSend(WeatherAlert a){sent++;return service.Send(a,fixture.City,false,city);}
   void Process(WeatherSnapshot s)=>delivery.Process(city,s,true,AlertLevel.Blue,NativeSend,slot=>service.Remove(city,slot));
   Process(fixture);Pump(800);
   Check(Windows.UI.Notifications.ToastNotificationManager.History.GetHistory(WeatherBeacon.Platform.ToastRegistration.AppId).Count(n=>n.Tag==tag)==1,"native first item");
   Process(fixture);Check(sent==1,"native duplicate suppressed");
   Process(fixture with{Alerts=[testAlert with{Id="test-b",Level=AlertLevel.Red,PublishedAt=DateTimeOffset.Now}]});Pump(800);
   var matching=Windows.UI.Notifications.ToastNotificationManager.History.GetHistory(WeatherBeacon.Platform.ToastRegistration.AppId).Where(n=>n.Tag==tag).ToArray();
   Check(matching.Length==1&&matching[0].Content.GetXml().Contains("红色预警"),"native upgrade replaces old item");
   Process(fixture with{Alerts=[]});Pump(500);
   Check(!Windows.UI.Notifications.ToastNotificationManager.History.GetHistory(WeatherBeacon.Platform.ToastRegistration.AppId).Any(n=>n.Tag==tag),"native resolved item removed");
   Console.WriteLine("PASS actual Windows history: first item=1; duplicate sends=1; upgraded item=1 and red; resolved item=0. Product identity unchanged; separate test ledger only.");return;
  }
  if(args[0]=="--probe-fixed-toast")
  {
   var probeAlert=new WeatherAlert("probe","测试","这是 WeatherBeacon 通知注册验证，不是真实天气预警。",AlertLevel.Yellow,null,"大风",DateTimeOffset.Now);
   var xml=new Windows.Data.Xml.Dom.XmlDocument();xml.LoadXml(WeatherToastService.BuildXml(probeAlert,"兰州市",true,WeatherBeacon.Platform.ToastRegistration.ImagePath));
   var toast=new Windows.UI.Notifications.ToastNotification(xml){Tag="registrationtest",Group="debug"};
   toast.Failed+=(_,e)=>Console.WriteLine("Toast failed: "+e.ErrorCode);
   Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("WeatherBeacon.Desktop").Show(toast);Pump(1500);
   Console.WriteLine("Fixed identity history: "+Windows.UI.Notifications.ToastNotificationManager.History.GetHistory("WeatherBeacon.Desktop").Count);return;
  }
  if(args[0]=="--inspect-notifications")
  {
   if(args[1]=="WeatherBeacon.Desktop")Console.WriteLine(WeatherBeacon.Platform.ToastRegistration.InspectShortcut());
   var notifier=Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(args[1]);
   Console.WriteLine("Actual EXE notification setting: "+notifier.Setting);
   var history=Windows.UI.Notifications.ToastNotificationManager.History.GetHistory(args[1]);
   Console.WriteLine("Actual EXE notification history: "+history.Count);
   foreach(var entry in history)Console.WriteLine($"tag={entry.Tag}; group={entry.Group}; expires={entry.ExpirationTime}; xml={entry.Content.GetXml()}");
   return;
  }
  if(args[0]=="--toast")
  {
   var service=new WeatherToastService(()=>{if(!File.Exists(WeatherBeacon.Platform.ToastRegistration.ShortcutPath))throw new Exception("Start WeatherBeacon.exe first");});
   var ok=service.Send(new("test","测试","这是第四步自动检查发送的测试通知，不是真实气象预警。",AlertLevel.Yellow,DateTimeOffset.Now.AddMinutes(5),"暴雨",DateTimeOffset.Now),"测试城市",true);
   Pump(1500);Console.WriteLine($"Toast submitted={ok}; {service.Status}");
   if(!ok)throw new Exception("Toast submission failed");
   var history=Windows.UI.Notifications.ToastNotificationManager.History.GetHistory(WeatherBeacon.Platform.ToastRegistration.AppId);
   Console.WriteLine($"Windows notification history entries: {history.Count}");return;
  }
  using var doc=JsonDocument.Parse(File.ReadAllText(args[0]));
  var sample=XiaomiWeatherProvider.Parse(doc.RootElement,"福州");
  Check(sample.AlertsComplete&&sample.Alerts.Count>0,"real Xiaomi alert sample");
  var alert=sample.Alerts[0];Check(alert.Type=="台风"&&alert.Level==AlertLevel.Yellow&&alert.PublishedAt is not null,"real type/level/publication time");
  var content=System.Xml.Linq.XElement.Parse(WeatherToastService.BuildXml(alert,"城关区 · 兰州市, 甘肃, 中国",true,Path.GetFullPath("icon.png")));
  Check(content.Attribute("displayTimestamp") is not null&&content.Descendants("text").Count()==2,"native timestamp and two-text layout");
  Check(content.Descendants("text").First().Value=="[调试] 兰州市 · 台风黄色预警","city/type/level title");
  Check(content.Descendants("action").Single().Attribute("arguments")?.Value=="dismiss","native dismiss button");
  var updatedId=alert with{Id="updated-id",Level=AlertLevel.Red,PublishedAt=DateTimeOffset.Now};
  Check(WeatherToastService.TagFor("city",AlertDelivery.Slot(alert))==WeatherToastService.TagFor("city",AlertDelivery.Slot(updatedId)),"upgrades reuse notification tag");
  foreach(var raw in new[]{"{}","{\"alerts\":null}","{\"alerts\":{}}","{\"alerts\":[{}]}","{\"alerts\":[null]}"})
  {using var invalid=JsonDocument.Parse(raw);Check(!XiaomiWeatherProvider.ParseAlerts(invalid.RootElement).Complete,"unsupported arrays must not clear ledger");}
  var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
  var path=Path.Combine(output,"ledger-"+Guid.NewGuid()+".json");int sends=0;
  bool Send(WeatherAlert a){sends++;return true;}
  var engine=new AlertDelivery(path);
  engine.Process("city",sample,false,AlertLevel.Blue,Send);Check(sends==0,"master switch");
  engine.Process("city",sample,true,AlertLevel.Red,Send);Check(sends==0,"level threshold");
  engine.Process("city",sample,true,AlertLevel.Blue,Send);Check(sends==1,"first alert");
  engine.Process("city",sample,true,AlertLevel.Blue,Send);Check(sends==1,"in-process duplicate");
  engine=new AlertDelivery(path);engine.Process("city",sample,true,AlertLevel.Blue,Send);Check(sends==1,"restart duplicate");
  engine.Process("city",sample with{Alerts=[],AlertsComplete=false},true,AlertLevel.Blue,Send);
  engine.Process("city",sample,true,AlertLevel.Blue,Send);Check(sends==1,"missing field preserves history");
  var updated=sample with{Alerts=[alert with{Description=alert.Description+"测试更新"}]};
  engine.Process("city",updated,true,AlertLevel.Blue,Send);Check(sends==2,"updated alert");
  engine.Process("city",sample with{Alerts=[]},true,AlertLevel.Blue,Send);
  engine.Process("city",sample,true,AlertLevel.Blue,Send);Check(sends==3,"cleared/reissued alert");
  engine.Process("other-city",sample,true,AlertLevel.Blue,Send);Check(sends==4,"city isolation");
  var failPath=Path.Combine(output,"retry-"+Guid.NewGuid()+".json");var failed=new AlertDelivery(failPath);
  failed.Process("city",sample,true,AlertLevel.Blue,_=>false);
  new AlertDelivery(failPath).Process("city",sample,true,AlertLevel.Blue,Send);Check(sends==5,"failed delivery retry");
  var corrupt=Path.Combine(output,"corrupt-"+Guid.NewGuid()+".json");File.WriteAllText(corrupt,"broken");
  var broken=new AlertDelivery(corrupt);broken.Process("city",sample,true,AlertLevel.Blue,Send);Check(sends==5&&broken.Status.Contains("无法读取"),"corrupt ledger blocks replay");
  var expired=sample with{Alerts=[alert with{Expires=DateTimeOffset.Now.AddMinutes(-1)}]};
  new AlertDelivery(Path.Combine(output,"expired.json")).Process("city",expired,true,AlertLevel.Blue,Send);Check(sends==5,"expired not delivered");
  var replacements=0;var removals=0;var testPath=Path.Combine(output,"slots-"+Guid.NewGuid()+".json");var slots=new AlertDelivery(testPath);
  slots.Process("city",sample,true,AlertLevel.Blue,_=>{replacements++;return true;});
  slots.Process("city",sample with{Alerts=[updatedId]},true,AlertLevel.Blue,_=>{replacements++;return true;});
  Check(replacements==2,"different alert IDs update same type");
  slots.Process("city",sample with{Alerts=[],AlertsComplete=false},true,AlertLevel.Blue,_=>true,_=>{removals++;return true;});Check(removals==0,"unsupported data cannot clear notifications");
  slots.Process("city",sample with{Alerts=[]},true,AlertLevel.Blue,_=>true,_=>{removals++;return false;});
  new AlertDelivery(testPath).Process("city",sample with{Alerts=[]},true,AlertLevel.Blue,_=>true,_=>{removals++;return true;});Check(removals==2,"failed removal retried after restart");
  var migration=Path.Combine(output,"legacy-"+Guid.NewGuid()+".json");File.WriteAllText(migration,JsonSerializer.Serialize(new Dictionary<string,Dictionary<string,string>>{{"city",new(){{alert.Id,AlertDelivery.Fingerprint(alert)}}}}));
  new AlertDelivery(migration).Process("city",sample,true,AlertLevel.Blue,_=>{throw new Exception("Legacy record replayed");});
  Check(File.ReadAllText(migration).Contains("v2:type:"),"legacy ledger migrated");
  var downgrade=new AlertDelivery(Path.Combine(output,"downgrade-"+Guid.NewGuid()+".json"));int downgradeRemoved=0;
  downgrade.Process("city",sample with{Alerts=[updatedId]},true,AlertLevel.Red,_=>true);
  downgrade.Process("city",sample,true,AlertLevel.Red,_=>throw new Exception("Below-threshold update sent"),_=>{downgradeRemoved++;return true;});
  Check(downgradeRemoved==1,"downgraded below-threshold alert removes stale high-severity notification");
  var vm=new WidgetViewModel();var apply=typeof(WidgetViewModel).GetMethod("Apply",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
  apply.Invoke(vm,[sample]);var temp=vm.Temperature;var days=vm.Days.ToArray();
  foreach(var preset in WeatherPreset.All)
  foreach(var day in new[]{false,true})
  {
   vm.SetOverride(new(preset.Name,preset.Kind,day,preset.RainIntensity));
   Check(vm.Kind==preset.Kind&&vm.IsDebugging&&vm.Description==preset.Name&&vm.Temperature==temp&&vm.Days.SequenceEqual(days),"debug override preserves real data");
   apply.Invoke(vm,[sample]);Check(vm.IsDebugging&&vm.Kind==preset.Kind,"refresh preserves override");
  }
  vm.SetOverride(null);Check(!vm.IsDebugging&&vm.Kind==sample.Kind&&vm.Description==sample.Description,"restore real weather");
  Check(!new WidgetViewModel().IsDebugging,"restart clears override");
  var settings=new SettingsModel();Check(!settings.NotificationsEnabled&&!settings.DeveloperMode&&settings.AlertThreshold==1,"safe defaults");
  settings.AlertThreshold=9;Check(settings.AlertThreshold==3,"threshold clamp");
  var window=new SettingsWindow(settings,new(),()=>{},()=>{});window.Show();Pump(300);
  var navigation=(ListBox)window.FindName("Navigation");navigation.SelectedIndex=3;Pump(300);
  Capture(window,Path.Combine(output,"notifications.png"));
  Check(((FrameworkElement)window.FindName("DeveloperNavigation")).Visibility==Visibility.Collapsed,"developer hidden by default");
  settings.DeveloperMode=true;navigation.SelectedIndex=5;Pump(300);Capture(window,Path.Combine(output,"developer.png"));
  Check(((FrameworkElement)window.FindName("DeveloperPage")).Visibility==Visibility.Visible,"developer page enabled");
  settings.DeveloperMode=false;Check(navigation.SelectedIndex==2,"disable exits developer page");
  var toggle=(WeatherBeacon.Controls.ConfirmationSwitch)window.FindName("DeveloperToggle");
  var dialog=(WeatherBeacon.Controls.ConfirmationDialog)window.FindName("DeveloperConfirmation");
  var confirm=(Button)dialog.FindName("ConfirmButton");var cancel=(Button)dialog.FindName("CancelButton");
  typeof(WeatherBeacon.Controls.ConfirmationSwitch).GetMethod("OnToggle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(toggle,null);
  Check(toggle.IsChecked!=true,"initial click cannot animate switch on");
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(320);
  Check(!settings.DeveloperMode&&toggle.IsChecked==false&&dialog.IsVisible,"pending confirmation does not toggle");
  Capture(window,Path.Combine(output,"confirm-dialog.png"));
  cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(50);
  Check(!settings.DeveloperMode&&!dialog.IsVisible,"cancel preserves off state");
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(300);
  confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Pump(260);if(SystemParameters.ClientAreaAnimation)Check(!settings.DeveloperMode,"spinner does not commit");Capture(window,Path.Combine(output,"confirm-spinner.png"));
  Pump(460);if(SystemParameters.ClientAreaAnimation)Check(!settings.DeveloperMode,"stroke does not commit early");Capture(window,Path.Combine(output,"confirm-stroke.png"));
  Pump(440);Capture(window,Path.Combine(output,"confirm-complete.png"));Pump(350);
  Check(settings.DeveloperMode&&toggle.IsChecked==true&&!dialog.IsVisible,"complete then switch on");
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(30);Check(!settings.DeveloperMode,"disable remains immediate");
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));dialog.Cancel();Pump(50);
  Check(!settings.DeveloperMode,"cancel during stroke preserves off");
  toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.Close();Pump(1400);
  Check(!settings.DeveloperMode,"closing window cancels pending activation");
  Console.WriteLine("PASS themed confirmation: cancel, duplicate confirm, spinner/stroke ordering, delayed toggle, disable, close cancellation.");
  window.Close();Console.WriteLine("PASS real alert parsing; malformed schema; threshold/off; repeat/restart/update/clear/city isolation; failed send retry; corruption; expiration; 20 weather/day-night overrides; real-data restore; notification/developer UI.");
 }
 static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL "+name);}
 static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static void Capture(Window w,string path){var content=(FrameworkElement)w.Content;var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);}
}
