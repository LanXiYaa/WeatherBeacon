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
  var id=WindowsIdentity.GetCurrent().User!.Value;
  mutex=new Mutex(true,@"Local\WeatherBeacon-"+id,out var first);
  if(!first){for(int i=0;i<20;i++){try{using var existing=EventWaitHandle.OpenExisting(@"Local\WeatherBeacon-Show-"+id);existing.Set();break;}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(50);}}Shutdown();return;}
  signal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\WeatherBeacon-Show-"+id);
  try{Platform.StartupService.RepairPath();}catch{}
  var widget=new WidgetWindow();MainWindow=widget;widget.Show();
  _=Task.Run(()=>{while(!stop.IsCancellationRequested){if(signal.WaitOne(500)&&!stop.IsCancellationRequested)Dispatcher.BeginInvoke(new Action(widget.ShowFromSecondInstance));}});
 }
 protected override void OnExit(ExitEventArgs e){stop.Cancel();signal?.Set();mutex?.Dispose();base.OnExit(e);}
}

