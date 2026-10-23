using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace WeatherBeacon.Platform;
internal static class SettingsBackdrop
{
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(nint h,int attribute,ref int value,int size);
 [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(nint h,ref Margins margins);
 [StructLayout(LayoutKind.Sequential)]struct Margins {public int Left,Right,Top,Bottom;}
 public static void Apply(Window window,int material)
 {
  var handle=new WindowInteropHelper(window).Handle;if(handle==0)return;
  window.Background=new SolidColorBrush(Color.FromRgb(240,245,250));
  if(Environment.OSVersion.Version.Build<22621)return;
  int type=material switch{0=>2,1=>4,2=>3,_=>1};
  if(DwmSetWindowAttribute(handle,38,ref type,4)!=0)return;
  var margins=new Margins{Left=material==3?0:-1};DwmExtendFrameIntoClientArea(handle,ref margins);
  if(material!=3){window.Background=Brushes.Transparent;var source=HwndSource.FromHwnd(handle);if(source?.CompositionTarget is not null)source.CompositionTarget.BackgroundColor=Colors.Transparent;}
 }
}
