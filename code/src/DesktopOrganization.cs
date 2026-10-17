using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

namespace QiDesk {
// Keep desktop paths when Windows permits the Hidden attribute. For protected
// shortcuts, stage the path mapping on disk before an atomic same-volume move.
public static class DesktopOrganization {
    public static string LastError="";
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern void SHChangeNotify(uint eventId,uint flags,string path,IntPtr other);
    static void Notify(string path){SHChangeNotify(0x00002000,0x0005,path,IntPtr.Zero);}
    // 属性变化使用 SHCNE_ATTRIBUTES，确保 Explorer 立刻重画该图标。
    static void NotifyAttributes(string path){SHChangeNotify(0x00000800,0x0005,path,IntPtr.Zero);}
    static void Move(string source,string target){Directory.CreateDirectory(Path.GetDirectoryName(target));if(Directory.Exists(source))Directory.Move(source,target);else File.Move(source,target);Notify(source);Notify(target);}
    static void Replace(State state,string before,string after){foreach(var group in state.Groups)for(int i=0;i<group.Items.Count;i++)if(String.Equals(group.Items[i],before,StringComparison.OrdinalIgnoreCase))group.Items[i]=after;}
    public static string Release(State state,string path){
        string original;if(state.ManagedDesktopItems.TryGetValue(path,out original)){
            if(Store.Exists(path)){
                if(Store.Exists(original))throw new IOException("桌面上已有同名文件，无法移回："+original);
                try{Move(path,original);}
                catch(UnauthorizedAccessException){
                    // Public Desktop is often readable and deletable but not writable
                    // by the current user. Restore into their own Desktop instead.
                    string personal=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Path.GetFileName(original));
                    if(String.Equals(personal,original,StringComparison.OrdinalIgnoreCase)||Store.Exists(personal))throw;
                    Move(path,personal);original=personal;
                }
            }
            else if(!Store.Exists(original))throw new FileNotFoundException("整理目录和原桌面均未找到文件",path);
            state.ManagedDesktopItems.Remove(path);return original;
        }
        if(state.HiddenDesktopItems.Contains(path,StringComparer.OrdinalIgnoreCase)){
            if(Store.Exists(path)&&Functions.SetHidden(path,false))NotifyAttributes(path);
            state.HiddenDesktopItems.RemoveAll(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase));
        }
        return path;
    }
    public static void Restore(State state){
        FileRenaming.Recover(state);
        LastError="";
        foreach(var managed in state.ManagedDesktopItems.Keys.ToArray()){
            try{string original=Release(state,managed);Replace(state,managed,original);}
            catch(Exception ex){if(LastError.Length==0)LastError=managed+": "+ex.Message;}
        }
        foreach(string hidden in state.HiddenDesktopItems.ToArray()){
            try{Release(state,hidden);}
            catch(Exception ex){if(LastError.Length==0)LastError=hidden+": "+ex.Message;}
        }
    }
    public static void Sync(State state,string root,Action persist){
        LastError="";state.ManagedDesktopItems=new Dictionary<string,string>(state.ManagedDesktopItems??new Dictionary<string,string>(),StringComparer.OrdinalIgnoreCase);
        var assigned=new HashSet<string>(state.Groups.Where(g=>String.IsNullOrEmpty(g.Folder)).SelectMany(g=>g.Items),StringComparer.OrdinalIgnoreCase);
        foreach(var mapping in state.ManagedDesktopItems.ToArray()){
            string managed=mapping.Key,original=mapping.Value;
            try{
                if(assigned.Contains(managed)){if(!Store.Exists(managed)&&Store.Exists(original))Move(original,managed);continue;}
                Release(state,managed);
                state.Excluded.RemoveAll(p=>String.Equals(p,managed,StringComparison.OrdinalIgnoreCase));if(!state.Excluded.Contains(original,StringComparer.OrdinalIgnoreCase))state.Excluded.Add(original);
            }catch(Exception ex){if(LastError.Length==0)LastError=original+": "+ex.Message;}
        }
        foreach(string path in state.HiddenDesktopItems.ToArray()){
            if(assigned.Contains(path))continue;
            try{Release(state,path);}catch(Exception ex){if(LastError.Length==0)LastError=path+": "+ex.Message;}
        }
        foreach(string path in assigned.Where(DesktopItemDrag.IsDesktopFile).ToArray()){
            if(!Store.Exists(path)||state.HiddenDesktopItems.Contains(path,StringComparer.OrdinalIgnoreCase))continue;
            try{
                if(Functions.IsHidden(path)){state.HiddenDesktopItems.Add(path);continue;}
                try{if(Functions.SetHidden(path,true)){state.HiddenDesktopItems.Add(path);NotifyAttributes(path);}continue;}
                catch(UnauthorizedAccessException){}
                string target=Path.Combine(root,"organized",Guid.NewGuid().ToString("N"),Path.GetFileName(path));
                state.ManagedDesktopItems[target]=path;Replace(state,path,target);persist();
                try{Move(path,target);}catch{Replace(state,target,path);state.ManagedDesktopItems.Remove(target);persist();throw;}
            }catch(Exception ex){if(LastError.Length==0)LastError=path+": "+ex.Message;}
        }
    }
    // 还原守护进程：主进程异常退出（崩溃/被强杀）时立刻还原隐藏项与受管项。
    // 正常退出时主进程会 set 停止事件，守护进程直接退出，不重复还原。
    sealed class ProcessWaitHandle : WaitHandle {public ProcessWaitHandle(IntPtr handle){SafeWaitHandle=new Microsoft.Win32.SafeHandles.SafeWaitHandle(handle,false);}}
    public static int Guard(string[] args){
        if(args.Length!=5)return 2;
        int ownerId;long ownerTicks;string token=args[3],root=args[4];
        if(!Int32.TryParse(args[1],out ownerId)||!Int64.TryParse(args[2],out ownerTicks)||!token.StartsWith("Local\\QiDeskRestore-"))return 2;
        bool cancelled=false;
        try{
            using(var stop=EventWaitHandle.OpenExisting(token+"-stop"))
            using(var owner=Process.GetProcessById(ownerId)){
                if(owner.StartTime.ToUniversalTime().Ticks!=ownerTicks)return 2;
                using(var exited=new ProcessWaitHandle(owner.Handle)){cancelled=WaitHandle.WaitAny(new WaitHandle[]{stop,exited})==0;}
            }
        }catch{}
        if(!cancelled)try{var store=new Store(root);Restore(store.Data);store.Save();}catch{}
        return 0;
    }
}
}
