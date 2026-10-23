using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WeatherBeacon.Features.Settings;
using WeatherBeacon.Infrastructure.Weather;
using Forms=System.Windows.Forms;
namespace WeatherBeacon.Features.Widget;
public partial class WidgetWindow : Window
{
 private readonly WidgetViewModel vm=new();
 private readonly XiaomiWeatherProvider provider=new();
 private readonly CancellationTokenSource lifetime=new();
 private readonly SettingsModel settings=SettingsModel.Load();
 private readonly DispatcherTimer clock=new(){Interval=TimeSpan.FromSeconds(10)};
 private readonly DispatcherTimer refresh=new(){Interval=TimeSpan.FromMinutes(15)};
 private readonly DispatcherTimer click=new(){Interval=TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime+30)};
 private readonly WeatherBeacon.Platform.DesktopHost desktop=new();
 private System.Drawing.Icon? trayIcon;private DateTime lastDoubleClick;private Forms.ToolStripMenuItem? visibilityMenu;private Forms.NotifyIcon? tray;private SettingsWindow? settingsWindow;private bool refreshing,started;
 public WidgetWindow()
 {
  InitializeComponent();DataContext=vm;ShowInTaskbar=false;
  SceneClip.SizeChanged+=(_,_)=>SceneClip.Clip=new RectangleGeometry(new Rect(0,0,SceneClip.ActualWidth,SceneClip.ActualHeight),settings.Radius,settings.Radius);
  var menu=new ContextMenu();var open=new MenuItem{Header="设置…"};open.Click+=(_,_)=>OpenSettings();menu.Items.Add(open);
  var retry=new MenuItem{Header="立即刷新"};retry.Click+=async (_,_)=>await Refresh();menu.Items.Add(retry);ContextMenu=menu;
  settings.PropertyChanged+=(_,e)=>{if(e.PropertyName!=nameof(SettingsModel.DesktopStatus))ApplyAppearance();};
  desktop.StatusChanged+=text=>settings.SetDesktopStatus(text);
  clock.Tick+=(_,_)=>{vm.AlignHours(DateTimeOffset.Now);};
  refresh.Tick+=async (_,_)=>await Refresh();
  click.Tick+=(_,_)=>{click.Stop();Toggle();};
  Loaded+=async (_,_)=>
  {
   if(started)return;started=true;ApplyAppearance();CreateTray();clock.Start();refresh.Start();desktop.SavedPosition=settings.Position;desktop.SavedHidden=settings.WidgetHidden;settings.WidgetHidden=false;desktop.UserHidden=false;desktop.StartOrdinary(this);settings.SetDesktopStatus(desktop.Status);ApplyAppearance();WindowState=WindowState.Normal;Show();Activate();desktop.VerifyStartup();if(visibilityMenu!=null)visibilityMenu.Text="隐藏小组件";
   if(settings.City is null)OpenSettings();else await Refresh();
  };
  Closed+=(_,_)=>{clock.Stop();refresh.Stop();click.Stop();lifetime.Cancel();desktop.Dispose();settingsWindow?.Close();tray?.Dispose();trayIcon?.Dispose();};
 }
 private void CreateTray()
 {
  trayIcon=WeatherBeacon.Platform.TrayArtwork.Create();
  tray=new Forms.NotifyIcon{Text="WeatherBeacon 1.0.0.0",Icon=trayIcon,Visible=true};
  var menu=new Forms.ContextMenuStrip();
  visibilityMenu=new Forms.ToolStripMenuItem(settings.WidgetHidden?"显示小组件":"隐藏小组件");visibilityMenu.Click+=(_,_)=>Dispatcher.Invoke(Toggle);menu.Items.Add(visibilityMenu);
  menu.Items.Add("打开设置",null,(_,_)=>Dispatcher.Invoke(OpenSettings));

  menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出",null,(_,_)=>Dispatcher.Invoke(()=>Application.Current.Shutdown()));
  tray.ContextMenuStrip=menu;
  tray.MouseClick+=(_,e)=>{if(e.Button==Forms.MouseButtons.Left)Dispatcher.Invoke(()=>{if((DateTime.UtcNow-lastDoubleClick).TotalMilliseconds>Forms.SystemInformation.DoubleClickTime){click.Stop();click.Start();}});};
  tray.MouseDoubleClick+=(_,e)=>{if(e.Button==Forms.MouseButtons.Left)Dispatcher.Invoke(()=>{lastDoubleClick=DateTime.UtcNow;click.Stop();OpenSettings();});};
 }
 private void Toggle()
 {
  settings.WidgetHidden=!settings.WidgetHidden;desktop.UserHidden=settings.WidgetHidden;
  if(settings.WidgetHidden)Hide();else{WindowState=WindowState.Normal;Show();ApplyAppearance();Activate();}
  if(visibilityMenu!=null)visibilityMenu.Text=settings.WidgetHidden?"显示小组件":"隐藏小组件";
  desktop.Record("user-toggle");
  try{settings.Save();}catch{MessageBox.Show("显示状态保存失败，请检查配置目录权限。","WeatherBeacon");}
 }
 private void OpenSettings()
 {
  if(settingsWindow is null){settingsWindow=new(settings,provider,async ()=>await Refresh());settingsWindow.Closed+=(_,_)=>settingsWindow=null;settingsWindow.Show();}
  else {settingsWindow.WindowState=WindowState.Normal;settingsWindow.Show();settingsWindow.Activate();}
 }
 private async Task Refresh()
 {
  if(settings.City is null || refreshing)return;refreshing=true;
  var city=settings.City;
  try{await vm.RefreshAsync(provider,city,lifetime.Token);HoursScroll.ScrollToHorizontalOffset(0);}finally{refreshing=false;}
  if(settings.City!=city)await Refresh();
 }
 private void ApplyAppearance()
 {
  Opacity=settings.Opacity;Shell.CornerRadius=new(settings.Radius);
  Scene.Animated=settings.Animations;SceneClip.Clip=new RectangleGeometry(new Rect(0,0,Width-2,Height-2),settings.Radius,settings.Radius);
  int material=Environment.OSVersion.Version.Build<22000?3:settings.Material;
  Shell.Background=material switch
  {
   0=>new LinearGradientBrush(Color.FromRgb(26,49,66),Color.FromRgb(12,24,39),45),
   1=>new LinearGradientBrush(Color.FromRgb(43,39,64),Color.FromRgb(15,31,44),55),
   2=>new LinearGradientBrush(Color.FromArgb(220,30,63,77),Color.FromArgb(235,20,33,51),70),
   _=>new SolidColorBrush(Color.FromRgb(30,46,64))
  };
  if(settings.Position<0||settings.Position>3)settings.Position=1;
  var work=SystemParameters.WorkArea;
  var x=settings.Position%2==0?work.Left+24:work.Right-Width-24;
  var y=settings.Position<2?work.Top+24:work.Bottom-Height-24;
  if(desktop.IsAttached)desktop.Position(this,x,y);else{Left=x;Top=y;}
 }
}






