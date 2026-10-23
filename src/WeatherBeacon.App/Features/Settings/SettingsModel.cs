using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text.Json;
using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Features.Settings;
public sealed class SettingsModel : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;
 private bool animations=true;
 public bool Animations {get=>animations;set{animations=value;Changed();}}
 private int position=3,material=0; private double opacity=0.96,radius=24;
 public int Position {get=>position;set{position=value;Changed();}}
 public int Material {get=>material;set{material=value;Changed();}}
 public double Opacity {get=>opacity;set{opacity=Math.Clamp(value,.2,1);Changed();}}
 public double Radius {get=>radius;set{radius=Math.Clamp(value,0,48);Changed();}}
 public bool WidgetHidden {get;set;}
 [System.Text.Json.Serialization.JsonIgnore] public string DesktopStatus {get;set;}="桌面嵌入尚未初始化";
 public void SetDesktopStatus(string text){DesktopStatus=text;Changed(nameof(DesktopStatus));}
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


