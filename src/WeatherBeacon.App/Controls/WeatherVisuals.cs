using System.Windows;
using System.Windows.Media;
using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Controls;

public sealed class WeatherIcon : FrameworkElement
{
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(WeatherKind),typeof(WeatherIcon),new FrameworkPropertyMetadata(WeatherKind.Unknown,FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty IsDaytimeProperty=DependencyProperty.Register(nameof(IsDaytime),typeof(bool),typeof(WeatherIcon),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.AffectsRender));
 public WeatherKind Kind{get=>(WeatherKind)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
 public bool IsDaytime{get=>(bool)GetValue(IsDaytimeProperty);set=>SetValue(IsDaytimeProperty,value);}
 private static readonly SolidColorBrush Gold=new(Color.FromRgb(255,211,116));
 private static readonly SolidColorBrush Moon=new(Color.FromRgb(235,241,255));
 private static readonly SolidColorBrush White=new(Color.FromRgb(220,235,245));
 private static readonly SolidColorBrush CloudShadow=new(Color.FromRgb(158,176,193));
 private static readonly SolidColorBrush Blue=new(Color.FromRgb(126,201,249));
 protected override void OnRender(DrawingContext dc)
 {
  base.OnRender(dc);
  if(ActualWidth<=0||ActualHeight<=0)return;
  dc.PushTransform(new ScaleTransform(ActualWidth/80,ActualHeight/80));
  if(Kind==WeatherKind.Unknown){dc.DrawEllipse(null,new Pen(White,2),new(40,40),16,16);dc.DrawLine(new Pen(White,2),new(32,40),new(48,40));}
  if(Kind==WeatherKind.Clear)
  {
   if(IsDaytime)
   {
    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(25,255,211,116)),null,new(40,39),28,28);
    for(int i=0;i<8;i++){double a=i*Math.PI/4;dc.DrawLine(new Pen(Gold,2.4){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(40+Math.Cos(a)*22,39+Math.Sin(a)*22),new(40+Math.Cos(a)*29,39+Math.Sin(a)*29));}
    dc.DrawEllipse(Gold,null,new(40,39),16,16);
   }
   else
   {
    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(24,215,230,255)),null,new(40,39),28,28);
    dc.DrawEllipse(Moon,null,new(40,39),17,17);
    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(50,64,101)),null,new(47,34),14,15);
   }
  }
  if(Kind==WeatherKind.Cloudy)
  {
   var orb=IsDaytime?Gold:Moon;
   dc.DrawEllipse(orb,null,new(30,29),13,13);
   dc.DrawRoundedRectangle(White,null,new Rect(16,35,50,21),10,10);
   dc.DrawEllipse(White,null,new(32,35),12,12);dc.DrawEllipse(White,null,new(47,32),15,15);
  }
  if(Kind==WeatherKind.Overcast)
  {
   dc.DrawRoundedRectangle(CloudShadow,null,new Rect(9,25,51,18),9,9);
   dc.DrawEllipse(CloudShadow,null,new(25,25),11,11);dc.DrawEllipse(CloudShadow,null,new(41,23),14,14);
   dc.DrawRoundedRectangle(White,null,new Rect(16,35,50,21),10,10);
   dc.DrawEllipse(White,null,new(32,35),12,12);dc.DrawEllipse(White,null,new(47,32),15,15);
  }
  if(Kind is WeatherKind.Rain or WeatherKind.Snow or WeatherKind.Thunderstorm)
  {
   dc.DrawRoundedRectangle(White,null,new Rect(16,35,50,21),10,10);dc.DrawEllipse(White,null,new(32,35),12,12);dc.DrawEllipse(White,null,new(47,32),15,15);
  }
  if(Kind==WeatherKind.Rain)for(int i=0;i<3;i++)dc.DrawLine(new Pen(Blue,3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(26+i*13,61),new(22+i*13,69));
  if(Kind==WeatherKind.Snow)for(int i=0;i<3;i++){double x=25+i*14;for(int j=0;j<3;j++){double a=j*Math.PI/3;dc.DrawLine(new Pen(White,1.5),new(x-4*Math.Cos(a),65-4*Math.Sin(a)),new(x+4*Math.Cos(a),65+4*Math.Sin(a)));}}
  if(Kind==WeatherKind.Thunderstorm)dc.DrawGeometry(Gold,null,Geometry.Parse("M 42,51 L 31,65 L 40,65 L 35,77 L 54,59 L 44,59 L 49,51 Z"));
  if(Kind==WeatherKind.Fog)for(int i=0;i<4;i++)dc.DrawLine(new Pen(White,3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new(15+(i%2)*7,25+i*10),new(65-(i%2)*7,25+i*10));
  dc.Pop();
 }
}
