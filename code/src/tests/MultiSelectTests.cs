using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace QiDesk {
public static class MultiSelectTests {
    [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
    public static void Run(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"multi-select"));
        store.Data.Groups.Clear();store.Data.Rules.Clear();store.Data.Excluded.Clear();store.Data.Pages=2;store.Data.Page=0;
        var a=new Group(){Name="选区甲",X=100,Y=100,Width=300,Height=200,Page=0};
        var b=new Group(){Name="选区乙",X=450,Y=100,Width=300,Height=200,Page=0};
        var c=new Group(){Name="另一页",X=100,Y=100,Width=300,Height=200,Page=1};
        store.Data.Groups.Add(a);store.Data.Groups.Add(b);store.Data.Groups.Add(c);
        var controller=new Controller(store);
        var hits=controller.GroupsInRect(new Rect(80,80,700,260));
        check(hits.Length==2&&hits.Contains(a)&&hits.Contains(b),"marquee selection includes only intersecting partitions on the current page");
        check(controller.GroupsInRect(new Rect(1000,500,50,50)).Length==0,"marquee selection returns no partitions outside the rectangle");
        a.Rolled=true;
        check(controller.GroupsInRect(new Rect(100,100,300,40)).Contains(a),"a folded partition can be selected by its header");
        a.Rolled=false;
        var box=new BoxSelect();int token=box.Begin(100,100);box.Move(300,250);
        Rect rect=box.Bounds;
        check(token>0&&box.Active&&rect.X==100&&rect.Y==100&&rect.Width==200&&rect.Height==150,"ctrl marquee tracks a logical rectangle while dragging");
        check(!box.End(320,260,8).IsEmpty&&!box.Active,"ctrl marquee completes with a usable rectangle");
    }

    public static void Native(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"multi-native"));
        store.Data.Groups.Clear();store.Data.Rules.Clear();store.Data.Excluded.Clear();
        var a=new Group(){Name="多选甲",X=120,Y=120,Width=320,Height=240,Page=0};
        var b=new Group(){Name="多选乙",X=500,Y=120,Width=320,Height=240,Page=0};
        store.Data.Groups.Add(a);store.Data.Groups.Add(b);store.Data.Pages=1;store.Data.Page=0;
        var controller=new Controller(store){Running=true,Docked=false};
        var manager=new ManagerWindow(controller);
        try{
            controller.Rebuild();
            controller.ToggleGroupSelection(a);controller.ToggleGroupSelection(b);
            var window=controller.Windows.Single(w=>w.Group.Id==a.Id);window.UpdateLayout();
            double ax=a.X,ay=a.Y,bx=b.X,by=b.Y;
            DragGrip(window);
            check(a.X>ax&&a.Y>ay&&b.X>bx&&b.Y>by,"dragging one selected partition moves the whole selected group");
            check(Math.Abs((a.X-ax)-(b.X-bx))<1.5&&Math.Abs((a.Y-ay)-(b.Y-by))<1.5,"group move keeps the relative offsets of selected partitions");
        }finally{manager.Quit();}
    }

    static void DragGrip(FenceWindow window){
        var original=System.Windows.Forms.Cursor.Position;
        bool pressed=false;
        try{
            Shell.Float(window);window.Topmost=true;Shell.BeginInlineInput(window);window.UpdateLayout();
            var grip=TextNodes(window).First(t=>t.Text=="⠿");
            Point location=grip.PointToScreen(new Point(8,8));
            System.Windows.Forms.Cursor.Position=new System.Drawing.Point((int)location.X,(int)location.Y);
            PumpFor(50);
            mouse_event(2,0,0,0,UIntPtr.Zero);pressed=true;PumpFor(50);
            System.Windows.Forms.Cursor.Position=new System.Drawing.Point((int)location.X+80,(int)location.Y+60);
            PumpFor(90);
            mouse_event(4,0,0,0,UIntPtr.Zero);pressed=false;PumpFor(120);
        }finally{
            if(pressed)mouse_event(4,0,0,0,UIntPtr.Zero);
            System.Windows.Forms.Cursor.Position=original;
        }
    }
    static IEnumerable<TextBlock> TextNodes(DependencyObject node){
        var text=node as TextBlock;if(text!=null)yield return text;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in TextNodes(VisualTreeHelper.GetChild(node,i)))yield return child;
    }
    static void PumpFor(int milliseconds){var end=DateTime.UtcNow.AddMilliseconds(milliseconds);PumpUntil(()=>DateTime.UtcNow>=end,milliseconds+120);}
    static void PumpUntil(Func<bool> ready,int milliseconds){
        var end=DateTime.UtcNow.AddMilliseconds(milliseconds);
        while(!ready()&&DateTime.UtcNow<end){var frame=new DispatcherFrame();var timer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    }
}
}
