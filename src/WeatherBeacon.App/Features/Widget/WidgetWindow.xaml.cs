using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
 private readonly WeatherBeacon.Features.Notifications.WeatherToastService notifications=new();
 private readonly WeatherBeacon.Features.Notifications.AlertDelivery alertDelivery=new(System.IO.Path.Combine(SettingsModel.Folder,"alert-delivery.json"));
 private WeatherBeacon.Features.Developer.WeatherOverride? weatherOverride;
 private readonly DispatcherTimer clock=new(){Interval=TimeSpan.FromSeconds(10)};
 private readonly DispatcherTimer refresh=new(){Interval=TimeSpan.FromMinutes(15)};
 private readonly DispatcherTimer occlusion=new(){Interval=TimeSpan.FromMilliseconds(450)};
 private readonly DispatcherTimer click=new(){Interval=TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime+30)};
 private readonly WeatherBeacon.Platform.DesktopHost desktop=new();
 private System.Drawing.Icon? trayIcon;private DateTime lastDoubleClick;private Forms.ToolStripMenuItem? visibilityMenu;private Forms.NotifyIcon? tray;private SettingsWindow? settingsWindow;
 private string? lastAttemptCity;private int failures;private DateTimeOffset lastAttempt=DateTimeOffset.MinValue;
 private bool refreshing,started,dragging;private System.Drawing.Point dragCursor;private System.Drawing.Rectangle dragBounds;

 public WidgetWindow()
 {
  InitializeComponent();
  DataContext=vm;vm.SetUnit(settings.Fahrenheit);
  notifications.Activated+=()=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(OpenSettings));};
  notifications.StatusChanged+=()=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>settings.SetNotificationStatus(notifications.DetailedStatus)));};
  notifications.Initialize();
  settings.PropertyChanged+=NewSettingsChanged;
  System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged+=NetworkChanged;
  ShowInTaskbar=false;
  Scene.RenderFailed+=(_,_)=>{Scene.Visibility=Visibility.Collapsed;Scene.Effect=null;desktop.AnimationStatus="failed-static-fallback";desktop.Record("animation-failed-static-fallback");};
  SceneClip.SizeChanged+=(_,_)=>UpdateClip();
  var menu=new ContextMenu();var open=new MenuItem{Header="设置…"};open.Click+=(_,_)=>OpenSettings();menu.Items.Add(open);
  var retry=new MenuItem{Header="立即刷新"};retry.Click+=async (_,_)=>await Refresh();menu.Items.Add(retry);ContextMenu=menu;
  settings.PropertyChanged+=(_,e)=>{if(e.PropertyName is not nameof(SettingsModel.DesktopStatus) and not nameof(SettingsModel.MaterialStatus))ApplyAppearance();if(e.PropertyName==nameof(SettingsModel.Fahrenheit))vm.SetUnit(settings.Fahrenheit);if(e.PropertyName==nameof(SettingsModel.RefreshMinutes))refresh.Interval=TimeSpan.FromMinutes(settings.RefreshMinutes);};
  desktop.StatusChanged+=text=>settings.SetDesktopStatus(text);
  desktop.DisplayChanged+=RestoreForDisplayChange;
  clock.Tick+=(_,_)=>{vm.AlignHours(DateTimeOffset.Now);UpdateDayNight();};
  refresh.Tick+=async (_,_)=>await Refresh();
  occlusion.Tick+=(_,_)=>UpdateAnimationActivity();
  click.Tick+=(_,_)=>{click.Stop();Toggle();};
  PreviewMouseLeftButtonDown+=BeginDrag;
  PreviewMouseMove+=ContinueDrag;
  PreviewMouseLeftButtonUp+=EndDrag;
  LostMouseCapture+=(_,_)=>CancelDrag();
  DpiChanged+=(_,_)=>SchedulePlacementClamp();
  Loaded+=OnLoaded;
  Closed+=OnClosed;
 }

 private async void OnLoaded(object sender,RoutedEventArgs e)
 {
  if(started)return;
  started=true;
  ApplyAppearance();
  CreateTray();
  if(settings.NotificationsEnabled||Environment.GetCommandLineArgs().Contains("-ToastActivated"))notifications.CheckAvailability();
  refresh.Interval=TimeSpan.FromMinutes(settings.RefreshMinutes);clock.Start();refresh.Start();occlusion.Start();
  desktop.SavedHidden=settings.WidgetHidden;
  settings.WidgetHidden=false;desktop.UserHidden=false;
  WindowState=WindowState.Normal;Show();Activate();
  desktop.SavedPlacement=settings.Placement;
  desktop.StartOrdinary(this);
  settings.Placement=desktop.ApplyPlacement(this,settings.Placement);
  settings.SetDesktopStatus(desktop.Status);
  ApplyAppearance();
  desktop.VerifyStartup();
  _=Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{desktop.AnimationFrames=Scene.RenderedFrames;desktop.AnimationStatus=Scene.RenderFailure is null?$"initialized; frames={Scene.RenderedFrames}; running={Scene.IsAnimating}":"failed-static-fallback";desktop.Record("animation-startup-check");}));
  if(visibilityMenu!=null)visibilityMenu.Text="隐藏小组件";
  if(Environment.GetCommandLineArgs().Contains("-ToastActivated"))OpenSettings();
  if(settings.City is null){if(!App.IsAutostart)OpenSettings();}else await Refresh();
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
  if(settings.WidgetHidden){Scene.SetWindowActive(false);Hide();}
  else
  {
   WindowState=WindowState.Normal;Show();ApplyAppearance();Activate();UpdateAnimationActivity();
   if(desktop.IsAttached)desktop.ApplyPlacement(this,settings.Placement);
  }
  if(visibilityMenu!=null)visibilityMenu.Text=settings.WidgetHidden?"显示小组件":"隐藏小组件";
  desktop.Record("user-toggle");
  try{settings.Save();}catch{MessageBox.Show("显示状态保存失败，请检查配置目录权限。","WeatherBeacon");}
 }

 private void OpenSettings()
 {
  if(settingsWindow is null)
  {
   settingsWindow=new(settings,provider,async ()=>await Refresh(),ResetPosition,notifications,SetWeatherOverride);
   settingsWindow.Closed+=(_,_)=>{settingsWindow=null;UpdateAnimationActivity();};
   settingsWindow.Show();
  }
  else{settingsWindow.WindowState=WindowState.Normal;settingsWindow.Show();settingsWindow.Activate();}
 }

 private async Task Refresh()
 {
  if(settings.City is null || refreshing)return;
  if(lastAttemptCity==settings.City.ProviderCityId && DateTimeOffset.UtcNow-lastAttempt<TimeSpan.FromSeconds(15)){refresh.Interval=TimeSpan.FromSeconds(15);return;}
  lastAttemptCity=settings.City.ProviderCityId;lastAttempt=DateTimeOffset.UtcNow;refreshing=true;var city=settings.City;
  try
  {
   await vm.RefreshAsync(provider,city,lifetime.Token);HoursScroll.ScrollToHorizontalOffset(0);UpdateDayNight();
   if(vm.LastRefreshSucceeded&&vm.LastSnapshot is {} snapshot&&settings.City==city)
   {
    settings.SetAlertDataStatus(snapshot.AlertStatus);
    alertDelivery.Process(city.ProviderCityId,snapshot,settings.NotificationsEnabled,(WeatherBeacon.Core.Models.AlertLevel)settings.AlertThreshold,a=>notifications.Send(a,city.Name,false,city.ProviderCityId),slot=>notifications.Remove(city.ProviderCityId,slot));
    settings.SetDeliveryStatus(alertDelivery.Status);
   }
   else if(!vm.LastRefreshSucceeded)settings.SetAlertDataStatus("天气刷新失败；无法确认最新预警，保留去重记录等待重试");
  }
  finally{refreshing=false;failures=vm.LastRefreshSucceeded?0:Math.Min(failures+1,5);refresh.Stop();refresh.Interval=vm.LastRefreshSucceeded?TimeSpan.FromMinutes(settings.RefreshMinutes):TimeSpan.FromSeconds(Math.Min(300,30*Math.Pow(2,failures-1)));refresh.Start();}
  if(settings.City!=city)await Refresh();
 }

 private void ApplyAppearance()
 {
  Opacity=settings.Opacity;
  Shell.CornerRadius=new CornerRadius(settings.Radius);
  Scene.Animated=settings.Animations;
  UpdateClip();UpdateDayNight();UpdateCursor();
  UpdateAnimationActivity();
 }

 private void UpdateClip()
 {
  var width=Math.Max(0,SceneClip.ActualWidth);var height=Math.Max(0,SceneClip.ActualHeight);
  SceneClip.Clip=new RectangleGeometry(new Rect(0,0,width,height),settings.Radius,settings.Radius);
 }

 private void UpdateDayNight()
 {
  var now=DateTime.Now.TimeOfDay;var daytime=now>=TimeSpan.FromHours(6.5)&&now<TimeSpan.FromHours(18.5);
  Scene.IsDaytime=weatherOverride?.Daytime??daytime;CurrentWeatherIcon.IsDaytime=weatherOverride?.Daytime??daytime;
 }

 private void UpdateAnimationActivity()
 {
  var active=!settings.WidgetHidden&&IsVisible&&desktop.IsWindowAvailable&&!desktop.IsCompletelyCovered();
  Scene.SetWindowActive(active);
 }

 private void UpdateCursor()
 {
  Cursor=settings.LockPosition?Cursors.Arrow:Cursors.SizeAll;
 }

 private void BeginDrag(object sender,MouseButtonEventArgs e)
 {
  if(e.LeftButton!=MouseButtonState.Pressed||settings.LockPosition)return;
  dragging=true;dragCursor=desktop.CursorPosition;dragBounds=desktop.WindowBounds;CaptureMouse();Cursor=Cursors.SizeAll;e.Handled=true;
 }
 private void ContinueDrag(object sender,MouseEventArgs e)
 {
  if(!dragging||e.LeftButton!=MouseButtonState.Pressed||settings.LockPosition)return;
  var cursor=desktop.CursorPosition;
  desktop.MoveToScreen(dragBounds.Left+cursor.X-dragCursor.X,dragBounds.Top+cursor.Y-dragCursor.Y);
  e.Handled=true;
 }
 private void EndDrag(object sender,MouseButtonEventArgs e)
 {
  if(!dragging)return;
  dragging=false;ReleaseMouseCapture();UpdateCursor();e.Handled=true;
  try
  {
   settings.Placement=desktop.CapturePlacement(this);settings.Save();
   settings.SetDesktopStatus($"位置已保存 · {desktop.Status}");
  }
  catch{settings.SetDesktopStatus("位置已移动，但保存失败，请检查配置目录权限");}
  SchedulePlacementClamp();
 }
 private void CancelDrag()
 {
  if(!dragging)return;
  dragging=false;UpdateCursor();
 }

 private void ResetPosition()
 {
  try
  {
   settings.Placement=desktop.ApplyPlacement(this,null);settings.Save();
   settings.SetDesktopStatus($"位置已重置 · {desktop.Status}");
  }
  catch{settings.SetDesktopStatus("无法重置位置");}
 }

 private void RestoreForDisplayChange()
 {
  try
  {
   settings.Placement=desktop.ApplyPlacement(this,settings.Placement);settings.Save();
   settings.SetDesktopStatus($"显示器变化，位置已重新约束 · {desktop.Status}");
  }
  catch{settings.SetDesktopStatus("显示器变化后无法恢复位置");}
  UpdateAnimationActivity();
 }
 private void SchedulePlacementClamp()
 {
  Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
  {
   if(!started||dragging)return;
   try
   {
    var current=desktop.CapturePlacement(this);
    settings.Placement=desktop.ApplyPlacement(this,current);settings.Save();
   }
   catch{settings.SetDesktopStatus("DPI 变化后无法重新约束位置");}
  }));
 }

 public void ShowFromSecondInstance(){settings.WidgetHidden=false;desktop.UserHidden=false;WindowState=WindowState.Normal;Show();Activate();if(visibilityMenu!=null)visibilityMenu.Text="隐藏小组件";UpdateAnimationActivity();}
 private void SetWeatherOverride(WeatherBeacon.Features.Developer.WeatherOverride? value)
 {
  weatherOverride=settings.DeveloperMode?value:null;vm.SetOverride(weatherOverride);Scene.RainIntensity=weatherOverride?.RainIntensity??1;UpdateDayNight();UpdateAnimationActivity();
 }
 private void NewSettingsChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
 {
  if(e.PropertyName==nameof(SettingsModel.DeveloperMode)&&!settings.DeveloperMode)SetWeatherOverride(null);
  if(e.PropertyName is nameof(SettingsModel.NotificationsEnabled) or nameof(SettingsModel.AlertThreshold) or nameof(SettingsModel.DeveloperMode))
  {
   try{settings.Save();}catch{settings.SetNotificationStatus("新设置保存失败，请检查配置目录权限");}
   if(e.PropertyName==nameof(SettingsModel.NotificationsEnabled)&&settings.NotificationsEnabled){notifications.CheckAvailability();_=Refresh();}
  }
 }
 private void NetworkChanged(object? sender,System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
 {if(e.IsAvailable)Dispatcher.BeginInvoke(new Action(()=>{refresh.Stop();refresh.Interval=TimeSpan.FromSeconds(15);refresh.Start();}));}
 private void OnClosed(object? sender,EventArgs e)
 {
  System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged-=NetworkChanged;
  clock.Stop();refresh.Stop();occlusion.Stop();click.Stop();lifetime.Cancel();desktop.Dispose();settingsWindow?.Close();tray?.Dispose();trayIcon?.Dispose();
 }
}










