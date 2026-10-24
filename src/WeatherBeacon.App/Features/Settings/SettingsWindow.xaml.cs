using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Infrastructure.Weather;
namespace WeatherBeacon.Features.Settings;
public partial class SettingsWindow : Window
{
 private readonly SettingsModel settings;private readonly XiaomiWeatherProvider provider;private readonly Action changed;private readonly Action resetPosition;
 private readonly System.Windows.Threading.DispatcherTimer startupPoll=new(){Interval=TimeSpan.FromSeconds(2)};
 private readonly CancellationTokenSource lifetime=new();
 private readonly WeatherBeacon.Features.Notifications.WeatherToastService notifications;
 private readonly Action<WeatherBeacon.Features.Developer.WeatherOverride?> setOverride;
 private bool ready;private DateTimeOffset lastDebugToast=DateTimeOffset.MinValue;
 public SettingsWindow(SettingsModel s,XiaomiWeatherProvider p,Action cityChanged,Action resetPosition,WeatherBeacon.Features.Notifications.WeatherToastService? notifications=null,Action<WeatherBeacon.Features.Developer.WeatherOverride?>? setOverride=null)
 {
  InitializeComponent();settings=s;provider=p;changed=cityChanged;this.resetPosition=resetPosition;DataContext=s;CurrentCity.Text=s.City?.Name??"尚未选择城市";
  this.notifications=notifications??new();this.setOverride=setOverride??(_=>{});
  DebugWeather.ItemsSource=WeatherBeacon.Features.Developer.WeatherPreset.All;DebugWeather.SelectedIndex=0;DebugDaytime.SelectedIndex=0;
  DebugAlertType.ItemsSource=new[]{"暴雨","台风","雷电","高温","寒潮","大风","暴雪","大雾"};DebugAlertType.SelectedIndex=0;
  UpdateDeveloperNavigation();ready=true;
  RefreshOptions.ItemsSource=new[]{5,10,15,30,60};RefreshStartup();startupPoll.Tick+=(_,_)=>{RefreshStartup();if(settings.NotificationsEnabled){this.notifications.CheckAvailability();settings.SetNotificationStatus(this.notifications.DetailedStatus);}};startupPoll.Start();
  SourceInitialized+=(_,_)=>ApplyBackdrop();
  Loaded+=(_,_)=>ShowPage(Navigation.SelectedIndex,false);
  settings.PropertyChanged+=SettingsChanged;
  Closed+=(_,_)=>{startupPoll.Stop();lifetime.Cancel();settings.PropertyChanged-=SettingsChanged;};
 }
 private void RefreshStartup(){var state=WeatherBeacon.Platform.StartupService.Read();StartupToggle.IsChecked=state.Enabled;StartupStatus.Text=state.Message;}
 private void Startup_Click(object sender,RoutedEventArgs e)
 {try{WeatherBeacon.Platform.StartupService.Set(StartupToggle.IsChecked==true);}catch(Exception ex){Status.Text=ex.Message;}RefreshStartup();}
 private async void Locate_Click(object sender,RoutedEventArgs e)
 {
  LocateButton.IsEnabled=false;Status.Text="正在查询 IP 对应城市…";
  try{Results.ItemsSource=await provider.LocateAsync(lifetime.Token);Status.Text=Results.Items.Count>0?"定位完成，请确认并使用选中城市。":"未匹配到城市，请手动搜索。";if(Results.Items.Count>0)Results.SelectedIndex=0;}
  catch{Status.Text="IP 定位失败，请稍后重试或手动搜索。";}finally{LocateButton.IsEnabled=true;}
 }
 private void OpenDiagnostics(object sender,RoutedEventArgs e)
 {
  try{System.IO.Directory.CreateDirectory(WeatherBeacon.Platform.DesktopHost.LogFolder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WeatherBeacon.Platform.DesktopHost.LogFolder){UseShellExecute=true});}
  catch{Status.Text="无法打开诊断日志文件夹。";}
 }
 private void Navigate(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
 {
  if(AppearancePage is null || WeatherPage is null || AboutPage is null || NotificationsPage is null || DeveloperPage is null)return;
  ShowPage(Navigation.SelectedIndex,true);
 }
 private void ShowPage(int index,bool animate)
 {
  var pages=new FrameworkElement[]{AppearancePage,WeatherPage,AboutPage,NotificationsPage,DeveloperPage};
  if(index<0||index>=pages.Length)return;
  var target=pages[index];
  foreach(var page in pages)
  {
   page.BeginAnimation(OpacityProperty,null);
   page.Visibility=PageEquals(page,target)?Visibility.Visible:Visibility.Collapsed;
   page.Opacity=1;page.IsHitTestVisible=page==target;
   if(page.RenderTransform is TranslateTransform transform)transform.Y=0;else page.RenderTransform=new TranslateTransform();
  }
  if(!animate)return;
  target.Opacity=0;
  var enter=new TranslateTransform(0,8);target.RenderTransform=enter;
  var easing=new CubicEase{EasingMode=EasingMode.EaseOut};
  target.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(250)){EasingFunction=easing});
  enter.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(8,0,TimeSpan.FromMilliseconds(250)){EasingFunction=easing});
 }
 static bool PageEquals(FrameworkElement left,FrameworkElement right)=>ReferenceEquals(left,right);
 private void SettingsChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
 {
  if(e.PropertyName==nameof(SettingsModel.Material))ApplyBackdrop();
  if(e.PropertyName==nameof(SettingsModel.DeveloperMode))UpdateDeveloperNavigation();
  if(e.PropertyName==nameof(SettingsModel.NotificationsEnabled)&&!settings.NotificationsEnabled)settings.SetNotificationStatus("通知已关闭，真实和调试预警均不发送");
 }
 private void ApplyBackdrop()
 {
  settings.Material=Math.Clamp(settings.Material,0,3);
  var result=WeatherBeacon.Platform.SettingsBackdrop.Apply(this,settings.Material);
  settings.SetMaterialStatus(result.Description);
 }
 private void ResetPosition_Click(object sender,RoutedEventArgs e)
 {
  resetPosition();Status.Text="小组件位置已重置到主显示器右上。";
 }
 private async void Search_Click(object sender,RoutedEventArgs e)
 {
  SearchButton.IsEnabled=false;Status.Text="正在搜索…";
  try{Results.ItemsSource=await provider.SearchAsync(Query.Text,lifetime.Token);Status.Text=Results.Items.Count==0?"未找到城市，请换一个名称。":"请选择匹配的城市。";}
  catch{Status.Text="搜索失败，请检查网络后重试。";}finally{SearchButton.IsEnabled=true;}
 }
 private void SelectCity_Click(object sender,RoutedEventArgs e)
 {
  if(Results.SelectedItem is not WeatherLocation city){Status.Text="请先选择城市。";return;}
  settings.City=city;CurrentCity.Text=city.Name;changed();Save_Click(sender,e);
 }
 private void Save_Click(object sender,RoutedEventArgs e)
 {try{settings.Save();Status.Text="设置已保存。";}catch{Status.Text="保存失败，请检查本地目录权限。";}}
 private void UpdateDeveloperNavigation()
 {
  DeveloperToggle.IsChecked=settings.DeveloperMode;
  if(!settings.DeveloperMode&&Navigation.SelectedIndex==4)Navigation.SelectedIndex=2;
  DeveloperNavigation.Visibility=settings.DeveloperMode?Visibility.Visible:Visibility.Collapsed;
 }
 private void DeveloperToggle_Click(object sender,RoutedEventArgs e)
 {
  var enable=DeveloperToggle.IsChecked==true;
  if(enable&&MessageBox.Show(this,"非专业人士最好不要打开该选项\n\n开启后可以覆盖天气显示和发送调试通知。是否继续？","开发者模式",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)
  {DeveloperToggle.IsChecked=false;return;}
  settings.DeveloperMode=enable;if(!enable)setOverride(null);UpdateDeveloperNavigation();
 }
 private void DebugWeather_Changed(object sender,SelectionChangedEventArgs e){if(ready)ApplyDebugWeather();}
 private void ApplyDebugWeather_Click(object sender,RoutedEventArgs e)=>ApplyDebugWeather();
 private void ApplyDebugWeather()
 {
  if(!settings.DeveloperMode||DebugWeather.SelectedItem is not WeatherBeacon.Features.Developer.WeatherPreset preset||DebugDaytime.SelectedIndex<0)return;
  setOverride(new(preset.Name,preset.Kind,DebugDaytime.SelectedIndex==0,preset.RainIntensity));Status.Text="天气预览中；温度与预报仍为真实数据。";
 }
 private void RestoreWeather_Click(object sender,RoutedEventArgs e){setOverride(null);Status.Text="已恢复真实天气。";}
 private void DebugAlert_Click(object sender,RoutedEventArgs e)
 {
  if(!settings.DeveloperMode)return;
  if(!settings.NotificationsEnabled){Status.Text="请先在通知页开启通知总开关。";return;}
  if(DateTimeOffset.UtcNow-lastDebugToast<TimeSpan.FromSeconds(2)){Status.Text="请稍候再发送测试通知。";return;}
  lastDebugToast=DateTimeOffset.UtcNow;
  var level=(AlertLevel)int.Parse(((Button)sender).Tag.ToString()!);var type=DebugAlertType.SelectedItem?.ToString()??"暴雨";
  var alert=new WeatherAlert(Guid.NewGuid().ToString(),type+"调试预警","这是 WeatherBeacon 功能测试，不是真实气象预警。",level,null,type,DateTimeOffset.Now);
  notifications.Send(alert,settings.City?.Name??"测试城市",true);settings.SetNotificationStatus(notifications.DetailedStatus);Status.Text=notifications.Status;
 }
 private void OpenNotificationSettings_Click(object sender,RoutedEventArgs e)
 {
  try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:notifications"){UseShellExecute=true});}catch{Status.Text="无法打开系统设置，请手动进入 Windows 设置 → 系统 → 通知。";}
 }
}




