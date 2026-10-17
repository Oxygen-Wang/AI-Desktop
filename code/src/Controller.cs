using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Threading;
using System.Diagnostics;

namespace QiDesk {
// 中键横滑与翻页过渡的纯计算：跟手位移、翻页阈值、滑行距离与时长。便于独立验证和调参。
public static class PageSwipe {
    public const int CommitDistance=90;
    // 翻页滑行时长（毫秒）：280 以下显得急促，620 以上会拖沓。
    public const double DefaultSlideMilliseconds=360,MinimumSlideMilliseconds=280,MaximumSlideMilliseconds=620;
    public static double FollowOffset(int delta,double scale){scale=scale>0?scale:1;return delta/scale;}
    public static bool Commits(int delta){return Math.Abs(delta)>=CommitDistance;}
    // 滑行距离取屏宽的一小部分：距离越短，同样时长下的速度越接近手指，不会一松手就窜出去。
    public static double Travel(double screenWidth){return Math.Max(180,Math.Min(screenWidth*.30,900));}
    // 缓动起步速度约为 3×距离/时长，因此「按手速走完这段距离」需要 3×距离/手速。
    public static double SlideMilliseconds(double distance,double velocity){
        if(velocity<=0)return DefaultSlideMilliseconds;
        return Math.Max(MinimumSlideMilliseconds,Math.Min(MaximumSlideMilliseconds,distance*3/velocity));
    }
    // 只有手速够快、时长没被上限截断时，动画才能真的从手指速度起步。
    public static bool MatchesHand(double distance,double velocity){
        return velocity>0&&distance*3/velocity<=MaximumSlideMilliseconds;
    }
}
public partial class Controller {
    public readonly Store Store;
    public ManagerWindow Manager;
    public string Search="";
    public bool Running,Hidden,Docked;
    public List<FenceWindow> Windows=new List<FenceWindow>();
    public List<WidgetWindow> Widgets=new List<WidgetWindow>();
    DispatcherTimer projectTimer;
    IntPtr host;
    DispatcherTimer timer;
    bool busy;
    string fileSignature="";
    DesktopIcons icons;
    DesktopGesture gesture;
    DesktopCreation creation;
    DesktopMarquee marquee;
    McpBridge mcp;
    public bool DragCreationActive {get{return Running&&Docked&&Store.Data.DesktopDragCreate&&creation!=null&&gesture!=null&&gesture.Active;}}
    List<Rect> previousAreas=new List<Rect>();
    long lastDesktopActivity=DateTime.UtcNow.Ticks;
    bool autoHidden;
    DateTime lastDaily;
    public Controller(Store store){Store=store;foreach(string id in store.Data.SelectedWorkspaceGroups)SelectedGroups.Add(id);}
    public void StartProjectDetection(){if(projectTimer==null){projectTimer=new DispatcherTimer(){Interval=TimeSpan.FromSeconds(5)};projectTimer.Tick+=delegate{try{if(IgnoreFiles.Sync(Store.Data)){Store.Save();Refresh();}}catch(Exception ex){Manager.SetStatus("忽略文件同步未完成："+ex.Message);}};}projectTimer.Start();}
    public void CloseProjectDetection(){if(projectTimer!=null)projectTimer.Stop();}
    public void AddStarterWidgets(){if(Store.Data.StarterWidgetsAdded)return;Store.Data.StarterWidgetsAdded=true;Store.Save();}
    public void EnsureDefaultPanels(){if(Store.Data.DefaultPanelsAdded)return;Store.Data.DefaultPanelsAdded=true;GenerateDefaultPanels();}
    public void GenerateDefaultPanels(){
        IList<Rect> areas=Manager==null?new List<Rect>{SystemParameters.WorkArea}:Shell.WorkingAreas(Manager);
        Rect area=areas.OrderByDescending(a=>a.Width*a.Height).First();
        double margin=16,gap=16,width=Math.Min(600,area.Width-margin*2);double available=Math.Max(480,area.Height-margin*2-gap*2);double appHeight=Math.Max(300,available*.42);double height=(available-appHeight)/2;
        var folder=Store.Data.Groups.FirstOrDefault(g=>g.Name=="文件夹"&&g.Kind=="文件夹"&&String.IsNullOrEmpty(g.Folder));
        var work=Store.Data.Groups.FirstOrDefault(g=>g.Name=="文档"&&Store.Data.Groups.Any(child=>child.HostGroupId==g.Id&&child.Name=="图片"));
        var apps=Store.Data.Groups.FirstOrDefault(g=>g.Name=="应用"&&g.Kind=="应用");
        var picture=work==null?null:Store.Data.Groups.FirstOrDefault(g=>g.HostGroupId==work.Id&&g.Name=="图片");
        Store.Checkpoint();bool first=folder==null&&work==null&&apps==null;
        bool newFolder=folder==null,newWork=work==null,newApps=apps==null;
        if(newFolder){folder=new Group(){Name="文件夹",Kind="文件夹"};Store.Data.Groups.Add(folder);}
        if(newWork){work=new Group(){Name="文档"};Store.Data.Groups.Add(work);}
        if(picture==null){picture=new Group(){Name="图片",Color=work.Color,HostGroupId=work.Id,Page=work.Page,Width=work.Width,Height=work.Height};Store.Data.Groups.Add(picture);}
        if(newApps){apps=new Group(){Name="应用",Kind="应用",IconSize=30};Store.Data.Groups.Add(apps);}
        var panels=new[]{folder,work,apps};double top=area.Bottom-margin-available-2*gap;for(int i=0;i<panels.Length;i++){var panel=panels[i];panel.Page=Store.Data.Page;panel.Width=width;panel.Height=i==2?appHeight:height;panel.X=area.Right-width-margin;panel.Y=top;top+=i==2?appHeight:height;top+=gap;}
        picture.Page=work.Page;
        if(first)Store.OrganizeDefaultPanels(Store.Data,false);
        SaveAndRefresh();if(Manager!=null)Manager.SetStatus("默认面板已生成并重新排列；现有分区内容保留，可撤销。");
    }
    public void UpdateMcp(){if(Store.Data.McpEnabled&&mcp==null&&Manager!=null)mcp=new McpBridge(this,McpBridge.PipeName);else if(!Store.Data.McpEnabled&&mcp!=null){mcp.Dispose();mcp=null;}}
    public void CloseMcp(){if(mcp!=null){mcp.Dispose();mcp=null;}}
    void UpdateComponentDock(){} // 「组件区」面板已移除
    public void Start(bool docked){if(Running&&Docked&&!docked)Store.RestoreDesktop();Store.DesktopActive=docked;Running=true;Docked=docked;Hidden=false;autoHidden=false;if(docked)ExplorerDesktop.RestoreInteraction();Interlocked.Exchange(ref lastDesktopActivity,DateTime.UtcNow.Ticks);previousAreas=Shell.WorkingAreas(Manager);string key=Layout.Key(previousAreas);if(Store.Data.LayoutMode=="按显示器恢复"&&Store.Data.LastLayoutKey!=key)Store.RestoreLayout(key);Rebuild();Store.RecordLayout(key);Store.Save();if(docked)StartDesktopGuard();if(timer==null){timer=new DispatcherTimer(){Interval=TimeSpan.FromSeconds(2)};timer.Tick+=Tick;}timer.Start();Manager.Refresh();}
    // 还原守护：桌面模式期间启动，主进程崩溃/被强杀时立刻还原原始桌面。
    Process desktopGuard;EventWaitHandle desktopGuardStop;string desktopGuardToken;
    void StartDesktopGuard(){
        StopDesktopGuard();
        if(Store.Data.HiddenDesktopItems==null||(Store.Data.HiddenDesktopItems.Count==0&&(Store.Data.ManagedDesktopItems==null||Store.Data.ManagedDesktopItems.Count==0)))return;
        try{
            var owner=Process.GetCurrentProcess();desktopGuardToken="Local\\QiDeskRestore-"+Guid.NewGuid().ToString("N");
            desktopGuardStop=new EventWaitHandle(false,EventResetMode.ManualReset,desktopGuardToken+"-stop");
            desktopGuard=Process.Start(new ProcessStartInfo(owner.MainModule.FileName,"--desktop-guard "+owner.Id+" "+owner.StartTime.ToUniversalTime().Ticks+" "+desktopGuardToken+" \""+Store.Root+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
        }catch{StopDesktopGuard();}
    }
    void StopDesktopGuard(){if(desktopGuardStop!=null){try{desktopGuardStop.Set();}catch{}try{desktopGuardStop.Dispose();}catch{}desktopGuardStop=null;}if(desktopGuard!=null){try{desktopGuard.Dispose();}catch{}desktopGuard=null;}}
    void Tick(object sender,EventArgs args){if(busy)return;busy=true;try{
        if(Store.Scan(false)>0){Store.Save();Refresh();}
        string signature=String.Join("\n",Store.Data.Groups.SelectMany(g=>Store.Items(g,Search)));
        if(signature!=fileSignature){fileSignature=signature;Refresh();}
        foreach(var window in Windows.ToArray())window.Refresh();
        var areas=Shell.WorkingAreas(Manager);if(!areas.SequenceEqual(previousAreas)){Store.Checkpoint();Store.RecordLayout(Layout.Key(previousAreas));if(Store.Data.LayoutMode=="按工作区缩放")Layout.ScaleGroups(Store.Data.Groups.Cast<Group>().Concat(Store.Data.Widgets),previousAreas,areas);else if(Store.Data.LayoutMode=="按显示器恢复")Store.RestoreLayout(Layout.Key(areas));previousAreas=areas;Rebuild();Store.RecordLayout(Layout.Key(areas));Store.Save();}
        if(Store.Data.AutoDailySnapshot&&lastDaily.Date!=DateTime.Today){Store.DailySnapshot(DateTime.Today);lastDaily=DateTime.Today;}
        if(Docked&&Store.Data.IdleHideSeconds>0){long idle=DateTime.UtcNow.Ticks-Interlocked.Read(ref lastDesktopActivity);if(!Hidden&&idle>TimeSpan.FromSeconds(Store.Data.IdleHideSeconds).Ticks){SetHidden(true);autoHidden=true;}else if(autoHidden&&Hidden&&Store.Data.AutoShowOnDesktop&&idle<TimeSpan.FromSeconds(2).Ticks){SetHidden(false);autoHidden=false;}}
        if(Docked){IntPtr current=Shell.DesktopHost();if(current!=host||(Windows.Any(w=>!WorkspaceBindingValid(w,current))||Widgets.Any(w=>!Shell.DesktopBindingValid(w,current)))){if(current!=host)ExplorerDesktop.RestoreInteraction();host=current;Rebuild();}else UpdateDesktopIntegration();}
    }catch(Exception ex){Manager.SetStatus("刷新失败："+ex.Message);}finally{busy=false;}}
    public bool Stop(){ResetWorkspacePresentation();StopDesktopGuard();Running=false;if(timer!=null)timer.Stop();AbandonTransition();ReleaseDesktopIntegration();foreach(var w in Windows.ToArray())try{w.Close();}catch{}Windows.Clear();foreach(var w in Widgets.ToArray())try{w.Close();}catch{}Widgets.Clear();bool restored=true;try{Store.RestoreDesktop();if(Store.LastError.StartsWith("桌面图标恢复未完成")){restored=false;if(Manager!=null)Manager.SetStatus(Store.LastError);}}catch(Exception ex){restored=false;if(Manager!=null)Manager.SetStatus("桌面图标恢复未完成："+ex.Message);}ExplorerDesktop.RestoreInteraction();if(Manager!=null)Manager.Refresh();return restored;}
    // 上次未正常退出（崩溃或被强制结束）会残留隐藏项；未启用桌面模式时在启动阶段自动还原，
    // 避免再次出现“停止后只剩回收站”。返回是否确实执行了恢复。
    public bool RecoverDesktopIfNeeded(){
        var data=Store.Data;bool residue=(data.HiddenDesktopItems!=null&&data.HiddenDesktopItems.Count>0)||(data.ManagedDesktopItems!=null&&data.ManagedDesktopItems.Count>0);
        if(!residue)return false;bool restored=true;
        try{Store.RestoreDesktop();if(Store.LastError.StartsWith("桌面图标恢复未完成"))restored=false;}catch(Exception ex){restored=false;Store.LastError="桌面图标恢复未完成："+ex.Message;}
        if(Manager!=null)Manager.SetStatus(restored?"已恢复上次未正常退出时隐藏的 Windows 桌面图标。":Store.LastError);
        return restored;
    }
    public void Rebuild(){
        SelectedGroups.RemoveWhere(id=>!Store.Data.SelectedWorkspaceGroups.Contains(id,StringComparer.OrdinalIgnoreCase));
        foreach(string id in Store.Data.SelectedWorkspaceGroups)SelectedGroups.Add(id);
        AbandonTransition();
        foreach(var w in Windows.ToArray())try{w.Close();}catch{}Windows.Clear();
        foreach(var w in Widgets.ToArray())try{w.Close();}catch{}Widgets.Clear();if(!Running)return;
        BuildPageWindows();
        UpdateDesktopIntegration();ReapplyGroupVisibility();ScheduleIconPush();
    }
    // 按当前页建立分区与组件窗口；调用方负责先清空 Windows / Widgets。
    void BuildPageWindows(){
        host=Docked?Shell.DesktopHost():IntPtr.Zero;int attached=0,index=0;bool recovered=false;
        var areas=Manager==null?new List<Rect>{SystemParameters.WorkArea}:Shell.WorkingAreas(Manager);
        foreach(var item in Store.Data.Groups.Where(g=>g.Page==Store.Data.Page&&String.IsNullOrEmpty(g.HostGroupId)).Cast<Group>().Concat(Store.Data.Widgets.Where(w=>w.Page==Store.Data.Page))){recovered=Layout.EnsureVisible(item,areas,index++)||recovered;}
        foreach(Group g in Store.Data.Groups.Where(g=>g.Page==Store.Data.Page&&String.IsNullOrEmpty(g.HostGroupId))){
            var w=new FenceWindow(this,g);Windows.Add(w);w.Show();if(Docked&&w.PlaceOnDesktop(host))attached++;if(Hidden&&!g.ExcludeFromHide)w.Hide();
        }
        foreach(var widget in Store.Data.Widgets.Where(w=>w.Page==Store.Data.Page)){var plugin=WidgetPlugins.Find(Store.Data,widget.PluginId);if(plugin==null||!plugin.Enabled)continue;var window=new WidgetWindow(this,widget);Widgets.Add(window);window.Show();if(Docked&&window.PlaceOnDesktop(host))attached++;if(Hidden&&!widget.ExcludeFromHide)window.Hide();}
        if(recovered){Store.Save();if(Manager!=null)Manager.SetStatus("已将屏幕外的分组恢复到可见区域，原配置保存在 state.json.bak。");}
        if(Docked&&attached!=Windows.Count+Widgets.Count&&Manager!=null)Manager.SetStatus("部分分组或组件未连接桌面，当前以普通窗口显示。可切换预览模式后重试。");
    }
    void ReleaseDesktopIntegration(){if(creation!=null){creation.Dispose();creation=null;}if(marquee!=null){marquee.Dispose();marquee=null;}if(gesture!=null){gesture.Dispose();gesture=null;}if(icons!=null){icons.Dispose();icons=null;}}
    // 隐藏/恢复 Explorer 的原生图标层。切到别的图标窗口或关闭开关时会释放旧的租约。
    void ApplyDesktopIconVisibility(){
        IntPtr list=Store.Data.HideDesktopIcons&&host!=IntPtr.Zero?Shell.DesktopList(host):IntPtr.Zero;
        if(icons!=null&&icons.Window==list)return;
        if(icons!=null){icons.Dispose();icons=null;}
        if(list==IntPtr.Zero)return;
        try{icons=new DesktopIcons(list);}catch(Exception ex){icons=null;if(Manager!=null)Manager.SetStatus("隐藏原生桌面图标未启用："+ex.Message);}
    }
    public void UpdateDesktopIntegration(){
        if(Running&&Docked)ExplorerDesktop.RestoreInput();
        bool active=Running&&Docked&&host!=IntPtr.Zero&&Windows.All(w=>w.Docked)&&Widgets.All(w=>w.Docked);
        if(!active){ReleaseDesktopIntegration();return;}
        if(Store.Data.IdleHideSeconds==0&&autoHidden){SetHidden(false);autoHidden=false;}
        try{
            // 桌面模式时按设置隐藏整个原生图标层，避免散落图标与分区重叠。
            // DesktopIcons 用独立的守护进程，栖桌异常退出也能恢复图标显示。
            ApplyDesktopIconVisibility();
            if(Store.Data.DoubleClickHide||Store.Data.IdleHideSeconds>0||Store.Data.DesktopDragCreate||Store.Data.Pages>1){
                if(marquee==null)marquee=new DesktopMarquee(Manager,delegate(Rect bounds){if(Running&&Docked)SelectGroupsInRect(bounds);});
                if(gesture==null){gesture=new DesktopGesture(Manager.Dispatcher,delegate{if(Running&&Docked&&Store.Data.DoubleClickHide)ToggleHidden();},delegate{Interlocked.Exchange(ref lastDesktopActivity,DateTime.UtcNow.Ticks);},delegate(int token,Rect bounds){if(creation!=null)creation.Update(token,bounds);},delegate(int token,Rect bounds){if(creation!=null)creation.Complete(token,bounds);},Shell.WindowScale(Manager),delegate(int delta){if(Running&&Docked)Page(delta);},marqueePreview:delegate(int token,Rect bounds){if(marquee!=null)marquee.Update(token,bounds);},marqueeComplete:delegate(int token,Rect bounds){if(marquee!=null)marquee.Complete(token,bounds);});gesture.SwipeStart=delegate{if(Running&&Docked)SwipeStart();};gesture.SwipeMove=delegate(int delta){if(Running&&Docked)SwipeMove(delta);};gesture.SwipeEnd=delegate(int delta,double velocity){if(Running&&Docked)SwipeEnd(delta,velocity);};}
                if(Store.Data.DesktopDragCreate&&creation==null)creation=new DesktopCreation(Manager,delegate(Rect bounds,bool folder){if(Running&&Docked)Manager.CreateSelectedGroup(bounds,folder,false);},delegate{if(gesture!=null)gesture.ResetCreation();});
                if(!Store.Data.DesktopDragCreate&&creation!=null){creation.Dispose();creation=null;}
                gesture.EnableCreation(Store.Data.DesktopDragCreate);gesture.Start(host);
            }
            else {if(creation!=null){creation.Dispose();creation=null;}if(marquee!=null){marquee.Dispose();marquee=null;}if(gesture!=null){gesture.Dispose();gesture=null;}}
        }catch(Exception ex){ReleaseDesktopIntegration();if(Manager!=null)Manager.SetStatus("桌面交互未启用："+ex.Message);}
    }
    public void Refresh(){foreach(var w in Windows.ToArray())w.Refresh();foreach(var w in Widgets.ToArray())w.Refresh();if(Manager!=null)Manager.Refresh();}
    public bool TryMergeGroup(Group source,Point screenPoint,FenceWindow moving){
        foreach(var targetWindow in Windows.ToArray()){
            if(targetWindow==moving||targetWindow.Group==source||!targetWindow.IsVisible)continue;
            Point local=targetWindow.PointFromScreen(screenPoint);
            if(local.X<0||local.X>targetWindow.ActualWidth||local.Y<0||local.Y>GroupArrangement.HeaderHeight)continue;
            Group target=targetWindow.Group;if(target.Id==source.Id||!String.IsNullOrEmpty(target.HostGroupId))continue;
            MergeWorkspaceState(source,target);
            source.HostGroupId=target.Id;source.Page=target.Page;
            foreach(var child in Store.Data.Groups.Where(g=>g.HostGroupId==source.Id).ToArray()){child.HostGroupId=target.Id;child.Page=target.Page;}
            target.ActiveGroupId=source.Id;SaveAndRefresh();return true;
        }return false;
    }
    public void DetachGroup(Group host,Group member){DetachGroup(host,member,new Point(host.X+36,host.Y+GroupArrangement.HeaderHeight));}
    public bool MoveMergedTab(Group host,Group member,Group target){
        if(host==null||member==null||target==null||host==target||member.HostGroupId!=host.Id||!String.IsNullOrEmpty(target.HostGroupId))return false;
        Store.Checkpoint();
        member.HostGroupId=target.Id;
        member.Page=target.Page;
        target.ActiveGroupId=member.Id;
        if(host.ActiveGroupId==member.Id)host.ActiveGroupId=host.Id;
        SaveAndRefresh();
        return true;
    }
    public void DetachGroup(Group host,Group member,Point position){
        if(member.HostGroupId!=host.Id)return;
        Store.Checkpoint();
        try{PartitionIgnores.CopySharedRulesForDetach(Store.Data,member);}
        catch(InvalidDataException ex){if(Manager!=null)Manager.SetStatus("分区未拆出："+ex.Message);return;}
        member.HostGroupId="";member.Page=host.Page;member.X=position.X;member.Y=position.Y;
        host.ActiveGroupId=host.Id;SaveAndRefresh();
    }
    public void SaveAndRefresh(bool rebuild=true){try{if(Running)Store.RecordLayout(Layout.Key(previousAreas));Store.Save();if(rebuild)Rebuild();Refresh();ScheduleIconPush();}catch(Exception ex){Manager.SetStatus("保存失败："+ex.Message);}}
    // ---- 翻页过渡：新旧两页的分区与组件同时滑动，旧窗口在过渡结束后才关闭 ----
    sealed class PageTransition {
        public int Direction;
        public double Travel;
        public List<Window> Outgoing=new List<Window>();
        public List<Tuple<Window,double>> Incoming=new List<Tuple<Window,double>>();
    }
    PageTransition transition;
    int queuedPage;
    double queuedVelocity;
    double swipeScale=1;
    // 翻页动画所在的 UI 调度器：有管理窗口时用它，测试或无窗口场景用当前线程。
    Dispatcher Ui{get{return Manager==null?Dispatcher.CurrentDispatcher:Manager.Dispatcher;}}
    public void Page(int delta,double velocity=0){
        if(delta==0||Store.Data.Pages<2)return;
        if(!Running||Hidden||Windows.Count+Widgets.Count==0){Store.Data.Page=(Store.Data.Page+delta+Store.Data.Pages)%Store.Data.Pages;SaveAndRefresh();return;}
        if(transition!=null){queuedPage=Math.Sign(delta);queuedVelocity=velocity;return;}
        StartPageTransition(Math.Sign(delta),velocity);
    }
    // 先把新一页的窗口建好并停在屏幕外，再让两组窗口同时滑动。中途不做整体重建，
    // 所以不会出现「飞出 → 闪一下 → 飞入」的断档，翻页期间也能继续滑动。
    void StartPageTransition(int direction,double velocity){
        FinishSnapBack();
        var outgoing=Windows.Cast<Window>().Concat(Widgets).ToList();
        Windows=new List<FenceWindow>();Widgets=new List<WidgetWindow>();
        Store.Data.Page=(Store.Data.Page+direction+Store.Data.Pages)%Store.Data.Pages;
        var run=new PageTransition(){Direction=direction,Travel=PageSwipe.Travel(SystemParameters.PrimaryScreenWidth),Outgoing=outgoing};
        transition=run;
        try{BuildPageWindows();ReapplyGroupVisibility();}
        catch{FinishPageTransition();throw;}
        foreach(Window window in Windows.Cast<Window>().Concat(Widgets)){run.Incoming.Add(Tuple.Create(window,window.Left));window.Opacity=0;}
        // 手速够快就按手速起步（缓出），否则用一段平缓的滑动，避免松手瞬间速度突然变快。
        bool matches=PageSwipe.MatchesHand(run.Travel,velocity);
        EasingFunctionBase ease=matches?(EasingFunctionBase)new CubicEase(){EasingMode=EasingMode.EaseOut}:new SineEase(){EasingMode=EasingMode.EaseInOut};
        var duration=TimeSpan.FromMilliseconds(PageSwipe.SlideMilliseconds(run.Travel,velocity));
        // 等新窗口在屏幕外渲染完这一帧再开始滑动，否则会在目标位置闪一下。
        Ui.BeginInvoke(DispatcherPriority.Render,new Action(delegate{
            if(transition!=run)return;
            foreach(var target in run.Incoming){
                target.Item1.BeginAnimation(Window.LeftProperty,new DoubleAnimation(target.Item2+direction*run.Travel,target.Item2,duration){FillBehavior=FillBehavior.HoldEnd,EasingFunction=ease});
                target.Item1.BeginAnimation(Window.OpacityProperty,new DoubleAnimation(0,1,duration){FillBehavior=FillBehavior.HoldEnd,EasingFunction=ease});
            }
            foreach(Window window in run.Outgoing){
                double from=window.Left;
                window.BeginAnimation(Window.LeftProperty,new DoubleAnimation(from,from-direction*run.Travel,duration){FillBehavior=FillBehavior.HoldEnd,EasingFunction=ease});
                window.BeginAnimation(Window.OpacityProperty,new DoubleAnimation(window.Opacity,0,duration){FillBehavior=FillBehavior.HoldEnd,EasingFunction=ease});
            }
        }));
        var finish=new DispatcherTimer(){Interval=duration+TimeSpan.FromMilliseconds(40)};
        finish.Tick+=delegate{finish.Stop();if(transition!=run)return;FinishPageTransition();int next=queuedPage;double nextVelocity=queuedVelocity;queuedPage=0;queuedVelocity=0;if(next!=0)Page(next,nextVelocity);};
        finish.Start();
    }
    // 让正在进行的翻页立刻落到终点：新窗口停在各自位置，仍在滑出的旧窗口关闭。
    void FinishPageTransition(){
        var run=transition;if(run==null)return;transition=null;
        foreach(var target in run.Incoming){
            target.Item1.BeginAnimation(Window.LeftProperty,null);
            target.Item1.BeginAnimation(Window.OpacityProperty,null);
            target.Item1.Opacity=1;
        }
        CloseWindows(run.Outgoing);
        // 与 SaveAndRefresh 一致：保存失败只提示，不让异常从动画计时器冒出去。
        try{Store.Save();}catch(Exception ex){if(Manager!=null)Manager.SetStatus("保存失败："+ex.Message);}
        UpdateDesktopIntegration();ScheduleIconPush();if(Manager!=null)Manager.Refresh();
    }
    // 窗口即将整体重建或退出：立刻结束过渡，避免滑动中的旧窗口残留在桌面上。
    void AbandonTransition(){
        queuedPage=0;swipeBases=null;FinishSnapBack();
        var run=transition;transition=null;
        if(run!=null)CloseWindows(run.Outgoing);
    }
    static void CloseWindows(IEnumerable<Window> windows){
        foreach(Window window in windows)try{window.Close();}catch{}
    }
    public void ToggleHidden(){autoHidden=false;SetHidden(!Hidden);}
    // 中键横滑：拖动时分区与指针 1:1 跟手，松手后按位移翻页并接上手指速度，未达到距离则缓动回位。
    List<Tuple<Window,double>> swipeBases;
    public void SwipeStart(){
        swipeBases=null;
        if(!Running||Hidden||Store.Data.Pages<2)return;
        if(transition!=null)FinishPageTransition();
        FinishSnapBack();
        swipeScale=Manager==null?1:Shell.WindowScale(Manager);
        if(swipeScale<=0)swipeScale=1;
        swipeBases=new List<Tuple<Window,double>>();
        foreach(Window window in Windows.Cast<Window>().Concat(Widgets))swipeBases.Add(Tuple.Create(window,window.Left));
    }
    public void SwipeMove(int delta){
        if(swipeBases==null)return;
        double offset=PageSwipe.FollowOffset(delta,swipeScale);
        foreach(var basis in swipeBases){try{basis.Item1.Left=basis.Item2+offset;}catch{}}
    }
    // velocity 是松手时的指针速度（物理像素/毫秒），换算成逻辑单位后交给翻页动画。
    public void SwipeEnd(int delta,double velocity=0){
        if(swipeBases==null)return;
        var bases=swipeBases;swipeBases=null;
        if(PageSwipe.Commits(delta)){Page(delta<0?1:-1,velocity/swipeScale);return;}
        SnapSwipeBack(bases);
    }
    // 回弹用缓动，并在结束时写回本地值。只依赖动画的 FillBehavior 会让窗口在结束后跳回拖动位置。
    List<Tuple<Window,double>> snapBackBases;
    void SnapSwipeBack(List<Tuple<Window,double>> bases){
        FinishSnapBack();
        snapBackBases=bases;
        var ease=new CubicEase(){EasingMode=EasingMode.EaseOut};
        var duration=TimeSpan.FromMilliseconds(200);
        foreach(var basis in bases){
            Window window=basis.Item1;
            if(!window.IsVisible||Math.Abs(window.Left-basis.Item2)<.5){window.Left=basis.Item2;continue;}
            window.BeginAnimation(Window.LeftProperty,new DoubleAnimation(window.Left,basis.Item2,duration){FillBehavior=FillBehavior.HoldEnd,EasingFunction=ease});
        }
        var finish=new DispatcherTimer(){Interval=duration+TimeSpan.FromMilliseconds(40)};
        finish.Tick+=delegate{
            finish.Stop();
            if(snapBackBases!=bases)return;
            FinishSnapBack();
        };
        finish.Start();
    }
    // 回弹立刻落到终点：取消动画并写回拖动前的位置，供新的滑动或翻页接管。
    void FinishSnapBack(){
        var bases=snapBackBases;snapBackBases=null;
        if(bases==null)return;
        foreach(var basis in bases){basis.Item1.BeginAnimation(Window.LeftProperty,null);basis.Item1.Left=basis.Item2;}
    }
    // 原生图标自动避开分区与插件：防抖 500ms，避免拖动过程中频繁移动。
    DispatcherTimer iconPushTimer;
    public void ScheduleIconPush(){if(!Running||!Docked||!Store.Data.PushIconsAside)return;if(iconPushTimer==null){iconPushTimer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(500)};iconPushTimer.Tick+=delegate{iconPushTimer.Stop();PushIconsAside();};}iconPushTimer.Stop();iconPushTimer.Start();}
    void PushIconsAside(){
        if(!Running||!Docked||!Store.Data.PushIconsAside||host==IntPtr.Zero||iconPushRunning)return;
        var obstacles=new List<Rect>();
        foreach(var window in Windows)obstacles.Add(new Rect(window.Left,window.Top,window.ActualWidth>1?window.ActualWidth:window.Width,window.ActualHeight>1?window.ActualHeight:window.Height));
        foreach(var window in Widgets)obstacles.Add(new Rect(window.Left,window.Top,window.ActualWidth>1?window.ActualWidth:window.Width,window.ActualHeight>1?window.ActualHeight:window.Height));
        IntPtr list=Shell.DesktopList(host);if(list==IntPtr.Zero)return;double scale=Shell.WindowScale(Manager);
        iconPushRunning=true;
        var thread=new Thread(delegate(){
            try{
                int moved=DesktopIconLayout.PushAside(list,scale,obstacles,true);string error=DesktopIconLayout.LastError;var manager=Manager;
                if(manager!=null)manager.Dispatcher.BeginInvoke(new Action(delegate{if(moved>0)manager.SetStatus("已将 "+moved+" 个原生图标动态避让到空位。");else if(error.Length>0)manager.SetStatus(error);}));
            }catch(Exception ex){var manager=Manager;if(manager!=null)manager.Dispatcher.BeginInvoke(new Action(delegate{manager.SetStatus("图标避让未完成："+ex.Message);}));}
            finally{iconPushRunning=false;}
        }){IsBackground=true,Name="QiDesk icon avoid"};
        thread.Start();
    }
    volatile bool iconPushRunning;
    void SetHidden(bool hidden){Hidden=hidden;foreach(var w in Windows){if(Hidden&&!w.Group.ExcludeFromHide)w.Hide();else {w.Show();if(Docked)w.PlaceOnDesktop(host);}}foreach(var w in Widgets){if(Hidden&&!w.Widget.ExcludeFromHide)w.Hide();else{w.Show();if(Docked)w.PlaceOnDesktop(host);}}ReapplyGroupVisibility();UpdateComponentDock();Manager.Refresh();}
    public int OrganizeLoose(IEnumerable<string> paths){int count=0;foreach(string p in paths){if(Store.Assigned(p)!=null||Store.Data.Excluded.Contains(p,StringComparer.OrdinalIgnoreCase))continue;var g=Store.Match(p);if(g!=null){Store.Assign(p,g);count++;}}return count;}
    // 把文件从某个分区移出，并在数据层归到另一个现有分区，避免隐藏原生图标后文件“看不见”。
    public void MoveOut(string path,Group source){
        if(Store.Assigned(path)==null)return;
        Store.Checkpoint();Store.RemoveFromGroups(path);
        Group target=DesktopItemDrag.MoveOutTarget(Store,source,path);
        if(target!=null)Store.Assign(path,target);
        SaveAndRefresh();
        if(Manager!=null)Manager.SetStatus(target!=null?"已将 "+Path.GetFileName(path)+" 移到「"+target.Name+"」分区。":"已将 "+Path.GetFileName(path)+" 移出分区；没有其它分区可接收，暂保留在桌面。");
    }
    public void ApplyRules(){Store.Checkpoint();int count=OrganizeLoose(Store.DesktopItems());SaveAndRefresh();if(Manager!=null)Manager.SetStatus("按规则整理了 "+count+" 个散落图标；已在分区中的图标保持不变。");}
    public Group CreateAt(Rect bounds,string name,string folder){if(bounds.IsEmpty||bounds.Width<220||bounds.Height<100)throw new ArgumentException("创建区域至少为 220 × 100。");Store.Checkpoint();var group=new Group(){Name=name,Folder=folder??"",Page=Store.Data.Page,X=bounds.X,Y=bounds.Y,Width=bounds.Width,Height=bounds.Height};Store.Data.Groups.Add(group);SaveAndRefresh();return group;}
    public void ApplySuggestions(IEnumerable<Suggestion> suggestions){Store.Checkpoint();foreach(var s in suggestions){var g=Store.Data.Groups.FirstOrDefault(x=>x.Id==s.GroupId);if(g!=null&&!Store.Ignored(s.Path,g))Store.Assign(s.Path,g);}SaveAndRefresh();}
    public void ImportFences(string path){
        var root=Store.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
        var groups=root["groups"] as System.Collections.ArrayList;var icons=root["icons"] as System.Collections.ArrayList;
        if(groups==null||icons==null)throw new InvalidDataException("导出文件缺少分组和图标记录");
        var imported=new List<Group>();var idMap=new Dictionary<string,Group>();
        foreach(Dictionary<string,object> row in groups){
            string oldId=Convert.ToString(row["id"]);var g=new Group(){Name=Convert.ToString(row["title"]),X=Number(row,"x",40),Y=Number(row,"y",65),Width=Number(row,"width",320),Height=Number(row,"height",340),Folder=Value(row,"folder"),Page=(int)Math.Max(0,Number(row,"page",0)),Rolled=Number(row,"rolled",0)!=0};
            g.Folder=Environment.ExpandEnvironmentVariables(g.Folder);imported.Add(g);idMap[oldId]=g;
        }
        if(imported.Count==0)throw new InvalidDataException("没有可导入的分组");
        foreach(Dictionary<string,object> row in icons){Group g;string p=Environment.ExpandEnvironmentVariables(Value(row,"path"));if(idMap.TryGetValue(Value(row,"group_id"),out g)&&String.IsNullOrEmpty(g.Folder)&&Store.Exists(p)){p=Path.GetFullPath(p);if(!g.Items.Contains(p,StringComparer.OrdinalIgnoreCase))g.Items.Add(p);}}
        // Imported organization stays in this application's own JSON state.
        Store.Checkpoint();Store.Data.Groups=imported;Store.Data.Rules=new List<Rule>();Store.Data.Pages=Math.Max(3,imported.Max(g=>g.Page)+1);Store.Data.Page=0;Store.Data.AutoRules=false;Store.Validate(Store.Data);SaveAndRefresh();
    }
    static string Value(Dictionary<string,object> row,string key){object value;return row.TryGetValue(key,out value)?Convert.ToString(value):"";}
    static double Number(Dictionary<string,object> row,string key,double fallback){double value;return Double.TryParse(Value(row,key),out value)?value:fallback;}
}
}
