using System.IO;
using System.Net;
using System.Net.Http;
using WeatherBeacon.Infrastructure.Weather;
using WeatherBeacon.Features.Widget;
using WeatherBeacon.Features.Settings;
using WeatherBeacon.Core.Models;
if(args[0]=="--startup-tests")
{
 using var run=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
 using var approved=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
 const string key="WeatherBeacon";
 var oldRun=run.GetValue(key);var oldApproved=approved.GetValue(key);
 var oldRunKind=oldRun is null?Microsoft.Win32.RegistryValueKind.String:run.GetValueKind(key);
 var oldApprovedKind=oldApproved is null?Microsoft.Win32.RegistryValueKind.Binary:approved.GetValueKind(key);
 try
 {
  run.DeleteValue(key,false);approved.DeleteValue(key,false);
  if(WeatherBeacon.Platform.StartupService.Read().Enabled)throw new Exception("Missing startup enabled");
  WeatherBeacon.Platform.StartupService.Set(true);
  if(!WeatherBeacon.Platform.StartupService.Read().Enabled)throw new Exception("Enable failed");
  var current=run.GetValue(key);
  run.SetValue(key,"\"C:\\Old\\WeatherBeacon.exe\" --autostart");
  WeatherBeacon.Platform.StartupService.RepairPath();
  if(!Equals(current,run.GetValue(key)))throw new Exception("Path repair failed");
  var disabled=new byte[12];disabled[0]=3;approved.SetValue(key,disabled);
  run.SetValue(key,"\"C:\\Disabled\\WeatherBeacon.exe\" --autostart");
  WeatherBeacon.Platform.StartupService.RepairPath();
  if(WeatherBeacon.Platform.StartupService.Read().Enabled||!run.GetValue(key)!.ToString()!.Contains("Disabled"))throw new Exception("System disable not respected");
  WeatherBeacon.Platform.StartupService.Set(false);
  if(run.GetValue(key)!=null)throw new Exception("Disable failed");
  Console.WriteLine("PASS startup enable/disable, moved path repair, system-disabled path preserved; original registry values restored in finally.");
 }
 finally
 {
  if(oldRun is null)run.DeleteValue(key,false);else run.SetValue(key,oldRun,oldRunKind);
  if(oldApproved is null)approved.DeleteValue(key,false);else approved.SetValue(key,oldApproved,oldApprovedKind);
 }
 return;
}
var handler=new Handler(File.ReadAllText(args[0]));
var provider=new XiaomiWeatherProvider(new HttpClient(handler));
var vm=new WidgetViewModel();var city=new WeatherLocation("测试城市","weathercn:101010100",39.9,116.4);
await vm.RefreshAsync(provider,city,CancellationToken.None);
if(!vm.LastRefreshSucceeded||vm.Days.Count!=5)throw new Exception("Valid response failed");
var temp=vm.Temperature;vm.SetUnit(true);if(!vm.Temperature.EndsWith("F")||vm.Temperature==temp)throw new Exception("Fahrenheit conversion failed");
vm.SetUnit(false);if(vm.Temperature!=temp)throw new Exception("Round-trip conversion failed");
var oldCity=vm.City;var oldDays=vm.Days.ToArray();handler.Body="{broken";
await vm.RefreshAsync(provider,city with{Name="新城市"},CancellationToken.None);
if(vm.LastRefreshSucceeded||vm.City!=oldCity||vm.Temperature!=temp||!vm.Days.SequenceEqual(oldDays))throw new Exception("Parse failure erased valid state");
handler.Fail=true;await vm.RefreshAsync(provider,city,CancellationToken.None);
if(vm.LastRefreshSucceeded||vm.Temperature!=temp)throw new Exception("Network failure erased state");
if(XiaomiWeatherProvider.DailyLabel("0","1")!="晴转多云"||XiaomiWeatherProvider.CodeLabel("8")!="中雨")throw new Exception("Weather names failed");
var s=new SettingsModel();if(s.RefreshMinutes!=15)throw new Exception("Default interval");s.RefreshMinutes=1;if(s.RefreshMinutes!=15)throw new Exception("Invalid interval");
Console.WriteLine("PASS units, parse/network failure preservation, daily names, interval validation; request count="+handler.Count);
sealed class Handler(string body):HttpMessageHandler
{
 public string Body=body;public bool Fail;public int Count;
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Count++;if(Fail)throw new HttpRequestException("simulated offline");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Body)});}
}

