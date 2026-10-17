using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QiDesk {
public static class VectorCatTests {
    public static void Run(string root,Action<bool,string> check){
        var cat=new VectorCatVisual();cat.Animate(0);
        var idle=Render(cat,1);byte[] idlePixels=Pixels(idle);
        check(idlePixels.Where((value,index)=>index%4==3).Any(value=>value>0),"vector cat renders without bitmap assets");
        check(idlePixels[3]==0,"vector pet keeps transparent background corners");
        cat.Animate(1);check(!idlePixels.SequenceEqual(Pixels(Render(cat,1))),"breathing and tail animation change the rendered frame");
        cat.Animate(4.59);check(!idlePixels.SequenceEqual(Pixels(Render(cat,1))),"vector blink changes the rendered eyes");
        cat.SetPose("sleep");cat.Animate(0);
        check(!idlePixels.SequenceEqual(Pixels(Render(cat,1))),"sleep pose closes the eyes and displays sleep marks");
        cat.SetPose("idle");cat.ApplySkin("矢量猫");cat.Animate(0);
        check(!idlePixels.SequenceEqual(Pixels(Render(cat,1))),"legacy vector skin retains a distinct grey palette");
        cat.ApplySkin("毛绒三花");
        var large=Render(cat,2);
        check(large.PixelWidth==200&&large.PixelHeight==224,"vector cat renders at double resolution without a raster source");
        string output=Path.Combine(root,"vector-cat-preview.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(large));
        using(var stream=File.Create(output))png.Save(stream);
        check(File.Exists(output),"vector cat preview can be exported for visual inspection");
    }
    static RenderTargetBitmap Render(FrameworkElement visual,int scale){
        visual.Measure(new Size(100,112));visual.Arrange(new Rect(0,0,100,112));visual.UpdateLayout();
        var bitmap=new RenderTargetBitmap(100*scale,112*scale,96*scale,96*scale,PixelFormats.Pbgra32);
        bitmap.Render(visual);return bitmap;
    }
    static byte[] Pixels(BitmapSource bitmap){
        byte[] pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];
        bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);return pixels;
    }
}
}
