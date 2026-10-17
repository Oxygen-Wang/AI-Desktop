using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace QiDesk {
public static class PageSwipeTests {
    public static void Pure(Action<bool,string> check){
        check(PageSwipe.FollowOffset(120,1.5)==80,"middle-button swipe follows the pointer one to one in logical units");
        check(PageSwipe.FollowOffset(120,0)==120&&PageSwipe.FollowOffset(-90,-1)==-90,"invalid display scale falls back to one to one");
        check(PageSwipe.Commits(90)&&PageSwipe.Commits(-140)&&!PageSwipe.Commits(89),"a page change needs the full swipe commit distance");
        check(Math.Abs(PageSwipe.Travel(1920)-576)<.001&&PageSwipe.Travel(200)==180&&PageSwipe.Travel(3840)==900,"page slide distance stays a small fraction of the screen");
        check(PageSwipe.SlideMilliseconds(576,0)==PageSwipe.DefaultSlideMilliseconds,"keyboard and button page changes keep the default slide time");
        check(PageSwipe.SlideMilliseconds(576,2)==PageSwipe.MaximumSlideMilliseconds&&!PageSwipe.MatchesHand(576,2),"a slow release slides slowly instead of speeding up");
        check(Math.Abs(PageSwipe.SlideMilliseconds(576,4)-432)<.001&&PageSwipe.MatchesHand(576,4),"a fling keeps the release speed at the start of the slide");
        check(PageSwipe.SlideMilliseconds(576,20)==PageSwipe.MinimumSlideMilliseconds,"a very fast fling stays within the minimum slide time");
        var motion=new MiddleButtonSwipe();
        motion.Begin(100,100,true);motion.Approve(true);
        motion.Move(160,100,1000);motion.Move(220,100,1050);motion.Move(280,100,1100);
        motion.End(340,100,1150);
        check(motion.ReleaseVelocity>1,"the swipe reports the release speed for the page slide");
        motion.Begin(100,100,true);motion.Approve(true);
        motion.Move(160,100,2000);motion.Move(220,100,2050);
        motion.End(280,100,2400);
        check(motion.ReleaseVelocity==0,"a pause before releasing means no fling speed");
    }
    public static void Native(string root,Action<bool,string> check){
        string storeRoot=Path.Combine(root,"page-swipe");Directory.CreateDirectory(storeRoot);
        var store=new Store(storeRoot,false);
        store.Data=new State();
        State data=store.Data;data.Pages=2;data.Page=0;
        var first=new Group(){Name="Swipe first page",X=60,Y=60,Width=320,Height=300,Page=0};
        var second=new Group(){Name="Swipe second page",X=420,Y=120,Width=300,Height=280,Page=1};
        data.Groups.Add(first);data.Groups.Add(second);Store.Validate(data);
        var controller=new Controller(store){Running=true,Docked=false};
        var shown=new List<Window>();
        Action track=delegate{foreach(Window window in controller.Windows.Cast<Window>().Concat(controller.Widgets))if(!shown.Contains(window))shown.Add(window);};
        try{
            controller.Rebuild();
            check(controller.Windows.Count==1&&controller.Windows[0].Group==first,"the current page builds its own partition window");
            Window firstWindow=controller.Windows[0];track();
            double baseLeft=firstWindow.Left;
            controller.SwipeStart();controller.SwipeMove(100);
            check(Math.Abs(firstWindow.Left-(baseLeft+100))<1,"middle-button swipe moves the partition one to one with the pointer");
            controller.SwipeEnd(0);Pump(400);
            check(Math.Abs(firstWindow.Left-baseLeft)<1,"a short swipe returns the partition to its original position");
            // 松手带速度：按手速滑出并翻页
            controller.SwipeStart();controller.SwipeMove(140);controller.SwipeEnd(140,3);
            check(controller.Windows.Count==1&&controller.Windows[0].Group==second,"the incoming page is built before the slide starts");
            track();Pump(900);
            check(store.Data.Page==1,"a swipe released with speed commits the new page index");
            check(!firstWindow.IsVisible,"the outgoing page window is closed once the slide finished");
            check(controller.Windows.Count==1&&controller.Windows[0].Group==second&&controller.Windows[0].Opacity>0.99&&Math.Abs(controller.Windows[0].Left-second.X)<1,"the incoming page ends visible at its configured position");
            controller.Page(1);controller.Page(1);Pump(1300);track();
            check(store.Data.Page==1&&controller.Windows.Count==1&&controller.Windows[0].Group==second,"a page change requested during the slide runs after it instead of being dropped");
            controller.Page(1);controller.SwipeStart();controller.SwipeMove(140);controller.SwipeEnd(0);Pump(800);track();
            check(store.Data.Page==0&&controller.Windows.Count==1,"a swipe during a running slide lands the slide instead of piling up windows");
            check(shown.Count(window=>window.IsVisible)==controller.Windows.Count,"no orphan window is left visible after the slide");
        }finally{
            controller.Running=false;
            foreach(Window window in controller.Windows.Cast<Window>().Concat(controller.Widgets).Concat(shown))try{window.Close();}catch{}
            controller.Windows.Clear();controller.Widgets.Clear();
        }
    }
    static void Pump(int milliseconds){
        var watch=Stopwatch.StartNew();
        while(watch.ElapsedMilliseconds<milliseconds){
            var frame=new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{frame.Continue=false;}));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(15);
        }
    }
}
}
