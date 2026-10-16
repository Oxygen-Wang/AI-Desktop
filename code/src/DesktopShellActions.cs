using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;

namespace QiDesk {
// Static Explorer verbs add QiDesk actions to the native desktop menu.
// The running app handles them through events, so a second process never edits state.json.
public sealed class DesktopShellActions : IDisposable {
    const string OrganizeEvent="Local\\QiDeskOrganizeLooseV1";
    readonly Controller controller;
    readonly EventWaitHandle organize;
    readonly RegisteredWaitHandle organizeWait;
    bool disposed;
    public DesktopShellActions(Controller owner){
        controller=owner;organize=new EventWaitHandle(false,EventResetMode.AutoReset,OrganizeEvent);
        organizeWait=ThreadPool.RegisterWaitForSingleObject(organize,delegate(object state,bool timeout){Dispatch();},null,-1,false);
    }
    void Dispatch(){if(disposed||controller.Manager==null)return;controller.Manager.Dispatcher.BeginInvoke(new Action(delegate{if(disposed)return;try{controller.ApplyRules();}catch(Exception ex){controller.Manager.SetStatus("桌面整理未完成："+ex.Message);}}));}
    public static bool Signal(string action){try{using(var item=EventWaitHandle.OpenExisting(OrganizeEvent)){return item.Set();}}catch(WaitHandleCannotBeOpenedException){return false;}}
    public static void Register(string executable){
        RemoveVerb("QiDeskArrangeLayout");
        RegisterVerb("QiDeskOrganizeLoose","栖桌：整理散落图标",executable,"--organize-loose");
    }
    static void RegisterVerb(string keyName,string title,string executable,string argument){
        using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Classes\DesktopBackground\Shell\"+keyName)){
            key.SetValue("MUIVerb",title);key.SetValue("Icon",executable);key.SetValue("Position","Bottom");
            using(var command=key.CreateSubKey("command"))command.SetValue("","\""+executable+"\" "+argument);
        }
    }
    static void RemoveVerb(string keyName){
        using(var shell=Registry.CurrentUser.OpenSubKey(@"Software\Classes\DesktopBackground\Shell",true)){
            if(shell==null)return;
            if(Array.Exists(shell.GetSubKeyNames(),key=>String.Equals(key,keyName,StringComparison.OrdinalIgnoreCase)))shell.DeleteSubKeyTree(keyName);
        }
    }
    public static void Unregister(){RemoveVerb("QiDeskOrganizeLoose");RemoveVerb("QiDeskArrangeLayout");}
    public void Dispose(){disposed=true;organizeWait.Unregister(null);organize.Dispose();}
}
}
