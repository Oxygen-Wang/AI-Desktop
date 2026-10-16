using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace QiDesk {
public static class DragTests {
    public static void Run(string root,Action<bool,string> check){
        var drag=new DesktopDrag();int token=drag.Begin(100,10,20,true);drag.Approve(token,true);drag.Move(320,250,true);check(drag.Progress(300,350).IsEmpty&&drag.End(300,320,250,true,350,220,100).IsEmpty,"quick native desktop selection does not create a group");
        token=drag.Begin(100,400,300,true);drag.Approve(token,true);drag.Move(80,50,true);Rect preview=drag.Progress(500,350),final=drag.End(520,80,50,true,350,220,100);check(!preview.IsEmpty&&final.X==80&&final.Y==50&&final.Width==320&&final.Height==250&&drag.AwaitingChoice,"held reverse drag keeps geometry and opens one creation choice");
        check(drag.Begin(600,10,20,true)==-1&&drag.AwaitingChoice,"clicking creation menu does not restart a selection");drag.Cancel();
        token=drag.Begin(100,10,20,true);drag.Move(400,300,true);drag.End(500,400,300,true,350,220,100);drag.Approve(token,true);check(!drag.Pressed&&!drag.AwaitingChoice&&drag.Progress(600,350).IsEmpty,"late blank-space approval after release cannot create a group");
        token=drag.Begin(100,10,20,true);drag.Approve(token,false);drag.Move(400,300,true);check(drag.End(600,400,300,true,350,220,100).IsEmpty,"file item hit rejection prevents drag creation");
        token=drag.Begin(100,10,20,true);drag.Approve(token,true);drag.Move(400,300,false);check(drag.End(600,400,300,true,350,220,100).IsEmpty,"moving into an application cancels desktop creation");
        token=drag.Begin(100,10,20,true);drag.Approve(token,true);drag.Move(150,70,true);check(drag.End(600,150,70,true,350,220,100).IsEmpty,"too-small creation rectangle is discarded");
        token=drag.Begin(100,10,20,true);drag.Approve(token,true);drag.Move(400,300,true);drag.Cancel();check(drag.End(600,400,300,true,350,220,100).IsEmpty,"Escape cancellation cannot be completed by subsequent mouse release");
        token=drag.Begin(UInt32.MaxValue-200,-1500,30,true);drag.Approve(token,true);drag.Move(-900,400,true);check(!drag.End(250,-900,400,true,350,220,100).IsEmpty,"long press survives native timestamp rollover on secondary monitor");drag.Cancel();
        var logical=DesktopDrag.Logical(new Rect(-1500,75,450,300),1.5);check(logical.X==-1000&&logical.Y==50&&logical.Width==300&&logical.Height==200,"physical selection maps negative coordinates and DPI scale correctly");
        var store=new Store(Path.Combine(root,"drag-create"));store.Data.Page=2;var controller=new Controller(store);int count=store.Data.Groups.Count;var group=controller.CreateAt(new Rect(-1200,80,360,260),"New selection","");var reload=new Store(store.Root);check(reload.Data.Groups.Count==count+1&&reload.Data.Groups[count].X==-1200&&group.Page==2,"selected group geometry and current page persist");check(store.Undo()&&store.Data.Groups.Count==count,"desktop-created group can be undone");
        string folder=Path.Combine(root,"mapped");Directory.CreateDirectory(folder);controller.CreateAt(new Rect(100,200,320,240),"Folder selection",folder);check(store.Data.Groups[store.Data.Groups.Count-1].Folder==folder,"same rectangle creates a folder portal");
        store.Data.DesktopDragCreate=false;store.Save();check(!new Store(store.Root).Data.DesktopDragCreate&&!new State().DesktopDragCreate,"drag creation is enabled by default and opt-out persists");
        var desktopFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"QiDesk-release-probe.txt");var source=store.Data.Groups[0];source.Items.Add(desktopFile);check(DesktopItemDrag.ShouldRemove(source,desktopFile,false,true,store)&&!DesktopItemDrag.ShouldRemove(source,desktopFile,true,true,store)&&!DesktopItemDrag.ShouldRemove(source,desktopFile,false,false,store),"dropping an assigned desktop item on blank Explorer desktop releases its logical membership even when Explorer considers same-location drop a no-op");
        var logicalDrag=Functions.LogicalDrag(new[]{desktopFile});check(!logicalDrag.GetDataPresent(DataFormats.FileDrop)&&Functions.DragPaths(logicalDrag)!=null&&Functions.DragPaths(logicalDrag)[0]==desktopFile,"a logical desktop drag carries no shell file drop but keeps a QiDesk-internal path format");
        var moveStore=new Store(Path.Combine(root,"move-out"));var moveSource=moveStore.Data.Groups[0];string moveFile=Path.Combine(root,"move-out.txt");File.WriteAllText(moveFile,"x");moveSource.Items.Add(moveFile);var moveTarget=DesktopItemDrag.MoveOutTarget(moveStore,moveSource,moveFile);check(moveTarget!=null&&moveTarget!=moveSource,"moving a file out of a section picks another existing section at the data level");
        var freeSlot=DesktopIconLayout.FindFree(new Point(100,100),80,80,0,0,800,600,new[]{new Rect(0,0,300,600)},new Rect[0]);check(!Double.IsNaN(freeSlot.X)&&freeSlot.X>=296,"desktop icons can find a free grid slot outside a blocking panel");
    }
    public static void Native(string root,Action<bool,string> check){
        using(var form=new Forms.Form(){Text="QiDesk drag hit test",Width=360,Height=240,ShowInTaskbar=false,StartPosition=Forms.FormStartPosition.Manual,Left=40,Top=400}){
            form.Show();Forms.Application.DoEvents();var point=form.PointToScreen(new System.Drawing.Point(80,80));var native=new DesktopGesture.NativePoint(point.X,point.Y);IntPtr before=DesktopGesture.TargetAt(native);
            var overlay=new SelectionPreview(){Left=40,Top=400,Width=360,Height=240,Topmost=true};try{
                overlay.Show();Shell.SetClickThrough(overlay);Pump();check(Shell.ClickThrough(overlay)&&DesktopGesture.TargetAt(native)==before,"selection preview is visible and passes mouse hit testing through");
                var store=new Store(Path.Combine(root,"menu-routing"));var controller=new Controller(store);int count=store.Data.Groups.Count,cancelled=0;
                overlay.ShowChoice(new Rect(40,400,360,240),delegate(bool folder){controller.CreateAt(new Rect(40,400,360,240),"Menu group",folder?root:"");},delegate{cancelled++;});Pump();
                var item=overlay.Choice.Items[0] as MenuItem;item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));overlay.Choice.IsOpen=false;Pump();check(store.Data.Groups.Count==count+1&&store.Data.Groups[count].Name=="Menu group"&&cancelled==0,"creation menu action reaches persisted group creation");
                overlay.ShowChoice(new Rect(40,400,360,240),delegate(bool folder){controller.CreateAt(new Rect(40,400,360,240),"Unexpected","");},delegate{cancelled++;});Pump();overlay.Choice.IsOpen=false;var closing=Stopwatch.StartNew();while(cancelled==0&&closing.ElapsedMilliseconds<500){Pump();Thread.Sleep(10);}check(cancelled==1&&store.Data.Groups.Count==count+1,"dismissing creation menu leaves groups unchanged");
            }finally{overlay.Close();form.Close();}
        }
    }
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
}
}

