using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace QiDesk {
// Folder, document and image outlines from Microsoft Fluent UI System Icons.
// The six original SVGs and their MIT license live in code/assets/fluent.
public static class FluentIcons {
    static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>();
    public static ImageSource Icon(string path,string skin,Color color){
        if(skin!="Fluent 线条"&&skin!="Fluent 填充")return null;
        string kind=Directory.Exists(path)?"folder":Thumbnails.IsPicture(path)?"image":"document";
        string style=skin=="Fluent 填充"?"filled":"regular";
        string key=kind+style+color.ToString();ImageSource cached;if(cache.TryGetValue(key,out cached))return cached;
        string resource="QiDesk.Fluent.ic_fluent_"+kind+"_32_"+style+".svg";
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){
            if(stream==null)return null;
            var xml=new XmlDocument();xml.Load(stream);var group=new DrawingGroup();group.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,32,32))));
            foreach(XmlNode node in xml.SelectNodes("//*[local-name()='path']")){var attribute=node.Attributes["d"];if(attribute==null)continue;var geometry=Geometry.Parse(attribute.Value);geometry.Freeze();var brush=new SolidColorBrush(color);brush.Freeze();group.Children.Add(new GeometryDrawing(brush,null,geometry));}
            group.Freeze();var image=new DrawingImage(group);image.Freeze();cache[key]=image;return image;
        }
    }
}
}
