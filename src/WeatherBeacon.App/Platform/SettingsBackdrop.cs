using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace WeatherBeacon.Platform;
internal sealed record SettingsBackdropResult(string AppliedName,bool IsFallback,string Description);
internal static class SettingsBackdrop
{
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(nint h,int attribute,ref int value,int size);
 [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(nint h,ref Margins margins);
 [StructLayout(LayoutKind.Sequential)]struct Margins {public int Left,Right,Top,Bottom;}
 public static SettingsBackdropResult Apply(Window window,int material)
 {
  var handle=new WindowInteropHelper(window).Handle;
  var fallback=new SolidColorBrush(Color.FromRgb(23,23,28));
  window.Background=fallback;
  if(handle==0)return Fallback(material,"设置窗口句柄尚未创建");
  var build=Environment.OSVersion.Version.Build;
  if(build<22621)return Fallback(material,$"当前 Windows 内部版本 {build} 不支持此系统背景材质");
  var type=material switch{0=>2,1=>4,2=>3,_=>1};
  if(type==1)return new("纯色",false,"当前使用纯色设置背景");
  var dark=1;
  _=DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
  var result=DwmSetWindowAttribute(handle,38,ref type,sizeof(int));
  if(result!=0)return Fallback(material,$"DWM 拒绝该材质（HRESULT 0x{result:X8}）");
  var margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};
  result=DwmExtendFrameIntoClientArea(handle,ref margins);
  if(result!=0)return Fallback(material,$"无法扩展设置窗口背景（HRESULT 0x{result:X8}）");
  window.Background=Brushes.Transparent;
  var source=HwndSource.FromHwnd(handle);
  if(source?.CompositionTarget is not null)source.CompositionTarget.BackgroundColor=Colors.Transparent;
  var name=material switch{0=>"云母",1=>"云母 Alt",2=>"亚克力",_=>"纯色"};
  return new(name,false,$"设置窗口正在使用 {name}；小组件背景不受此设置影响");
 }
 static SettingsBackdropResult Fallback(int material,string reason)
 {
  var requested=material switch{0=>"云母",1=>"云母 Alt",2=>"亚克力",_=>"纯色"};
  return new("纯色",true,$"{requested}不可用：{reason}；已自动降级为纯色设置背景");
 }
}
