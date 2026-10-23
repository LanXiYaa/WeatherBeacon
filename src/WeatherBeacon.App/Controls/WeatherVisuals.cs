using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Controls;
public sealed class WeatherIcon : FrameworkElement
{
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(WeatherKind),typeof(WeatherIcon),new FrameworkPropertyMetadata(WeatherKind.Unknown,FrameworkPropertyMetadataOptions.AffectsRender));
 public WeatherKind Kind{get=>(WeatherKind)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
 protected override void OnRender(DrawingContext dc)
 {
  base.OnRender(dc);dc.PushTransform(new ScaleTransform(ActualWidth/80,ActualHeight/80));
  var gold=new SolidColorBrush(Color.FromRgb(255,211,116));var white=new SolidColorBrush(Color.FromRgb(220,235,245));var blue=new SolidColorBrush(Color.FromRgb(126,201,249));
  if(Kind==WeatherKind.Unknown){dc.DrawEllipse(null,new Pen(white,2),new(40,40),16,16);dc.DrawLine(new Pen(white,2),new(32,40),new(48,40));}
  if(Kind is WeatherKind.Clear or WeatherKind.Cloudy)
  {
   var c=Kind==WeatherKind.Clear?new Point(40,39):new Point(29,28);var radius=Kind==WeatherKind.Clear?16:13;
   dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(22,255,211,116)),null,c,radius+11,radius+11);
   for(int i=0;i<8;i++){double a=i*Math.PI/4;dc.DrawLine(new Pen(gold,2.4){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(c.X+Math.Cos(a)*(radius+6),c.Y+Math.Sin(a)*(radius+6)),new(c.X+Math.Cos(a)*(radius+11),c.Y+Math.Sin(a)*(radius+11)));}
   dc.DrawEllipse(gold,null,c,radius,radius);
  }
  if(Kind is WeatherKind.Cloudy or WeatherKind.Rain or WeatherKind.Snow or WeatherKind.Thunderstorm)
  {
   dc.DrawRoundedRectangle(white,null,new Rect(16,35,50,21),10,10);dc.DrawEllipse(white,null,new(32,35),12,12);dc.DrawEllipse(white,null,new(47,32),15,15);
  }
  if(Kind==WeatherKind.Rain)for(int i=0;i<3;i++)dc.DrawLine(new Pen(blue,3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(26+i*13,61),new(22+i*13,69));
  if(Kind==WeatherKind.Snow)for(int i=0;i<3;i++){double x=25+i*14;for(int j=0;j<3;j++){double a=j*Math.PI/3;dc.DrawLine(new Pen(white,1.5),new(x-4*Math.Cos(a),65-4*Math.Sin(a)),new(x+4*Math.Cos(a),65+4*Math.Sin(a)));}}
  if(Kind==WeatherKind.Thunderstorm)dc.DrawGeometry(gold,null,Geometry.Parse("M 42,51 L 31,65 L 40,65 L 35,77 L 54,59 L 44,59 L 49,51 Z"));
  if(Kind==WeatherKind.Fog)for(int i=0;i<4;i++)dc.DrawLine(new Pen(white,3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(15+(i%2)*7,25+i*10),new(65-(i%2)*7,25+i*10));
  dc.Pop();
 }
}
public sealed class WeatherScene : FrameworkElement
{
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(WeatherKind),typeof(WeatherScene),new FrameworkPropertyMetadata(WeatherKind.Clear,FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty AnimatedProperty=DependencyProperty.Register(nameof(Animated),typeof(bool),typeof(WeatherScene),new PropertyMetadata(true,(d,e)=>((WeatherScene)d).UpdateTimer()));
 public WeatherKind Kind{get=>(WeatherKind)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
 public bool Animated{get=>(bool)GetValue(AnimatedProperty);set=>SetValue(AnimatedProperty,value);}
 private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(40)};private double time;
 public WeatherScene(){IsHitTestVisible=false;timer.Tick+=(_,_)=>{time+=.04;InvalidateVisual();};Loaded+=(_,_)=>UpdateTimer();Unloaded+=(_,_)=>timer.Stop();IsVisibleChanged+=(_,_)=>UpdateTimer();}
 private void UpdateTimer(){if(Animated&&IsLoaded&&IsVisible)timer.Start();else{timer.Stop();time=0;InvalidateVisual();}}
 protected override void OnRender(DrawingContext dc)
 {
  double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;
  dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h)));
  if(Kind is WeatherKind.Clear or WeatherKind.Unknown)
  {
   var glow=new RadialGradientBrush(Color.FromArgb(78,255,204,120),Colors.Transparent);
   dc.DrawEllipse(glow,null,new(w*.84+Math.Sin(time*.15)*8,h*.15),135,135);
   for(int i=0;i<9;i++){var p=new Point((i*67+time*2)%w,32+(i*41)%(int)h);dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(35,255,227,163)),null,p,1.4,1.4);}
  }
  if(Kind is WeatherKind.Cloudy or WeatherKind.Fog or WeatherKind.Thunderstorm)
  {
   for(int i=0;i<5;i++){double x=((i*117+time*(4+i))%(w+200))-100;dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb((byte)(Kind==WeatherKind.Fog?37:22),208,225,240),Colors.Transparent),null,new(x,40+i*35),150,45);}
  }
  if(Kind is WeatherKind.Rain or WeatherKind.Thunderstorm or WeatherKind.Snow)
  {
   for(int i=0;i<34;i++){double x=(i*73+Math.Sin(time+i)*5)%w;double y=(i*47+time*(Kind==WeatherKind.Snow?14:85))%(h+30)-15;
    if(Kind==WeatherKind.Snow)dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(95,233,245,255)),null,new(x,y),1.3+i%3*.4,1.3+i%3*.4);
    else dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(48,157,208,243)),1),new(x,y),new(x-4,y+12));
   }
  }
  // Slow lightning glow; no rapid full-screen flashes.
  if(Kind==WeatherKind.Thunderstorm){double alpha=Math.Pow(Math.Max(0,Math.Sin(time*.45)),12)*25;dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)alpha,195,192,255)),null,new Rect(0,0,w,h));}
  dc.Pop();
 }
}
