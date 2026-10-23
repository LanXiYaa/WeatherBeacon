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
 private readonly DispatcherTimer clock=new(){Interval=TimeSpan.FromSeconds(10)};
 private readonly DispatcherTimer refresh=new(){Interval=TimeSpan.FromMinutes(15)};
 private readonly DispatcherTimer occlusion=new(){Interval=TimeSpan.FromMilliseconds(450)};
 private readonly DispatcherTimer click=new(){Interval=TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime+30)};
 private readonly WeatherBeacon.Platform.DesktopHost desktop=new();
 private System.Drawing.Icon? trayIcon;private DateTime lastDoubleClick;private Forms.ToolStripMenuItem? visibilityMenu;private Forms.NotifyIcon? tray;private SettingsWindow? settingsWindow;
 private bool refreshing,started,dragging;private System.Drawing.Point dragCursor;private System.Drawing.Rectangle dragBounds;

 public WidgetWindow()
 {
  InitializeComponent();
  DataContext=vm;
  ShowInTaskbar=false;
  Scene.RenderFailed+=(_,_)=>{Scene.Visibility=Visibility.Collapsed;Scene.Effect=null;desktop.AnimationStatus="failed-static-fallback";desktop.Record("animation-failed-static-fallback");};
  SceneClip.SizeChanged+=(_,_)=>UpdateClip();
  var menu=new ContextMenu();var open=new MenuItem{Header="设置…"};open.Click+=(_,_)=>OpenSettings();menu.Items.Add(open);
  var retry=new MenuItem{Header="立即刷新"};retry.Click+=async (_,_)=>await Refresh();menu.Items.Add(retry);ContextMenu=menu;
  settings.PropertyChanged+=(_,e)=>{if(e.PropertyName is not nameof(SettingsModel.DesktopStatus) and not nameof(SettingsModel.MaterialStatus))ApplyAppearance();};
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
  clock.Start();refresh.Start();occlusion.Start();
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
  if(settings.City is null)OpenSettings();else await Refresh();
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
   settingsWindow=new(settings,provider,async ()=>await Refresh(),ResetPosition);
   settingsWindow.Closed+=(_,_)=>{settingsWindow=null;UpdateAnimationActivity();};
   settingsWindow.Show();
  }
  else{settingsWindow.WindowState=WindowState.Normal;settingsWindow.Show();settingsWindow.Activate();}
 }

 private async Task Refresh()
 {
  if(settings.City is null || refreshing)return;
  refreshing=true;var city=settings.City;
  try{await vm.RefreshAsync(provider,city,lifetime.Token);HoursScroll.ScrollToHorizontalOffset(0);UpdateDayNight();}
  finally{refreshing=false;}
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
  Scene.IsDaytime=daytime;CurrentWeatherIcon.IsDaytime=daytime;
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

 private void OnClosed(object? sender,EventArgs e)
 {
  clock.Stop();refresh.Stop();occlusion.Stop();click.Stop();lifetime.Cancel();desktop.Dispose();settingsWindow?.Close();tray?.Dispose();trayIcon?.Dispose();
 }
}








