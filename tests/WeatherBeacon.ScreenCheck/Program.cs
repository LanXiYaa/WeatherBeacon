using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
internal static class Program
{
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
 delegate bool EnumProc(nint window,nint parameter);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc callback,nint parameter);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(nint parent,EnumProc callback,nint parameter);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(nint window,StringBuilder text,int length);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(nint window,out uint processId);
 [DllImport("user32.dll")]static extern nint GetParent(nint window);
 [DllImport("user32.dll")]static extern nint GetWindow(nint window,uint command);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(nint window);
 [DllImport("user32.dll")]static extern bool GetWindowRect(nint window,out Rect rect);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern nint GetWindowLongPtr(nint window,int index);
 [DllImport("user32.dll")]static extern bool PrintWindow(nint window,nint dc,uint flags);
 [STAThread]static void Main()
 {
  var output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"screen-check.png"));
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
  var bounds=SystemInformation.VirtualScreen;
  using var bitmap=new Bitmap(bounds.Width,bounds.Height);
  using var graphics=Graphics.FromImage(bitmap);
  graphics.CopyFromScreen(bounds.Left,bounds.Top,0,0,bitmap.Size);
  bitmap.Save(output,ImageFormat.Png);
  var processIds=System.Diagnostics.Process.GetProcessesByName("WeatherBeacon").Select(p=>p.Id).ToHashSet();
  var lines=new List<string>();var hosts=new List<nint>();
  EnumWindows((window,_)=>
  {
   var className=ClassName(window);
   if(className is "Progman" or "WorkerW")
   {
    lines.Add(TopLine(window,"TOP"));hosts.Add(window);
    EnumChildWindows(window,(child,__)=>{lines.Add(TopLine(child,"  CHILD"));return true;},0);
   }
   return true;
  },0);
  File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"screen-check.txt"),lines);
  var index=0;
  foreach(var host in hosts.Where(IsWindowVisible))
  {
   try{CaptureWindow(host,Path.Combine(AppContext.BaseDirectory,$"workerw-{index++}.png"));}catch{}
  }
  string TopLine(nint window,string indent)
  {
   GetWindowThreadProcessId(window,out var processId);GetWindowRect(window,out var rect);
   return $"{indent} {window.ToInt64():X} {ClassName(window)} pid={processId} app={processIds.Contains((int)processId)} visible={IsWindowVisible(window)} parent={GetParent(window).ToInt64():X} prev={GetWindow(window,3).ToInt64():X} next={GetWindow(window,2).ToInt64():X} style={GetWindowLongPtr(window,-16).ToInt64():X} exStyle={GetWindowLongPtr(window,-20).ToInt64():X} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}";
  }
 }
 static string ClassName(nint window){var text=new StringBuilder(256);GetClassName(window,text,text.Capacity);return text.ToString();}
 static void CaptureWindow(nint window,string path)
 {
  GetWindowRect(window,out var rect);var width=Math.Max(1,rect.Right-rect.Left);var height=Math.Max(1,rect.Bottom-rect.Top);
  using var bitmap=new Bitmap(width,height);using var graphics=Graphics.FromImage(bitmap);var dc=graphics.GetHdc();
  try{PrintWindow(window,dc,2);}finally{graphics.ReleaseHdc(dc);}
  bitmap.Save(path,ImageFormat.Png);
 }
}
