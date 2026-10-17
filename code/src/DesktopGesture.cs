using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Accessibility;
using System.Windows;

namespace QiDesk {
public sealed class ClickPair {
    bool pending;
    uint lastTime;
    int lastX,lastY;
    IntPtr lastTarget;
    public void Reset(){pending=false;}
    public void Move(int x,int y,int distanceX,int distanceY){if(pending&&(Math.Abs((long)x-lastX)>distanceX||Math.Abs((long)y-lastY)>distanceY))Reset();}
    public bool Add(uint time,int x,int y,IntPtr target,bool desktop,uint interval,int distanceX,int distanceY){
        if(!desktop){Reset();return false;}
        bool pair=pending&&target==lastTarget&&unchecked(time-lastTime)<=interval&&Math.Abs((long)x-lastX)<=distanceX&&Math.Abs((long)y-lastY)<=distanceY;
        pending=!pair;lastTime=time;lastX=x;lastY=y;lastTarget=target;return pair;
    }
}
public sealed class DesktopGesture : IDisposable {
    [StructLayout(LayoutKind.Sequential)] public struct NativePoint {public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
    [StructLayout(LayoutKind.Sequential)] struct MouseData {public NativePoint Point;public uint Data,Flags,Time;public IntPtr Extra;}
    delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPhysicalPoint(NativePoint point);
    [DllImport("user32.dll")] static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int metric);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr window,uint id,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IAccessible accessible);
    readonly ClickPair clicks=new ClickPair();
    readonly Action toggle;
    readonly Action activity;
    readonly Action<int,Rect> selectionPreview,selectionComplete;
    readonly Action<int,Rect> marqueePreview,marqueeComplete;
    readonly double selectionScale;
    readonly DesktopDrag drag=new DesktopDrag();
    readonly BoxSelect box=new BoxSelect();
    readonly MiddleButtonSwipe swipe=new MiddleButtonSwipe();
    readonly Action<int> navigate;
    readonly Func<IntPtr,bool> testSurface,testBlank;
    bool navigationKeyDown;
    const uint MiddleDown=0x207,MiddleUp=0x208;
    int lastSwipeDx;
    int pendingSwipeDx;
    bool swipeMovePosted;
    volatile bool creationEnabled;
    DispatcherTimer selectionTimer;
    Rect publishedRect=Rect.Empty;
    Rect publishedBoxRect=Rect.Empty;
    int publishedToken;
    readonly Dispatcher ui;
    Thread thread;
    Dispatcher dispatcher;
    HookProc proc,keyProc;
    IntPtr hook,keyHook,host;
    volatile bool enabled;
    int generation;
    public bool Active {get{return enabled&&hook!=IntPtr.Zero;}}
    // 按住中键横滑的分页动画：开始 / 拖动进度(像素) / 结束(像素, 松手速度 像素每毫秒)。
    public Action SwipeStart;
    public Action<int> SwipeMove;
    public Action<int,double> SwipeEnd;
    public DesktopGesture(Dispatcher uiDispatcher,Action action,Action desktopActivity=null,Action<int,Rect> preview=null,Action<int,Rect> completed=null,double scale=1,Action<int> pageNavigation=null,Func<IntPtr,bool> testSurface=null,Func<IntPtr,bool> testBlank=null,Action<int,Rect> marqueePreview=null,Action<int,Rect> marqueeComplete=null){ui=uiDispatcher;toggle=action;activity=desktopActivity;selectionPreview=preview;selectionComplete=completed;selectionScale=scale;navigate=pageNavigation;this.testSurface=testSurface;this.testBlank=testBlank;this.marqueePreview=marqueePreview;this.marqueeComplete=marqueeComplete;}
    bool Surface(IntPtr target,IntPtr desktop){return testSurface==null?Shell.DesktopSurface(target,desktop):testSurface(target);}
    bool Blank(IntPtr target,IntPtr desktop,NativePoint point){return testBlank==null?IsBlank(target,desktop,point):testBlank(target);}
    public void EnableCreation(bool value){if(creationEnabled==value)return;creationEnabled=value;if(!value)ResetCreation();}
    public void ResetCreation(){var pump=dispatcher;if(pump!=null&&!pump.HasShutdownStarted)pump.BeginInvoke(new Action(delegate{CancelSelection();CancelBox();}));}
    public void Start(IntPtr desktop){
        IntPtr previous=Interlocked.Exchange(ref host,desktop);
        if(Active){if(previous!=desktop)ResetCreation();return;}
        Exception failure=null;var ready=new ManualResetEventSlim();enabled=true;
        thread=new Thread(delegate(){
            dispatcher=Dispatcher.CurrentDispatcher;proc=Callback;keyProc=KeyCallback;
            try{hook=SetWindowsHookEx(14,proc,GetModuleHandle(null),0);if(hook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());keyHook=SetWindowsHookEx(13,keyProc,GetModuleHandle(null),0);if(keyHook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());}
            catch(Exception ex){failure=ex;enabled=false;}finally{ready.Set();}
            if(enabled){selectionTimer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(20)};selectionTimer.Tick+=delegate{SelectionTick();};selectionTimer.Start();Dispatcher.Run();selectionTimer.Stop();}
            if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);if(keyHook!=IntPtr.Zero)UnhookWindowsHookEx(keyHook);hook=keyHook=IntPtr.Zero;proc=keyProc=null;
        }){IsBackground=true,Name="QiDesk desktop gesture"};thread.SetApartmentState(ApartmentState.STA);thread.Start();
        if(!ready.Wait(3000)){Dispose();throw new InvalidOperationException("桌面双击监听启动超时。");}
        ready.Dispose();if(failure!=null)throw new InvalidOperationException("桌面双击监听失败："+failure.Message);
    }
    IntPtr Callback(int code,IntPtr message,IntPtr data){
        if(!enabled||code<0)return CallNextHookEx(hook,code,message,data);
        uint kind=unchecked((uint)message.ToInt32());
        if(kind==MiddleDown){clicks.Reset();CancelSelection();if(navigate!=null){var input=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));IntPtr desktop=Interlocked.CompareExchange(ref host,IntPtr.Zero,IntPtr.Zero);BeginSwipe(input,desktop);if(swipe.Active)return new IntPtr(1);}}
        if(enabled&&code>=0&&message.ToInt32()==0x200){var move=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));if(box.Active){box.Move(move.Point.X,move.Point.Y);PublishBox();return new IntPtr(1);}clicks.Move(move.Point.X,move.Point.Y,Math.Max(1,GetSystemMetrics(36)/2),Math.Max(1,GetSystemMetrics(37)/2));IntPtr desktop=Interlocked.CompareExchange(ref host,IntPtr.Zero,IntPtr.Zero),at=WindowFromPhysicalPoint(move.Point);if(activity!=null&&(Shell.DesktopSurface(at,desktop)||Shell.DesktopBindingValidHandle(at,desktop)))activity();if(drag.Pressed)drag.Move(move.Point.X,move.Point.Y,!Modified()&&(Shell.DesktopSurface(at,desktop)||Shell.PassiveDesktopWindow(at,desktop)));if(swipe.Active){swipe.Move(move.Point.X,move.Point.Y,move.Time);ReportSwipeMove(swipe.DeltaX);}}
        if(kind==0x204){clicks.Reset();CancelBox();if(!drag.AwaitingChoice)CancelSelection();}
        if(enabled&&code>=0&&message.ToInt32()==0x201){
            try{
                var input=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));
                IntPtr desktop=Interlocked.CompareExchange(ref host,IntPtr.Zero,IntPtr.Zero),target=WindowFromPhysicalPoint(input.Point);
                bool surface=Shell.DesktopSurface(target,desktop);
                if(CtrlDown()&&surface&&!drag.Pressed&&!drag.AwaitingChoice&&!box.Active){
                    clicks.Reset();CancelSelection();
                    box.Begin(input.Point.X,input.Point.Y);
                    publishedBoxRect=Rect.Empty;PublishBox();
                    return new IntPtr(1);                }
                if(creationEnabled&&selectionPreview!=null&&selectionComplete!=null&&!drag.AwaitingChoice){
                    CancelSelection();int session=drag.Begin(input.Time,input.Point.X,input.Point.Y,surface&&!Modified());
                    if(session>=0){int version=generation;NativePoint point=input.Point;Task.Factory.StartNew(delegate{bool blank=IsBlank(target,desktop,point);var pump=dispatcher;if(enabled&&version==generation&&pump!=null&&!pump.HasShutdownStarted)pump.BeginInvoke(new Action(delegate{if(enabled&&version==generation)drag.Approve(session,blank);}));});}
                }
                if(surface&&activity!=null)activity();
                if(clicks.Add(input.Time,input.Point.X,input.Point.Y,target,surface,GetDoubleClickTime(),Math.Max(1,GetSystemMetrics(36)/2),Math.Max(1,GetSystemMetrics(37)/2))){
                    int version=generation;NativePoint point=input.Point;
                    // Accessibility can contact Explorer. It runs off both the hook and UI thread.
                    Task.Factory.StartNew(delegate{
                        if(!enabled||version!=generation||!IsBlank(target,desktop,point))return;
                        if(enabled&&version==generation)ui.BeginInvoke(new Action(delegate{if(enabled&&version==generation)toggle();}));
                    });
                }
            }catch{ }
        }
        if(kind==MiddleUp&&swipe.Active){
            var input=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));bool approved=swipe.Approved;int direction=swipe.End(input.Point.X,input.Point.Y,input.Time);int dx=swipe.LastDelta;double velocity=swipe.ReleaseVelocity;
            if(SwipeEnd!=null)ReportSwipeEnd(approved?dx:0,approved?velocity:0);
            else if(direction!=0){int step=direction;ui.BeginInvoke(new Action(delegate{if(enabled&&navigate!=null)navigate(step);}));}
            return new IntPtr(1);
        }
        if(enabled&&code>=0&&message.ToInt32()==0x202&&box.Active){
            var input=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));
            Rect rect=box.End(input.Point.X,input.Point.Y,8*selectionScale);
            int session=box.Token,version=generation;
            if(rect.IsEmpty){
                publishedBoxRect=Rect.Empty;
                if(marqueePreview!=null)ui.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(enabled&&version==generation)marqueePreview(session,Rect.Empty);}));
            }else{
                publishedBoxRect=Rect.Empty;
                ui.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(enabled&&version==generation&&marqueeComplete!=null)marqueeComplete(session,rect);}));
            }
            return new IntPtr(1);
        }
        if(enabled&&code>=0&&message.ToInt32()==0x202&&drag.Pressed){
            var input=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));IntPtr desktop=Interlocked.CompareExchange(ref host,IntPtr.Zero,IntPtr.Zero),at=WindowFromPhysicalPoint(input.Point);
            Rect rect=drag.End(input.Time,input.Point.X,input.Point.Y,!Modified()&&(Shell.DesktopSurface(at,desktop)||Shell.PassiveDesktopWindow(at,desktop)),350,220*selectionScale,100*selectionScale);
            if(rect.IsEmpty)CancelSelection();else{clicks.Reset();int session=drag.Token,version=generation;publishedRect=rect;publishedToken=session;ui.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(enabled&&version==generation&&creationEnabled)selectionComplete(session,rect);}));}
        }
        return CallNextHookEx(hook,code,message,data);
    }
    void BeginSwipe(MouseData input,IntPtr desktop){
        IntPtr target=WindowFromPhysicalPoint(input.Point);swipe.Begin(input.Point.X,input.Point.Y,Surface(target,desktop)&&!Modified());if(!swipe.Active)return;
        CancelSelection();
        // 立即开始跟手，不再等异步的空白判定；判定返回后只决定保留还是回弹，避免起步延迟。
        ReportSwipeStart();
        int version=generation;NativePoint point=input.Point;
        Task.Factory.StartNew(delegate{bool blank=Blank(target,desktop,point);var pump=dispatcher;if(enabled&&version==generation&&pump!=null&&!pump.HasShutdownStarted)pump.BeginInvoke(new Action(delegate{if(!enabled||version!=generation)return;swipe.Approve(blank);if(!swipe.Approved){swipe.Reset();ReportSwipeEnd(0,0);}}));});
    }
    void ReportSwipeStart(){lastSwipeDx=0;if(SwipeStart==null||ui.HasShutdownStarted)return;int version=generation;ui.BeginInvoke(new Action(delegate{if(enabled&&version==generation)SwipeStart();}));}
    void ReportSwipeMove(int dx){
        if(SwipeMove==null)return;
        if(Math.Abs(dx-lastSwipeDx)<2)return;lastSwipeDx=dx;
        if(ui.HasShutdownStarted)return;
        int version=generation;Interlocked.Exchange(ref pendingSwipeDx,dx);
        // UI 线程忙时只保留最新位移；否则积压的更新会让分区追着鼠标一跳一跳地走。
        if(swipeMovePosted)return;swipeMovePosted=true;
        ui.BeginInvoke(new Action(delegate{
            swipeMovePosted=false;
            if(!enabled||version!=generation)return;
            SwipeMove(Interlocked.CompareExchange(ref pendingSwipeDx,0,0));
        }));
    }
    void ReportSwipeEnd(int dx,double velocity){lastSwipeDx=0;if(SwipeEnd==null||ui.HasShutdownStarted)return;int version=generation;ui.BeginInvoke(new Action(delegate{if(enabled&&version==generation)SwipeEnd(dx,velocity);}));}
    static bool Modified(){return (GetAsyncKeyState(0x10)&0x8000)!=0||(GetAsyncKeyState(0x11)&0x8000)!=0||(GetAsyncKeyState(0x12)&0x8000)!=0;}
    static bool CtrlDown(){return (GetAsyncKeyState(0x11)&0x8000)!=0;}
    void PublishBox(){
        if(marqueePreview==null)return;
        Rect rect=box.Bounds;if(rect==publishedBoxRect)return;
        publishedBoxRect=rect;int session=box.Token,version=generation;
        ui.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(enabled&&version==generation)marqueePreview(session,rect);}));
    }
    void CancelBox(){
        if(!box.Active)return;
        int session=box.Token;box.Cancel();publishedBoxRect=Rect.Empty;
        if(marqueePreview==null)return;
        int version=generation;
        ui.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(enabled&&version==generation)marqueePreview(session,Rect.Empty);}));
    }
    IntPtr KeyCallback(int code,IntPtr message,IntPtr data){
        if(!enabled||code<0)return CallNextHookEx(keyHook,code,message,data);int kind=message.ToInt32(),key=Marshal.ReadInt32(data);
        if((kind==0x101||kind==0x105)&&(key==0x25||key==0x27))navigationKeyDown=false;
        if((kind==0x100||kind==0x104)&&key==0x1b&&box.Active)CancelBox();
        if((kind==0x100||kind==0x104)&&key==0x1b&&creationEnabled&&(drag.Pressed||drag.AwaitingChoice))CancelSelection();
        if(navigate!=null&&(kind==0x100||kind==0x104)&&(key==0x25||key==0x27)&&(GetAsyncKeyState(0x11)&0x8000)!=0&&(GetAsyncKeyState(0x12)&0x8000)==0&&(GetAsyncKeyState(0x10)&0x8000)==0){
            IntPtr desktop=Interlocked.CompareExchange(ref host,IntPtr.Zero,IntPtr.Zero),foreground=GetForegroundWindow();if(Shell.DesktopSurface(foreground,desktop)||Shell.PassiveDesktopWindow(foreground,desktop)){
                if(!navigationKeyDown){navigationKeyDown=true;int direction=key==0x25?-1:1;ui.BeginInvoke(new Action(delegate{if(enabled)navigate(direction);}));}return new IntPtr(1);
            }
        }return CallNextHookEx(keyHook,code,message,data);
    }
    public static IntPtr TargetAt(NativePoint point){return WindowFromPhysicalPoint(point);}
    void SelectionTick(){
        if(box.Active){PublishBox();return;}
        if(!enabled||!creationEnabled){if(drag.Pressed||drag.AwaitingChoice||!publishedRect.IsEmpty)CancelSelection();return;}
        if((drag.Pressed||drag.AwaitingChoice)&&(GetAsyncKeyState(0x1b)&0x8000)!=0){CancelSelection();return;}
        if(!drag.Pressed){if(!drag.AwaitingChoice&&!publishedRect.IsEmpty)CancelSelection();return;}
        if(Modified()){CancelSelection();return;}
        Rect rect=drag.Progress(unchecked((uint)Environment.TickCount),350);
        if(rect.IsEmpty||rect==publishedRect)return;clicks.Reset();publishedRect=rect;publishedToken=drag.Token;int session=drag.Token,version=generation;
        ui.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(enabled&&version==generation&&creationEnabled)selectionPreview(session,rect);}));
    }
    void CancelSelection(){
        drag.Cancel();if(!publishedRect.IsEmpty&&selectionPreview!=null){int session=publishedToken,version=generation;ui.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(enabled&&version==generation)selectionPreview(session,Rect.Empty);}));}publishedRect=Rect.Empty;
    }
    public static bool IsBlank(IntPtr target,IntPtr desktop,NativePoint point){
        if(!Shell.IsWindow(desktop)||!Shell.DesktopSurface(target,desktop)||WindowFromPhysicalPoint(point)!=target)return false;
        if(target==desktop||target==Shell.DesktopView(desktop))return true;
        return BlankListPoint(target,point.X,point.Y);
    }
    public static bool BlankListPoint(IntPtr list,int x,int y){
        IAccessible accessible=null;
        try{Guid iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");if(AccessibleObjectFromWindow(list,0xfffffffc,ref iid,out accessible)!=0||accessible==null)return false;object hit=accessible.accHitTest(x,y);return hit is int&&(int)hit==0;}
        catch{return false;}finally{if(accessible!=null&&Marshal.IsComObject(accessible))Marshal.ReleaseComObject(accessible);}
    }
    public void Dispose(){
        enabled=false;Interlocked.Increment(ref generation);
        if(dispatcher!=null&&!dispatcher.HasShutdownStarted)dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        if(thread!=null&&thread!=Thread.CurrentThread)thread.Join(250);
        thread=null;dispatcher=null;clicks.Reset();swipe.Reset();
    }
}
}
