using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WeatherBeacon.Core.Models;
using WeatherBeacon.Features.History;
namespace WeatherBeacon.Controls;
/// <summary>
/// A year of days drawn as a week-by-week grid (columns = weeks, rows = Monday…Sunday), coloured by the day's weather.
/// Colours run along one green ramp from deep pine (clear) to pale lichen (thunderstorm): the brighter the cell, the wetter or rougher the day.
/// Drawn in one pass for speed; hover shows a tooltip for the day under the pointer.
/// </summary>
public sealed class WeatherHeatmap : FrameworkElement
{
 public const int Weeks=53;
 private const double Gap=3,Gutter=30,Header=20,MinCell=7,MaxCell=15;
 /// <summary>Weather kinds in ramp order, with their colour and a short meaning for legends and tooltips.</summary>
 public static readonly IReadOnlyList<(WeatherKind Kind,string Name,Color Color,string Meaning)> Ramp=
 [
  (WeatherKind.Clear,"晴",Color.FromRgb(0x1F,0x5A,0x48),"晴天为主"),
  (WeatherKind.Cloudy,"多云",Color.FromRgb(0x25,0x70,0x50),"多云，有阳光"),
  (WeatherKind.Overcast,"阴",Color.FromRgb(0x2F,0x85,0x57),"全天阴沉"),
  (WeatherKind.Fog,"雾 / 霾",Color.FromRgb(0x48,0x99,0x60),"有雾或霾，能见度低"),
  (WeatherKind.Rain,"雨",Color.FromRgb(0x64,0xB2,0x6F),"有降雨，出门需带伞"),
  (WeatherKind.Snow,"雪",Color.FromRgb(0x99,0xD4,0x9B),"有降雪或雨夹雪"),
  (WeatherKind.Thunderstorm,"雷暴",Color.FromRgb(0xD3,0xEF,0xA6),"雷阵雨或强对流"),
 ];
 public static readonly Color EmptyColor=Color.FromRgb(0x2A,0x45,0x53),UnknownColor=Color.FromRgb(0x46,0x5E,0x5C),Accent=Color.FromRgb(0xE8,0xC7,0x83);
 public static Color ColorOf(WeatherKind kind){var match=Ramp.FirstOrDefault(x=>x.Kind==kind);return match.Name is null?UnknownColor:match.Color;}
 public static string NameOf(WeatherKind kind)=>Ramp.FirstOrDefault(r=>r.Kind==kind).Name??"未知";

 public static readonly DependencyProperty RecordsProperty=DependencyProperty.Register(nameof(Records),typeof(IReadOnlyDictionary<DateOnly,DayRecord>),typeof(WeatherHeatmap),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
 public IReadOnlyDictionary<DateOnly,DayRecord>? Records{get=>(IReadOnlyDictionary<DateOnly,DayRecord>?)GetValue(RecordsProperty);set=>SetValue(RecordsProperty,value);}
 public static readonly DependencyProperty EndDateProperty=DependencyProperty.Register(nameof(EndDate),typeof(DateOnly),typeof(WeatherHeatmap),new FrameworkPropertyMetadata(DateOnly.FromDateTime(DateTime.Today),FrameworkPropertyMetadataOptions.AffectsRender));
 public DateOnly EndDate{get=>(DateOnly)GetValue(EndDateProperty);set=>SetValue(EndDateProperty,value);}
 /// <summary>0→1 sweep from the oldest week to today, played once when the page opens.</summary>
 public static readonly DependencyProperty RevealProperty=DependencyProperty.Register(nameof(Reveal),typeof(double),typeof(WeatherHeatmap),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Reveal{get=>(double)GetValue(RevealProperty);set=>SetValue(RevealProperty,value);}
 /// <summary>How strongly other kinds are dimmed while a legend entry is hovered (animated 0→1).</summary>
 public static readonly DependencyProperty FocusLevelProperty=DependencyProperty.Register(nameof(FocusLevel),typeof(double),typeof(WeatherHeatmap),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double FocusLevel{get=>(double)GetValue(FocusLevelProperty);set=>SetValue(FocusLevelProperty,value);}
 public static readonly DependencyProperty HoverGlowProperty=DependencyProperty.Register(nameof(HoverGlow),typeof(double),typeof(WeatherHeatmap),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
 public double HoverGlow{get=>(double)GetValue(HoverGlowProperty);set=>SetValue(HoverGlowProperty,value);}
 public bool Fahrenheit{get;set;}

 private WeatherKind? focusKind;private DateOnly? hovered;
 private readonly ToolTip tip=new(){Placement=PlacementMode.Relative,HasDropShadow=false};
 public WeatherHeatmap()
 {
  tip.PlacementTarget=this;tip.Style=TipStyle();
  MouseMove+=(_,e)=>Hover(e.GetPosition(this));
  MouseLeave+=(_,_)=>{hovered=null;tip.IsOpen=false;Animate(HoverGlowProperty,0,90);};
  IsVisibleChanged+=(_,_)=>{if(!IsVisible)tip.IsOpen=false;};
 }
 /// <summary>Highlights one weather kind (legend hover); null restores all.</summary>
 public void FocusOn(WeatherKind? kind)
 {
  if(kind is not null)focusKind=kind;
  // Keep the focused kind while fading back out, then forget it.
  Animate(FocusLevelProperty,kind is null?0:1,180,kind is null?()=>{if(FocusLevel<=.001)focusKind=null;InvalidateVisual();}:null);
  InvalidateVisual();
 }
 public void PlayReveal()
 {
  if(!SystemParameters.ClientAreaAnimation){BeginAnimation(RevealProperty,null);Reveal=1;return;}
  BeginAnimation(RevealProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(750)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
 }
 private void Animate(DependencyProperty property,double to,int ms,Action? done=null)
 {
  if(!SystemParameters.ClientAreaAnimation){BeginAnimation(property,null);SetValue(property,to);done?.Invoke();return;}
  var animation=new DoubleAnimation(to,TimeSpan.FromMilliseconds(ms)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
  if(done is not null)animation.Completed+=(_,_)=>done();
  BeginAnimation(property,animation);
 }

 // Geometry ---------------------------------------------------------------------------------
 private double Cell(double width)=>Math.Clamp(Math.Floor((width-Gutter-(Weeks-1)*Gap)/Weeks),MinCell,MaxCell);
 private DateOnly FirstDay=>EndDate.AddDays(-(Weeks-1)*7-Row(EndDate));
 private static int Row(DateOnly date)=>((int)date.DayOfWeek+6)%7;
 private Rect CellRect(int column,int row,double cell)=>new(Gutter+column*(cell+Gap),Header+row*(cell+Gap),cell,cell);
 protected override Size MeasureOverride(Size available)
 {
  var width=double.IsInfinity(available.Width)?Gutter+Weeks*(MaxCell+Gap):available.Width;
  var cell=Cell(width);
  return new(Math.Min(width,Gutter+Weeks*(cell+Gap)-Gap),Header+7*cell+6*Gap);
 }

 protected override void OnRender(DrawingContext dc)
 {
  var cell=Cell(ActualWidth);var radius=Math.Min(3,cell/4);var first=FirstDay;var records=Records;var today=DateOnly.FromDateTime(DateTime.Today);
  var dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;var family=TextElement.GetFontFamily(this);
  var typeface=new Typeface(family,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
  var ink=new SolidColorBrush(Color.FromArgb(0xB0,0xC1,0xD2,0xD8));ink.Freeze();
  // Weekday labels on alternate rows keep the gutter quiet.
  foreach(var (row,label) in new[]{(0,"周一"),(2,"周三"),(4,"周五"),(6,"周日")})
  {
   var text=new FormattedText(label,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,typeface,10.5,ink,dpi);
   dc.DrawText(text,new Point(0,Header+row*(cell+Gap)+(cell-text.Height)/2));
  }
  // Month labels where a month's first day falls; skip one that would collide with the previous label.
  double lastLabelEnd=-1;
  for(int column=0;column<Weeks;column++)
  {
   var weekStart=first.AddDays(column*7);var monthStart=Enumerable.Range(0,7).Select(weekStart.AddDays).FirstOrDefault(d=>d.Day==1);
   if(monthStart==default&&column!=0)continue;
   var labelDate=monthStart==default?weekStart:monthStart;
   var text=new FormattedText(column==0||labelDate.Month==1?$"{labelDate.Year}年{labelDate.Month}月":$"{labelDate.Month}月",CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,typeface,10.5,ink,dpi);
   var x=Gutter+column*(cell+Gap);if(x<lastLabelEnd+6)continue;
   if(x+text.Width>ActualWidth)break;
   dc.DrawText(text,new Point(x,0));lastLabelEnd=x+text.Width;
  }
  for(int column=0;column<Weeks;column++)
  {
   // Left-to-right sweep: each column fades in over a quarter of the reveal.
   var appear=Math.Clamp((Reveal*1.25-column/(double)Weeks)/.25,0,1);if(appear<=0)continue;
   for(int row=0;row<7;row++)
   {
    var date=first.AddDays(column*7+row);if(date>EndDate)break;
    DayRecord? record=null;records?.TryGetValue(date,out record);
    var color=record is null?EmptyColor:ColorOf(record.Kind);
    double alpha=appear;
    if(focusKind is not null&&(record is null||record.Kind!=focusKind))alpha*=1-.72*FocusLevel;
    var brush=new SolidColorBrush(Color.FromArgb((byte)(255*alpha),color.R,color.G,color.B));brush.Freeze();
    var rect=CellRect(column,row,cell);
    dc.DrawRoundedRectangle(brush,null,rect,radius,radius);
    if(date==today)
    {
     var pen=new Pen(new SolidColorBrush(Color.FromArgb((byte)(200*appear),Accent.R,Accent.G,Accent.B)),1);pen.Freeze();
     rect.Inflate(1.5,1.5);dc.DrawRoundedRectangle(null,pen,rect,radius+1.5,radius+1.5);
    }
   }
  }
  if(hovered is DateOnly h&&HoverGlow>0)
  {
   var offset=h.DayNumber-first.DayNumber;var rect=CellRect(offset/7,offset%7,cell);rect.Inflate(2,2);
   var pen=new Pen(new SolidColorBrush(Color.FromArgb((byte)(255*HoverGlow),0xF5,0xF2,0xE9)),1.5);pen.Freeze();
   dc.DrawRoundedRectangle(null,pen,rect,radius+2,radius+2);
  }
 }

 // Hover + tooltip ---------------------------------------------------------------------------
 private void Hover(Point point)
 {
  var cell=Cell(ActualWidth);
  int column=(int)Math.Floor((point.X-Gutter)/(cell+Gap)),row=(int)Math.Floor((point.Y-Header)/(cell+Gap));
  DateOnly? date=null;
  if(column is >=0 and <Weeks&&row is >=0 and <7&&CellRect(column,row,cell) is var r&&r.Contains(point))
  {
   var candidate=FirstDay.AddDays(column*7+row);if(candidate<=EndDate)date=candidate;
  }
  if(date==hovered)return;
  hovered=date;
  if(date is not DateOnly day){tip.IsOpen=false;Animate(HoverGlowProperty,0,90);return;}
  DayRecord? record=null;Records?.TryGetValue(day,out record);
  tip.Content=TipContent(day,record);
  var at=CellRect(column,row,cell);
  tip.HorizontalOffset=Math.Min(at.X-8,Math.Max(0,ActualWidth-230));tip.VerticalOffset=at.Bottom+8;
  tip.IsOpen=true;
  Animate(HoverGlowProperty,1,120);
 }
 private string Temp(double? value)=>value is double t?$"{(Fahrenheit?t*9/5+32:t):0}°":"—";
 private FrameworkElement TipContent(DateOnly day,DayRecord? record)
 {
  var week=new[]{"周一","周二","周三","周四","周五","周六","周日"}[Row(day)];
  var panel=new StackPanel{MinWidth=170};
  panel.Children.Add(new TextBlock{Text=$"{day.Year}年{day.Month}月{day.Day}日  {week}",FontWeight=FontWeights.SemiBold,FontSize=12.5,Foreground=new SolidColorBrush(Color.FromRgb(0xF5,0xF2,0xE9))});
  var line=new DockPanel{Margin=new Thickness(0,8,0,0)};
  var swatch=new Border{Width=10,Height=10,CornerRadius=new CornerRadius(2.5),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center,Background=new SolidColorBrush(record is null?EmptyColor:ColorOf(record.Kind))};
  if(record is null)swatch.BorderBrush=new SolidColorBrush(Color.FromRgb(0x5A,0x75,0x80));if(record is null)swatch.BorderThickness=new Thickness(1);
  DockPanel.SetDock(swatch,Dock.Left);line.Children.Add(swatch);
  line.Children.Add(new TextBlock{Text=record is null?"没有记录":$"{record.Description}   {Temp(record.Low)} / {Temp(record.High)}",FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(0xE8,0xC7,0x83))});
  panel.Children.Add(line);
  panel.Children.Add(new TextBlock{Text=record is null?"WeatherBeacon 当天没有成功刷新天气":$"{record.City} · {NameOf(record.Kind)}：{Ramp.FirstOrDefault(r=>r.Kind==record.Kind).Meaning??"天气类型未知"}",FontSize=11,Margin=new Thickness(0,6,0,0),Foreground=new SolidColorBrush(Color.FromRgb(0xC1,0xD2,0xD8)),TextWrapping=TextWrapping.Wrap,MaxWidth=230});
  return panel;
 }
 private static Style TipStyle()
 {
  var template=new ControlTemplate(typeof(ToolTip));
  var border=new FrameworkElementFactory(typeof(Border));
  border.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(0xF2,0x14,0x2B,0x3B)));
  border.SetValue(Border.BorderBrushProperty,new SolidColorBrush(Color.FromArgb(0x99,0xE8,0xC7,0x83)));
  border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
  border.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));
  border.SetValue(Border.PaddingProperty,new Thickness(12,10,12,10));
  border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
  template.VisualTree=border;
  var style=new Style(typeof(ToolTip));style.Setters.Add(new Setter(Control.TemplateProperty,template));style.Setters.Add(new Setter(Control.FontFamilyProperty,new FontFamily(new Uri("pack://application:,,,/WeatherBeacon;component/Assets/Fonts/"),"./#Source Han Sans SC")));
  return style;
 }
}
