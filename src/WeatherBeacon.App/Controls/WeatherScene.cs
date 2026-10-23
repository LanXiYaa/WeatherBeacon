using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WeatherBeacon.Core.Models;
namespace WeatherBeacon.Controls;

public sealed class WeatherScene : FrameworkElement
{
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(WeatherKind),typeof(WeatherScene),new FrameworkPropertyMetadata(WeatherKind.Clear,FrameworkPropertyMetadataOptions.AffectsRender,SceneChanged));
 public static readonly DependencyProperty AnimatedProperty=DependencyProperty.Register(nameof(Animated),typeof(bool),typeof(WeatherScene),new FrameworkPropertyMetadata(true,(d,_)=>((WeatherScene)d).UpdateTimer()));
 public static readonly DependencyProperty IsDaytimeProperty=DependencyProperty.Register(nameof(IsDaytime),typeof(bool),typeof(WeatherScene),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.AffectsRender));
 public WeatherKind Kind{get=>(WeatherKind)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
 public bool Animated{get=>(bool)GetValue(AnimatedProperty);set=>SetValue(AnimatedProperty,value);}
 public bool IsDaytime{get=>(bool)GetValue(IsDaytimeProperty);set=>SetValue(IsDaytimeProperty,value);}

 private readonly DispatcherTimer timer=new(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(33)};
 private readonly Stopwatch clock=new();
 private TimeSpan lastTick;
 private readonly Particle[] rain=BuildParticles(96,false);
 private readonly Particle[] snow=BuildParticles(68,true);
 private readonly Star[] stars=BuildStars(74);
 private double time;
 private bool windowActive=true;
 public long RenderedFrames{get;private set;}
 public bool IsAnimating=>timer.IsEnabled;

 private static readonly Brush[] RainBrushes=[Brush(74,171,215,248),Brush(100,190,226,252),Brush(58,129,183,224)];
 private static readonly Pen[] RainPens=[Pen(RainBrushes[0],1),Pen(RainBrushes[1],1.35),Pen(RainBrushes[2],.8)];
 private static readonly Brush[] SnowBrushes=[Brush(190,242,249,255),Brush(135,230,242,255),Brush(225,250,253,255)];
 private static readonly Brush[] StarBrushes=[Brush(92,232,242,255),Brush(145,244,247,255),Brush(210,255,255,255),Brush(55,209,226,250)];
 private static readonly Brush FogBrush=Gradient3(Color.FromArgb(0,225,235,241),Color.FromArgb(72,225,235,241),Color.FromArgb(0,225,235,241));
 private static readonly Brush FogThin=Gradient3(Color.FromArgb(0,217,229,234),Color.FromArgb(34,217,229,234),Color.FromArgb(0,217,229,234));
 private static readonly Brush CloudLight=CloudBrush(196,219,229,238);
 private static readonly Brush CloudMid=CloudBrush(216,160,174,188);
 private static readonly Brush CloudDark=CloudBrush(236,72,84,98);
 private static readonly Brush StormCloud=CloudBrush(244,45,54,68);
 private static readonly Brush CloudNightUpper=CloudBrush(132,138,151,174);
 private static readonly Brush CloudNightLower=CloudBrush(166,104,118,139);
 private static readonly Brush OvercastBottom=CloudBrush(228,50,60,72);
 private static readonly Brush RainUpper=CloudBrush(240,30,38,50);
 private static readonly Brush RainMiddle=CloudBrush(218,49,61,76);
 private static readonly Brush RainLower=CloudBrush(246,22,29,41);
 private static readonly Brush SnowCloudUpper=CloudBrush(154,188,202,214);
 private static readonly Brush SnowCloudLower=CloudBrush(198,142,160,178);
 private static readonly Brush FogCloudUpper=CloudBrush(124,149,160,167);
 private static readonly Brush FogCloudLower=CloudBrush(156,105,119,129);
 private static readonly Brush SunHalo=Radial(Color.FromArgb(92,255,221,143),0,Color.FromArgb(0,255,221,143),1);
 private static readonly Brush MoonHalo=Radial(Color.FromArgb(58,219,232,255),0,Color.FromArgb(0,219,232,255),1);
 private static readonly Brush Moon=Brush(245,233,238,247);
 private static readonly Brush MoonMuted=Brush(86,116,133,166);
 private static readonly Brush SunDisk=Brush(235,255,236,184);
 private static readonly Brush CloudySun=Brush(125,255,230,170);
 private static readonly Brush NightThinCloud=Brush(60,78,104,154);
 private static readonly Brush LightningOverlay=Brush(255,229,232,255);
 private static readonly Brush BgClearDay=Gradient(Color.FromRgb(53,134,199),Color.FromRgb(140,205,235));
 private static readonly Brush BgClearNight=Gradient(Color.FromRgb(7,17,47),Color.FromRgb(20,42,91));
 private static readonly Brush BgCloudyDay=Gradient(Color.FromRgb(74,119,154),Color.FromRgb(143,176,194));
 private static readonly Brush BgCloudyNight=Gradient(Color.FromRgb(18,31,55),Color.FromRgb(52,70,95));
 private static readonly Brush BgOvercast=Gradient(Color.FromRgb(65,77,89),Color.FromRgb(118,133,144));
 private static readonly Brush BgRain=Gradient(Color.FromRgb(25,39,55),Color.FromRgb(54,75,94));
 private static readonly Brush BgSnowDay=Gradient(Color.FromRgb(77,103,124),Color.FromRgb(151,174,190));
 private static readonly Brush BgSnowNight=Gradient(Color.FromRgb(24,41,61),Color.FromRgb(73,96,119));
 private static readonly Brush BgFog=Gradient(Color.FromRgb(79,92,96),Color.FromRgb(139,148,147));
 private static readonly Brush BgStorm=Gradient(Color.FromRgb(13,20,34),Color.FromRgb(37,48,65));
 private static readonly Brush BgUnknown=Gradient(Color.FromRgb(30,46,64),Color.FromRgb(64,93,118));

 public WeatherScene()
 {
  IsHitTestVisible=false;
  SnapsToDevicePixels=false;
  clock.Start();
  timer.Tick+=(_,_)=>OnTimerTick();
  Loaded+=(_,_)=>{UpdateTimer();InvalidateVisual();};
  Unloaded+=(_,_)=>{timer.Stop();};
  IsVisibleChanged+=(_,_)=>UpdateTimer();
 }

 public void SetWindowActive(bool active)
 {
  if(windowActive==active)return;
  windowActive=active;
  UpdateTimer();
 }

 public void NudgeFrame()
 {
  if(!IsAnimating)return;
  time+=1d/30;
  InvalidateVisual();
 }

 private static void SceneChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
 {
  var scene=(WeatherScene)d;scene.time=0;scene.RenderedFrames=0;scene.InvalidateVisual();
 }
 private void OnTimerTick()
 {
  var now=clock.Elapsed;var delta=(now-lastTick).TotalSeconds;lastTick=now;
  time+=Math.Clamp(delta,0,.1);
  InvalidateVisual();
 }
 private void UpdateTimer()
 {
  var shouldRun=Animated&&windowActive&&IsLoaded&&IsVisible;
  if(shouldRun&&!timer.IsEnabled){lastTick=clock.Elapsed;timer.Start();}
  else if(!shouldRun&&timer.IsEnabled)timer.Stop();
  InvalidateVisual();
 }

 protected override void OnRender(DrawingContext dc)
 {
  base.OnRender(dc);
  double w=ActualWidth,h=ActualHeight;
  if(w<=0||h<=0)return;
  RenderedFrames++;
  var t=RenderedFrames==1?0:time;
  dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h)));
  dc.DrawRectangle(BackgroundFor(Kind,IsDaytime),null,new Rect(0,0,w,h));
  switch(Kind)
  {
   case WeatherKind.Clear:DrawClear(dc,w,h,t);break;
   case WeatherKind.Cloudy:DrawCloudy(dc,w,h,t);break;
   case WeatherKind.Overcast:DrawOvercast(dc,w,h,t);break;
   case WeatherKind.Rain:DrawRain(dc,w,h,t,false);break;
   case WeatherKind.Snow:DrawSnow(dc,w,h,t);break;
   case WeatherKind.Fog:DrawFog(dc,w,h,t);break;
   case WeatherKind.Thunderstorm:DrawRain(dc,w,h,t,true);break;
   default:DrawClear(dc,w,h,t);break;
  }
  dc.Pop();
 }

 private void DrawClear(DrawingContext dc,double w,double h,double t)
 {
  var x=w*.80+Math.Sin(t*.10)*3;var y=h*.16;
  if(IsDaytime)
  {
   dc.DrawEllipse(SunHalo,null,new(x,y),150,150);
   dc.DrawEllipse(SunDisk,null,new(x,y),30,30);
   DrawThinClouds(dc,w,t,.62,CloudLight);
  }
  else
  {
   dc.DrawEllipse(MoonHalo,null,new(x,y),112,112);
   dc.DrawEllipse(Moon,null,new(x,y),29,29);
   dc.DrawEllipse(MoonMuted,null,new(x+8,y-6),23,25);
   for(int i=0;i<stars.Length;i++)
   {
    var s=stars[i];var flicker=.58+.42*(Math.Sin(t*(.7+s.Phase*.08)+s.Phase)+1)/2;
    var brush=StarBrushes[(int)(flicker*3.99)];
    dc.DrawEllipse(brush,null,new(s.X*w+Math.Sin(t*.12+s.Phase)*1.2,s.Y*h*.72),s.Size*flicker,s.Size*flicker);
   }
   DrawThinClouds(dc,w,t,.56,NightThinCloud);
  }
 }
 private void DrawCloudy(DrawingContext dc,double w,double h,double t)
 {
  var orbX=w*.78;var orbY=h*.17;
  dc.DrawEllipse(IsDaytime?SunHalo:MoonHalo,null,new(orbX,orbY),105,105);
  dc.DrawEllipse(IsDaytime?CloudySun:Moon,null,new(orbX,orbY),23,23);
  DrawCloudBands(dc,w,h,t,IsDaytime?CloudLight:CloudNightUpper,IsDaytime?CloudMid:CloudNightLower,.5,4.2);
 }
 private void DrawOvercast(DrawingContext dc,double w,double h,double t)
 {
  DrawCloudBands(dc,w,h,t,CloudMid,CloudDark,.85,2.7);
  DrawCloudBands(dc,w,h,t+8,CloudDark,OvercastBottom,1.1,1.8,44);
 }
 private void DrawRain(DrawingContext dc,double w,double h,double t,bool storm)
 {
  DrawCloudBands(dc,w,h,t,StormCloud,RainUpper,1.15,storm?1.5:2.6);
  DrawCloudBands(dc,w,h,t+7,RainMiddle,RainLower,1.4,1.2,55);
  for(int i=0;i<rain.Length;i++)
  {
   var p=rain[i];var speed=p.Speed*(storm?1.25:1);var length=p.Length*(storm?1.2:1);
   var x=Wrap(p.X*w+t*p.Drift*18,w+32)-16;
   var y=Wrap(p.Y*h+t*speed,h+50)-25;
   dc.DrawLine(RainPens[i%3],new(x,y),new(x-length*.22,y+length));
  }
  if(storm)
  {
   var alpha=ThunderAlpha(t);
   if(alpha>0){dc.PushOpacity(alpha);dc.DrawRectangle(LightningOverlay,null,new Rect(0,0,w,h));dc.Pop();}
  }
 }
 private void DrawSnow(DrawingContext dc,double w,double h,double t)
 {
  DrawCloudBands(dc,w,h,t,SnowCloudUpper,SnowCloudLower,.55,1.8);
  for(int i=0;i<snow.Length;i++)
  {
   var p=snow[i];var x=Wrap(p.X*w+Math.Sin(t*p.Sway+p.Phase)*13,w+24)-12;
   var y=Wrap(p.Y*h+t*p.Speed,h+30)-15;
   dc.DrawEllipse(SnowBrushes[i%3],null,new(x,y),p.Size,p.Size);
   if(i%8==0)dc.DrawEllipse(SnowBrushes[2],null,new(x-3,y-2),.8,.8);
  }
 }
 private void DrawFog(DrawingContext dc,double w,double h,double t)
 {
  DrawCloudBands(dc,w,h,t,FogCloudUpper,FogCloudLower,.35,2.1);
  for(int i=0;i<8;i++)
  {
   var y=h*(.23+i*.09)+Math.Sin(t*.11+i)*4;
   var x=Wrap(i*w*.18+t*(3+i*.75),w*1.35)-w*.28;
   dc.DrawRoundedRectangle(i%2==0?FogBrush:FogThin,null,new Rect(x,y,w*.78,h*.068),h*.034,h*.034);
  }
 }
 private void DrawCloudBands(DrawingContext dc,double w,double h,double t,Brush upper,Brush lower,double opacity,double speed,int offset=0)
 {
  for(int i=0;i<7;i++)
  {
   var y=h*(.03+(i%4)*.15)+(offset%37);
   var x=Wrap(i*w*.22+offset+t*(speed+i*.18),w+300)-150;
   dc.PushOpacity(opacity*(.66+(i%3)*.14));
   dc.DrawEllipse(i%2==0?upper:lower,null,new(x,y),w*.36,h*.075);
   dc.DrawEllipse(i%2==0?upper:lower,null,new(x+w*.12,y-h*.035),w*.22,h*.09);
   dc.DrawEllipse(lower,null,new(x+w*.27,y),w*.18,h*.055);
   dc.Pop();
  }
 }
 private void DrawThinClouds(DrawingContext dc,double w,double t,double opacity,Brush brush)
 {
  for(int i=0;i<4;i++)
  {
   var x=Wrap(i*w*.31+t*(1.3+i*.16),w+220)-110;var y=48+i*37;
   dc.PushOpacity(opacity);
   dc.DrawRoundedRectangle(brush,null,new Rect(x,y,w*.31,12),6,6);
   dc.DrawRoundedRectangle(brush,null,new Rect(x+34,y-8,w*.19,12),6,6);
   dc.Pop();
  }
 }
 private static double ThunderAlpha(double t)
 {
  var cycle=t%9.5;
  var first=Math.Max(0,1-Math.Abs(cycle-5.15)/.13);
  var second=Math.Max(0,1-Math.Abs(cycle-5.48)/.10);
  return Math.Min(.62,(first+second*.72)*.62);
 }

 private static Brush BackgroundFor(WeatherKind kind,bool day)
 {
  if(kind==WeatherKind.Clear)return day?BgClearDay:BgClearNight;
  if(kind==WeatherKind.Cloudy)return day?BgCloudyDay:BgCloudyNight;
  if(kind==WeatherKind.Overcast)return BgOvercast;
  if(kind==WeatherKind.Rain)return BgRain;
  if(kind==WeatherKind.Snow)return day?BgSnowDay:BgSnowNight;
  if(kind==WeatherKind.Fog)return BgFog;
  if(kind==WeatherKind.Thunderstorm)return BgStorm;
  return BgUnknown;
 }
 static double Wrap(double value,double length){var result=value%length;return result<0?result+length:result;}
 static Brush Brush(byte alpha,byte red,byte green,byte blue){var b=new SolidColorBrush(Color.FromArgb(alpha,red,green,blue));b.Freeze();return b;}
 static Brush CloudBrush(byte alpha,byte red,byte green,byte blue){var b=new RadialGradientBrush(Color.FromArgb(alpha,red,green,blue),Color.FromArgb(0,red,green,blue)){MappingMode=BrushMappingMode.RelativeToBoundingBox,GradientOrigin=new Point(.5,.45)};b.Freeze();return b;}
 static Pen Pen(Brush brush,double thickness){var p=new Pen(brush,thickness);p.Freeze();return p;}
 static Brush Gradient(Color from,Color to){var b=new LinearGradientBrush(from,to,58);b.Freeze();return b;}
 static Brush Gradient3(Color from,Color middle,Color to){var b=new LinearGradientBrush{StartPoint=new Point(0,.5),EndPoint=new Point(1,.5)};b.GradientStops.Add(new(from,0));b.GradientStops.Add(new(middle,.5));b.GradientStops.Add(new(to,1));b.Freeze();return b;}
 static Brush Radial(Color center,double centerOffset,Color edge,double edgeOffset)
 {
  var b=new RadialGradientBrush(center,edge){MappingMode=BrushMappingMode.RelativeToBoundingBox,GradientOrigin=new Point(centerOffset,centerOffset)};
  b.Freeze();return b;
 }
 static Particle[] BuildParticles(int count,bool snow)
 {
  var result=new Particle[count];
  for(int i=0;i<count;i++)
  {
   var n=i+1d;var x=Fract(Math.Sin(n*12.9898)*43758.5453);var y=Fract(Math.Sin(n*78.233)*19571.441);
   result[i]=new(x,y,snow?28+Fract(n*.37)*32:230+Fract(n*.71)*250,snow?1.6+Fract(n*.53)*2.2:7+Fract(n*.43)*15,Fract(n*.27)*Math.PI*2,snow?.35+Fract(n*.61)*.85:.35+Fract(n*.29)*1.5);
  }
  return result;
 }
 static Star[] BuildStars(int count)
 {
  var result=new Star[count];
  for(int i=0;i<count;i++)
  {
   var n=i+1d;result[i]=new(Fract(Math.Sin(n*91.73)*4137.13),Fract(Math.Sin(n*41.11)*1519.73),.55+Fract(n*.67)*1.35,Fract(n*.31)*Math.PI*2);
  }
  return result;
 }
 static double Fract(double value)=>value-Math.Floor(value);
 readonly record struct Particle(double X,double Y,double Speed,double Length,double Phase,double Drift){public double Sway=>.35+Drift*.9;public double Size=>1.25+Length*.42;}
 readonly record struct Star(double X,double Y,double Size,double Phase);
}
