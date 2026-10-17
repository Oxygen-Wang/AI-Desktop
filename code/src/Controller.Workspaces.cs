using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace QiDesk {
public partial class Controller {
    public readonly HashSet<string> SelectedGroups=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> workspaceHiddenGroups=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> workspaceRaisedGroups=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Point> selectionOrigins=new Dictionary<string,Point>(StringComparer.OrdinalIgnoreCase);

    public bool WorkspaceVisible {get{return Windows.Any(w=>workspaceRaisedGroups.Contains(w.Group.Id)&&w.IsVisible);}}
    void SaveWorkspaceSelection(){Store.Data.SelectedWorkspaceGroups=SelectedGroups.ToList();Store.Save();}
    void WorkspaceStatus(string text){if(Manager!=null){Manager.SetStatus(text);Manager.NotifyPanelVisibility(WorkspaceVisible);}}

    public void ToggleSelectedWorkspace(){
        var targets=Windows.Where(w=>SelectedGroups.Contains(w.Group.Id)).ToArray();
        if(targets.Length==0){WorkspaceStatus("先点击桌面分区右上角的圆点选择工作区；Ctrl＋左键可多选。");return;}
        if(targets.All(w=>workspaceRaisedGroups.Contains(w.Group.Id)&&w.IsVisible)){
            foreach(var window in targets)RestoreDesktopWindow(window);
            WorkspaceStatus("已将选中的分区放回桌面，分区仍然可见。");
        }else RaiseWorkspace(targets);
    }

    public void HideWorkspaceGroup(Group group){
        var targets=Windows.Where(w=>group==null?SelectedGroups.Contains(w.Group.Id):w.Group.Id==group.Id).ToArray();
        foreach(var window in targets){
            workspaceHiddenGroups.Add(window.Group.Id);
            workspaceRaisedGroups.Remove(window.Group.Id);
            window.Topmost=false;
            window.Hide();
            if(Docked)window.PlaceOnDesktop(host!=IntPtr.Zero?host:Shell.DesktopHost());
        }
        WorkspaceStatus("已收起分区；点击小猫或按 Alt＋Space 可再次唤起已选分区。");
    }

    // 中键只切换已选工作区，避免无意中把普通桌面分区加入前景集合。
    public void ToggleGroupVisibility(Group group){
        if(group==null||!SelectedGroups.Contains(group.Id)){WorkspaceStatus("请先选择工作区，再用中键切换。");return;}
        var targets=Windows.Where(w=>SelectedGroups.Contains(w.Group.Id)).ToArray();
        if(targets.All(w=>workspaceRaisedGroups.Contains(w.Group.Id)&&w.IsVisible))HideWorkspaceGroup(null);
        else RaiseWorkspace(targets);
    }

    void RaiseWorkspace(IEnumerable<FenceWindow> targets){
        var pending=targets.ToArray();
        foreach(var window in Windows.Where(w=>!SelectedGroups.Contains(w.Group.Id)))RestoreDesktopWindow(window);
        foreach(var window in pending){
            workspaceHiddenGroups.Remove(window.Group.Id);
            if(!window.IsVisible)window.Show();
            // Detach the Explorer owner before making this window topmost. Owned-window
            // ordering must never promote Explorer and its other desktop partitions.
            Shell.Float(window);
            window.Topmost=false;
            Shell.Raise(window);
            workspaceRaisedGroups.Add(window.Group.Id);
        }
        if(pending.Length>0)Shell.BeginInlineInput(pending[pending.Length-1]);
        WorkspaceStatus("已显示选中的桌面分区；Ctrl＋F 搜索，× 收起，Ctrl＋Alt＋Backspace 取消全部选择。");
    }

    void RestoreDesktopWindow(FenceWindow window){
        workspaceRaisedGroups.Remove(window.Group.Id);
        window.Topmost=false;
        Shell.Demote(window);
        if(Docked)window.PlaceOnDesktop(host!=IntPtr.Zero?host:Shell.DesktopHost());
        bool visible=!workspaceHiddenGroups.Contains(window.Group.Id)&&(!Hidden||window.Group.ExcludeFromHide);
        if(visible){if(!window.IsVisible)window.Show();}
        else window.Hide();
    }

    bool WorkspaceBindingValid(FenceWindow window,IntPtr desktop){
        return workspaceRaisedGroups.Contains(window.Group.Id)?Shell.FloatingBindingValid(window):Shell.DesktopBindingValid(window,desktop);
    }

    void ReapplyGroupVisibility(){
        var valid=new HashSet<string>(Store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.HostGroupId)).Select(g=>g.Id),StringComparer.OrdinalIgnoreCase);
        workspaceHiddenGroups.RemoveWhere(id=>!valid.Contains(id));
        workspaceRaisedGroups.RemoveWhere(id=>!valid.Contains(id)||!SelectedGroups.Contains(id));
        foreach(var window in Windows){
            if(workspaceHiddenGroups.Contains(window.Group.Id)||Hidden&&!window.Group.ExcludeFromHide){window.Topmost=false;window.Hide();continue;}
            if(workspaceRaisedGroups.Contains(window.Group.Id)){
                if(!window.IsVisible)window.Show();
                Shell.Float(window);window.Topmost=false;Shell.Raise(window);
            }else RestoreDesktopWindow(window);
        }
    }

    // Ctrl+左键在桌面空白处拖出的框选：选中框内可见分区；只改选择，不改变窗口层级。
    public Group[] GroupsInRect(Rect bounds){
        if(bounds.IsEmpty)return new Group[0];
        return Store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.HostGroupId)&&g.Page==Store.Data.Page)
            .Where(g=>GroupArrangement.VisibleBounds(g).IntersectsWith(bounds))
            .ToArray();
    }
    public void SelectGroupsInRect(Rect bounds){
        if(bounds.IsEmpty)return;
        var hits=GroupsInRect(bounds).Where(g=>Windows.Any(w=>w.Group.Id==g.Id&&w.IsVisible)).ToArray();
        SelectedGroups.Clear();
        foreach(var group in hits)SelectedGroups.Add(group.Id);
        Store.Data.SelectedWorkspaceGroups=SelectedGroups.ToList();
        Store.Save();
        foreach(var window in Windows)window.RefreshSelection();
        WorkspaceStatus(hits.Length==0?"框选没有命中分区。":"已框选 "+hits.Length+" 个分区；拖动任一选中分区的标题栏可一起移动。");
    }
    public bool ToggleGroupSelection(Group group){        if(group==null)return false;
        bool selected=SelectedGroups.Add(group.Id);
        if(selected)selectionOrigins[group.Id]=new Point(group.X,group.Y);
        if(!selected){
            SelectedGroups.Remove(group.Id);
            RestoreSelectionOrigin(group);
            workspaceHiddenGroups.Remove(group.Id);
            foreach(var window in Windows.Where(w=>w.Group.Id==group.Id))RestoreDesktopWindow(window);
        }
        SaveWorkspaceSelection();
        foreach(var window in Windows)window.RefreshSelection();
        WorkspaceStatus(selected?SelectedGroups.Count+" 个分区已选中。":"已取消选中，分区已回到桌面。");
        return selected;
    }

    public void ClearGroupSelection(){
        foreach(var group in Store.Data.Groups.Where(g=>SelectedGroups.Contains(g.Id)))RestoreSelectionOrigin(group);
        foreach(string id in SelectedGroups.ToArray())workspaceHiddenGroups.Remove(id);
        SelectedGroups.Clear();workspaceRaisedGroups.Clear();
        foreach(var window in Windows){RestoreDesktopWindow(window);window.RefreshSelection();}
        SaveWorkspaceSelection();
        WorkspaceStatus("已取消全部工作区选择，分区保留在桌面。");
    }

    public void PopOutSelectedGroups(){ToggleSelectedWorkspace();}
    void RestoreSelectionOrigin(Group group){
        Point origin;
        if(!selectionOrigins.TryGetValue(group.Id,out origin))return;
        group.X=origin.X;group.Y=origin.Y;group.Edge="";
        selectionOrigins.Remove(group.Id);
        foreach(var window in Windows.Where(w=>w.Group.Id==group.Id))window.RestoreSavedBounds();
    }
    void MergeWorkspaceState(Group source,Group target){
        selectionOrigins.Remove(source.Id);
        if(SelectedGroups.Remove(source.Id))SelectedGroups.Add(target.Id);
        if(workspaceRaisedGroups.Remove(source.Id))workspaceRaisedGroups.Add(target.Id);
        workspaceHiddenGroups.Remove(source.Id);workspaceHiddenGroups.Remove(target.Id);
        Store.Data.SelectedWorkspaceGroups=SelectedGroups.ToList();
    }
    void ResetWorkspacePresentation(){workspaceRaisedGroups.Clear();workspaceHiddenGroups.Clear();}
    public void ShowDesktopGroup(Group group){
        group=PartitionIgnores.Owner(Store.Data,group);
        if(group==null)return;
        workspaceHiddenGroups.Remove(group.Id);workspaceRaisedGroups.Remove(group.Id);
        Store.Data.Page=group.Page;
        if(Running)Rebuild();else if(Manager!=null)Start(true);
    }

    // Old floating-panel tabs are migrated once, keeping desktop styles and rule scopes.
    public bool MigrateWorkspaceTabs(){
        if(Store.Data.PanelTabs.Count==0)return false;
        var data=Store.Json.Deserialize<State>(Store.Json.Serialize(Store.Data));
        var selected=new HashSet<string>(SelectedGroups,StringComparer.OrdinalIgnoreCase);
        var tabs=data.PanelTabs.ToArray();
        if(tabs.Length==0)return false;
        foreach(var tab in tabs){
            Group group=null;
            if(tab.IsFolder){
                group=data.Groups.FirstOrDefault(g=>String.Equals(IgnoreRule.Canonical(g.Folder),IgnoreRule.Canonical(tab.Folder),StringComparison.OrdinalIgnoreCase));
                if(group==null){
                    group=new Group(){Kind="文件夹",Name=FilePresentation.FolderName(tab.Folder),Folder=tab.Folder,Page=data.Page,X=80,Y=80,Width=560,Height=400};
                    group.Folders.Add(tab.Folder);data.Groups.Add(group);
                }
                if(tab.IgnoreRules.Count>0){
                    List<IgnoreRule> local;
                    if(!group.FolderIgnoreRules.TryGetValue(tab.Folder,out local)){local=new List<IgnoreRule>();group.FolderIgnoreRules[tab.Folder]=local;}
                    foreach(var rule in tab.IgnoreRules)if(!local.Any(r=>r.Id==rule.Id))local.Add(rule);
                }
                foreach(var file in data.IgnoreFiles.Where(f=>f.Automatic&&String.IsNullOrEmpty(f.ScopeId)&&String.Equals(IgnoreRule.Canonical(f.Root),IgnoreRule.Canonical(tab.Folder),StringComparison.OrdinalIgnoreCase)).ToArray()){
                    if(data.IgnoreFiles.Any(f=>f.ScopeId==group.Id&&String.Equals(f.Source,file.Source,StringComparison.OrdinalIgnoreCase)))continue;
                    var copy=Store.Json.Deserialize<IgnoreFileSet>(Store.Json.Serialize(file));
                    copy.Id=Guid.NewGuid().ToString("N");copy.ScopeId=group.Id;copy.Automatic=false;copy.Revision="manual";
                    copy.Enabled=file.Enabled&&data.AutoImportIgnoreFiles;
                    data.IgnoreFiles.Add(copy);
                }
            }else group=data.Groups.FirstOrDefault(g=>g.Id==tab.GroupId);
            if(group!=null){
                if(!String.IsNullOrEmpty(group.HostGroupId))group=data.Groups.FirstOrDefault(g=>g.Id==group.HostGroupId)??group;
                selected.Add(group.Id);
            }
        }
        data.PanelTabs.Clear();data.PanelFolders.Clear();data.PanelIndex=-1;
        data.SelectedWorkspaceGroups=selected.ToList();
        Store.Validate(data);
        Store.Checkpoint();
        Store.Data=data;
        SelectedGroups.Clear();
        foreach(string id in selected)SelectedGroups.Add(id);
        SaveWorkspaceSelection();
        return true;
    }
}
}
