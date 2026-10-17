using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace QiDesk {
public partial class Controller {
    // 标签拖动的唯一入口：落在别的分区标题栏就合并/移动；落在桌面就拆成独立分区。
    // 文件夹标签和合并分区标签走同一条路径，区别只在数据源（Folders / HostGroupId）。
    public bool DropPartitionTab(PartitionTab tab,Point screenPoint){
        if(tab==null||tab.Host==null)return false;
        FenceWindow target=HeaderWindowAt(screenPoint,tab.Host);
        if(target!=null){
            if(tab.IsFolder)return MoveFolderTab(tab.Host,tab.Owner,tab.Folder,target.Group);
            if(tab.IsMember)return MoveMergedTab(tab.Host,tab.Owner,target.Group);
            return false;
        }
        if(tab.IsFolder)return DetachFolderTab(tab.Host,tab.Owner,tab.Folder,screenPoint)!=null;
        if(tab.IsMember){DetachGroup(tab.Host,tab.Owner,screenPoint);return true;}
        return false;
    }
    FenceWindow HeaderWindowAt(Point screenPoint,Group exclude){
        foreach(var window in Windows.ToArray()){
            if(window.Group==exclude||!window.IsVisible)continue;
            if(!String.IsNullOrEmpty(window.Group.HostGroupId))continue;
            Point local=window.PointFromScreen(screenPoint);
            if(local.X<0||local.X>window.ActualWidth||local.Y<0||local.Y>GroupArrangement.HeaderHeight)continue;
            return window;
        }
        return null;
    }
    // 关闭文件夹标签只移除映射，不移动或删除磁盘文件。
    public void CloseFolderTab(Group owner,int index){
        if(owner==null||owner.Folders==null||index<0||index>=owner.Folders.Count)return;
        Store.Checkpoint();
        CloseFolder(owner,index);
        SaveAndRefresh(false);
    }
    // 文件夹标签拖到别的分区：把目录从来源的 Folders 移到目标，规则跟着目录走。
    public bool MoveFolderTab(Group host,Group owner,string folder,Group target){
        if(host==null||owner==null||target==null||target==owner||String.IsNullOrEmpty(folder))return false;
        int index=FolderPosition(owner,folder);
        if(index<0)return false;
        if(FolderPosition(target,folder)>=0){
            Store.Checkpoint();
            TakeFolderRules(owner,folder);
            CloseFolder(owner,index);
            target.FolderIndex=FolderPosition(target,folder);
            target.ActiveGroupId=target.Id;
            host.ActiveGroupId=owner==host?host.Id:owner.Id;
            SaveAndRefresh(false);return true;
        }
        if(target.Folders!=null&&target.Folders.Count>=60)return false;
        Store.Checkpoint();
        List<IgnoreRule> rules=TakeFolderRules(owner,folder);
        CloseFolder(owner,index);
        if(target.Folders==null)target.Folders=new List<string>();
        target.Folders.Add(folder);
        if(rules.Count>0){
            List<IgnoreRule> existing;
            if(target.FolderIgnoreRules==null)target.FolderIgnoreRules=new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);
            if(!target.FolderIgnoreRules.TryGetValue(folder,out existing)){existing=new List<IgnoreRule>();target.FolderIgnoreRules[folder]=existing;}
            foreach(var rule in rules)if(!existing.Any(r=>r.Id==rule.Id))existing.Add(rule);
        }
        target.FolderIndex=target.Folders.Count-1;
        target.ActiveGroupId=target.Id;
        host.ActiveGroupId=owner==host?host.Id:owner.Id;
        SaveAndRefresh(false);
        return true;
    }
    // 文件夹标签拖到桌面：变成独立文件夹分区，外观与来源一致，磁盘文件不动。
    public Group DetachFolderTab(Group host,Group owner,string folder,Point position){
        if(host==null||owner==null||String.IsNullOrEmpty(folder))return null;
        int index=FolderPosition(owner,folder);
        if(index<0)return null;
        Store.Checkpoint();
        List<IgnoreRule> rules=TakeFolderRules(owner,folder);
        CloseFolder(owner,index);
        var group=new Group(){Name=FilePresentation.FolderName(folder),Kind="文件夹",Folder=folder,Page=host.Page,X=position.X,Y=position.Y,Width=Math.Max(240,Math.Min(host.Width,720)),Height=Math.Max(160,Math.Min(host.Height,720))};
        group.Folders.Add(folder);group.FolderIndex=0;
        CopyAppearance(host,group);
        if(rules.Count>0)group.IgnoreRules=rules;
        group.DisabledGlobalIgnoreIds=owner.DisabledGlobalIgnoreIds==null?new List<string>():owner.DisabledGlobalIgnoreIds.ToList();
        Store.Data.Groups.Add(group);
        SaveAndRefresh();
        return group;
    }
    // 从标签栏新建一个空分区标签；它和文档、图片一样是普通分区，只是当前挂在 host 下面。
    public Group NewSectionTab(Group host,string name){
        if(host==null)return null;
        Store.Checkpoint();
        var group=new Group(){Name=String.IsNullOrWhiteSpace(name)?"新分区":name.Trim(),HostGroupId=host.Id,Page=host.Page,X=host.X,Y=host.Y,Width=host.Width,Height=host.Height};
        CopyAppearance(host,group);
        Store.Data.Groups.Add(group);
        host.ActiveGroupId=group.Id;
        SaveAndRefresh(false);
        return group;
    }
    static int FolderPosition(Group owner,string folder){
        if(owner==null||owner.Folders==null||String.IsNullOrEmpty(folder))return -1;
        return owner.Folders.FindIndex(f=>String.Equals(f,folder,StringComparison.OrdinalIgnoreCase));
    }
    List<IgnoreRule> TakeFolderRules(Group owner,string folder){
        List<IgnoreRule> rules;
        if(owner==null||owner.FolderIgnoreRules==null||!owner.FolderIgnoreRules.TryGetValue(folder,out rules))return new List<IgnoreRule>();
        owner.FolderIgnoreRules.Remove(folder);
        var copy=new List<IgnoreRule>();
        foreach(var rule in rules)copy.Add(Store.Json.Deserialize<IgnoreRule>(Store.Json.Serialize(rule)));
        return copy;
    }
    static void CloseFolder(Group owner,int index){
        if(owner==null||owner.Folders==null||index<0||index>=owner.Folders.Count)return;
        string folder=owner.Folders[index];
        owner.Folders.RemoveAt(index);
        if(owner.FolderIgnoreRules!=null)owner.FolderIgnoreRules.Remove(folder);
        if(String.Equals(owner.Folder,folder,StringComparison.OrdinalIgnoreCase))owner.Folder=owner.Folders.Count>0?owner.Folders[0]:"";
        if(owner.FolderIndex>index)owner.FolderIndex--;
        if(owner.FolderIndex>=owner.Folders.Count)owner.FolderIndex=owner.Folders.Count-1;
        if(owner.FolderIndex<0&&owner.Kind=="文件夹"&&!String.IsNullOrEmpty(owner.Folder))owner.FolderIndex=0;
    }
    static void CopyAppearance(Group source,Group target){
        target.Color=source.Color;target.LabelColor=source.LabelColor;target.LabelSize=source.LabelSize;target.LabelFont=source.LabelFont;
        target.IconSize=source.IconSize;target.IconSkin=source.IconSkin;target.IconColor=source.IconColor;
        target.CornerStyle=source.CornerStyle;target.TintStrength=source.TintStrength;target.BackgroundStyle=source.BackgroundStyle;
        target.BackgroundImage=source.BackgroundImage;target.ImageMode=source.ImageMode;target.ViewMode=source.ViewMode;
        target.BackgroundOpacity=source.BackgroundOpacity;target.BorderOpacity=source.BorderOpacity;target.TitleMode=source.TitleMode;
        target.Sort=source.Sort;target.SortDescending=source.SortDescending;
    }
}
}
