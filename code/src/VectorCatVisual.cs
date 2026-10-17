using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ShapePath=System.Windows.Shapes.Path;

namespace QiDesk {
// Geometry is created once. Animation only updates existing transforms, with no
// bitmap frames, per-frame timers or image decoding on the desktop UI thread.
public sealed class VectorCatVisual : Canvas {
    readonly Canvas head,eyes,closedEyes;
    readonly ScaleTransform breathing=new ScaleTransform(1,1,50,102);
    readonly ScaleTransform blinking=new ScaleTransform(1,1,50,40);
    readonly RotateTransform tailSwing=new RotateTransform(0,76,96);
    readonly TranslateTransform headBob=new TranslateTransform();
    readonly TranslateTransform gaze=new TranslateTransform();
    readonly ShapePath tail,tailTip,leftEar,rightEar,leftPatch,rightPatch,bodyPatch;
    readonly TextBlock sleeper;
    string pose="idle",skinName;

    public VectorCatVisual(){
        Width=100;Height=112;IsHitTestVisible=false;
        var shadow=Oval(21,103,66,6,"#18000000");Children.Add(shadow);
        var character=new Canvas(){Width=100,Height=112,RenderTransform=breathing};Children.Add(character);
        var tailGroup=new Canvas(){RenderTransform=tailSwing};character.Children.Add(tailGroup);
        tail=Draw("M 72,97 C 87,103 90,84 82,72","#D58B4C",null,0);
        tail.Fill=null;tail.Stroke=Brush("#D58B4C");tail.StrokeThickness=15;
        tail.StrokeStartLineCap=PenLineCap.Round;tail.StrokeEndLineCap=PenLineCap.Round;tailGroup.Children.Add(tail);
        tailTip=Draw("M 88,87 C 89,81 85,75 82,72",null,"#51505A",14);
        tailTip.StrokeStartLineCap=PenLineCap.Round;tailTip.StrokeEndLineCap=PenLineCap.Round;tailGroup.Children.Add(tailTip);

        var body=Oval(24,53,53,53,"#FFF5E7");body.Stroke=Brush("#A08A78");body.StrokeThickness=1;
        character.Children.Add(body);
        bodyPatch=Draw("M 27,67 C 35,60 41,71 38,85 C 36,92 28,93 25,87 Z","#DD9855",null,0);
        character.Children.Add(bodyPatch);
        character.Children.Add(Oval(39,64,27,35,"#FFFCF5"));
        foreach(double x in new[]{29.0,53.0}){
            var paw=Oval(x,96,19,12,"#FFF9EF");paw.Stroke=Brush("#A08A78");paw.StrokeThickness=.8;
            character.Children.Add(paw);
            character.Children.Add(Draw("M "+(x+7)+",103 L "+(x+7)+",107 M "+(x+12)+",103 L "+(x+12)+",107",null,"#C8B8A5",.7));
        }

        head=new Canvas(){Width=100,Height=112,RenderTransform=headBob};character.Children.Add(head);
        leftEar=Draw("M 21,33 Q 15,14 20,4 Q 31,6 42,21 Z","#DD9855","#8F7463",1.1);
        rightEar=Draw("M 60,20 Q 73,5 84,5 Q 87,19 78,36 Z","#51505A","#716565",1.1);
        head.Children.Add(leftEar);head.Children.Add(rightEar);
        head.Children.Add(Draw("M 23,24 L 23,11 Q 30,13 35,20 Z","#E9A9A3",null,0));
        head.Children.Add(Draw("M 68,20 Q 74,13 81,12 L 78,28 Z","#E9A9A3",null,0));
        var face=Oval(15,17,70,55,"#FFF9EF");face.Stroke=Brush("#A08A78");face.StrokeThickness=1;
        head.Children.Add(face);
        var patches=new Canvas(){Clip=new EllipseGeometry(new Rect(15,17,70,55))};head.Children.Add(patches);
        leftPatch=Draw("M 13,17 L 43,14 Q 42,30 35,40 Q 26,47 12,47 Z","#DD9855",null,0);
        rightPatch=Draw("M 59,14 L 88,16 L 88,49 Q 72,48 63,36 Z","#51505A",null,0);
        patches.Children.Add(leftPatch);patches.Children.Add(rightPatch);
        head.Children.Add(Draw("M 45,17 Q 50,26 47,34 Q 43,40 42,43 Q 49,39 54,43 Q 53,29 55,18 Z","#FFFCF7",null,0));
        head.Children.Add(Oval(21,45,12,6,"#22EAA3A0"));head.Children.Add(Oval(68,45,12,6,"#22EAA3A0"));

        eyes=new Canvas(){Width=100,Height=80,RenderTransform=blinking};head.Children.Add(eyes);
        var look=new Canvas(){RenderTransform=gaze};eyes.Children.Add(look);
        foreach(double x in new[]{31.0,57.0}){
            var eye=Oval(x,33,13,16,"#3F2E27");eye.Stroke=Brush("#9D754E");eye.StrokeThickness=1.5;look.Children.Add(eye);
            look.Children.Add(Oval(x+3,34,4,5,"#FFFEFA"));look.Children.Add(Oval(x+8,43,2,2,"#B6FFFFFF"));
        }
        closedEyes=new Canvas(){Visibility=Visibility.Collapsed};head.Children.Add(closedEyes);
        closedEyes.Children.Add(Draw("M 31,42 Q 37,47 44,42 M 57,42 Q 64,47 70,42",null,"#6A5145",1.8));
        head.Children.Add(Draw("M 46,49 Q 50,46 54,49 Q 52,53 50,53 Q 48,53 46,49 Z","#DA9290",null,0));
        head.Children.Add(Draw("M 50,53 Q 49,59 44,55 M 50,53 Q 51,59 56,55",null,"#886557",1.1));
        head.Children.Add(Draw("M 29,51 L 9,47 M 28,55 L 8,56 M 72,51 L 91,47 M 73,55 L 93,56",null,"#A99582",.8));
        sleeper=new TextBlock(){Text="z Z",FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Brush("#7E9BA3"),Visibility=Visibility.Collapsed};
        SetLeft(sleeper,70);SetTop(sleeper,0);Children.Add(sleeper);
        ApplySkin("默认猫");
    }

    static SolidColorBrush Brush(string value){
        var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));brush.Freeze();return brush;
    }
    static Ellipse Oval(double x,double y,double width,double height,string fill){
        var ellipse=new Ellipse(){Width=width,Height=height,Fill=Brush(fill)};
        SetLeft(ellipse,x);SetTop(ellipse,y);return ellipse;
    }
    static ShapePath Draw(string data,string fill,string stroke,double thickness){
        var geometry=Geometry.Parse(data);geometry.Freeze();
        return new ShapePath(){Data=geometry,Fill=fill==null?null:Brush(fill),
            Stroke=stroke==null?null:Brush(stroke),StrokeThickness=thickness,StrokeLineJoin=PenLineJoin.Round};
    }
    public void ApplySkin(string name){
        if(skinName==name)return;
        skinName=name;
        bool grey=name=="矢量猫";
        var orange=Brush(grey?"#9AA6B5":"#DD9855");var dark=Brush(grey?"#5C687A":"#51505A");
        leftEar.Fill=orange;leftPatch.Fill=orange;bodyPatch.Fill=orange;tail.Stroke=orange;
        rightEar.Fill=dark;rightPatch.Fill=dark;tailTip.Stroke=dark;
    }
    public void SetPose(string value){pose=value??"idle";}
    public void Animate(double seconds){
        bool asleep=pose=="sleep",alert=pose=="drag"||pose=="drop";
        double wave=Math.Sin(seconds*(asleep?1.25:2));
        breathing.ScaleY=1+wave*(asleep?.013:.009);breathing.ScaleX=1-wave*.004;
        headBob.Y=wave*.45+(pose=="click"?.9:0);
        tailSwing.Angle=(alert?-20:pose=="hover"?-9:0)+Math.Sin(seconds*(asleep?.8:2.4))*(asleep?2:7);
        double blinkPhase=seconds%4.7;
        blinking.ScaleY=asleep?.05:blinkPhase>4.48?Math.Max(.05,Math.Abs((blinkPhase-4.59)/.11)):1;
        gaze.X=pose=="hover"||pose=="panel"?1:0;
        eyes.Visibility=asleep?Visibility.Collapsed:Visibility.Visible;
        closedEyes.Visibility=asleep?Visibility.Visible:Visibility.Collapsed;
        sleeper.Visibility=asleep?Visibility.Visible:Visibility.Collapsed;
        sleeper.Opacity=.65+.3*Math.Sin(seconds*1.5);
    }
}
}
