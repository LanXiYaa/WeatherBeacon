using System.Windows;
using System.Windows.Media;
namespace WeatherBeacon.Controls;
public sealed class SuccessStroke : FrameworkElement
{
 public static readonly DependencyProperty ProgressProperty=DependencyProperty.Register(nameof(Progress),typeof(double),typeof(SuccessStroke),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Progress {get=>(double)GetValue(ProgressProperty);set=>SetValue(ProgressProperty,value);}
 protected override void OnRender(DrawingContext dc)
 {
  var p=Math.Clamp(Progress,0,1);if(p<=0)return;
  var scale=Math.Min(ActualWidth,ActualHeight)/120;
  dc.PushTransform(new TranslateTransform((ActualWidth-120*scale)/2,(ActualHeight-120*scale)/2));dc.PushTransform(new ScaleTransform(scale,scale));
  var pen=new Pen(new SolidColorBrush(Color.FromRgb(146,218,213)),3.5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();
  var ring=Math.Min(p/.5,1);var angle=-90+360*ring;var sweep=65+295*ring;
  if(ring>=1)dc.DrawEllipse(null,pen,new Point(60,60),47,47);
  else
  {
   Point At(double a)=>new(60+47*Math.Cos(a*Math.PI/180),60+47*Math.Sin(a*Math.PI/180));
   var geometry=new StreamGeometry();using(var g=geometry.Open()){g.BeginFigure(At(angle),false,false);g.ArcTo(At(angle+sweep),new Size(47,47),0,sweep>180,SweepDirection.Clockwise,true,false);}geometry.Freeze();dc.DrawGeometry(null,pen,geometry);
  }
  var ink=Math.Clamp((p-.5)/.5,0,1);
  if(ink>0)
  {
   var a=new Point(34,61);var b=new Point(52,79);var c=new Point(86,40);
   var first=(b-a).Length;var second=(c-b).Length;var length=ink*(first+second);
   var geometry=new StreamGeometry();using(var g=geometry.Open()){g.BeginFigure(a,false,false);if(length<=first)g.LineTo(a+(b-a)*(length/first),true,false);else{g.LineTo(b,true,false);g.LineTo(b+(c-b)*((length-first)/second),true,false);}}geometry.Freeze();dc.DrawGeometry(null,pen,geometry);
  }
  dc.Pop();dc.Pop();
 }
}
