using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace WeatherBeacon.Controls;
public static class PressMotion
{
 public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(PressMotion),new PropertyMetadata(false,Changed));
 private static readonly DependencyProperty HandlerProperty=DependencyProperty.RegisterAttached("Handler",typeof(EventHandler),typeof(PressMotion));
 private static readonly DependencyPropertyDescriptor Pressed=DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty,typeof(Button));
 public static bool GetEnabled(DependencyObject value)=>(bool)value.GetValue(EnabledProperty);
 public static void SetEnabled(DependencyObject value,bool enabled)=>value.SetValue(EnabledProperty,enabled);
 private static void Changed(DependencyObject value,DependencyPropertyChangedEventArgs e)
 {
  if(value is not Button button)return;
  if((bool)e.NewValue){button.Loaded+=Loaded;button.Unloaded+=Unloaded;if(button.IsLoaded)Attach(button);}
  else{button.Loaded-=Loaded;button.Unloaded-=Unloaded;Detach(button);}
 }
 private static void Loaded(object sender,RoutedEventArgs e)=>Attach((Button)sender);
 private static void Unloaded(object sender,RoutedEventArgs e)=>Detach((Button)sender);
 private static void Attach(Button button){if(button.GetValue(HandlerProperty) is not null)return;EventHandler handler=(_,_)=>Animate(button);button.SetValue(HandlerProperty,handler);Pressed.AddValueChanged(button,handler);}
 private static void Detach(Button button){if(button.GetValue(HandlerProperty) is EventHandler handler){Pressed.RemoveValueChanged(button,handler);button.ClearValue(HandlerProperty);}}
 private static void Animate(Button button)
 {
  button.ApplyTemplate();if(button.Template.FindName("Surface",button) is not FrameworkElement surface)return;
  if(surface.RenderTransform is not ScaleTransform scale||scale.IsFrozen){scale=new ScaleTransform(1,1);surface.RenderTransform=scale;}
  bool pressed=button.IsPressed;double target=pressed?.96:1;
  foreach(var property in new[]{ScaleTransform.ScaleXProperty,ScaleTransform.ScaleYProperty})
  {
   if(!SystemParameters.ClientAreaAnimation){scale.BeginAnimation(property,null);scale.SetValue(property,1d);continue;}
   // Ease out both ways with no overshoot: a bouncy release reads as exaggerated on a 0.96 press.
   scale.BeginAnimation(property,new DoubleAnimation(target,TimeSpan.FromMilliseconds(pressed?100:160)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
  }
 }
}
