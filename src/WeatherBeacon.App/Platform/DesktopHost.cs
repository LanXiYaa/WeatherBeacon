using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WeatherBeacon.Features.Settings;
namespace WeatherBeacon.Platform;
public sealed class DesktopHost : IDisposable
{
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern nint FindWindow(string c,string? t);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern nint FindWindowEx(nint p,nint a,string c,string? t);
 delegate bool EnumProc(nint h,nint p);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc p,nint l);
 [DllImport("user32.dll")]static extern nint SendMessageTimeout(nint h,uint m,nint w,nint l,uint f,uint t,out nint r);
 [DllImport("user32.dll",SetLastError=true)]static extern nint SetParent(nint h,nint p);
 [DllImport("user32.dll")]static extern nint GetParent(nint h);
 [DllImport("user32.dll")]static extern bool IsWindow(nint h);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(nint h);
 [DllImport("user32.dll")]static extern bool IsIconic(nint h);
 [DllImport("user32.dll")]static extern nint GetForegroundWindow();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(nint h,StringBuilder s,int n);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern nint GetStyle64(nint h,int n);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]static extern nint SetStyle64(nint h,int n,nint v);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetStyle32(nint h,int n);
 [DllImport("user32.dll",EntryPoint="SetWindowLongW",SetLastError=true)]static extern int SetStyle32(nint h,int n,int v);
 static long Style(nint h)=>IntPtr.Size==8?GetStyle64(h,-16).ToInt64():unchecked((uint)GetStyle32(h,-16));
 static bool SetStyle(nint h,long s){Marshal.SetLastPInvokeError(0);if(IntPtr.Size==8)SetStyle64(h,-16,(nint)s);else SetStyle32(h,-16,unchecked((int)s));return Marshal.GetLastPInvokeError()==0;}
 [DllImport("user32.dll")]static extern bool SetWindowPos(nint h,nint a,int x,int y,int w,int z,uint f);
 [DllImport("user32.dll")]static extern bool ScreenToClient(nint h,ref P p);
 [StructLayout(LayoutKind.Sequential)]struct P{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct R{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]static extern bool GetWindowRect(nint h,out R r);
 [DllImport("user32.dll")]static extern bool GetClientRect(nint h,out R r);
 [DllImport("user32.dll")]static extern nint WindowFromPoint(P point);
 [DllImport("user32.dll")]static extern bool GetCursorPos(out P point);
 [DllImport("user32.dll")]static extern nint GetAncestor(nint h,uint flags);
 [DllImport("user32.dll")]static extern bool ShowWindow(nint h,int cmd);
 public object? SavedPlacement{get;set;}
 public string AnimationStatus{get;set;}="not-initialized";
 public long AnimationFrames{get;set;}
 public int SavedPosition{get;set;}
 public bool SavedHidden{get;set;}
 delegate void EventProc(nint hook,uint e,nint h,int obj,int child,uint thread,uint time);
 [DllImport("user32.dll")]static extern nint SetWinEventHook(uint min,uint max,nint module,EventProc callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")]static extern bool UnhookWinEvent(nint hook);
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(250)};
 readonly List<nint> hooks=[];EventProc? callback;Window? window;nint host,handle;string last="";bool disposed;
 public static string LogFolder=>AppLog.Folder;
 readonly string log=Path.Combine(LogFolder,$"desktop-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.jsonl");
 public string Status{get;private set;}="桌面嵌入尚未初始化";
 public event Action<string>? StatusChanged;
 public event Action? DisplayChanged;
 public bool UserHidden{get;set;}
 public bool IsAttached=>handle!=0&&IsWindow(host)&&GetParent(handle)==host&&(Style(handle)&0x40000000)!=0&&(Style(handle)&0x80000000)==0;
 public bool IsWindowAvailable=>handle!=0&&IsWindow(handle)&&IsWindowVisible(handle)&&!IsIconic(handle)&&!UserHidden;
 static string Class(nint h){var s=new StringBuilder(256);GetClassName(h,s,256);return s.ToString();}
 void SetStatus(string s){if(Status==s)return;Status=s;AppLog.Info("Desktop",s);StatusChanged?.Invoke(s);}
 public void Record(string reason,int error=0)
 {
  var fg=GetForegroundWindow();var parent=GetParent(handle);
  GetWindowRect(handle,out var rect);GetWindowRect(host,out var hostRect);
  var screens=System.Windows.Forms.Screen.AllScreens.Select(s=>new{name=s.DeviceName,bounds=s.Bounds,workArea=s.WorkingArea,primary=s.Primary}).ToArray();
  var hit=WindowFromPoint(new P{X=(rect.Left+rect.Right)/2,Y=(rect.Top+rect.Bottom)/2});
  var state=new{savedPlacement=SavedPlacement,animationStatus=AnimationStatus,animationFrames=AnimationFrames,centerHit=$"0x{hit:X}",centerRoot=$"0x{GetAncestor(hit,2):X}",centerBelongsToWidget=GetAncestor(hit,2)==handle,rect,hostRect,screens,monitor=System.Windows.Forms.Screen.FromHandle(handle).DeviceName,savedPosition=SavedPosition,savedHidden=SavedHidden,attached=IsAttached,hwnd=$"0x{handle:X}",parent=$"0x{parent:X}",host=$"0x{host:X}",hostType=Class(host),visible=IsWindowVisible(handle),hostVisible=IsWindowVisible(host),minimized=IsIconic(handle),userHidden=UserHidden,style=$"0x{Style(handle):X}",foreground=$"0x{fg:X}",foregroundType=Class(fg)};
  var options=new JsonSerializerOptions{IncludeFields=true};var json=JsonSerializer.Serialize(state,options);if(reason=="state-change"&&json==last)return;last=json;
  try{Directory.CreateDirectory(LogFolder);File.AppendAllText(log,JsonSerializer.Serialize(new{time=DateTimeOffset.Now,reason,error,state},options)+Environment.NewLine);}
  catch{SetStatus("诊断日志写入失败，请检查本地目录权限");}
 }
 public void StartOrdinary(Window w)
 {
  window=w;handle=new WindowInteropHelper(w).Handle;host=0;
  SetStatus("回退模式：普通非置顶窗口；桌面嵌入已停用，Win+D 可能隐藏小组件");
  Record("ordinary-mode-start");StartMonitoring();
 }
 public bool Attach(Window w)
 {
  window=w;handle=new WindowInteropHelper(w).Handle;
  var progman=FindWindow("Progman",null);if(progman==0)return Fail("找不到 Explorer 桌面宿主",0);
  SendMessageTimeout(progman,0x052C,0,0,2,1000,out _);
  nint candidate=0;
  EnumWindows((h,_)=>{if(FindWindowEx(h,0,"SHELLDLL_DefView",null)!=0){var next=FindWindowEx(0,h,"WorkerW",null);if(next!=0&&IsWindowVisible(next))candidate=next;}return true;},0);
  // New Explorer layouts may place WorkerW inside Progman.
  if(candidate==0){var child=FindWindowEx(progman,0,"WorkerW",null);if(child!=0&&IsWindowVisible(child))candidate=child;}
  if(candidate==0)return Fail("未发现可用 WorkerW",0);
  var old=Style(handle);var origin=w.PointToScreen(new Point(0,0));
  if(!SetStyle(handle,(old&~0x80000000L)|0x40000000L))return Fail("设置子窗口样式失败",Marshal.GetLastPInvokeError());
  Marshal.SetLastPInvokeError(0);var previous=SetParent(handle,candidate);var error=Marshal.GetLastPInvokeError();
  if((previous==0&&error!=0)||GetParent(handle)!=candidate){SetStyle(handle,old);return Fail("设置父窗口失败",error);}
  host=candidate;var p=new P{X=(int)origin.X,Y=(int)origin.Y};ScreenToClient(host,ref p);
  SetWindowPos(handle,0,p.X,p.Y,0,0,0x0035); // FRAMECHANGED | NOACTIVATE | NOZORDER | NOSIZE
  SetStatus("桌面嵌入成功 · WorkerW 子窗口");Record("attach-success");StartMonitoring();return true;
 }
 bool Fail(string detail,int error){RestoreOrdinary();SetStatus("桌面嵌入失败，当前为普通窗口模式："+detail);Record("attach-failed",error);StartMonitoring();return false;}
 void StartMonitoring()
 {
  if(callback!=null)return;
  callback=(_,e,h,_,_,_,_)=>{if(disposed||window==null)return;window.Dispatcher.BeginInvoke(new Action(()=>{if(!disposed)Record($"win-event-0x{e:X}");}));};
  foreach(var range in new[]{(3u,3u),(0x16u,0x17u)}){var hook=SetWinEventHook(range.Item1,range.Item2,0,callback,0,0,0);if(hook!=0)hooks.Add(hook);}
  timer.Tick+=(_,_)=>{Record("state-change");if(host!=0&&!IsAttached)SetStatus("桌面嵌入已失效；请退出后重新启动，诊断信息已记录");};timer.Start();
  try{Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=(_,_)=>window?.Dispatcher.BeginInvoke(new Action(()=>{if(!disposed)DisplayChanged?.Invoke();}));}catch{}
 }
 public void RestoreOrdinary()
 {
  if(handle==0)return;
  GetWindowRect(handle,out var r);SetParent(handle,0);SetStyle(handle,(Style(handle)&~0x40000000L)|0x80000000L);host=0;
  var work=System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
  var dpi=window is null?1:System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX;
  int width=(int)((window is {ActualWidth:>0}?window.ActualWidth:420)*dpi),height=(int)((window is {ActualHeight:>0}?window.ActualHeight:430)*dpi);
  SetWindowPos(handle,0,Math.Max(work.Left,work.Right-width-24),Math.Max(work.Top,work.Top+24),width,height,0x0034);
  ShowWindow(handle,4);
 }
 public void VerifyStartup()
 {
  GetWindowRect(handle,out var r);var rectangle=System.Drawing.Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);
  var onScreen=System.Windows.Forms.Screen.AllScreens.Any(s=>s.WorkingArea.IntersectsWith(rectangle));
  bool clipped=false;if(IsAttached){var p=new P{X=r.Left,Y=r.Top};ScreenToClient(host,ref p);GetClientRect(host,out var client);clipped=p.X>=client.Right||p.Y>=client.Bottom||p.X+rectangle.Width<=0||p.Y+rectangle.Height<=0;}
  Record("startup-geometry-check");
  if(!onScreen||rectangle.Width<=0||rectangle.Height<=0||!IsWindowVisible(handle)||clipped)Fail("启动可见性验证失败，已重置到主显示器",0);
  Record("startup-final");
 }
 public System.Drawing.Rectangle WindowBounds
 {
  get{GetWindowRect(handle,out var r);return System.Drawing.Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);}
 }
 public System.Drawing.Point CursorPosition{get{GetCursorPos(out var p);return new(p.X,p.Y);}}
 public bool IsCompletelyCovered()
 {
  if(!IsWindowAvailable)return true;
  GetWindowRect(handle,out var r);var width=r.Right-r.Left;var height=r.Bottom-r.Top;
  if(width<=0||height<=0)return true;
  for(int row=1;row<=5;row++)for(int column=1;column<=7;column++)
  {
   var sample=new P{X=r.Left+width*column/8,Y=r.Top+height*row/6};
   if(WindowFromPoint(sample)==handle)return false;
  }
  return true;
 }
 public void MoveToScreen(int screenX,int screenY)
 {
  if(handle==0)return;var p=new P{X=screenX,Y=screenY};
  if(IsAttached)ScreenToClient(host,ref p);
  SetWindowPos(handle,0,p.X,p.Y,0,0,0x0015); // NOSIZE | NOZORDER | NOACTIVATE
 }
 public WidgetPlacement CapturePlacement(Window w)
 {
  GetWindowRect(handle,out var r);var rect=System.Drawing.Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);
  var screen=System.Windows.Forms.Screen.FromRectangle(rect);var work=screen.WorkingArea;
  var availableWidth=Math.Max(1,work.Width-rect.Width);var availableHeight=Math.Max(1,work.Height-rect.Height);
  var dpi=VisualTreeHelper.GetDpi(w);
  return new WidgetPlacement
  {
   MonitorDeviceName=screen.DeviceName,Left=rect.Left,Top=rect.Top,Width=rect.Width,Height=rect.Height,
   MonitorLeft=screen.Bounds.Left,MonitorTop=screen.Bounds.Top,MonitorWidth=screen.Bounds.Width,MonitorHeight=screen.Bounds.Height,
   WorkAreaLeft=work.Left,WorkAreaTop=work.Top,WorkAreaWidth=work.Width,WorkAreaHeight=work.Height,
   DpiScale=dpi.DpiScaleX,
   RelativeX=Math.Clamp((rect.Left-work.Left)/(double)availableWidth,0,1),
   RelativeY=Math.Clamp((rect.Top-work.Top)/(double)availableHeight,0,1)
  };
 }
 public WidgetPlacement ApplyPlacement(Window w,WidgetPlacement? saved)
 {
  var screens=System.Windows.Forms.Screen.AllScreens;
  if(screens.Length==0)throw new InvalidOperationException("系统没有可用显示器");
  var primary=System.Windows.Forms.Screen.PrimaryScreen??screens[0];
  if(saved is not null && (!double.IsFinite(saved.RelativeX)||!double.IsFinite(saved.RelativeY)||!screens.Any(s=>s.WorkingArea.IntersectsWith(new System.Drawing.Rectangle(saved.Left,saved.Top,Math.Max(1,saved.Width),Math.Max(1,saved.Height)))))){Record("saved-placement-invalid-reset-primary");saved=null;}
  var target=saved is null?primary:screens.FirstOrDefault(s=>string.Equals(s.DeviceName,saved.MonitorDeviceName,StringComparison.OrdinalIgnoreCase));
  if(target is null&&saved is not null)
  {
   var oldMonitor=new System.Drawing.Rectangle(saved.MonitorLeft,saved.MonitorTop,Math.Max(1,saved.MonitorWidth),Math.Max(1,saved.MonitorHeight));
   target=screens.OrderByDescending(s=>IntersectionArea(oldMonitor,s.Bounds)).ThenBy(s=>Distance(s.Bounds,oldMonitor)).First();
  }
  target??=primary;
  GetWindowRect(handle,out var current);
  var width=Math.Max(1,current.Right-current.Left);var height=Math.Max(1,current.Bottom-current.Top);
  var work=target.WorkingArea;
  var availableWidth=Math.Max(0,work.Width-width);var availableHeight=Math.Max(0,work.Height-height);
  var absoluteLeft=saved?.Left??work.Right-width-24;
  var absoluteTop=saved?.Top??work.Top+24;
  var relativeX=saved is null?1:Math.Clamp(saved.RelativeX,0,1);
  var relativeY=saved is null?0:Math.Clamp(saved.RelativeY,0,1);
  var hasUsableRelative=saved is not null&&(relativeX>0.0001||relativeY>0.0001||Math.Abs(saved.Left-work.Left)<8);
  var x=hasUsableRelative?work.Left+(int)Math.Round(relativeX*availableWidth):Math.Clamp(absoluteLeft,work.Left,work.Left+availableWidth);
  var y=hasUsableRelative?work.Top+(int)Math.Round(relativeY*availableHeight):Math.Clamp(absoluteTop,work.Top,work.Top+availableHeight);
  x=Math.Clamp(x,work.Left,work.Left+availableWidth);y=Math.Clamp(y,work.Top,work.Top+availableHeight);
  MoveToScreen(x,y);
  return CapturePlacement(w);
 }
 static long IntersectionArea(System.Drawing.Rectangle a,System.Drawing.Rectangle b)
 {
  var intersection=System.Drawing.Rectangle.Intersect(a,b);
  return intersection.IsEmpty?0:(long)intersection.Width*intersection.Height;
 }
 static double Distance(System.Drawing.Rectangle a,System.Drawing.Rectangle b)
 {
  var dx=a.Left+a.Width/2.0-(b.Left+b.Width/2.0);var dy=a.Top+a.Height/2.0-(b.Top+b.Height/2.0);return dx*dx+dy*dy;
 }
 public void Dispose(){disposed=true;timer.Stop();foreach(var hook in hooks)UnhookWinEvent(hook);Record("shutdown");}
}



