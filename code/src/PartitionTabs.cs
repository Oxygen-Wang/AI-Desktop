using System;
using System.Collections.Generic;
using System.Linq;

namespace QiDesk {
// 一个分区标签。窗口自身、文件夹映射和合并进来的分区都是同一种标签：
// 标题、内容来源、活动状态。区别只有来源规则——文件夹读磁盘，分区读自己的 Items。
public sealed class PartitionTab {
    public Group Host;
    public Group Owner;
    public string Folder;
    public string Title;
    public bool IsSelf;
    public bool Active;
    public bool Pinned;
    public bool IsFolder{get{return !String.IsNullOrEmpty(Folder);}}
    public bool IsMember{get{return !IsSelf&&!IsFolder;}}
    public string Key{get{return IsFolder?"folder:"+Owner.Id+":"+Folder:IsSelf?"self:"+Host.Id:"section:"+Owner.Id;}}
    public string ToolTip{get{return IsFolder?Folder:IsSelf?Host.Name:"桌面分区 · "+Owner.Name;}}
}

// 顶栏标签的唯一生成函数。增删标签只改两个数据源：Group.Folders（文件夹标签）
// 和 Group.HostGroupId（合并分区标签）；界面不再为“文档/图片/桌面/文件夹”各写一套。
public static class PartitionTabs {
    public static Group Active(State data,Group host){
        if(data==null||host==null)return host;
        return data.Groups.FirstOrDefault(g=>g.Id==host.ActiveGroupId&&g.HostGroupId==host.Id)??host;
    }
    // 文件夹标签的来源列表：优先 Folders，旧配置只有 Folder 时退化成单个标签。
    public static List<string> Folders(Group group){
        var result=new List<string>();
        if(group==null)return result;
        if(group.Folders!=null)foreach(string folder in group.Folders)if(!String.IsNullOrEmpty(folder)&&!result.Contains(folder,StringComparer.OrdinalIgnoreCase))result.Add(folder);
        if(result.Count==0&&!String.IsNullOrEmpty(group.Folder))result.Add(group.Folder);
        return result;
    }
    // 当前哪一份分区提供文件夹标签：活动成员自己有目录时用它，否则用窗口自身。
    // 合并两个文件夹分区后，成员目录仍然可以切换，不会被窗口自身的文件夹挡住。
    public static Group FolderOwner(State data,Group host){
        Group view=Active(data,host);
        if(view!=host&&view.Folders!=null&&view.Folders.Count>0)return view;
        return host;
    }
    // 单文件夹分区用它自己的文件夹标签表示，不再额外显示同名的自身标签。
    public static bool ShowsSelf(Group host){
        return host==null||!(host.Kind=="文件夹"&&!String.IsNullOrEmpty(host.Folder));
    }
    public static string SelfTitle(Group host){
        if(host==null)return "分区";
        if(host.Kind=="文件夹"&&String.IsNullOrEmpty(host.Folder)&&host.Name=="文件夹")return "桌面";
        return String.IsNullOrEmpty(host.Name)?"分区":host.Name;
    }
    // 标签顺序固定为：自身 → 窗口文件夹 → 活动成员的文件夹 → 合并分区。
    // 三种标签只看数据来源，不看 Kind；合并两个文件夹分区后两边的目录都还能点到。
    public static List<PartitionTab> Of(State data,Group host){
        var tabs=new List<PartitionTab>();
        if(host==null)return tabs;
        Group view=Active(data,host);
        Group folderOwner=FolderOwner(data,host);
        List<string> hostFolders=Folders(host);
        int hostActive=ActiveFolder(host,hostFolders,view==host,ShowsSelf(host));
        if(ShowsSelf(host))tabs.Add(new PartitionTab(){Host=host,Owner=host,Title=SelfTitle(host),IsSelf=true,Active=view==host&&hostActive<0});
        AddFolderTabs(tabs,host,host,hostFolders,hostActive);
        if(folderOwner!=host){
            List<string> memberFolders=Folders(folderOwner);
            var extra=new List<string>();
            foreach(string folder in memberFolders)if(!hostFolders.Contains(folder,StringComparer.OrdinalIgnoreCase))extra.Add(folder);
            int memberIndex=folderOwner.FolderIndex;
            string activeFolder=memberIndex>=0&&memberIndex<memberFolders.Count?memberFolders[memberIndex]:null;
            for(int i=0;i<extra.Count;i++){
                tabs.Add(new PartitionTab(){Host=host,Owner=folderOwner,Folder=extra[i],Title=FilePresentation.FolderName(extra[i]),Active=view==folderOwner&&String.Equals(extra[i],activeFolder,StringComparison.OrdinalIgnoreCase)});
            }
        }
        foreach(var member in data.Groups.Where(g=>g.HostGroupId==host.Id)){
            tabs.Add(new PartitionTab(){Host=host,Owner=member,Title=(member.TabPinned?"⌖  ":"")+member.Name,Pinned=member.TabPinned,Active=view==member});
        }
        return tabs;
    }
    static int ActiveFolder(Group owner,List<string> folders,bool active,bool showsSelf){
        if(!active||folders.Count==0)return -1;
        if(owner.FolderIndex>=0&&owner.FolderIndex<folders.Count)return owner.FolderIndex;
        return showsSelf?-1:0;
    }
    static void AddFolderTabs(List<PartitionTab> tabs,Group host,Group owner,List<string> folders,int active){
        for(int i=0;i<folders.Count;i++)tabs.Add(new PartitionTab(){Host=host,Owner=owner,Folder=folders[i],Title=FilePresentation.FolderName(folders[i]),Active=active==i});
    }
    // 内容规则：文件夹标签和分区标签都经由 Store.ItemsAt，共用忽略规则、搜索与排序。
    public static IEnumerable<string> Items(Store store,Group owner,string folder,string search){
        return store.ItemsAt(owner,search??"",folder??"");
    }
}
}
