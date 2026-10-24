using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WeatherBeacon.Controls;
using WeatherBeacon.Features.Settings;
using WeatherBeacon.Infrastructure.Weather;
internal static class Program
{
 [STAThread] static void Main(string[] args)
 {
  _=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
  var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
  var handler=new Cities();var provider=new XiaomiWeatherProvider(new HttpClient(handler));var model=new SettingsModel();
  var w=new SettingsWindow(model,provider,()=>{},()=>{});w.Show();Pump(300);
  var wizard=(OnboardingWizard)w.FindName("Wizard");int commits=0;bool fail=true;OnboardingChoices? saved=null;
  var task=wizard.ShowAsync(model,provider,c=>{commits++;if(fail)return "模拟保存失败，请重试。";saved=c;return null;},CancellationToken.None);
  Check(wizard.CurrentStep==0,"first step");Capture(w,"startup");Click("Next");Click("Next");Pump(250);Capture(w,"connector-mid");Pump(500);Check(wizard.CurrentStep==1,"double next advances once");
  Click("Next");Pump(50);Check(wizard.CurrentStep==1,"city required");
  ((TextBox)wizard.FindName("CityQuery")).Text="兰州";Click("Search");Pump(100);
  var list=(ListBox)wizard.FindName("CityResults");Check(list.Items.Count==1,"search results");list.SelectedIndex=0;Capture(w,"city");
  Click("Next");Pump(750);((ComboBox)wizard.FindName("ThemeChoice")).SelectedIndex=2;Capture(w,"theme");Click("Previous");Pump(80);Check(wizard.CurrentStep==1&&((TextBlock)wizard.FindName("ChosenCity")).Text.Contains("兰州"),"back retains city");
  Click("Next");Pump(750);Check(((ComboBox)wizard.FindName("ThemeChoice")).SelectedIndex==2,"back retains theme");Click("Next");Pump(750);
  Check(((ToggleButton)wizard.FindName("ConsentChoice")).IsChecked==false,"consent default off");Capture(w,"privacy");
  Check(model.City is null&&!model.OnboardingCompleted&&!model.SystemInfoConsent,"draft has no side effects");
  Click("Next");Pump(800);Check(commits==1&&!task.IsCompleted&&wizard.CurrentStep==3,"save failure stays in wizard");
  fail=false;Click("Next");Pump(800);Check(task.IsCompletedSuccessfully&&task.Result&&saved is {Material:2,SystemInfoConsent:false},"retry commits exact choices");
  var cancel=wizard.ShowAsync(model,provider,_=>throw new Exception("must not save cancelled draft"),CancellationToken.None);Click("Next");wizard.Cancel();Pump(750);Check(cancel.IsCompletedSuccessfully&&!cancel.Result,"cancel during transition");
  var roundTrip=System.Text.Json.JsonSerializer.Deserialize<SettingsModel>(System.Text.Json.JsonSerializer.Serialize(new SettingsModel{OnboardingCompleted=true,SystemInfoConsent=true}));Check(roundTrip!.OnboardingCompleted&&roundTrip.SystemInfoConsent,"consent serialization");
  var replay=wizard.ShowAsync(new SettingsModel{City=saved!.City,SystemInfoConsent=true,Material=2},provider,_=>null,CancellationToken.None);Check(((ToggleButton)wizard.FindName("ConsentChoice")).IsChecked==true,"replay loads previous consent");wizard.Cancel();
  model.DeveloperMode=true;((ListBox)w.FindName("Navigation")).SelectedIndex=4;Pump(300);var replayButton=(Button)w.FindName("RestartOnboarding");replayButton.ApplyTemplate();
  var key=(DependencyPropertyKey)typeof(ButtonBase).GetField("IsPressedPropertyKey",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
  replayButton.SetValue(key,true);Pump(120);
  var scale=(ScaleTransform)((Grid)replayButton.Template.FindName("Surface",replayButton)).RenderTransform;var depth=(Border)replayButton.Template.FindName("InnerDepth",replayButton);
  Console.WriteLine($"PRESS pressed={replayButton.IsPressed} scale={scale.ScaleX} depth={depth.Opacity} animations={SystemParameters.ClientAreaAnimation}");if(SystemParameters.ClientAreaAnimation)Check(Math.Abs(scale.ScaleX-.96)<.002,"button press scale");Check(depth.Opacity==1,"button inner depth");
  replayButton.SetValue(key,false);Pump(80);if(SystemParameters.ClientAreaAnimation)Check(scale.ScaleX>.96&&scale.ScaleX<=1,"button release eases back without overshoot");Pump(200);Check(Math.Abs(scale.ScaleX-1)<.001,"button release settles");
  replayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(80);Check(wizard.IsVisible,"developer replay button wired");wizard.Cancel();Pump(50);
  w.Close();Console.WriteLine("PASS wizard: steps, duplicate guard, city validation/search, back preserves drafts, consent default/serialization, save failure/retry, cancel transition, developer replay.");
  void Click(string name)=>((Button)wizard.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  void Capture(Window window,string name){var c=(FrameworkElement)window.Content;c.UpdateLayout();var b=new RenderTargetBitmap((int)c.ActualWidth,(int)c.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(c);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(output,name+".png"));e.Save(f);}
 }
 static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
 static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 sealed class Cities:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("[{\"name\":\"兰州\",\"affiliation\":\"甘肃\",\"locationKey\":\"test\",\"latitude\":36,\"longitude\":103}]")});}
}
