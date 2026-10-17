using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QiDesk {
public static class AppIcon {
    static ImageSource image;
    public static ImageSource Image {get{if(image!=null)return image;var assembly=System.Reflection.Assembly.GetExecutingAssembly();using(var stream=assembly.GetManifestResourceStream("QiDesk.Logo")){if(stream!=null){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();image=bitmap;}}return image??(image=Shell.Icon(assembly.Location));}}
    static double Number(XmlElement e,string key,double fallback){double value;return Double.TryParse(e.GetAttribute(key),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)?value:fallback;}
    // 支持的 SVG 子集：根节点下的 rect/path/circle/ellipse/line/polygon/polyline，
    // fill 支持颜色与 linearGradient 引用，另支持 stroke/stroke-width。
    // 早先只认 rect 和 path：AI 生成的图标大量使用圆形与描边，会被整张渲染成空白，
    // 所以这里做增量扩展；rect/path/渐变的现有行为不变。
    // 任何无法解析的元素都会先收集后整体抛出，避免写出半个图标还报告成功。
    public static int Generate(string source,string output){
        var doc=new XmlDocument();doc.XmlResolver=null;doc.Load(source);var brushes=new Dictionary<string,Brush>();foreach(XmlElement node in doc.GetElementsByTagName("linearGradient")){var brush=new LinearGradientBrush(){StartPoint=new Point(Number(node,"x1",0),Number(node,"y1",0)),EndPoint=new Point(Number(node,"x2",1),Number(node,"y2",1))};foreach(XmlNode child in node.ChildNodes){var stop=child as XmlElement;if(stop==null)continue;brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(stop.GetAttribute("stop-color")),Number(stop,"offset",0)));}brushes[node.GetAttribute("id")]=brush;}
        var images=new List<byte[]>();int[] sizes={16,24,32,48,64,128,256};var problems=new List<string>();
        foreach(int size in sizes){
            var visual=new DrawingVisual();
            using(var canvas=visual.RenderOpen()){
                canvas.PushTransform(new ScaleTransform(size/512.0,size/512.0));
                foreach(XmlNode child in doc.DocumentElement.ChildNodes){var element=child as XmlElement;if(element==null)continue;DrawElement(canvas,element,brushes,problems);}
                canvas.Pop();
            }
            if(problems.Count>0)throw new InvalidDataException("SVG 包含无法解析的元素："+problems[0]);
            var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var memory=new MemoryStream()){png.Save(memory);images.Add(memory.ToArray());}if(size==256)File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output),"qidesk.png"),images[images.Count-1]);
        }
        using(var writer=new BinaryWriter(File.Create(output))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write((images[i].Length));writer.Write(offset);offset+=images[i].Length;}foreach(byte[] bytes in images)writer.Write(bytes);}return 0;
    }
    static void DrawElement(DrawingContext canvas,XmlElement element,Dictionary<string,Brush> brushes,List<string> problems){
        string name=element.LocalName,fill=element.GetAttribute("fill"),stroke=element.GetAttribute("stroke");
        double width=Number(element,"stroke-width",1);
        bool filled=fill.Length>0&&fill!="none",stroked=stroke.Length>0&&stroke!="none"&&width>0;
        if(!filled&&!stroked)return;
        Brush brush=filled?Lookup(brushes,fill,problems):null;
        Pen pen=stroked?new Pen(Lookup(brushes,stroke,problems),width):null;
        if(brush==null&&pen==null)return;
        try{
            if(name=="rect")canvas.DrawRoundedRectangle(brush,pen,new Rect(Number(element,"x",0),Number(element,"y",0),Number(element,"width",0),Number(element,"height",0)),Number(element,"rx",0),Number(element,"rx",0));
            else if(name=="path")canvas.DrawGeometry(brush,pen,Geometry.Parse(element.GetAttribute("d")));
            else if(name=="circle")canvas.DrawEllipse(brush,pen,new Point(Number(element,"cx",0),Number(element,"cy",0)),Number(element,"r",0),Number(element,"r",0));
            else if(name=="ellipse")canvas.DrawEllipse(brush,pen,new Point(Number(element,"cx",0),Number(element,"cy",0)),Number(element,"rx",0),Number(element,"ry",0));
            else if(name=="line")canvas.DrawLine(pen??new Pen(brush,1),new Point(Number(element,"x1",0),Number(element,"y1",0)),new Point(Number(element,"x2",0),Number(element,"y2",0)));
            else if(name=="polygon")Points(canvas,brush,pen,element,true);
            else if(name=="polyline")Points(canvas,brush,pen,element,false);
        }catch(Exception ex){problems.Add(name+" 元素无法解析："+ex.Message);}
    }
    static Brush Lookup(Dictionary<string,Brush> brushes,string value,List<string> problems){
        if(!value.StartsWith("url(#"))return Ui.Brush(value);
        string key=value.Substring(5,value.Length-6);Brush found;
        if(brushes.TryGetValue(key,out found))return found;
        problems.Add("渐变 "+key+" 不存在");return null;
    }
    static void Points(DrawingContext canvas,Brush brush,Pen pen,XmlElement element,bool closed){
        string raw=element.GetAttribute("points")??"";
        var parts=raw.Split(new[]{' ',','},StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length<4||parts.Length%2!=0)throw new InvalidDataException("points 需要成对的 x,y 坐标");
        var figure=new PathFigure(){StartPoint=new Point(NumberText(parts[0]),NumberText(parts[1])),IsClosed=closed,IsFilled=closed};
        for(int i=2;i<parts.Length;i+=2)figure.Segments.Add(new LineSegment(new Point(NumberText(parts[i]),NumberText(parts[i+1])),true));
        canvas.DrawGeometry(brush,pen,new PathGeometry(new PathFigure[]{figure}));
    }
    static double NumberText(string text){double value;return Double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)?value:0;}
}
}
