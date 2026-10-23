using Microsoft.Win32;
namespace WeatherBeacon.Platform;
public sealed record StartupState(bool Enabled,bool DisabledBySystem,string Message);
public static class StartupService
{
 const string Run=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string Approved=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
 const string Name="WeatherBeacon";
 static string Command=>$"\"{Environment.ProcessPath}\" --autostart";
 public static StartupState Read()
 {
  try{
   using var run=Registry.CurrentUser.OpenSubKey(Run);using var approved=Registry.CurrentUser.OpenSubKey(Approved);
   var exists=run?.GetValue(Name) is string;
   var bytes=approved?.GetValue(Name) as byte[];
   var disabled=bytes is {Length:>=4}&&BitConverter.ToUInt32(bytes,0) is 3 or 7;
   var unknown=bytes is {Length:>=4}&&BitConverter.ToUInt32(bytes,0) is not (2 or 3 or 6 or 7);
   return new(exists&&!disabled&&!unknown,disabled,unknown?"系统启动状态无法识别，请在任务管理器确认":disabled?"已被系统禁用，请在任务管理器中重新启用":exists?"已开启当前用户登录自启":"未开启自启");
  }catch{return new(false,false,"无法读取系统启动状态");}
 }
 public static void Set(bool enabled)
 {
  var state=Read();if(enabled&&state.DisabledBySystem)throw new InvalidOperationException("请先在任务管理器的启动应用中启用 WeatherBeacon。");
  using var run=Registry.CurrentUser.CreateSubKey(Run);
  if(enabled)run.SetValue(Name,Command);else run.DeleteValue(Name,false);
 }
 public static void RepairPath()
 {
  if(!Read().Enabled)return;
  using var run=Registry.CurrentUser.OpenSubKey(Run,true);
  if(run?.GetValue(Name) is string old&&!string.Equals(old,Command,StringComparison.OrdinalIgnoreCase))run.SetValue(Name,Command);
 }
}
