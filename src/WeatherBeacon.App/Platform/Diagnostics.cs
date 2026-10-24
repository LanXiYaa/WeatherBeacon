using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using WeatherBeacon.Features.Settings;
namespace WeatherBeacon.Platform;
/// <summary>Consent-aware system details shown in the diagnostics report.</summary>
public sealed record SystemDetails(string? WindowsVersion,string? WindowsEdition,string? Graphics,string? Processor,string? Memory);
/// <summary>
/// Builds the text you can copy for a bug report and the zip with all local logs.
/// System information (Windows version/edition, GPU, CPU, memory) is read only when the user granted it in onboarding;
/// otherwise every one of those fields is null. Paths under the user profile are shortened to %USERPROFILE%.
/// </summary>
public static class Diagnostics
{
 public const int ReportLogLines=40;
 public static string AppVersion=>typeof(Diagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"1.0.0.0";
 private static readonly DateTimeOffset started=DateTimeOffset.Now;
 public static string Redact(string text)
 {
  try
  {
   var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   return string.IsNullOrEmpty(profile)?text:text.Replace(profile,"%USERPROFILE%",StringComparison.OrdinalIgnoreCase);
  }
  catch{return text;}
 }
 public static SystemDetails ReadSystem(bool consent)
 {
  if(!consent)return new(null,null,null,null,null);
  return new(Safe(WindowsVersion),Safe(WindowsEdition),Safe(Graphics),Safe(Processor),Safe(Memory));
 }
 public static string BuildReport(SettingsModel s,string? weatherStatus=null)
 {
  var b=new StringBuilder();
  void Line(string label,object? value)=>b.Append(label).Append("：").AppendLine(value?.ToString()??"null");
  b.AppendLine("==== WeatherBeacon 诊断信息 ====");
  Line("生成时间",DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
  Line("应用版本",$"{AppVersion} · {RuntimeInformation.ProcessArchitecture}");
  Line("运行时",RuntimeInformation.FrameworkDescription);
  Line("已运行",(DateTimeOffset.Now-started).ToString(@"d\.hh\:mm\:ss"));
  Line("时区",TimeZoneInfo.Local.Id);
  Line("界面语言",System.Globalization.CultureInfo.CurrentUICulture.Name);
  Line("安装位置",Redact(AppContext.BaseDirectory));
  Line("日志位置",Redact(AppLog.Folder));
  b.AppendLine().AppendLine(s.SystemInfoConsent?"-- 系统信息（已授权）--":"-- 系统信息（未授权，以下字段为 null）--");
  var system=ReadSystem(s.SystemInfoConsent);
  Line("系统版本",system.WindowsVersion);Line("版本类型",system.WindowsEdition);Line("显卡",system.Graphics);Line("CPU",system.Processor);Line("内存",system.Memory);
  b.AppendLine().AppendLine("-- 设置 --");
  Line("城市",s.City?.Name??"未选择");
  Line("刷新间隔",$"{s.RefreshMinutes} 分钟");Line("温度单位",s.Fahrenheit?"℉":"℃");
  Line("小组件",$"大小 {s.WidgetScale:P0} · 字体 {s.FontScale:P0} · {(s.CompactMode?"紧凑模式":"标准模式")} · 不透明度 {s.Opacity:P0} · 圆角 {s.Radius:F0}px");
  Line("降雨提醒",s.RainReminder?"开":"关");Line("动态背景",s.Animations?"开":"关");Line("锁定位置",s.LockPosition?"是":"否");
  Line("设置窗口材质",s.Material switch{0=>"云母",1=>"云母 Alt",2=>"亚克力",_=>"纯色"});
  Line("预警通知",s.NotificationsEnabled?$"开 · {s.AlertThresholdLabel}":"关");
  Line("开发者模式",s.DeveloperMode?"开":"关");Line("使用引导",s.OnboardingCompleted?"已完成":"未完成");
  b.AppendLine().AppendLine("-- 状态 --");
  Line("天气",weatherStatus??"未知");Line("桌面",s.DesktopStatus);Line("材质",s.MaterialStatus);
  Line("通知",s.NotificationStatus);Line("预警数据",s.AlertDataStatus);Line("预警发送",s.DeliveryStatus);
  b.AppendLine().AppendLine($"-- 最近 {ReportLogLines} 行日志 --");
  var tail=AppLog.Tail(ReportLogLines);
  if(tail.Count==0)b.AppendLine("（暂无日志）");else foreach(var line in tail)b.AppendLine(line);
  return b.ToString();
 }
 /// <summary>Writes a zip with the report, recent log files and settings.json. Returns the final path.</summary>
 public static string PackageLogs(SettingsModel s,string destination,string? weatherStatus=null)
 {
  var temp=destination+".partial";
  if(File.Exists(temp))File.Delete(temp);
  using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create))
  {
   AddText(zip,"diagnostics.txt",BuildReport(s,weatherStatus));
   if(Directory.Exists(AppLog.Folder))
   {
    long budget=48L*1024*1024;
    foreach(var file in new DirectoryInfo(AppLog.Folder).EnumerateFiles().Where(f=>f.Extension is ".log" or ".jsonl").OrderByDescending(f=>f.LastWriteTimeUtc))
    {
     if(file.Length>budget)continue;budget-=file.Length;
     AddFile(zip,"logs/"+file.Name,file.FullName,redact:true);
    }
   }
   var settingsFile=Path.Combine(SettingsModel.Folder,"settings.json");
   if(File.Exists(settingsFile))AddFile(zip,"settings.json",settingsFile,redact:false);
  }
  File.Move(temp,destination,true);
  AppLog.Info("Diagnostics","已打包日志："+destination);
  return destination;
 }
 public static string DefaultPackageName=>$"WeatherBeacon-日志-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
 private static void AddText(ZipArchive zip,string name,string text)
 {
  var entry=zip.CreateEntry(name,CompressionLevel.Optimal);
  using var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false));writer.Write(text);
 }
 private static void AddFile(ZipArchive zip,string name,string path,bool redact)
 {
  try
  {
   if(redact){AddText(zip,name,Redact(AppLog.ReadShared(path)));return;}
   var entry=zip.CreateEntry(name,CompressionLevel.Optimal);
   using var source=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
   using var target=entry.Open();source.CopyTo(target);
  }
  catch(Exception ex){AppLog.Warn("Diagnostics","无法加入压缩包："+Path.GetFileName(path),ex);}
 }
 private static string? Safe(Func<string?> read){try{var value=read();return string.IsNullOrWhiteSpace(value)?null:value.Trim();}catch{return null;}}
 private static RegistryKey? Machine(string path)=>RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64).OpenSubKey(path);
 private static string? WindowsVersion()
 {
  using var key=Machine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");if(key is null)return null;
  var build=key.GetValue("CurrentBuild")?.ToString();var ubr=key.GetValue("UBR")?.ToString();var display=key.GetValue("DisplayVersion")?.ToString();
  var family=int.TryParse(build,out var number)&&number>=22000?"Windows 11":"Windows 10";
  return $"{family} {display} ({build}{(string.IsNullOrEmpty(ubr)?"":"."+ubr)})".Replace("  "," ");
 }
 private static string? WindowsEdition()
 {
  using var key=Machine(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");if(key is null)return null;
  var id=key.GetValue("EditionID")?.ToString();var build=key.GetValue("CurrentBuild")?.ToString();
  var family=int.TryParse(build,out var number)&&number>=22000?"Windows 11":"Windows 10";
  var edition=id switch{"Core"=>"家庭版","CoreSingleLanguage"=>"家庭单语言版","CoreCountrySpecific"=>"家庭中文版","Professional"=>"专业版","ProfessionalWorkstation"=>"专业工作站版","ProfessionalEducation"=>"专业教育版","Education"=>"教育版","Enterprise"=>"企业版","EnterpriseS"=>"企业版 LTSC","IoTEnterprise"=>"IoT 企业版",null or ""=>null,_=>id};
  return edition is null?null:$"{family} {edition}";
 }
 private static string? Graphics()
 {
  using var root=Machine(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");if(root is null)return null;
  var names=new List<string>();
  foreach(var sub in root.GetSubKeyNames().Where(n=>n.All(char.IsDigit)))
  {
   using var key=root.OpenSubKey(sub);var name=key?.GetValue("DriverDesc")?.ToString();
   if(!string.IsNullOrWhiteSpace(name)&&!names.Contains(name))names.Add(name);
  }
  return names.Count==0?null:string.Join("；",names);
 }
 private static string? Processor()
 {
  using var key=Machine(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
  var name=key?.GetValue("ProcessorNameString")?.ToString()?.Trim();
  return name is null?null:$"{name}（{Environment.ProcessorCount} 线程）";
 }
 [StructLayout(LayoutKind.Sequential)]
 private struct MemoryStatus{public uint Length,Load;public ulong TotalPhys,AvailPhys,TotalPageFile,AvailPageFile,TotalVirtual,AvailVirtual,AvailExtendedVirtual;}
 [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
 private static string? Memory()
 {
  var status=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};
  return GlobalMemoryStatusEx(ref status)?$"{status.TotalPhys/1073741824d:F1} GB":null;
 }
}
