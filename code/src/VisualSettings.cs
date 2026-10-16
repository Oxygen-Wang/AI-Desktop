using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QiDesk {
public static class Appearance {
    public static Brush Background(string color,double alpha,double strength){Color tint=(Color)ColorConverter.ConvertFromString(color);return new SolidColorBrush(Color.FromArgb((byte)Math.Max(1,255*Math.Max(0,Math.Min(1,alpha))),(byte)(22*(1-strength)+tint.R*strength),(byte)(32*(1-strength)+tint.G*strength),(byte)(49*(1-strength)+tint.B*strength)));}
    static double Clamp(double value){return Double.IsNaN(value)?0:Math.Max(0,Math.Min(1,value));}
    static Color Tint(string color){try{return (Color)ColorConverter.ConvertFromString(color);}catch{return Color.FromRgb(119,191,163);}}
    static Color Mix(Color color,double target,double amount){return Color.FromRgb((byte)(color.R*(1-amount)+target*amount),(byte)(color.G*(1-amount)+target*amount),(byte)(color.B*(1-amount)+target*amount));}
    // 桌面分区与工作区面板共用同一套底色规则：跟随颜色 / 纯色 / 渐变 / 毛玻璃 / 图片。
    public static Brush FrameBackground(Group style,double alpha){
        if(style==null)return Background("#77BFA3",alpha,.55);
        string kind=String.IsNullOrEmpty(style.BackgroundStyle)?"跟随颜色":style.BackgroundStyle;
        if(kind=="图片"){
            var image=Picture(style.BackgroundImage);
            if(image!=null){
                var fill=new ImageBrush(image){Stretch=Stretch.UniformToFill,TileMode=TileMode.None,Opacity=Clamp(alpha)};
                string mode=String.IsNullOrEmpty(style.ImageMode)?"填充":style.ImageMode;
                if(mode=="适应")fill.Stretch=Stretch.Uniform;
                else if(mode=="居中")fill.Stretch=Stretch.None;
                else if(mode=="平铺"){fill.Stretch=Stretch.None;fill.TileMode=TileMode.Tile;fill.ViewportUnits=BrushMappingMode.Absolute;fill.Viewport=new Rect(0,0,Math.Max(8,image.PixelWidth),Math.Max(8,image.PixelHeight));}
                return fill;
            }
            kind="跟随颜色";
        }
        Color tint=Tint(style.Color);double a=Clamp(alpha);
        if(kind=="纯色")return new SolidColorBrush(Color.FromArgb((byte)Math.Max(1,255*a),tint.R,tint.G,tint.B));
        if(kind=="渐变"){
            Color upper=Mix(tint,255,.18),lower=Mix(tint,0,.42);
            return new LinearGradientBrush(Color.FromArgb((byte)Math.Max(1,255*a),upper.R,upper.G,upper.B),Color.FromArgb((byte)Math.Max(1,255*a*.62),lower.R,lower.G,lower.B),90);
        }
        if(kind=="毛玻璃"){
            // WPF 分层窗口拿不到系统级模糊，这里用半透明渐变加细噪点模拟磨砂质感。
            Color upper=Mix(tint,235,.30),lower=Mix(tint,18,.30);
            var gradient=new LinearGradientBrush(Color.FromArgb((byte)Math.Max(1,255*a*.94),upper.R,upper.G,upper.B),Color.FromArgb((byte)Math.Max(1,255*a*.74),lower.R,lower.G,lower.B),90);
            var group=new DrawingGroup();
            group.Children.Add(new GeometryDrawing(gradient,null,new RectangleGeometry(new Rect(0,0,1,1))));
            group.Children.Add(new GeometryDrawing(Noise(),null,new RectangleGeometry(new Rect(0,0,1,1))));
            return new DrawingBrush(group){Viewport=new Rect(0,0,1,1),ViewportUnits=BrushMappingMode.RelativeToBoundingBox,Stretch=Stretch.Fill};
        }
        return Background(style.Color,a,style.TintStrength);
    }
    static Brush noise;
    static Brush Noise(){
        if(noise!=null)return noise;
        var group=new DrawingGroup();var random=new Random(20261016);
        for(int y=0;y<24;y++)for(int x=0;x<24;x++){
            int value=random.Next(0,22);
            if(value==0)continue;
            bool light=random.Next(0,2)==0;
            var brush=new SolidColorBrush(light?Color.FromArgb((byte)value,255,255,255):Color.FromArgb((byte)value,0,0,0));
            brush.Freeze();
            group.Children.Add(new GeometryDrawing(brush,null,new RectangleGeometry(new Rect(x,y,1,1))));
        }
        group.Freeze();
        noise=new DrawingBrush(group){TileMode=TileMode.Tile,Viewport=new Rect(0,0,24,24),ViewportUnits=BrushMappingMode.Absolute,Stretch=Stretch.None};
        noise.Freeze();
        return noise;
    }
    static readonly Dictionary<string,BitmapImage> pictures=new Dictionary<string,BitmapImage>(StringComparer.OrdinalIgnoreCase);
    static BitmapImage Picture(string path){
        if(String.IsNullOrEmpty(path))return null;
        try{
            var info=new FileInfo(path);if(!info.Exists)return null;
            string key=path+"|"+info.LastWriteTimeUtc.Ticks+"|"+info.Length;
            lock(pictures){foreach(var stale in pictures.Keys.Where(k=>k.StartsWith(path+"|",StringComparison.OrdinalIgnoreCase)).ToArray())pictures.Remove(stale);BitmapImage cached;if(pictures.TryGetValue(key,out cached))return cached;
                var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.CreateOptions=BitmapCreateOptions.IgnoreColorProfile;image.UriSource=new Uri(path,UriKind.Absolute);image.EndInit();image.Freeze();
                if(pictures.Count>12)pictures.Clear();pictures[key]=image;return image;}
        }catch{return null;}
    }
}
public static class SnapshotPreview {
    public static BitmapSource Render(State s,int width=720,int height=270){var groups=s.Groups.Cast<Group>().Concat(s.Widgets).Where(g=>g.Page==s.Page).ToArray();var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRoundedRectangle(Ui.Brush("#DEE8F0"),null,new Rect(0,0,width,height),14,14);if(groups.Length==0){Label(d,"此页面没有分区",new Point(20,20),16,Ui.Muted);}else{double left=Math.Min(0,groups.Min(g=>g.X))-20,top=Math.Min(0,groups.Min(g=>g.Y))-20,right=Math.Max(1280,groups.Max(g=>g.X+g.Width))+20,bottom=Math.Max(720,groups.Max(g=>g.Y+g.Height))+20;double scale=Math.Min((width-24)/(right-left),(height-24)/(bottom-top));foreach(var g in groups){Rect r=new Rect(12+(g.X-left)*scale,12+(g.Y-top)*scale,Math.Max(40,g.Width*scale),Math.Max(24,(g.Rolled?42:g.Height)*scale));Brush border=Ui.Brush(g.Color);d.DrawRoundedRectangle(Appearance.Background(g.Color,Math.Max(.35,g.BackgroundOpacity),g.TintStrength),new Pen(border,1.5),r,g.CornerStyle=="方角"?0:7,g.CornerStyle=="方角"?0:7);Label(d,g.Name,new Point(r.X+6,r.Y+4),Math.Max(8,Math.Min(12,14*scale)),Ui.DesktopInk,Math.Max(24,r.Width-10));if(g.Rolled)continue;string[] paths;try{paths=String.IsNullOrEmpty(g.Folder)?g.Items.Where(Store.Exists).Take(8).ToArray():Directory.GetFileSystemEntries(g.Folder).Take(8).ToArray();}catch{paths=new string[0];}double cell=Math.Max(22,48*scale),size=Math.Max(12,24*scale);int cols=Math.Max(1,(int)((r.Width-12)/cell));for(int i=0;i<paths.Length;i++){double x=r.X+8+(i%cols)*cell,y=r.Y+23+(i/cols)*(cell+4);if(y+size+12>r.Bottom)break;var icon=Shell.Icon(paths[i]);if(icon!=null)d.DrawImage(icon,new Rect(x,y,size,size));Label(d,FilePresentation.DisplayName(paths[i],s.ShowFileExtensions),new Point(x-2,y+size+2),7,Ui.DesktopInk,cell-3);}var widget=g as DesktopWidget;if(widget!=null)Label(d,widget.PluginId=="clock"?DateTime.Now.ToString("HH:mm"):widget.PluginId=="calendar"?DateTime.Now.ToString("yyyy 年 M 月"):widget.Text,new Point(r.X+8,r.Y+30),12,Ui.DesktopInk,r.Width-16);}}}var result=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;}
    static void Label(DrawingContext d,string text,Point p,double size,Brush ink,double width=680){var formatted=new FormattedText(text??"",CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,ink,1.0){MaxTextWidth=Math.Max(1,width),MaxTextHeight=size*1.5,Trimming=TextTrimming.CharacterEllipsis};d.DrawText(formatted,p);}
    public static DateTime Date(string file){string name=Path.GetFileNameWithoutExtension(file);var matches=System.Text.RegularExpressions.Regex.Matches(name,"[0-9]{8}[-]?[0-9]{6}");DateTime value;if(matches.Count>0&&DateTime.TryParseExact(matches[matches.Count-1].Value.Replace("-",""),"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out value))return value;return File.GetLastWriteTime(file);}
}
public partial class ManagerWindow {
    void AddAppearancePreview(Panel p,TextBox color,Slider opacity,Slider strength,ComboBox corners){var sampleText=Ui.Text("工作资料     ⤢  ⇅",17,Ui.DesktopInk);var sample=new StackPanel();sample.Children.Add(sampleText);sample.Children.Add(Ui.Text("📄  报告       🖼  图片",13,Ui.DesktopInk));var preview=new Border(){Child=sample,Width=430,Height=118,Padding=new Thickness(18),CornerRadius=new CornerRadius(18),BorderThickness=new Thickness(2),Margin=new Thickness(0,8,0,18)};var basePlate=new Border(){Child=preview,Background=Ui.Brush("#CAD9E4"),CornerRadius=new CornerRadius(18),HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12)};int previewIndex=Math.Max(0,p.Children.Count-6);p.Children.Insert(previewIndex,basePlate);var palette=new WrapPanel(){Margin=new Thickness(0,14,0,4)};p.Children.Insert(previewIndex+1,palette);string[] names={"薄荷","天空","薰衣草","玫瑰","杏橙","石墨"},colors={"#77BFA3","#80B8E8","#B6A0E4","#D79CAF","#E1B077","#81909E"};for(int i=0;i<colors.Length;i++){string hex=colors[i];var b=Ui.Button(names[i],delegate{color.Text=hex;strength.Value=.65;opacity.Value=.85;});b.Background=Ui.Brush(hex);b.Foreground=Ui.Ink;b.MinWidth=80;b.Padding=new Thickness(16,12,16,12);palette.Children.Add(b);}palette.Children.Add(Ui.Button("透明",delegate{opacity.Value=0;}));Action update=delegate{try{preview.Background=Appearance.Background(color.Text,opacity.Value,strength.Value);preview.BorderBrush=Ui.Brush(color.Text);preview.CornerRadius=new CornerRadius(Convert.ToString(corners.SelectedItem)=="方角"?0:18);sampleText.Text="工作资料     ⤢  ⇅";}catch{sampleText.Text="请输入有效颜色";}};color.TextChanged+=delegate{update();};opacity.ValueChanged+=delegate{update();};strength.ValueChanged+=delegate{update();};corners.SelectionChanged+=delegate{update();};update();}
    Border SnapshotCard(string file){try{var state=Store.Json.Deserialize<State>(File.ReadAllText(file));Store.Validate(state);var content=new StackPanel();var image=new Image(){Source=SnapshotPreview.Render(state),Height=210,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};image.Cursor=System.Windows.Input.Cursors.Hand;image.ToolTip="点击放大布局预览";image.MouseLeftButtonUp+=delegate{var w=Ui.Dialog("布局预览 · "+SnapshotPreview.Date(file).ToString("MM-dd HH:mm"),1100,700,this);w.Content=new Image(){Source=SnapshotPreview.Render(state,1050,630),Stretch=Stretch.Uniform,Margin=new Thickness(12)};w.ShowDialog();};content.Children.Add(image);var row=new DockPanel(){Margin=new Thickness(0,12,0,0)};var restore=Ui.Button("恢复此布局",delegate{try{controller.Store.Restore(file);SaveSettings(true);ShowSettingsPage("布局和快照");SetStatus("已恢复布局；可以撤销。");}catch(Exception ex){SetStatus(ex.Message);}});DockPanel.SetDock(restore,Dock.Right);row.Children.Add(restore);var label=new StackPanel();label.Children.Add(Ui.Text(SnapshotPreview.Date(file).ToString("yyyy 年 M 月 d 日  HH:mm:ss"),14,Ui.Ink));label.Children.Add(Ui.Text(state.Groups.Count+" 个分区 · "+state.Widgets.Count+" 个组件 · 预览第 "+(state.Page+1)+" 页"+(Path.GetFileName(file).StartsWith("before-")?" · 更新前备份":""),11,Ui.Muted));row.Children.Add(label);content.Children.Add(row);return new Border(){Child=content,Background=Ui.Surface,CornerRadius=new CornerRadius(18),Padding=new Thickness(16),Margin=new Thickness(0,0,0,16)};}catch{return new Border(){Child=Ui.Text("无法预览此快照 · "+SnapshotPreview.Date(file).ToString("yyyy-MM-dd HH:mm"),12,Ui.Muted),Margin=new Thickness(0,0,0,12)};}}
}
}
