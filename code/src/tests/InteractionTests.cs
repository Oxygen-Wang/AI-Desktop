using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace QiDesk {
public static class InteractionTests {
    public static void Pure(Action<bool,string> check){
        IntPtr desktop=new IntPtr(1),folder=new IntPtr(2);var pair=new ClickPair();
        check(!pair.Add(100,30,40,desktop,true,500,4,4)&&pair.Add(200,32,42,desktop,true,500,4,4),"two nearby desktop clicks form one pair");
        check(!pair.Add(250,32,42,desktop,true,500,4,4),"third click does not repeat a completed pair");
        pair.Reset();pair.Add(100,30,40,desktop,true,500,4,4);check(!pair.Add(700,30,40,desktop,true,500,4,4),"slow clicks do not hide groups");
        pair.Reset();pair.Add(100,30,40,desktop,true,500,4,4);check(!pair.Add(200,50,40,desktop,true,500,4,4),"separate click positions do not hide groups");
        pair.Reset();pair.Add(100,30,40,desktop,true,500,4,4);pair.Add(130,30,40,folder,false,500,4,4);check(!pair.Add(200,30,40,desktop,true,500,4,4),"intervening application click resets desktop gesture");
        pair.Reset();pair.Add(100,30,40,desktop,true,500,4,4);pair.Move(80,40,4,4);check(!pair.Add(200,30,40,desktop,true,500,4,4),"mouse drag cancels pending double click");
        pair.Reset();pair.Add(UInt32.MaxValue-100,30,40,desktop,true,500,4,4);check(pair.Add(80,30,40,desktop,true,500,4,4),"double click remains valid across native timestamp rollover");
        var swipe=new DualButtonSwipe();swipe.Begin(100,100,true);swipe.Approve(true);check(swipe.End(240,115)==1,"two-button horizontal swipe navigates right");
        swipe.Begin(240,100,true);swipe.Approve(true);check(swipe.End(95,110)==-1,"two-button horizontal swipe navigates left");
        swipe.Begin(100,100,true);swipe.Approve(true);check(swipe.End(175,102)==0,"short two-button drag does not change page");
        swipe.Begin(100,100,true);swipe.Approve(true);check(swipe.End(240,260)==0,"vertical two-button drag does not change page");
        swipe.Begin(100,100,true);check(swipe.End(240,100)==0,"unapproved desktop position cannot trigger page navigation");
    }
    public static void Native(Window live,IntPtr desktop,Action<bool,string> check){
        IntPtr target=new WindowInteropHelper(live).Handle;
        using(var lease=new DesktopIcons(target)){Pump();check(lease.Active&&!Shell.NativeVisible(live),"icon visibility lease hides only the test window");}
        Pump();check(Shell.NativeVisible(live),"normal lease disposal restores original visibility");
        live.Hide();using(var hidden=new DesktopIcons(target)){check(!hidden.Active,"already hidden window needs no visibility lease");}Pump();check(!Shell.NativeVisible(live),"preexisting hidden state is preserved");live.Show();Pump();
        string eventName="Local\\QiDeskTestIcons-"+Guid.NewGuid().ToString("N");
        using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,eventName)){
            Process owner=null;
            try{
                owner=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--test-icon-owner "+target.ToInt64()+" "+eventName){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
                check(ready.WaitOne(5000),"test visibility owner and recovery process are ready");Pump();check(!Shell.NativeVisible(live),"test owner hid its leased window");
                owner.Kill();owner.WaitForExit(3000);
                var watch=Stopwatch.StartNew();while(!Shell.NativeVisible(live)&&watch.ElapsedMilliseconds<3000){Pump();Thread.Sleep(20);}
                check(Shell.NativeVisible(live),"independent guard restores visibility after owner is killed");
            }finally{if(owner!=null){if(!owner.HasExited)owner.Kill();owner.Dispose();}live.Show();Pump();}
        }
        using(var form=new Forms.Form(){Text="QiDesk accessibility regression",Width=420,Height=300,ShowInTaskbar=false,StartPosition=Forms.FormStartPosition.Manual,Left=40,Top=400}){
            var list=new Forms.ListView(){Dock=Forms.DockStyle.Fill,View=Forms.View.LargeIcon};list.Items.Add("Test item");form.Controls.Add(list);form.Show();Forms.Application.DoEvents();
            var item=list.Items[0].Bounds;var hit=list.PointToScreen(new System.Drawing.Point(item.Left+item.Width/2,item.Top+item.Height/2));var blank=list.PointToScreen(new System.Drawing.Point(300,180));
            check(!Shell.DesktopSurface(list.Handle,desktop),"ordinary folder list view is excluded from desktop gesture");
            check(!DesktopGesture.BlankListPoint(list.Handle,hit.X,hit.Y),"accessibility hit test excludes file item");
            check(DesktopGesture.BlankListPoint(list.Handle,blank.X,blank.Y),"accessibility hit test recognizes empty list view space");
            form.Close();
        }
        using(var gesture=new DesktopGesture(Dispatcher.CurrentDispatcher,delegate{})){gesture.Start(desktop);check(gesture.Active,"native desktop gesture hook installs on dedicated thread");gesture.Dispose();check(!gesture.Active,"native desktop gesture hook stops cleanly");}
    }
    public static int OwnerProbe(string[] args){
        if(args.Length!=3||!args[2].StartsWith("Local\\QiDeskTestIcons-"))return 2;
        long target;if(!Int64.TryParse(args[1],out target))return 2;
        try{using(var lease=new DesktopIcons(new IntPtr(target)))using(var ready=EventWaitHandle.OpenExisting(args[2])){ready.Set();new ManualResetEvent(false).WaitOne();}}catch{return 1;}return 0;
    }
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
}
}
