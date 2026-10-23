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
 private bool animations=true;
 public bool Animations {get=>animations;set{if(animations==value)return;animations=value;Changed();}}
 private int material=0; private double opacity=1,radius=24; private bool lockPosition;
 public int Material {get=>material;set{if(material==value)return;material=value;Changed();}}
 public double Opacity {get=>opacity;set{value=Math.Clamp(value,.2,1);if(Math.Abs(opacity-value)<.0001)return;opacity=value;Changed();}}
 public double Radius {get=>radius;set{value=Math.Clamp(value,0,48);if(Math.Abs(radius-value)<.0001)return;radius=value;Changed();}}
 public bool LockPosition {get=>lockPosition;set{if(lockPosition==value)return;lockPosition=value;Changed();}}
 public WidgetPlacement? Placement {get;set;}
 public bool WidgetHidden {get;set;}
 [System.Text.Json.Serialization.JsonIgnore] public string DesktopStatus {get;set;}="桌面嵌入尚未初始化";
 [System.Text.Json.Serialization.JsonIgnore] public string MaterialStatus {get;set;}="正在检测设置窗口材质…";
 public void SetDesktopStatus(string text){DesktopStatus=text;Changed(nameof(DesktopStatus));}
 public void SetMaterialStatus(string text){MaterialStatus=text;Changed(nameof(MaterialStatus));}
 public WeatherLocation? City {get;set;}
 private void Changed([CallerMemberName]string? n=null)=>PropertyChanged?.Invoke(this,new(n));
 public static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WeatherBeacon");
 public static SettingsModel Load()
 {
  try {if(File.Exists(Path.Combine(Folder,"settings.json"))) return JsonSerializer.Deserialize<SettingsModel>(File.ReadAllText(Path.Combine(Folder,"settings.json")))??new();}catch{}
  var s=new SettingsModel();try{if(File.Exists(Path.Combine(Folder,"city.json")))s.City=JsonSerializer.Deserialize<WeatherLocation>(File.ReadAllText(Path.Combine(Folder,"city.json")));}catch{}return s;
 }
 public void Save(){Directory.CreateDirectory(Folder);var path=Path.Combine(Folder,"settings.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this));File.Move(path+".tmp",path,true);}
}


