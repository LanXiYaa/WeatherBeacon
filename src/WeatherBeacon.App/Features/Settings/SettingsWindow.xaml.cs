using System.Windows;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Infrastructure.Weather;
namespace WeatherBeacon.Features.Settings;
public partial class SettingsWindow : Window
{
 private readonly SettingsModel settings;private readonly XiaomiWeatherProvider provider;private readonly Action changed;
 private readonly CancellationTokenSource lifetime=new();
 public SettingsWindow(SettingsModel s,XiaomiWeatherProvider p,Action cityChanged)
 {
  InitializeComponent();settings=s;provider=p;changed=cityChanged;DataContext=s;CurrentCity.Text=s.City?.Name??"尚未选择城市";
  SourceInitialized+=(_,_)=>WeatherBeacon.Platform.SettingsBackdrop.Apply(this,settings.Material);
  settings.PropertyChanged+=SettingsChanged;
  Closed+=(_,_)=>{lifetime.Cancel();settings.PropertyChanged-=SettingsChanged;};
 }
 private void OpenDiagnostics(object sender,RoutedEventArgs e)
 {
  try{System.IO.Directory.CreateDirectory(WeatherBeacon.Platform.DesktopHost.LogFolder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WeatherBeacon.Platform.DesktopHost.LogFolder){UseShellExecute=true});}
  catch{Status.Text="无法打开诊断日志文件夹。";}
 }
 private void Navigate(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
 {
  if(AppearancePage is null || WeatherPage is null || AboutPage is null)return;
  AppearancePage.Visibility=Navigation.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;
  WeatherPage.Visibility=Navigation.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;
  AboutPage.Visibility=Navigation.SelectedIndex==2?Visibility.Visible:Visibility.Collapsed;
 }
 private void SettingsChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName==nameof(SettingsModel.Material))WeatherBeacon.Platform.SettingsBackdrop.Apply(this,settings.Material); }
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
}



