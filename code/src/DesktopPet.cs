using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Directory=System.IO.Directory;
using File=System.IO.File;
using Path=System.IO.Path;
using ShapePath=System.Windows.Shapes.Path;

namespace QiDesk {
// 桌面小猫：默认使用 PathCat 自带的状态图（默认猫 / 毛绒三花），
// 素材缺失或选择「矢量猫」时回退到内置的 WPF 矢量画法。
// 点击 / 悬停 / Alt＋Space 开合工作区面板；把文件夹拖到猫身上会新建文件夹标签。
public sealed class DesktopPet : Window {
    const double BaseWidth=96;
    const double BaseHeight=112;
    const double SleepSeconds=300;
    const double HoverDelay=0.5;
    public static readonly string[] Skins={"默认猫","毛绒三花","矢量猫"};
    readonly Controller controller;
    readonly Canvas scene,vector;
    readonly Image skin;
    readonly ScaleTransform breathe,squash;
    readonly RotateTransform tailSwing;
    readonly ScaleTransform eyeLeftBlink,eyeRightBlink;
    readonly Ellipse eyeLeft,eyeRight,pupilLeft,pupilRight;
    readonly ShapePath earLeft,earRight,tail,mouth;
    readonly TextBlock sleeper;
    readonly DispatcherTimer ticker;
    readonly DispatcherTimer hoverTimer;
    readonly DispatcherTimer poseTimer;
    double phase,blinkAt;
    bool sleeping,dragging,pressed,hoverArmed,dropReady,closed;
    string pose="idle";
    Point pressPoint;
    double pressLeft,pressTop;
    long lastTouch=DateTime.UtcNow.Ticks;
    public DesktopPet(Controller owner){
        controller=owner;
        Title="栖桌小猫";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;
        Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;
        Width=BaseWidth;Height=BaseHeight;
        scene=new Canvas(){Width=100,Height=112,Background=Brushes.Transparent};
        squash=new ScaleTransform(1,1);
        scene.RenderTransform=squash;
        scene.RenderTransformOrigin=new Point(.5,.94);
        Content=scene;
        double scale=controller.Store.Data.PetScale;
        Width=BaseWidth*scale;Height=BaseHeight*scale;
        vector=new Canvas(){Width=100,Height=112,Background=Brushes.Transparent};
        // ---- 矢量猫（回退形象）----
        tail=new ShapePath(){Stroke=Ui.Brush("#5A6675"),StrokeThickness=8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse("M 74,100 C 98,96 100,68 84,60")};
        tailSwing=new RotateTransform(0,76,98);tail.RenderTransform=tailSwing;
        vector.Children.Add(tail);
        var body=new Ellipse(){Width=62,Height=56,Stroke=Ui.Brush("#93A1B0"),StrokeThickness=1.1};
        body.Fill=new LinearGradientBrush(Color.FromRgb(126,137,150),Color.FromRgb(74,84,96),90);
        body.Effect=Ui.Shadow(2,10,.30);
        Canvas.SetLeft(body,19);Canvas.SetTop(body,52);vector.Children.Add(body);
        foreach(double pawLeft in new double[]{29,53}){
            var paw=new Ellipse(){Width=17,Height=11,Fill=new SolidColorBrush(Color.FromArgb(225,150,161,174)),Stroke=Ui.Brush("#7C8896"),StrokeThickness=.9};
            Canvas.SetLeft(paw,pawLeft);Canvas.SetTop(paw,97);vector.Children.Add(paw);
        }
        earLeft=MakeEar("M 27,26 L 32,3 L 47,18 Z");
        earRight=MakeEar("M 73,26 L 68,3 L 53,18 Z");
        vector.Children.Add(earLeft);vector.Children.Add(earRight);
        vector.Children.Add(new ShapePath(){Fill=Ui.Brush("#D98A94"),Data=Geometry.Parse("M 33,23 L 36,10 L 43,17 Z")});
        vector.Children.Add(new ShapePath(){Fill=Ui.Brush("#D98A94"),Data=Geometry.Parse("M 67,23 L 64,10 L 57,17 Z")});
        var head=new Ellipse(){Width=58,Height=52,Stroke=Ui.Brush("#93A1B0"),StrokeThickness=1.1};
        head.Fill=new LinearGradientBrush(Color.FromRgb(137,148,161),Color.FromRgb(84,95,108),90);
        head.Effect=Ui.Shadow(2,10,.30);
        Canvas.SetLeft(head,21);Canvas.SetTop(head,14);vector.Children.Add(head);
        breathe=new ScaleTransform(1,1);
        head.RenderTransform=breathe;head.RenderTransformOrigin=new Point(.5,.5);
        eyeLeft=MakeEye(35,33);eyeRight=MakeEye(54,33);
        eyeLeftBlink=new ScaleTransform(1,1);eyeLeftBlink.CenterY=6;
        eyeRightBlink=new ScaleTransform(1,1);eyeRightBlink.CenterY=6;
        eyeLeft.RenderTransform=eyeLeftBlink;eyeRight.RenderTransform=eyeRightBlink;
        pupilLeft=MakePupil(38,37);pupilRight=MakePupil(57,37);
        vector.Children.Add(eyeLeft);vector.Children.Add(eyeRight);vector.Children.Add(pupilLeft);vector.Children.Add(pupilRight);
        vector.Children.Add(new ShapePath(){Fill=Ui.Brush("#E79AA4"),Data=Geometry.Parse("M 46,48 L 54,48 L 50,53 Z")});
        mouth=new ShapePath(){Stroke=Ui.Brush("#3B4553"),StrokeThickness=1.4,Data=Geometry.Parse("M 50,53 C 50,58 44,58 43,54 M 50,53 C 50,58 56,58 57,54")};
        vector.Children.Add(mouth);
        foreach(string line in new[]{"M 32,48 L 10,43","M 32,53 L 10,54","M 68,48 L 90,43","M 68,53 L 90,54"}){
            vector.Children.Add(new ShapePath(){Stroke=new SolidColorBrush(Color.FromArgb(150,232,237,246)),StrokeThickness=1.1,Data=Geometry.Parse(line)});
        }
        sleeper=new TextBlock(){Text="z z Z",FontSize=13,Foreground=Ui.DesktopMuted,Visibility=Visibility.Collapsed};
        Canvas.SetLeft(sleeper,62);Canvas.SetTop(sleeper,0);vector.Children.Add(sleeper);
        // ---- 图片猫 ----
        skin=new Image(){Width=100,Height=112,Stretch=Stretch.Uniform,SnapsToDevicePixels=true};
        RenderOptions.SetBitmapScalingMode(skin,BitmapScalingMode.HighQuality);
        Canvas.SetLeft(skin,0);Canvas.SetTop(skin,0);
        scene.Children.Add(vector);scene.Children.Add(skin);
        ToolTip="点击开合工作区面板（Alt＋Space）\n把文件夹拖到我身上可以新建文件夹标签\n右键：换形象、大小、隐藏";
        // ---- 交互 ----
        MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){pressed=true;pressPoint=PointToScreen(e.GetPosition(this));pressLeft=Left;pressTop=Top;dragging=false;CaptureMouse();Touch();e.Handled=true;};
        MouseMove+=delegate(object s,MouseEventArgs e){
            if(!pressed||e.LeftButton!=MouseButtonState.Pressed)return;
            Point point=PointToScreen(e.GetPosition(this));
            double dpi=Shell.WindowScale(this);if(dpi<=0)dpi=1;
            double dx=(point.X-pressPoint.X)/dpi,dy=(point.Y-pressPoint.Y)/dpi;
            if(!dragging&&Math.Abs(dx)+Math.Abs(dy)<6)return;
            dragging=true;SetPose("drag");
            Left=pressLeft+dx;Top=pressTop+dy;
        };
        MouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e){
            if(!pressed)return;pressed=false;ReleaseMouseCapture();
            if(dragging){dragging=false;SavePosition();SetPose("dropped",260);Pop();}
            else{SetPose("click",200);TogglePanel();}
            Touch();e.Handled=true;
        };
        MouseEnter+=delegate{Touch();if(sleeping)Wake();if(!dragging&&!dropReady)SetPose("hover");hoverArmed=true;hoverTimer.Stop();hoverTimer.Start();};
        MouseLeave+=delegate{hoverArmed=false;hoverTimer.Stop();if(!dragging&&!dropReady)SetPose(sleeping?"sleep":"idle");};
        MouseRightButtonUp+=delegate(object s,MouseButtonEventArgs e){ShowMenu();e.Handled=true;};
        AllowDrop=true;
        DragOver+=delegate(object s,DragEventArgs e){
            var paths=Functions.DragPaths(e.Data);
            bool folders=paths!=null&&paths.Any(Directory.Exists);
            e.Effects=folders?DragDropEffects.Link:DragDropEffects.None;
            if(folders!=dropReady){dropReady=folders;SetPose(folders?"drop":sleeping?"sleep":"idle");}
            e.Handled=true;
        };
        DragLeave+=delegate{dropReady=false;SetPose(sleeping?"sleep":"idle");};
        Drop+=delegate(object s,DragEventArgs e){
            e.Handled=true;dropReady=false;Touch();Pop();
            var paths=Functions.DragPaths(e.Data);
            if(paths==null||controller.Manager==null)return;
            var folders=paths.Where(Directory.Exists).ToArray();
            if(folders.Length==0)return;
            SetPose("file_in",320);
            controller.Manager.OpenWorkspaceWithFolders(folders);
        };
        // ---- 计时器 ----
        ticker=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(33)};
        ticker.Tick+=delegate{Tick();};
        ticker.Start();
        hoverTimer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds((int)(HoverDelay*1000))};
        hoverTimer.Tick+=delegate{hoverTimer.Stop();if(!hoverArmed||sleeping)return;if(controller.Manager==null)return;controller.Manager.ShowWorkspacePanel(false,false);};
        poseTimer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(200)};
        poseTimer.Tick+=delegate{poseTimer.Stop();SetPose(sleeping?"sleep":dropReady?"drop":dragging?"drag":"idle");};
        Closed+=delegate{closed=true;ticker.Stop();hoverTimer.Stop();poseTimer.Stop();};
        Place();ApplySkin();
    }
    ShapePath MakeEar(string geometry){
        var ear=new ShapePath(){Stroke=Ui.Brush("#93A1B0"),StrokeThickness=1.2,Fill=Ui.Brush("#6E7A89"),Data=Geometry.Parse(geometry)};
        ear.Effect=Ui.Shadow(1,6,.28);return ear;
    }
    Ellipse MakeEye(double left,double top){
        var eye=new Ellipse(){Width=12,Height=12,Fill=Ui.Brush("#2B3440"),Stroke=new SolidColorBrush(Color.FromArgb(120,232,237,246)),StrokeThickness=.8};
        Canvas.SetLeft(eye,left);Canvas.SetTop(eye,top);return eye;
    }
    Ellipse MakePupil(double left,double top){
        var pupil=new Ellipse(){Width=4,Height=4,Fill=new SolidColorBrush(Color.FromArgb(230,235,242,250))};
        Canvas.SetLeft(pupil,left);Canvas.SetTop(pupil,top);return pupil;
    }
    // ---- 形象 ----
    public static string AssetRoot{get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cat");}}
    static string AssetState(string pose){
        switch(pose){
            case "hover":return "hover";
            case "drag":return "dragging";
            case "drop":return "file_drag";
            case "dropped":return "dropped";
            case "sleep":return "sleeping";
            case "click":return "click";
            case "panel":return "panel_open";
            case "file_in":return "file_in";
            default:return "idle";
        }
    }
    public static string ResolveAsset(string skin,string pose){
        if(skin=="矢量猫")return null;
        string root=AssetRoot,name="cat_"+AssetState(pose)+"_0.png";
        string[] candidates=skin=="毛绒三花"?new[]{Path.Combine(root,"calico",name)}:new[]{Path.Combine(root,AssetState(pose),name),Path.Combine(root,"calico",name)};
        foreach(string path in candidates)if(File.Exists(path))return path;
        return null;
    }
    readonly Dictionary<string,BitmapImage> loaded=new Dictionary<string,BitmapImage>(StringComparer.OrdinalIgnoreCase);
    void ApplySkin(){
        if(closed)return;
        string path=ResolveAsset(controller.Store.Data.PetSkin,pose);
        if(path==null){skin.Visibility=Visibility.Collapsed;skin.Source=null;vector.Visibility=Visibility.Visible;return;}
        vector.Visibility=Visibility.Collapsed;skin.Visibility=Visibility.Visible;
        BitmapImage image;if(!loaded.TryGetValue(path,out image)){try{image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.CreateOptions=BitmapCreateOptions.IgnoreColorProfile;image.UriSource=new Uri(path,UriKind.Absolute);image.EndInit();image.Freeze();}catch{image=null;}if(loaded.Count>80)loaded.Clear();loaded[path]=image;}
        skin.Source=image;
        if(image==null){vector.Visibility=Visibility.Visible;skin.Visibility=Visibility.Collapsed;}
    }
    // ---- 状态 ----
    void SetPose(string next,int revertAfterMs=0){
        pose=next;ApplySkin();
        bool vectorMode=vector.Visibility==Visibility.Visible;
        if(vectorMode){
            bool eyesClosed=sleeping||next=="sleep";
            double eyeScale=eyesClosed?.12:next=="drag"||next=="drop"?1.2:1;
            eyeLeftBlink.ScaleY=eyeScale;eyeRightBlink.ScaleY=eyeScale;
            eyeLeftBlink.ScaleX=next=="drag"||next=="drop"?1.15:1;eyeRightBlink.ScaleX=eyeLeftBlink.ScaleX;
            pupilLeft.Visibility=eyesClosed?Visibility.Collapsed:Visibility.Visible;
            pupilRight.Visibility=pupilLeft.Visibility;
            mouth.Visibility=eyesClosed?Visibility.Collapsed:Visibility.Visible;
            sleeper.Visibility=eyesClosed?Visibility.Visible:Visibility.Collapsed;
            tailSwing.Angle=next=="drag"||next=="drop"?-26:next=="hover"?-12:0;
        }
        if(revertAfterMs>0){poseTimer.Stop();poseTimer.Interval=TimeSpan.FromMilliseconds(revertAfterMs);poseTimer.Start();}
    }
    void Tick(){
        if(closed)return;
        phase+=0.033;
        double speed=sleeping?.8:1.6;
        double breath=1+0.022*Math.Sin(phase*speed);
        breathe.ScaleY=breath;breathe.ScaleX=1-0.012*Math.Sin(phase*speed);
        skin.RenderTransformOrigin=new Point(.5,.94);
        double skinBreath=1+0.012*Math.Sin(phase*speed);
        skin.RenderTransform=new ScaleTransform(1,skinBreath);
        if(vector.Visibility==Visibility.Visible){
            tailSwing.Angle+=Math.Sin(phase*(sleeping?.6:2.2))*(sleeping?.08:.5)-(tailSwing.Angle-IdleTail())*0.08;
            if(!sleeping&&phase>blinkAt){
                blinkAt=phase+2.4+new Random().NextDouble()*3.4;
                eyeLeftBlink.ScaleY=.08;eyeRightBlink.ScaleY=.08;
                var restore=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(110)};
                restore.Tick+=delegate{restore.Stop();if(!closed&&pose=="idle")SetPose(sleeping?"sleep":"idle");};
                restore.Start();
            }
        }
        if(!sleeping&&DateTime.UtcNow.Ticks-lastTouch>TimeSpan.FromSeconds(SleepSeconds).Ticks)Sleep();
        squash.ScaleY+=(1-squash.ScaleY)*0.18;
    }
    double IdleTail(){return dropReady?-26:hoverArmed?-12:0;}
    void Pop(){squash.ScaleY=.86;}
    void Sleep(){if(sleeping)return;sleeping=true;SetPose("sleep");}
    void Wake(){sleeping=false;lastTouch=DateTime.UtcNow.Ticks;SetPose("idle");}
    public void Touch(){lastTouch=DateTime.UtcNow.Ticks;if(sleeping)Wake();}
    public void NotifyPanelVisibility(bool visible){if(!sleeping)SetPose(visible?"panel":"idle",visible?0:400);}
    void TogglePanel(){if(controller.Manager==null)return;controller.Manager.ShowWorkspacePanel(true);}
    // ---- 位置 ----
    void Place(){
        var data=controller.Store.Data;
        Width=BaseWidth*data.PetScale;Height=BaseHeight*data.PetScale;
        var areas=Shell.WorkingAreas(this);
        if(data.PetPositioned&&areas.Any(a=>a.IntersectsWith(new Rect(data.PetX,data.PetY,Width,Height)))){Left=data.PetX;Top=data.PetY;return;}
        var area=areas.Count>0?areas[0]:new Rect(0,0,1280,800);
        Left=area.Right-Width-24;Top=area.Bottom-Height-24;
        if(areas.Count==0){Left=SystemParameters.WorkArea.Right-Width-24;Top=SystemParameters.WorkArea.Bottom-Height-24;}
    }
    void SavePosition(){
        if(closed)return;
        var data=controller.Store.Data;data.PetX=Left;data.PetY=Top;data.PetPositioned=true;controller.Store.Save();
    }
    public void ApplyScale(double scale){
        var data=controller.Store.Data;data.PetScale=scale;controller.Store.Save();
        Place();SavePosition();Touch();
    }
    public void ApplySkin(string skin){
        controller.Store.Data.PetSkin=skin;controller.Store.Save();
        loaded.Clear();SetPose(pose);Touch();
    }
    // ---- 右键菜单 ----
    void ShowMenu(){
        Touch();
        var menu=new ContextMenu();
        bool open=controller.Manager!=null&&controller.Manager.WorkspacePanelVisible;
        menu.Items.Add(Ui.Menu(open?"收起工作区面板（Alt＋Space）":"打开工作区面板（Alt＋Space）",TogglePanel));
        menu.Items.Add(Ui.Menu("工作区搜索…",delegate{if(controller.Manager!=null)controller.Manager.ShowWorkspaceSearch();}));
        menu.Items.Add(new Separator());
        var skins=new MenuItem(){Header="小猫形象"};
        foreach(string value in Skins){string choice=value;bool available=choice=="矢量猫"||File.Exists(Path.Combine(AssetRoot,"calico","cat_idle_0.png"))||File.Exists(Path.Combine(AssetRoot,"idle","cat_idle_0.png"));var item=Ui.Menu(choice+(available?"":"（素材缺失）"),delegate{ApplySkin(choice);});item.IsChecked=controller.Store.Data.PetSkin==choice;item.IsEnabled=available;skins.Items.Add(item);}
        menu.Items.Add(skins);
        var sizes=new MenuItem(){Header="小猫大小"};
        foreach(double scale in new double[]{.75,1,1.5,2}){double value=scale;var item=Ui.Menu(((int)(scale*100)).ToString()+"%",delegate{ApplyScale(value);});item.IsChecked=Math.Abs(controller.Store.Data.PetScale-scale)<.01;sizes.Items.Add(item);}
        menu.Items.Add(sizes);
        menu.Items.Add(Ui.Menu(sleeping?"唤醒小猫":"让小猫睡觉",delegate{if(sleeping)Wake();else Sleep();}));
        menu.Items.Add(Ui.Menu("隐藏小猫（Ctrl＋Alt＋M 恢复）",delegate{if(controller.Manager!=null)controller.Manager.TogglePet();}));
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("桌面分区与组件…",delegate{if(controller.Manager!=null){controller.Manager.Show();controller.Manager.WindowState=WindowState.Normal;controller.Manager.Activate();}}));
        menu.Items.Add(Ui.Menu("完全退出栖桌",delegate{if(controller.Manager!=null)controller.Manager.Quit();}));
        ContextMenu=menu;menu.PlacementTarget=this;menu.IsOpen=true;
    }
}
}
