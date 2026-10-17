using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace QiDesk {
public static class ShellActions {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Operation {public IntPtr Window;public uint Function;public IntPtr From,To;public ushort Flags;[MarshalAs(UnmanagedType.Bool)]public bool Aborted;public IntPtr Mappings;public string Title;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,EntryPoint="SHFileOperationW")] static extern int SHFileOperation(ref Operation operation);
    public static bool Rename(Window owner,string path,string name){
        if(String.IsNullOrWhiteSpace(name)||name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith(".")||name.EndsWith(" "))throw new ArgumentException("请使用有效文件名，末尾不能是空格或点。");
        string destination=Path.Combine(Path.GetDirectoryName(path),name);if(path==destination)return false;IntPtr source=Marshal.StringToHGlobalUni(path+"\0\0"),target=Marshal.StringToHGlobalUni(destination+"\0\0");
        try{var operation=new Operation(){Window=new WindowInteropHelper(owner).Handle,Function=4,From=source,To=target,Flags=0x40};int result=SHFileOperation(ref operation);if(operation.Aborted)return false;if(result!=0)throw new Exception("Windows 重命名未完成，错误代码："+result);return Store.Exists(destination);}finally{Marshal.FreeHGlobal(source);Marshal.FreeHGlobal(target);}
    }
    public static string UpdatePath(string value,string before,string after,bool directory){if(String.IsNullOrEmpty(value))return value;if(String.Equals(value,before,StringComparison.OrdinalIgnoreCase))return after;return directory&&WorkDetection.Under(value,before)?after+value.Substring(before.Length):value;}
    static void UpdateRulePaths(IEnumerable<IgnoreRule> rules,string before,string after,bool directory){
        foreach(var rule in rules.Where(r=>r.Kind=="路径"))rule.Pattern=UpdatePath(rule.Pattern,before,after,directory);
    }
    public static void UpdateReferences(State state,string before,string after,bool directory){
        foreach(var group in state.Groups){
            group.Items=group.Items.Select(p=>UpdatePath(p,before,after,directory)).ToList();
            group.Folder=UpdatePath(group.Folder,before,after,directory);
            group.Folders=group.Folders.Select(p=>UpdatePath(p,before,after,directory)).ToList();
            UpdateRulePaths(group.IgnoreRules,before,after,directory);
            var folderRules=new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in group.FolderIgnoreRules){
                string folder=UpdatePath(entry.Key,before,after,directory);
                List<IgnoreRule> destination;
                if(!folderRules.TryGetValue(folder,out destination)){destination=new List<IgnoreRule>();folderRules[folder]=destination;}
                UpdateRulePaths(entry.Value,before,after,directory);
                destination.AddRange(entry.Value);
            }
            group.FolderIgnoreRules=folderRules;
        }
        foreach(var tab in state.PanelTabs.Where(t=>t!=null)){
            tab.Folder=UpdatePath(tab.Folder,before,after,directory);
            UpdateRulePaths(tab.IgnoreRules,before,after,directory);
        }
        if(state.PanelStyle!=null)state.PanelStyle.BackgroundImage=UpdatePath(state.PanelStyle.BackgroundImage,before,after,directory);
        state.Excluded=state.Excluded.Select(p=>UpdatePath(p,before,after,directory)).ToList();
        state.IgnoredWorkFolders=state.IgnoredWorkFolders.Select(p=>UpdatePath(p,before,after,directory)).ToList();
        state.WorkFolders=state.WorkFolders.Select(p=>UpdatePath(p,before,after,directory)).ToList();
        state.CurrentWorkspace=UpdatePath(state.CurrentWorkspace,before,after,directory);
        foreach(var set in state.IgnoreFiles){set.Root=UpdatePath(set.Root,before,after,directory);set.Source=UpdatePath(set.Source,before,after,directory);}
        state.WorkSources=state.WorkSources.ToDictionary(p=>UpdatePath(p.Key,before,after,directory),p=>p.Value);
        state.WorkConnectedAt=state.WorkConnectedAt.ToDictionary(p=>UpdatePath(p.Key,before,after,directory),p=>p.Value);
        UpdateRulePaths(state.IgnoreRules,before,after,directory);
    }
}
public partial class ManagerWindow {
    public void ShowFileMenu(Window owner,string path,Group group,Point screen,bool background,Action refresh){
        try{
            using(var menu=new NativeMenus(new WindowInteropHelper(owner).Handle,path,background)){
                if(background){
                    var views=new List<NativeMenuCommand>();foreach(int size in new[]{96,64,32}){int pixels=size;views.Add(new NativeMenuCommand(pixels==96?"大图标(&R)":pixels==64?"中等图标(&M)":"小图标(&N)",delegate{controller.Store.Checkpoint();group.ViewMode="图标";group.IconSize=pixels;controller.SaveAndRefresh(false);refresh();}){Checked=group.ViewMode=="图标"&&(size==96?group.IconSize>64:size==64?group.IconSize>32&&group.IconSize<=64:group.IconSize<=32)});}foreach(string mode in new[]{"列表","详细信息"}){string view=mode;views.Add(new NativeMenuCommand(view,delegate{controller.Store.Checkpoint();group.ViewMode=view;controller.SaveAndRefresh(false);refresh();}){Checked=group.ViewMode==view});}
                    var sorts=new List<NativeMenuCommand>();foreach(string kind in new[]{"名称","类型","修改时间","大小"}){string sort=kind;sorts.Add(new NativeMenuCommand(kind,delegate{controller.Store.Checkpoint();group.Sort=sort;group.SortDescending=sort=="修改时间"||sort=="大小";controller.SaveAndRefresh(false);refresh();}){Checked=group.Sort==kind});}
                    sorts.Add(new NativeMenuCommand("升序",delegate{controller.Store.Checkpoint();group.SortDescending=false;controller.SaveAndRefresh(false);refresh();}){Checked=!group.SortDescending});sorts.Add(new NativeMenuCommand("降序",delegate{controller.Store.Checkpoint();group.SortDescending=true;controller.SaveAndRefresh(false);refresh();}){Checked=group.SortDescending});menu.AddBackgroundCommands(new NativeMenuCommand("配置桌面分区",delegate{Show();WindowState=WindowState.Normal;Activate();EditGroup(group);}),views,sorts,refresh);
                    menu.AddPasteCommands(path,refresh);
                }
                var commands=new List<NativeMenuCommand>();
                if(!background){
                    commands.Add(new NativeMenuCommand("在资源管理器中查看",delegate{Shell.Reveal(path);}));commands.Add(new NativeMenuCommand("复制路径",delegate{Ui.CopyText(path);}));
                    commands.Add(new NativeMenuCommand("仅在此分区忽略",delegate{IgnoreItem(path,group);}));commands.Add(new NativeMenuCommand("全局忽略此项目",delegate{IgnoreItem(path,null);}));
                    commands.Add(NativeMenuCommand.Submenu("移动到分组",controller.Store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.Folder)).Select(g=>{var target=g;return new NativeMenuCommand(g.Name,delegate{controller.Store.Checkpoint();controller.Store.Assign(path,target);controller.SaveAndRefresh();});})));
                    if(String.IsNullOrEmpty(group.Folder))commands.Add(new NativeMenuCommand("移出此分组",delegate{controller.MoveOut(path,group);}));
                }
                commands.Add(new NativeMenuCommand("放大查看详情",delegate{ShowDetails(group,background?path:String.IsNullOrEmpty(group.Folder)?"":Path.GetDirectoryName(path),background?null:path);}));commands.Add(new NativeMenuCommand("管理此分区的忽略规则",delegate{ShowIgnoreRules(group);}));
                if(background){commands.Add(new NativeMenuCommand("显示文件后缀",delegate{controller.Store.Checkpoint();controller.Store.Data.ShowFileExtensions=!controller.Store.Data.ShowFileExtensions;controller.SaveAndRefresh(false);refresh();}){Checked=controller.Store.Data.ShowFileExtensions});commands.Add(new NativeMenuCommand("分区使用方角",delegate{controller.Store.Checkpoint();group.CornerStyle=group.CornerStyle=="方角"?"圆角":"方角";controller.SaveAndRefresh(false);refresh();}){Checked=group.CornerStyle=="方角"});commands.Add(new NativeMenuCommand("透明背景 / 恢复背景",delegate{controller.Store.Checkpoint();group.BackgroundOpacity=group.BackgroundOpacity<.05?.76:0;controller.SaveAndRefresh(false);}));commands.Add(new NativeMenuCommand("折叠 / 展开",delegate{controller.Store.Checkpoint();group.Rolled=!group.Rolled;controller.SaveAndRefresh();}));commands.Add(new NativeMenuCommand("锁定 / 解锁位置",delegate{controller.Store.Checkpoint();group.Locked=!group.Locked;controller.SaveAndRefresh(false);}));commands.Add(new NativeMenuCommand("移到下一页",delegate{controller.Store.Checkpoint();group.Page=(group.Page+1)%controller.Store.Data.Pages;controller.SaveAndRefresh();}));
                    string[] popped=controller.SelectedGroups.Count>0?controller.SelectedGroups.ToArray():new[]{group.Id};
                    commands.Add(new NativeMenuCommand(popped.Length>1?"弹成工作区面板（"+popped.Length+" 个分区）":"弹成工作区面板",delegate{controller.ClearGroupSelection();ShowWorkspacePanelWithGroups(popped);}));
                    commands.Add(new NativeMenuCommand("Ctrl＋左键多选分区后一起弹出",null){Enabled=false});}
                menu.AddGroupCommands(commands);
                menu.Show(screen,delegate(string verb){if(!background&&(verb=="open"||verb=="openas"))FileActivity.Record(path);if(background||verb!="rename")return false;string current=Path.GetFileName(path);string name=Ui.Ask("重命名", "文件名称",current,owner);if(String.IsNullOrEmpty(name)||name==current)return true;bool directory=Directory.Exists(path);if(ShellActions.Rename(owner,path,name)){string destination=Path.Combine(Path.GetDirectoryName(path),name);ShellActions.UpdateReferences(controller.Store.Data,path,destination,directory);controller.SaveAndRefresh();}return true;});
            }
            refresh();
        }catch(Exception ex){Ui.Info("Windows 菜单操作未完成："+ex.Message,owner);}
    }
}
}
