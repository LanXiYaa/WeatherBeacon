using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WeatherBeacon.Core.Models;

namespace WeatherBeacon.Features.Notifications;

// The ledger is notification state, not a diagnostic log. No weather descriptions are persisted.
public sealed class AlertDelivery
{
 private readonly string path;
 private Dictionary<string,Dictionary<string,string>> ledger=[];
 private readonly bool readable=true;
 public string Status {get;private set;}="尚未发送预警";
 public AlertDelivery(string path)
 {
  this.path=path;
  try
  {
   if(File.Exists(path))ledger=JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,string>>>(File.ReadAllText(path))??throw new InvalidDataException();
   if(ledger.Any(p=>p.Value is null))throw new InvalidDataException();
  }
  catch{readable=false;Status="预警去重记录无法读取，已暂停真实通知以避免重复推送；请检查配置目录";}
 }
 private void Save()
 {
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  File.WriteAllText(path+".tmp",JsonSerializer.Serialize(ledger));File.Move(path+".tmp",path,true);
 }
 public static string Fingerprint(WeatherAlert a)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(a))));
 public static string Slot(WeatherAlert a)=>"v2:"+(string.IsNullOrEmpty(a.Type)?"id:"+a.Id:"type:"+a.Type);
 public void Process(string cityId,WeatherSnapshot snapshot,bool enabled,AlertLevel minimum,Func<WeatherAlert,bool> send,Func<string,bool>? remove=null)
 {
  if(!readable)return;
  try
  {
   if(!ledger.TryGetValue(cityId,out var sent))ledger[cityId]=sent=[];
   var active=snapshot.Alerts.Where(a=>a.Expires is null||a.Expires>DateTimeOffset.UtcNow)
    .GroupBy(Slot).Select(g=>g.OrderByDescending(a=>a.PublishedAt).ThenByDescending(a=>a.Level).First()).ToArray();
   foreach(var a in active)
    if(sent.Remove(a.Id,out var old)&&!sent.ContainsKey(Slot(a)))sent[Slot(a)]=old;
   // Only an authoritative, successfully parsed response can clear previous alert identities.
   if(snapshot.AlertsComplete)
   {
    var ids=active.Select(Slot).ToHashSet();
    foreach(var id in sent.Keys.Where(id=>!ids.Contains(id)).ToArray())
    {
     if(remove is not null&&!remove(id)){Status="预警已解除，通知中心清除失败；下次刷新重试";return;}
     sent.Remove(id);
    }
    Save();
   }
   if(!enabled){Status="预警通知已关闭";return;}
   foreach(var a in active.Where(a=>a.Level<minimum&&sent.ContainsKey(Slot(a))))
   {
    if(remove is not null&&!remove(Slot(a))){Status="预警已降级，旧通知清除失败；下次刷新重试";return;}
   }
   foreach(var a in active.Where(a=>a.Level>=minimum))
   {
    var slot=Slot(a);var fingerprint=Fingerprint(a);
    if(sent.TryGetValue(slot,out var previous)&&previous==fingerprint)continue;
    // Reserve durably before delivery: a crash cannot replay the same alert on next startup.
    sent[slot]=fingerprint;Save();
    bool accepted;
    try{accepted=send(a);}catch{accepted=false;}
    if(!accepted)
    {
     if(previous is null)sent.Remove(slot);else sent[slot]=previous;
     Save();Status="通知未能提交到 Windows，将在下次天气刷新时重试";return;
    }
    Status="预警已提交到 Windows；实际横幅受系统设置控制";
   }
  }
  catch{Status="预警去重记录保存失败，已停止本次通知；请检查配置目录写入权限";}
 }
}
