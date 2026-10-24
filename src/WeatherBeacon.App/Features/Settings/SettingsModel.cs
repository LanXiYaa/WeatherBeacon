using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text.Json;
using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Features.Settings;

public sealed class WidgetPlacement
{
 public string MonitorDeviceName {get;set;}="";
 public int Left {get;set;}
 public int Top {get;set;}
 public int Width {get;set;}
 public int Height {get;set;}
 public int MonitorLeft {get;set;}
 public int MonitorTop {get;set;}
 public int MonitorWidth {get;set;}
 public int MonitorHeight {get;set;}
 public int WorkAreaLeft {get;set;}
 public int WorkAreaTop {get;set;}
 public int WorkAreaWidth {get;set;}
 public int WorkAreaHeight {get;set;}
 public double DpiScale {get;set;}=1;
 public double RelativeX {get;set;}
 public double RelativeY {get;set;}
}

public sealed class SettingsModel : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;
 private bool notificationsEnabled,developerMode;private int alertThreshold=1;
 public bool NotificationsEnabled{get=>notificationsEnabled;set{if(notificationsEnabled==value)return;notificationsEnabled=value;Changed();}}
 public int AlertThreshold{get=>alertThreshold;set{value=Math.Clamp(value,0,3);if(alertThreshold==value)return;alertThreshold=value;Changed();Changed(nameof(AlertThresholdLabel));}}
 [System.Text.Json.Serialization.JsonIgnore] public string AlertThresholdLabel=>new[]{"蓝色及以上","黄色及以上","橙色及以上","仅红色"}[AlertThreshold];
 public bool DeveloperMode{get=>developerMode;set{if(developerMode==value)return;developerMode=value;Changed();}}
 [System.Text.Json.Serialization.JsonIgnore] public string NotificationStatus{get;private set;}="通知默认关闭";
 [System.Text.Json.Serialization.JsonIgnore] public string AlertDataStatus{get;private set;}="等待当前城市天气刷新";
 [System.Text.Json.Serialization.JsonIgnore] public string DeliveryStatus{get;private set;}="尚未发送预警";
 public void SetNotificationStatus(string value){NotificationStatus=value;Changed(nameof(NotificationStatus));}
 public void SetAlertDataStatus(string value){AlertDataStatus=value;Changed(nameof(AlertDataStatus));}
 public void SetDeliveryStatus(string value){DeliveryStatus=value;Changed(nameof(DeliveryStatus));}
 private int refreshMinutes=15;private bool fahrenheit;
 public int RefreshMinutes{get=>refreshMinutes;set{if(value is not (5 or 10 or 15 or 30 or 60))value=15;if(refreshMinutes==value)return;refreshMinutes=value;Changed();}}
 public bool Fahrenheit{get=>fahrenheit;set{if(fahrenheit==value)return;fahrenheit=value;Changed();}}
 private bool animations=true;
 public bool Animations {get=>animations;set{if(animations==value)return;animations=value;Changed();}}
 private int material=0; private double opacity=1,radius=24; private bool lockPosition;
 public int Material {get=>material;set{if(material==value)return;material=value;Changed();}}
 public double Opacity {get=>opacity;set{value=Math.Clamp(value,.2,1);if(Math.Abs(opacity-value)<.0001)return;opacity=value;Changed();}}
 public double Radius {get=>radius;set{value=Math.Clamp(value,0,48);if(Math.Abs(radius-value)<.0001)return;radius=value;Changed();}}
 public bool LockPosition {get=>lockPosition;set{if(lockPosition==value)return;lockPosition=value;Changed();}}
 private double widgetScale=1,fontScale=1;private bool compactMode,rainReminder=true;
 /// <summary>Overall widget size, 75%–150%.</summary>
 public double WidgetScale {get=>widgetScale;set{value=Math.Round(Math.Clamp(double.IsFinite(value)?value:1,.75,1.5),2);if(Math.Abs(widgetScale-value)<.0001)return;widgetScale=value;Changed();}}
 /// <summary>Text size relative to the layout, 85%–130%.</summary>
 public double FontScale {get=>fontScale;set{value=Math.Round(Math.Clamp(double.IsFinite(value)?value:1,.85,1.3),2);if(Math.Abs(fontScale-value)<.0001)return;fontScale=value;Changed();}}
 /// <summary>Compact mode keeps only city, current weather, rain reminder and status.</summary>
 public bool CompactMode {get=>compactMode;set{if(compactMode==value)return;compactMode=value;Changed();}}
 public bool RainReminder {get=>rainReminder;set{if(rainReminder==value)return;rainReminder=value;Changed();}}
 public WidgetPlacement? Placement {get;set;}
 public bool WidgetHidden {get;set;}
 public bool OnboardingCompleted {get;set;}
 public bool SystemInfoConsent {get;set;}
 [System.Text.Json.Serialization.JsonIgnore] public string DesktopStatus {get;set;}="桌面嵌入尚未初始化";
 [System.Text.Json.Serialization.JsonIgnore] public string MaterialStatus {get;set;}="正在检测设置窗口材质…";
 public void SetDesktopStatus(string text){DesktopStatus=text;Changed(nameof(DesktopStatus));}
 public void SetMaterialStatus(string text){MaterialStatus=text;Changed(nameof(MaterialStatus));}
 public WeatherLocation? City {get;set;}
 private void Changed([CallerMemberName]string? n=null)=>PropertyChanged?.Invoke(this,new(n));
 public static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WeatherBeacon");
 public static SettingsModel Load()
 {
  try {if(File.Exists(Path.Combine(Folder,"settings.json"))) return JsonSerializer.Deserialize<SettingsModel>(File.ReadAllText(Path.Combine(Folder,"settings.json")))??new();}catch(Exception ex){WeatherBeacon.Platform.AppLog.Error("Settings","设置文件无法读取，已使用默认设置",ex);}
  var s=new SettingsModel();try{if(File.Exists(Path.Combine(Folder,"city.json")))s.City=JsonSerializer.Deserialize<WeatherLocation>(File.ReadAllText(Path.Combine(Folder,"city.json")));}catch{}return s;
 }
 public void Save(){try{SaveCore();}catch(Exception ex){WeatherBeacon.Platform.AppLog.Error("Settings","设置保存失败",ex);throw;}}
 private void SaveCore(){Directory.CreateDirectory(Folder);var path=Path.Combine(Folder,"settings.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}



