using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace WeatherBeacon.Controls;
/// <summary>Small, interruptible enter motion: opacity plus a short offset, eased out. Honors the system "show animations" setting.</summary>
public static class Motion
{
 public static void Enter(FrameworkElement element,double dx,double dy,TimeSpan delay,int milliseconds)
 {
  if(!SystemParameters.ClientAreaAnimation){Reset(element);return;}
  var offset=Translate(element);var ease=new CubicEase{EasingMode=EasingMode.EaseOut};var end=delay+TimeSpan.FromMilliseconds(milliseconds);
  // Key frames hold the start value during the stagger delay, so nothing flashes at its final state before it animates.
  element.BeginAnimation(UIElement.OpacityProperty,Frames(0,1,delay,end,ease));
  offset.BeginAnimation(TranslateTransform.XProperty,dx==0?null:Frames(dx,0,delay,end,ease));
  offset.BeginAnimation(TranslateTransform.YProperty,dy==0?null:Frames(dy,0,delay,end,ease));
  element.Opacity=1;offset.X=0;offset.Y=0;
 }
 public static void Reset(FrameworkElement element)
 {
  element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;
  if(element.RenderTransform is TranslateTransform offset&&!offset.IsFrozen){offset.BeginAnimation(TranslateTransform.XProperty,null);offset.BeginAnimation(TranslateTransform.YProperty,null);offset.X=0;offset.Y=0;}
 }
 private static TranslateTransform Translate(FrameworkElement element)
 {
  if(element.RenderTransform is TranslateTransform existing&&!existing.IsFrozen)return existing;
  var created=new TranslateTransform();element.RenderTransform=created;return created;
 }
 private static DoubleAnimationUsingKeyFrames Frames(double from,double to,TimeSpan delay,TimeSpan end,IEasingFunction ease)
 {
  var frames=new DoubleAnimationUsingKeyFrames{Duration=end};
  frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));
  if(delay>TimeSpan.Zero)frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(from,KeyTime.FromTimeSpan(delay)));
  frames.KeyFrames.Add(new EasingDoubleKeyFrame(to,KeyTime.FromTimeSpan(end),ease));
  return frames;
 }
}
