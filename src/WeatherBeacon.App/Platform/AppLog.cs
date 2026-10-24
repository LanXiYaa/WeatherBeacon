using System.IO;
using System.Text;
namespace WeatherBeacon.Platform;
public enum LogLevel { Info, Warn, Error }
/// <summary>
/// Plain-text application log kept only on this PC: %LOCALAPPDATA%\WeatherBeacon\logs\weatherbeacon-yyyyMMdd.log.
/// One line per event; files older than <see cref="RetentionDays"/> days are removed at startup and each file is capped.
/// Never throws: logging must not be able to break the app.
/// </summary>
public static class AppLog
{
 public const int RetentionDays=14;
 public const long MaxFileBytes=4*1024*1024;
 private static readonly object gate=new();
 private static string? folderOverride;
 private static string? cappedFile;
 private static readonly Encoding Utf8=new UTF8Encoding(false);
 public static string Folder=>folderOverride??Path.Combine(Features.Settings.SettingsModel.Folder,"logs");
 /// <summary>Redirects logging (used by tests so they never touch the real log folder).</summary>
 public static void UseFolder(string? folder){lock(gate){folderOverride=folder;cappedFile=null;}}
 public static string CurrentFile=>Path.Combine(Folder,$"weatherbeacon-{DateTime.Now:yyyyMMdd}.log");
 public static void Info(string area,string message)=>Write(LogLevel.Info,area,message,null);
 public static void Warn(string area,string message,Exception? error=null)=>Write(LogLevel.Warn,area,message,error);
 public static void Error(string area,string message,Exception? error=null)=>Write(LogLevel.Error,area,message,error);
 public static void Write(LogLevel level,string area,string message,Exception? error)
 {
  try
  {
   var line=new StringBuilder().Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")).Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] [").Append(area).Append("] ").Append(Diagnostics.Redact(message.ReplaceLineEndings(" ")));
   if(error is not null)line.Append(" | ").Append(Diagnostics.Redact(error.ToString().ReplaceLineEndings(Environment.NewLine+"    ")));
   line.Append(Environment.NewLine);
   lock(gate)
   {
    Directory.CreateDirectory(Folder);var file=CurrentFile;
    if(File.Exists(file)&&new FileInfo(file).Length>MaxFileBytes)
    {
     // Keep the start of the day (usually where the first error is) and note once that the rest was dropped.
     if(cappedFile!=file){cappedFile=file;File.AppendAllText(file,$"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [WARN] [Log] 今日日志已达 {MaxFileBytes/1048576} MB 上限，后续记录将丢弃{Environment.NewLine}",Utf8);}
     return;
    }
    File.AppendAllText(file,line.ToString(),Utf8);
   }
  }
  catch{}
 }
 /// <summary>Deletes log files older than the retention window. Call once at startup.</summary>
 public static void Prune()
 {
  try
  {
   if(!Directory.Exists(Folder))return;var limit=DateTime.Now.AddDays(-RetentionDays);
   foreach(var file in Directory.EnumerateFiles(Folder).Where(f=>f.EndsWith(".log",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase)))
    try{if(File.GetLastWriteTime(file)<limit)File.Delete(file);}catch{}
  }
  catch{}
 }
 /// <summary>Last lines of today's and yesterday's log, read without blocking writers.</summary>
 public static IReadOnlyList<string> Tail(int count)
 {
  try
  {
   if(!Directory.Exists(Folder))return [];
   var files=Directory.EnumerateFiles(Folder,"weatherbeacon-*.log").OrderByDescending(f=>f,StringComparer.OrdinalIgnoreCase).Take(2).Reverse();
   var lines=new List<string>();
   foreach(var file in files)lines.AddRange(ReadShared(file).Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries));
   return lines.Skip(Math.Max(0,lines.Count-count)).ToArray();
  }
  catch{return [];}
 }
 internal static string ReadShared(string path)
 {
  using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
  using var reader=new StreamReader(stream,Encoding.UTF8);return reader.ReadToEnd();
 }
}
