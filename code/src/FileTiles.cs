using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QiDesk {
// 桌面分区和搜索结果共用同一块文件图标底板，外观只在这里维护。
public static class FileTiles {
    public static Border Plate(string path,Group style,double size,bool listMode,State state=null){
        bool picture=Thumbnails.IsPicture(path),application=FilePresentation.IsApplication(path);
        Color accent=((SolidColorBrush)Ui.Brush(style.IconColor)).Color;
        var themed=FileIconPresentation.Themed(path,style,state,size);
        var icon=new Image(){Source=themed??Shell.Icon(path),Width=picture?size:size*.84,Height=picture?size:size*.84,Stretch=Stretch.Uniform,SnapsToDevicePixels=true};
        RenderOptions.SetBitmapScalingMode(icon,BitmapScalingMode.HighQuality);
        FileIconPresentation.Bind(icon,path,size,themed);
        double plateSize=size+(listMode?6:10);
        string skin=style.IconSkin;
        Brush plateBackground=skin=="Fluent 线条"?new SolidColorBrush(Color.FromArgb(225,236,243,250)):skin=="Fluent 填充"?new SolidColorBrush(Color.FromArgb(240,28,40,60)):picture?(Brush)Brushes.White:new LinearGradientBrush(Color.FromArgb(235,36,54,78),Color.FromArgb(245,19,32,50),90);
        var plate=new Border(){Width=plateSize,Height=plateSize,CornerRadius=new CornerRadius(skin=="Fluent 填充"?plateSize/2:skin=="Fluent 线条"?Math.Max(9,plateSize*.25):picture?Math.Max(5,plateSize*.18):Math.Max(7,plateSize*.27)),BorderThickness=new Thickness(skin=="Fluent 线条"?1.5:1),BorderBrush=skin=="经典"?(picture?Ui.Brush("#D6E2EE"):application?Ui.Brush(style.Color):Ui.Brush("#8296AD")):new SolidColorBrush(accent),Background=plateBackground,Child=icon,Margin=new Thickness(0,0,listMode?8:0,listMode?0:7),Effect=Ui.Shadow(1,5,.18)};
        if(AppIconSkins.IsPack(skin)){plate.Background=Brushes.Transparent;plate.BorderThickness=new Thickness(0);plate.Effect=null;icon.Width=size;icon.Height=size;}
        if(picture)icon.Clip=new RectangleGeometry(new Rect(0,0,size,size),Math.Max(4,size*.16),Math.Max(4,size*.16));
        return plate;
    }
}
}
