using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace QiDesk {
// A lease changes visibility only for its recorded HWND. The property prevents
// restoring a reused handle, and the independent process survives app failure.
public sealed class DesktopIcons : IDisposable {
    public readonly IntPtr Window;
    readonly uint windowProcess;
    readonly string token;
    EventWaitHandle stop;
    Process guard;
    bool changed;
    public bool Active {get{return changed;}}
    public DesktopIcons(IntPtr window){
        if(!Shell.IsWindow(window))throw new InvalidOperationException("没有找到原桌面图标窗口。");
        Window=window;windowProcess=Shell.ProcessId(window);token="Local\\QiDeskIcons-"+Guid.NewGuid().ToString("N");
        if(!Shell.Visible(window))return;
        try{
            using(var owner=Process.GetCurrentProcess()){
                if(!Shell.SetProp(window,token,new IntPtr(owner.Id)))throw new InvalidOperationException("无法记录桌面图标恢复状态。");
                stop=new EventWaitHandle(false,EventResetMode.ManualReset,token+"-stop");
                using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,token+"-ready")){
                    guard=Process.Start(new ProcessStartInfo(owner.MainModule.FileName,"--icon-guard "+owner.Id+" "+owner.StartTime.ToUniversalTime().Ticks+" "+window.ToInt64()+" "+windowProcess+" "+token){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
                    if(!ready.WaitOne(3000)||guard.HasExited)throw new InvalidOperationException("图标恢复进程启动失败，未隐藏原图标。");
                    changed=true;
                    if(!Shell.SetVisible(window,false))throw new InvalidOperationException("无法隐藏原桌面图标。");
                }
            }
        }catch{Dispose();throw;}
    }
    public void Dispose(){
        if(changed&&Shell.ProcessId(Window)==windowProcess&&Shell.GetProp(Window,token)!=IntPtr.Zero)Shell.SetVisible(Window,true);
        changed=false;
        if(Shell.GetProp(Window,token)!=IntPtr.Zero)Shell.RemoveProp(Window,token);
        if(stop!=null){stop.Set();stop.Dispose();stop=null;}
        if(guard!=null){guard.Dispose();guard=null;}
    }
    // Wraps an existing waitable handle without owning it, so no extra event object leaks.
    sealed class ProcessWaitHandle : WaitHandle {public ProcessWaitHandle(IntPtr handle){SafeWaitHandle=new SafeWaitHandle(handle,false);}}
    public static int Guard(string[] args){
        if(args.Length!=6)return 2;
        int ownerId;long ownerTicks,hwnd;uint targetProcess;
        if(!Int32.TryParse(args[1],out ownerId)||!Int64.TryParse(args[2],out ownerTicks)||!Int64.TryParse(args[3],out hwnd)||!UInt32.TryParse(args[4],out targetProcess)||!args[5].StartsWith("Local\\QiDeskIcons-"))return 2;
        string token=args[5];IntPtr window=new IntPtr(hwnd);bool cancelled=false;
        try{
            using(var stop=EventWaitHandle.OpenExisting(token+"-stop"))
            using(var ready=EventWaitHandle.OpenExisting(token+"-ready")){
                using(var owner=Process.GetProcessById(ownerId)){
                    if(owner.StartTime.ToUniversalTime().Ticks!=ownerTicks)return 2;
                    using(var exited=new ProcessWaitHandle(owner.Handle)){
                        ready.Set();
                        cancelled=WaitHandle.WaitAny(new WaitHandle[]{stop,exited})==0;
                    }
                }
            }
        }catch{ }
        finally{
            if(!cancelled&&Shell.IsWindow(window)&&Shell.ProcessId(window)==targetProcess&&Shell.GetProp(window,token)==new IntPtr(ownerId)){
                Shell.SetVisible(window,true);Shell.RemoveProp(window,token);
            }
        }
        return 0;
    }
}
}
