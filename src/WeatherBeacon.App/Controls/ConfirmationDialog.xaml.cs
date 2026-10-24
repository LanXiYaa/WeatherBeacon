using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace WeatherBeacon.Controls;
public partial class ConfirmationDialog : UserControl
{
 private TaskCompletionSource<bool>? completion;
 private CancellationTokenSource? animation;
 public ConfirmationDialog(){InitializeComponent();Unloaded+=(_,_)=>Cancel();}
 public Task<bool> ShowAsync(CancellationToken lifetime)
 {
  if(completion is not null)return completion.Task;
  completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
  animation=CancellationTokenSource.CreateLinkedTokenSource(lifetime);
  Visibility=Visibility.Visible;Question.Visibility=Visibility.Visible;Success.Visibility=Visibility.Collapsed;
  Stroke.BeginAnimation(SuccessStroke.ProgressProperty,null);Stroke.Progress=0;SuccessLabel.Text="正在确认…";
  if(SystemParameters.ClientAreaAnimation)
  {
   var enter=new DoubleAnimation(.96,1,TimeSpan.FromMilliseconds(280)){EasingFunction=new BackEase{Amplitude=.25,EasingMode=EasingMode.EaseOut}};
   CardScale.BeginAnimation(ScaleTransform.ScaleXProperty,enter);CardScale.BeginAnimation(ScaleTransform.ScaleYProperty,enter);
  }
  CancelButton.Focus();return completion.Task;
 }
 private async void Confirm_Click(object sender,RoutedEventArgs e)
 {
  if(completion is null||Success.Visibility==Visibility.Visible)return;
  Question.Visibility=Visibility.Collapsed;Success.Visibility=Visibility.Visible;Focus();
  var request=completion;var token=animation!.Token;
  try
  {
   if(SystemParameters.ClientAreaAnimation)
   {
    Stroke.BeginAnimation(SuccessStroke.ProgressProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(1100)));
    await Task.Delay(1100,token);
   }
   else Stroke.Progress=1;
   SuccessLabel.Text="确认完成";
   await Task.Delay(SystemParameters.ClientAreaAnimation?200:80,token);
   if(ReferenceEquals(completion,request))Finish(true);
  }
  catch(OperationCanceledException){if(ReferenceEquals(completion,request))Finish(false); }
 }
 private void Cancel_Click(object sender,RoutedEventArgs e)=>Cancel();
 private void OnKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;Cancel();}}
 public void Cancel(){animation?.Cancel();Finish(false);}
 private void Finish(bool accepted)
 {
  var pending=completion;if(pending is null)return;completion=null;
  animation?.Cancel();animation?.Dispose();animation=null;
  Stroke.BeginAnimation(SuccessStroke.ProgressProperty,null);
  CardScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);CardScale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
  Visibility=Visibility.Collapsed;pending.TrySetResult(accepted);
 }
}
