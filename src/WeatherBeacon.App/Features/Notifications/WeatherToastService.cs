using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Platform;
namespace WeatherBeacon.Features.Notifications;

public sealed class WeatherToastService
{
 private bool initialized;
 private readonly Action register;
 public WeatherToastService(Action? register=null){this.register=register??ToastRegistration.Ensure;}
 private readonly Dictionary<string,ToastNotification> live=[];
 public string Status {get;private set;}="尚未发送通知";
 public string AvailabilityStatus {get;private set;}="正在检测通知注册";
 public string DetailedStatus=>AvailabilityStatus+"\n"+Status;
 public event Action? Activated;
 public event Action? StatusChanged;
 private void SetStatus(string value){Status=value;StatusChanged?.Invoke();}
 public bool Initialize()
 {
  try
  {
   if(!initialized)
   {
    WeatherToastActivator.Activated+=HandleActivation;
    try{register();initialized=true;}
    catch{WeatherToastActivator.Activated-=HandleActivation;throw;}
   }
   return true;
  }
  catch(Exception ex){AvailabilityStatus=$"Windows 通知注册失败（0x{ex.HResult:X8}）：{ex.Message}";StatusChanged?.Invoke();return false;}
 }
 private void HandleActivation()=>Activated?.Invoke();
 public bool CheckAvailability()
 {
  if(!Initialize())return false;
  try
  {
   var setting=ToastNotificationManager.CreateToastNotifier(ToastRegistration.AppId).Setting;
   AvailabilityStatus=setting switch
   {
    NotificationSetting.Enabled=>"WeatherBeacon 通知注册正常；Windows 允许通知。勿扰时可能仅进入通知中心。",
    NotificationSetting.DisabledForApplication=>"Windows 已禁用 WeatherBeacon 通知，请在系统通知设置中检查。",
    NotificationSetting.DisabledForUser=>"Windows 已关闭当前用户通知。",
    NotificationSetting.DisabledByGroupPolicy=>"系统策略禁止通知。",
    _=>"Windows 通知不可用："+setting
   };
   StatusChanged?.Invoke();return setting==NotificationSetting.Enabled;
  }
  catch(Exception ex)when(ex.HResult==unchecked((int)0x80070490))
  {
   // Windows may create its per-app notification settings only after the first Show.
   // This is not an explicit denial. Show still enforces all Windows notification policy.
   AvailabilityStatus="Windows 尚未创建此应用的通知设置；首次提交由系统决定是否展示。";
   StatusChanged?.Invoke();return true;
  }
  catch(Exception ex){AvailabilityStatus=$"无法读取系统通知状态（0x{ex.HResult:X8}）";StatusChanged?.Invoke();return false;}
 }
 public static string LevelName(AlertLevel level)=>level switch{AlertLevel.Blue=>"蓝色",AlertLevel.Yellow=>"黄色",AlertLevel.Orange=>"橙色",_=>"红色"};
 public static string TagFor(string cityId,string slot)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cityId+"\n"+slot)))[..16];
 public static string CityName(string city)
 {
  var parts=city.Split(['·',',','，'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
  return parts.FirstOrDefault(p=>p.EndsWith("市")||p.EndsWith("州")||p.EndsWith("盟"))??parts.FirstOrDefault()??city;
 }
 public static string BuildXml(WeatherAlert alert,string city,bool debug,string imagePath)
 {
  var clean=new string(alert.Description.Where(c=>!char.IsControl(c)||c is '\n' or '\r' or '\t').ToArray());
  var boundaries=StringInfo.ParseCombiningCharacters(clean);
  var description=boundaries.Length>90?clean[..boundaries[90]]+"…":clean;
  var binding=new XElement("binding",new XAttribute("template","ToastGeneric"),
   new XElement("text",(debug?"[调试] ":"")+CityName(city)+" · "+alert.Type+LevelName(alert.Level)+"预警"),
   new XElement("text",new XAttribute("hint-maxLines","2"),description),
   new XElement("image",new XAttribute("placement","appLogoOverride"),new XAttribute("src",new Uri(imagePath).AbsoluteUri)));
  var toast=new XElement("toast",new XAttribute("launch","open-settings"),new XElement("visual",binding),
   new XElement("actions",new XElement("action",new XAttribute("content","知道了"),new XAttribute("activationType","system"),new XAttribute("arguments","dismiss"))));
  if(alert.PublishedAt is {} published)toast.SetAttributeValue("displayTimestamp",published.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture));
  return toast.ToString(SaveOptions.DisableFormatting);
 }
 public bool Send(WeatherAlert alert,string city,bool debug=false,string? cityId=null)
 {
  if(!CheckAvailability()){SetStatus(AvailabilityStatus);return false;}
  try
  {
   var xml=new XmlDocument();xml.LoadXml(BuildXml(alert,city,debug,ToastRegistration.ImagePath));
   var tag=debug?Guid.NewGuid().ToString("N")[..16]:TagFor(cityId??city,AlertDelivery.Slot(alert));
   var toast=new ToastNotification(xml){Tag=tag,Group=debug?"debug":"weather",SuppressPopup=false};
   // No invented expiry: Windows retains the notification until resolved, dismissed, or its own retention limit.
   if(alert.Expires is {} expires&&!debug)toast.ExpirationTime=expires;
   toast.Failed+=(_,e)=>SetStatus($"Windows 拒绝此通知（0x{e.ErrorCode.HResult:X8}）：{e.ErrorCode.Message}");
   toast.Dismissed+=(_,_)=>{var dispatcher=System.Windows.Application.Current?.Dispatcher;if(dispatcher is {HasShutdownStarted:false})dispatcher.BeginInvoke(new Action(()=>live.Remove(tag)));};
   live[tag]=toast;
   while(live.Count>64)live.Remove(live.Keys.First());
   ToastNotificationManager.CreateToastNotifier(ToastRegistration.AppId).Show(toast);
   SetStatus("已提交 Windows。通知中心保留消息；横幅显示由系统通知和勿扰设置决定。");return true;
  }
  catch(Exception ex){SetStatus($"通知发送失败（0x{ex.HResult:X8}）：{ex.Message}");return false;}
 }
 public bool Remove(string cityId,string slot)
 {
  if(!Initialize())return false;
  try{ToastNotificationManager.History.Remove(TagFor(cityId,slot),"weather",ToastRegistration.AppId);return true;}
  catch(Exception ex){SetStatus($"旧通知清除失败（0x{ex.HResult:X8}），下次刷新重试");return false;}
 }
}
