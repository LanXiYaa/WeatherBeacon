using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.Win32;

namespace WeatherBeacon.Platform;

// All three identities (process, shortcut, notifier) must agree, including after moving the portable app.
public static class ToastRegistration
{
 public const string AppId="WeatherBeacon.Desktop";
 public const string ActivatorId="07C27A3F-50D8-4612-9D96-6EE4D755986C";
 public static string AssetFolder=>Path.Combine(Features.Settings.SettingsModel.Folder,"Assets");
 public static string IconPath=>Path.Combine(AssetFolder,"WeatherBeacon.ico");
 public static string ImagePath=>Path.Combine(AssetFolder,"WeatherBeacon.png");
 public static string ShortcutPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"WeatherBeacon.lnk");
 private static bool registered;
 public static void Ensure()
 {
  if(registered)return;
  var exe=Environment.ProcessPath??throw new InvalidOperationException("无法确定当前 EXE 路径");
  // A test host must never replace the real product's shortcut or COM command.
  if(!string.Equals(Path.GetFileName(exe),"WeatherBeacon.exe",StringComparison.OrdinalIgnoreCase))
   throw new InvalidOperationException("通知注册必须从 WeatherBeacon.exe 执行，不能使用测试宿主身份");
  Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppId));
  Directory.CreateDirectory(AssetFolder);
  CopyResource("WeatherBeacon.ico",IconPath);CopyResource("WeatherBeacon.png",ImagePath);
  using(var app=Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\"+AppId))
  {
   app.SetValue("DisplayName","WeatherBeacon");app.SetValue("IconUri",ImagePath);
   app.SetValue("IconBackgroundColor","00000000");app.SetValue("CustomActivator","{"+ActivatorId+"}");
  }
  // This documented helper allows an explicit, stable AUMID and an explicit COM activator.
#pragma warning disable CS0618
  DesktopNotificationManagerCompat.RegisterAumidAndComServer<WeatherToastActivator>(AppId);
  using(var server=Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{"+ActivatorId+@"}\LocalServer32"))
   server.SetValue("",'"'+exe+"\" -ToastActivated");
  CreateShortcut(exe);
  DesktopNotificationManagerCompat.RegisterActivator<WeatherToastActivator>();
#pragma warning restore CS0618
  registered=true;
 }
 private static void CopyResource(string name,string destination)
 {
  var resource=System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/WeatherBeacon;component/Assets/"+name))??throw new FileNotFoundException(name);
  using var stream=resource.Stream;using var memory=new MemoryStream();stream.CopyTo(memory);
  var bytes=memory.ToArray();
  if(!File.Exists(destination)||!File.ReadAllBytes(destination).SequenceEqual(bytes))File.WriteAllBytes(destination,bytes);
 }
 private static void CreateShortcut(string exe)
 {
  var link=(IShellLinkW)new ShellLink();
  try
  {
   link.SetPath(exe);link.SetWorkingDirectory(Path.GetDirectoryName(exe)!);
   link.SetDescription("WeatherBeacon 桌面天气");link.SetIconLocation(IconPath,0);
   var store=(IPropertyStore)link;
   var idKey=new PropertyKey(5);var activatorKey=new PropertyKey(26);
   using var id=PropVariant.String(AppId);using var clsid=PropVariant.Guid(new Guid(ActivatorId));
   var idValue=id;var clsidValue=clsid;
   Marshal.ThrowExceptionForHR(store.SetValue(ref idKey,ref idValue));
   Marshal.ThrowExceptionForHR(store.SetValue(ref activatorKey,ref clsidValue));
   Marshal.ThrowExceptionForHR(store.Commit());
   Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);
   ((IPersistFile)link).Save(ShortcutPath,true);
   SHChangeNotify(0x00002000,0x1005,ShortcutPath,0);
  }
  finally{Marshal.FinalReleaseComObject(link);}
 }
 public static string InspectShortcut()
 {
  var link=(IShellLinkW)new ShellLink();
  try
  {
   ((IPersistFile)link).Load(ShortcutPath,0);
   var path=new System.Text.StringBuilder(1024);link.GetPath(path,path.Capacity,0,0);
   var store=(IPropertyStore)link;var key=new PropertyKey(5);var activator=new PropertyKey(26);
   Marshal.ThrowExceptionForHR(store.GetValue(ref key,out var id));
   Marshal.ThrowExceptionForHR(store.GetValue(ref activator,out var clsid));
   using(id)using(clsid)return $"target={path}; aumid={Marshal.PtrToStringUni(id.Pointer)}; activator={Marshal.PtrToStructure<Guid>(clsid.Pointer)}";
  }
  finally{Marshal.FinalReleaseComObject(link);}
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern void SHChangeNotify(uint eventId,uint flags,string item1,nint item2);
 [DllImport("ole32.dll")]private static extern int PropVariantClear(ref PropVariant value);
 [StructLayout(LayoutKind.Sequential)]private struct PropertyKey(uint id){public Guid Format=new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");public uint Id=id;}
 [StructLayout(LayoutKind.Explicit,Size=24)]private struct PropVariant:IDisposable
 {
  [FieldOffset(0)]private ushort type;[FieldOffset(8)]public nint Pointer;
  public static PropVariant String(string value)=>new(){type=31,Pointer=Marshal.StringToCoTaskMemUni(value)};
  public static PropVariant Guid(Guid value){var pointer=Marshal.AllocCoTaskMem(16);Marshal.StructureToPtr(value,pointer,false);return new(){type=72,Pointer=pointer};}
  public void Dispose()=>PropVariantClear(ref this);
 }
 [ComImport,Guid("00021401-0000-0000-C000-000000000046")]private class ShellLink{}
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IPropertyStore
 {
  [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out PropertyKey key);
  [PreserveSig]int GetValue(ref PropertyKey key,out PropVariant value);[PreserveSig]int SetValue(ref PropertyKey key,ref PropVariant value);[PreserveSig]int Commit();
 }
 [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IShellLinkW
 {
  void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]System.Text.StringBuilder path,int length,nint data,uint flags);
  void GetIDList(out nint list);void SetIDList(nint list);void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]System.Text.StringBuilder text,int length);
  void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]System.Text.StringBuilder text,int length);
  void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string directory);void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]System.Text.StringBuilder text,int length);
  void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string arguments);void GetHotkey(out short hotkey);void SetHotkey(short hotkey);void GetShowCmd(out int command);void SetShowCmd(int command);
  void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]System.Text.StringBuilder text,int length,out int index);void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string path,int index);
  void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(nint hwnd,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
 }
}

// Intentional legacy compatibility API: the newer helper derives AUMIDs from the EXE path.
#pragma warning disable CS0618
[ComVisible(true),Guid(ToastRegistration.ActivatorId),ClassInterface(ClassInterfaceType.None)]
public class WeatherToastActivator:NotificationActivator
{
 public static event Action? Activated;
 public override void OnActivated(string arguments,NotificationUserInput userInput,string appUserModelId)
 {if(appUserModelId==ToastRegistration.AppId)Activated?.Invoke();}
}
#pragma warning restore CS0618
