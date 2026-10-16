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
    public static int Generate(string source,string output){
        var doc=new XmlDocument();doc.XmlResolver=null;doc.Load(source);var brushes=new Dictionary<string,Brush>();foreach(XmlElement node in doc.GetElementsByTagName("linearGradient")){var brush=new LinearGradientBrush(){StartPoint=new Point(Number(node,"x1",0),Number(node,"y1",0)),EndPoint=new Point(Number(node,"x2",1),Number(node,"y2",1))};foreach(XmlNode child in node.ChildNodes){var stop=child as XmlElement;if(stop==null)continue;brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(stop.GetAttribute("stop-color")),Number(stop,"offset",0)));}brushes[node.GetAttribute("id")]=brush;}
        var images=new List<byte[]>();int[] sizes={16,24,32,48,64,128,256};foreach(int size in sizes){var visual=new DrawingVisual();using(var canvas=visual.RenderOpen()){canvas.PushTransform(new ScaleTransform(size/512.0,size/512.0));foreach(XmlNode child in doc.DocumentElement.ChildNodes){var element=child as XmlElement;if(element==null)continue;string fill=element.GetAttribute("fill");if(fill.Length==0)continue;Brush brush=fill.StartsWith("url(#")?brushes[fill.Substring(5,fill.Length-6)]:Ui.Brush(fill);if(element.LocalName=="rect")canvas.DrawRoundedRectangle(brush,null,new Rect(Number(element,"x",0),Number(element,"y",0),Number(element,"width",0),Number(element,"height",0)),Number(element,"rx",0),Number(element,"rx",0));else if(element.LocalName=="path")canvas.DrawGeometry(brush,null,Geometry.Parse(element.GetAttribute("d")));}canvas.Pop();}var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var memory=new MemoryStream()){png.Save(memory);images.Add(memory.ToArray());}if(size==256)File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(output),"qidesk.png"),images[images.Count-1]);}
        using(var writer=new BinaryWriter(File.Create(output))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}foreach(byte[] bytes in images)writer.Write(bytes);}return 0;
    }
}
}
