using System.Drawing;
using System.Runtime.InteropServices;
namespace WeatherBeacon.Platform;
internal static class TrayArtwork
{
 [DllImport("user32.dll")]private static extern bool DestroyIcon(nint handle);
 public static Icon Create()
 {
  using var bitmap=new Bitmap(32,32);using var g=Graphics.FromImage(bitmap);
  g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
  g.Clear(Color.Transparent);using var sky=new SolidBrush(Color.FromArgb(31,90,125));g.FillEllipse(sky,0,0,32,32);
  using var sun=new SolidBrush(Color.FromArgb(255,211,103));g.FillEllipse(sun,8,5,15,15);
  using var cloud=new SolidBrush(Color.FromArgb(228,244,255));g.FillEllipse(cloud,4,17,22,10);g.FillEllipse(cloud,10,13,13,13);
  var handle=bitmap.GetHicon();try{using var borrowed=Icon.FromHandle(handle);return (Icon)borrowed.Clone();}finally{DestroyIcon(handle);}
 }
}
