using System.Windows;
using System.Security.Principal;
using WeatherBeacon.Features.Widget;
namespace WeatherBeacon;
public partial class App : Application
{
 private Mutex? mutex;private EventWaitHandle? signal;private readonly CancellationTokenSource stop=new();
 public static bool IsAutostart{get;private set;}
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);IsAutostart=e.Args.Contains("--autostart");
  HookErrors();
  var id=WindowsIdentity.GetCurrent().User!.Value;
  mutex=new Mutex(true,@"Local\WeatherBeacon-"+id,out var first);
  if(!first){for(int i=0;i<20;i++){try{using var existing=EventWaitHandle.OpenExisting(@"Local\WeatherBeacon-Show-"+id);existing.Set();break;}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(50);}}Shutdown();return;}
  signal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\WeatherBeacon-Show-"+id);
  Platform.AppLog.Prune();
  Platform.AppLog.Info("App",$"启动 WeatherBeacon {Platform.Diagnostics.AppVersion} · {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} · {(IsAutostart?"开机自启":"手动启动")}");
  try{Platform.StartupService.RepairPath();}catch{}
  var widget=new WidgetWindow();MainWindow=widget;widget.Show();
  _=Task.Run(()=>{while(!stop.IsCancellationRequested){if(signal.WaitOne(500)&&!stop.IsCancellationRequested)Dispatcher.BeginInvoke(new Action(widget.ShowFromSecondInstance));}});
 }
 private void HookErrors()
 {
  // Record crashes before Windows closes the process; they are the main reason anyone opens the logs.
  DispatcherUnhandledException+=(_,args)=>Platform.AppLog.Error("Crash","界面线程未处理异常",args.Exception);
  AppDomain.CurrentDomain.UnhandledException+=(_,args)=>Platform.AppLog.Error("Crash",$"未处理异常（即将退出：{args.IsTerminating}）",args.ExceptionObject as Exception);
  TaskScheduler.UnobservedTaskException+=(_,args)=>{Platform.AppLog.Warn("Crash","后台任务异常未被观察",args.Exception);args.SetObserved();};
 }
 protected override void OnExit(ExitEventArgs e){Platform.AppLog.Info("App",$"退出，代码 {e.ApplicationExitCode}");stop.Cancel();signal?.Set();mutex?.Dispose();base.OnExit(e);}
}

