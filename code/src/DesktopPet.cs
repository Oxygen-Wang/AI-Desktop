using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Directory=System.IO.Directory;

namespace QiDesk {
// Desktop interaction stays in this window; VectorCatVisual owns drawing and animation.
public sealed class DesktopPet : Window {
    const double BaseWidth=80;
    const double BaseHeight=90;
    const double SleepSeconds=300;
    const double HoverDelay=0.5;
    public static readonly string[] Skins={"默认猫","毛绒三花","矢量猫"};
    readonly Controller controller;
    readonly Canvas scene;
    readonly VectorCatVisual vector;
    readonly ScaleTransform squash;
    readonly DispatcherTimer ticker;
    readonly DispatcherTimer hoverTimer;
    readonly DispatcherTimer poseTimer;
    double phase;
    bool sleeping,dragging,pressed,dropReady,closed;
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
        Content=new Viewbox(){Child=scene,Stretch=Stretch.Uniform};
        double scale=controller.Store.Data.PetScale;
        Width=BaseWidth*scale;Height=BaseHeight*scale;
        vector=new VectorCatVisual();
        scene.Children.Add(vector);
        ToolTip="点击：将选中分区提到前台或放回桌面（Alt＋Space）\n拖入文件夹：添加桌面文件夹分区\n右键：换形象、大小、隐藏";
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
        MouseEnter+=delegate{Touch();if(sleeping)Wake();if(!dragging&&!dropReady)SetPose("hover");hoverTimer.Stop();hoverTimer.Start();};
        MouseLeave+=delegate{hoverTimer.Stop();if(!dragging&&!dropReady)SetPose(sleeping?"sleep":"idle");};
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
        // Hover only changes the pet's pose. It must not toggle a workspace after a click.
        hoverTimer.Tick+=delegate{hoverTimer.Stop();};
        poseTimer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(200)};
        poseTimer.Tick+=delegate{poseTimer.Stop();SetPose(sleeping?"sleep":dropReady?"drop":dragging?"drag":"idle");};
        Closed+=delegate{closed=true;ticker.Stop();hoverTimer.Stop();poseTimer.Stop();};
        Place();ApplySkin();
    }
    void ApplySkin(){
        if(!closed)vector.ApplySkin(controller.Store.Data.PetSkin);
    }
    // ---- 状态 ----
    void SetPose(string next,int revertAfterMs=0){
        pose=next;ApplySkin();
        vector.SetPose(next);
        vector.Animate(phase);
        if(revertAfterMs>0){poseTimer.Stop();poseTimer.Interval=TimeSpan.FromMilliseconds(revertAfterMs);poseTimer.Start();}
    }
    void Tick(){
        if(closed)return;
        phase+=0.033;
        vector.Animate(phase);
        if(!sleeping&&DateTime.UtcNow.Ticks-lastTouch>TimeSpan.FromSeconds(SleepSeconds).Ticks)Sleep();
        squash.ScaleY+=(1-squash.ScaleY)*0.18;
    }
    void Pop(){squash.ScaleY=.86;}
    void Sleep(){if(sleeping)return;sleeping=true;SetPose("sleep");}
    void Wake(){sleeping=false;lastTouch=DateTime.UtcNow.Ticks;SetPose("idle");}
    public void Touch(){lastTouch=DateTime.UtcNow.Ticks;if(sleeping)Wake();}
    public void NotifyPanelVisibility(bool visible){if(!sleeping)SetPose(visible?"panel":"idle",visible?0:400);}
    // 小猫点击 / 悬停：和 Alt＋Space 一致，选中的工作区在前台与桌面层之间切换，
    // 什么都没有时才打开工作区面板。
    public void ClickPanel(){
        if(controller.Manager==null)return;
        hoverTimer.Stop();
        controller.Manager.ToggleWorkspaceFromPet();
        NotifyPanelVisibility(controller.Manager.DesktopWorkspaceVisible);
    }
    void TogglePanel(){ClickPanel();}
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
        SetPose(pose);Touch();
    }
    // ---- 右键菜单 ----
    void ShowMenu(){
        Touch();
        var menu=new ContextMenu();
        bool open=controller.Manager!=null&&controller.Manager.DesktopWorkspaceVisible;
        int selected=controller.Manager==null?0:controller.Manager.SelectedWorkspaceCount;
        string workspaceLabel=open?"将选中分区放回桌面（Alt＋Space）":selected>0?"提起选中的 "+selected+" 个桌面分区（Alt＋Space）":"请先选择桌面分区";
        menu.Items.Add(Ui.Menu(workspaceLabel,TogglePanel));
        menu.Items.Add(Ui.Menu("工作区搜索…",delegate{if(controller.Manager!=null)controller.Manager.ShowWorkspaceSearch();}));
        menu.Items.Add(new Separator());
        var skins=new MenuItem(){Header="小猫形象"};
        foreach(string value in Skins){
            string choice=value;
            var item=Ui.Menu(choice,delegate{ApplySkin(choice);});
            item.IsChecked=controller.Store.Data.PetSkin==choice;
            skins.Items.Add(item);
        }
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
