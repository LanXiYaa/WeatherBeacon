using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Features.Settings;
using WeatherBeacon.Infrastructure.Weather;
using WeatherBeacon.Platform;
namespace WeatherBeacon.Controls;
public sealed record OnboardingChoices(bool Startup,WeatherLocation City,int Material,bool SystemInfoConsent);
public partial class OnboardingWizard : UserControl
{
 // Step indicator geometry: every node is a 44px circle; the completion ring is drawn on the node's own edge.
 private const double NodeSize=44,ActiveScale=1.08,TrackThickness=2;
 // SuccessStroke draws its ring with radius 47 inside a 120 box; size it so the ring centre line sits on the node border (radius 21.5).
 private static readonly double CheckSize=Math.Round(120*(NodeSize/2-.5)/47,1);
 private static readonly Color ActiveFill=Color.FromRgb(35,126,131),IdleFill=Color.FromRgb(24,48,64),ActiveEdge=Color.FromRgb(232,199,131),IdleEdge=Color.FromRgb(83,120,132),DoneEdge=Color.FromArgb(0,146,218,213);
 private static readonly Color ActiveLabel=Color.FromRgb(245,242,233),IdleLabel=Color.FromRgb(167,189,200);
 private readonly Border[] nodes=new Border[4];private readonly TextBlock[] numbers=new TextBlock[4];private readonly TextBlock[] labels=new TextBlock[4];private readonly SuccessStroke[] checks=new SuccessStroke[4];private readonly ScaleTransform[] links=new ScaleTransform[3];
 private readonly SolidColorBrush[] fills=new SolidColorBrush[4],edges=new SolidColorBrush[4],labelInks=new SolidColorBrush[4];
 private TaskCompletionSource<bool>? completion;private CancellationTokenSource? stop;private XiaomiWeatherProvider provider=null!;private Func<OnboardingChoices,string?> commit=null!;
 private WeatherLocation? city;private int step,shownStep=-1,visibility;private bool busy,searching;
 public OnboardingWizard(){InitializeComponent();BuildSteps();Unloaded+=(_,_)=>Cancel();}
 public int CurrentStep=>step;
 private static bool Animations=>SystemParameters.ClientAreaAnimation;
 private void BuildSteps()
 {
  for(int i=0;i<7;i++)Steps.ColumnDefinitions.Add(new ColumnDefinition{Width=i%2==0?new GridLength(90):new GridLength(1,GridUnitType.Star)});
  var names=new[]{"开机自启","城市","主题材质","信息授权"};
  for(int i=0;i<4;i++)
  {
   var stack=new StackPanel();Grid.SetColumn(stack,i*2);Steps.Children.Add(stack);
   fills[i]=new SolidColorBrush(IdleFill);edges[i]=new SolidColorBrush(IdleEdge);labelInks[i]=new SolidColorBrush(IdleLabel);
   numbers[i]=new TextBlock{Text=(i+1).ToString(),FontSize=16,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,0,1)};
   // The ring is wider than the node's content box; negative margins keep it centred on the node instead of shrinking it.
   double overhang=(CheckSize-(NodeSize-2))/2;
   checks[i]=new SuccessStroke{Width=CheckSize,Height=CheckSize,Margin=new Thickness(-overhang),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
   nodes[i]=new Border{Width=NodeSize,Height=NodeSize,CornerRadius=new CornerRadius(NodeSize/2),BorderThickness=new Thickness(1),Background=fills[i],BorderBrush=edges[i],HorizontalAlignment=HorizontalAlignment.Center,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(1,1),Child=new Grid{Children={numbers[i],checks[i]}}};
   labels[i]=new TextBlock{Text=names[i],HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,12,0,0),Foreground=labelInks[i],FontSize=12};
   stack.Children.Add(nodes[i]);stack.Children.Add(labels[i]);
   if(i<3)
   {
    // Track spans exactly from one node edge to the next and is centred on the node's vertical midpoint.
    double edge=(90-NodeSize)/2;
    var track=new Grid{Height=TrackThickness,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(-edge,(NodeSize-TrackThickness)/2,-edge,0),IsHitTestVisible=false};
    Grid.SetColumn(track,i*2+1);Panel.SetZIndex(track,-1);Steps.Children.Add(track);
    track.Children.Add(new Border{Background=new SolidColorBrush(Color.FromRgb(73,99,112)),CornerRadius=new CornerRadius(1)});
    links[i]=new ScaleTransform(0,1);track.Children.Add(new Border{Background=new SolidColorBrush(Color.FromRgb(146,218,213)),CornerRadius=new CornerRadius(1),RenderTransform=links[i]});
   }
  }
 }
 public Task<bool> ShowAsync(SettingsModel settings,XiaomiWeatherProvider weather,Func<OnboardingChoices,string?> save,CancellationToken lifetime)
 {
  if(completion is not null)return completion.Task;
  completion=new(TaskCreationOptions.RunContinuationsAsynchronously);stop=CancellationTokenSource.CreateLinkedTokenSource(lifetime);provider=weather;commit=save;city=settings.City;step=0;shownStep=-1;busy=false;searching=false;
  StartupChoice.IsChecked=StartupService.Read().Enabled;StartupHint.Text=StartupService.Read().Message;ThemeChoice.SelectedIndex=settings.Material;ConsentChoice.IsChecked=settings.SystemInfoConsent;CityResults.ItemsSource=null;CityQuery.Text="";ChosenCity.Text=city?.Name??"尚未选择城市";
  visibility++;IsHitTestVisible=true;Visibility=Visibility.Visible;UpdateSteps(false);PlayEntrance();Next.Focus();return completion.Task;
 }
 // One-time staged entrance: header, steps, card and footer arrive in sequence.
 private void PlayEntrance()
 {
  Shade.BeginAnimation(OpacityProperty,null);Shade.Opacity=1;
  var parts=new FrameworkElement[]{Header,Steps,Card,Footer};
  for(int i=0;i<parts.Length;i++)Motion.Enter(parts[i],0,12,TimeSpan.FromMilliseconds(80*i),300);
 }
 private void UpdateSteps(bool animate)
 {
  animate&=Animations;
  var pages=new FrameworkElement[]{StartupPage,CityPage,ThemePage,PrivacyPage};
  for(int i=0;i<4;i++)
  {
   pages[i].Visibility=i==step?Visibility.Visible:Visibility.Collapsed;
   bool done=i<step,active=i==step;
   Tint(fills[i],active?ActiveFill:IdleFill,animate);Tint(edges[i],done?DoneEdge:active?ActiveEdge:IdleEdge,animate);Tint(labelInks[i],active||done?ActiveLabel:IdleLabel,animate);
   numbers[i].Visibility=done?Visibility.Collapsed:Visibility.Visible;checks[i].Visibility=done?Visibility.Visible:Visibility.Collapsed;checks[i].BeginAnimation(SuccessStroke.ProgressProperty,null);checks[i].Progress=done?1:0;
   ScaleNode(i,active?ActiveScale:1,animate);
   if(i<3){double from=links[i].ScaleX,to=done?1:0;links[i].BeginAnimation(ScaleTransform.ScaleXProperty,null);links[i].ScaleX=to;if(animate&&from!=to)links[i].BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}
  }
  if(shownStep!=step)
  {
   int direction=shownStep<0?0:Math.Sign(step-shownStep);shownStep=step;
   var children=((Panel)pages[step]).Children;
   for(int k=0;k<children.Count;k++)
   {
    if(children[k] is not FrameworkElement part)continue;
    if(animate)Motion.Enter(part,16*direction,direction==0?8:0,TimeSpan.FromMilliseconds(60*Math.Min(k,3)),240);else Motion.Reset(part);
   }
  }
  Previous.IsEnabled=step>0&&!busy;Next.IsEnabled=!busy&&!searching;Next.Content=step==3?"完成设置":"下一步";Hint.Text=$"第 {step+1} 步，共 4 步";
 }
 private void ScaleNode(int i,double target,bool animate)
 {
  var scale=(ScaleTransform)nodes[i].RenderTransform;
  foreach(var property in new[]{ScaleTransform.ScaleXProperty,ScaleTransform.ScaleYProperty})
  {
   if(animate)scale.BeginAnimation(property,new DoubleAnimation(target,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
   else{scale.BeginAnimation(property,null);scale.SetValue(property,target);}
  }
 }
 private static void Tint(SolidColorBrush brush,Color target,bool animate)
 {
  if(animate)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(target,TimeSpan.FromMilliseconds(200)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
  else{brush.BeginAnimation(SolidColorBrush.ColorProperty,null);brush.Color=target;}
 }
 private async void Next_Click(object sender,RoutedEventArgs e)
 {
  if(busy||searching||completion is null)return;
  if(step==1&&city is null){Hint.Text="请搜索或定位后，选中一座城市。";return;}
  busy=true;Previous.IsEnabled=false;Next.IsEnabled=false;Pages.IsEnabled=false;var request=completion;var token=stop!.Token;
  try
  {
   // The node settles to its resting size and colour first, so the ring draws exactly on its edge instead of inside an enlarged, still-bordered circle.
   bool animate=Animations;int current=step;
   ScaleNode(current,1,animate);Tint(fills[current],IdleFill,animate);Tint(edges[current],DoneEdge,animate);
   numbers[current].Visibility=Visibility.Collapsed;checks[current].Visibility=Visibility.Visible;
   int duration=animate?650:0;
   checks[current].BeginAnimation(SuccessStroke.ProgressProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(duration)));
   if(current<3){links[current].BeginAnimation(ScaleTransform.ScaleXProperty,null);links[current].ScaleX=1;if(animate)links[current].BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(duration)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut}});}
   await Task.Delay(duration,token);token.ThrowIfCancellationRequested();
   if(step==3)
   {
    var error=commit(new(StartupChoice.IsChecked==true,city!,ThemeChoice.SelectedIndex,ConsentChoice.IsChecked==true));
    if(error is not null){busy=false;UpdateSteps(true);Hint.Text=error;return;}
    Finish(true);return;
   }
   step++;busy=false;UpdateSteps(true);Next.Focus();
  }
  catch(OperationCanceledException){}
  finally{if(ReferenceEquals(completion,request)){busy=false;Pages.IsEnabled=true;Previous.IsEnabled=step>0;Next.IsEnabled=!searching;}}
 }
 private void Previous_Click(object sender,RoutedEventArgs e){if(busy||step==0)return;step--;UpdateSteps(true);}
 private void City_Selected(object sender,SelectionChangedEventArgs e){if(CityResults.SelectedItem is WeatherLocation location){city=location;ChosenCity.Text=location.Name;Hint.Text="城市已选择，完成引导后刷新天气。";}}
 private async void Search_Click(object sender,RoutedEventArgs e)=>await FindCity(false);
 private async void Locate_Click(object sender,RoutedEventArgs e)=>await FindCity(true);
 private void CityQuery_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){e.Handled=true;_=FindCity(false);}}
 private async Task FindCity(bool locate)
 {
  if(searching||completion is null)return;var request=completion;var token=stop!.Token;searching=true;Search.IsEnabled=Locate.IsEnabled=Next.IsEnabled=false;Hint.Text=locate?"正在查询 IP 对应城市…":"正在搜索城市…";
  try{var result=locate?await provider.LocateAsync(token):await provider.SearchAsync(CityQuery.Text,token);if(!ReferenceEquals(request,completion))return;CityResults.ItemsSource=result;Hint.Text=CityResults.Items.Count>0?"请选择匹配的城市。":"未找到城市，请换一个名称或手动搜索。";}
  catch(OperationCanceledException){}
  catch(Exception ex){WeatherBeacon.Platform.AppLog.Warn("City",locate?"引导中 IP 定位失败":"引导中城市搜索失败",ex);if(ReferenceEquals(request,completion))Hint.Text="查询失败，请检查网络后重试；已选城市保持不变。";}
  finally{if(ReferenceEquals(request,completion)){searching=false;Search.IsEnabled=Locate.IsEnabled=Next.IsEnabled=true;}}
 }
 private void Cancel_Click(object sender,RoutedEventArgs e)=>Cancel();
 private void Wizard_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;Cancel();}}
 public void Cancel()=>Finish(false);
 private void Finish(bool result)
 {
  var pending=completion;if(pending is null)return;completion=null;stop?.Cancel();stop?.Dispose();stop=null;
  foreach(var check in checks)check.BeginAnimation(SuccessStroke.ProgressProperty,null);foreach(var link in links)link.BeginAnimation(ScaleTransform.ScaleXProperty,null);
  Pages.IsEnabled=true;Search.IsEnabled=Locate.IsEnabled=true;IsHitTestVisible=false;
  // Softer, shorter exit than the entrance; collapse only if the wizard was not reopened meanwhile.
  int token=visibility;
  if(Animations&&IsLoaded)
  {
   var fade=new DoubleAnimation(0,TimeSpan.FromMilliseconds(150)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
   fade.Completed+=(_,_)=>{if(token==visibility&&completion is null)Hide();};Shade.BeginAnimation(OpacityProperty,fade);
  }
  else Hide();
  pending.TrySetResult(result);
 }
 private void Hide(){Visibility=Visibility.Collapsed;IsHitTestVisible=true;Shade.BeginAnimation(OpacityProperty,null);Shade.Opacity=1;}
}
