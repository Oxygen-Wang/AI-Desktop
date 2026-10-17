using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QiDesk {
// 状态文件损坏时的降级路径：必须保住桌面图标恢复记录，不能整份回退默认值。
public static class StoreRecoveryTests {
    public static void Run(string root,Action<bool,string> check){
        DuplicateGroupIdsKeepRecovery(root,check);
        BrokenPluginsOnlyDropPlugins(root,check);
        UnreadableGroupsStillKeepRecovery(root,check);
        SnapshotsStayBounded(root,check);
    }
    static string Write(string folder,string json){
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"state.json"),json);
        return folder;
    }
    static void DuplicateGroupIdsKeepRecovery(string root,Action<bool,string> check){
        string folder=Write(Path.Combine(root,"recovery-duplicate"),@"{
            ""Version"":8,""Pages"":3,""Page"":0,
            ""Groups"":[{""Id"":""dup"",""Name"":""资料"",""Items"":[]},{""Id"":""dup"",""Name"":""收件箱"",""Items"":[]}],
            ""Rules"":[],
            ""Excluded"":[],
            ""HiddenDesktopItems"":[""C:\\Users\\Public\\Desktop\\keep.lnk""],
            ""ManagedDesktopItems"":{""D:\\arch\\LOWER.lnk"":""c:\\users\\public\\desktop\\x.lnk""}
        }");
        var store=new Store(folder);
        check(store.Data.Groups.Count==2,"duplicate group ids are repaired instead of replaced by defaults");
        check(store.Data.Groups.Select(g=>g.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==2,"repaired groups keep unique ids");
        check(store.Data.HiddenDesktopItems.Count==1,"hidden desktop items survive a repaired configuration");
        check(store.Data.ManagedDesktopItems.Count==1,"managed desktop items survive a repaired configuration");
        check(store.Data.ManagedDesktopItems.ContainsKey(@"d:\arch\lower.lnk"),"managed path lookup stays case insensitive after loading");
        check(store.LastError.Contains("修复"),"a repaired configuration reports what happened ["+store.LastError+"]");
        check(Directory.GetFiles(folder,"state.json.corrupt-*").Length>0,"the damaged state file is backed up before repair");
        bool valid=true;try{Store.Validate(store.Data);}catch{valid=false;}
        check(valid,"the repaired configuration passes validation");
    }
    static void BrokenPluginsOnlyDropPlugins(string root,Action<bool,string> check){
        string folder=Write(Path.Combine(root,"recovery-plugin"),@"{
            ""Version"":8,""Pages"":3,""Page"":0,
            ""Groups"":[{""Id"":""g1"",""Name"":""应用与入口"",""Items"":[]}],
            ""Rules"":[],
            ""Plugins"":[{""Id"":""Bad Id"",""Name"":""坏插件"",""Kind"":""clock""},{""Id"":""ok.plugin"",""Name"":""好插件"",""Kind"":""note"",""Text"":"""",""Description"":"""",""Accent"":""#77BFA3""}],
            ""HiddenDesktopItems"":[""C:\\Users\\Public\\Desktop\\keep.lnk""]
        }");
        var store=new Store(folder);
        check(store.Data.Groups.Count==1&&store.Data.Groups[0].Id=="g1","an invalid plugin does not cost the partition data");
        check(store.Data.Plugins.Count==1&&store.Data.Plugins[0].Id=="ok.plugin","only the invalid plugin is dropped");
        check(store.Data.HiddenDesktopItems.Count==1,"desktop recovery survives an invalid plugin");
    }
    static void UnreadableGroupsStillKeepRecovery(string root,Action<bool,string> check){
        string folder=Write(Path.Combine(root,"recovery-structure"),@"{
            ""Version"":8,
            ""Groups"":""not-an-array"",
            ""HiddenDesktopItems"":[""C:\\Users\\Public\\Desktop\\keep.lnk""],
            ""ManagedDesktopItems"":{""D:\\arch\\x.lnk"":""C:\\Users\\Public\\Desktop\\x.lnk""}
        }");
        var store=new Store(folder);
        check(store.Data.HiddenDesktopItems.Count==1,"hidden desktop items survive an unreadable structure");
        check(store.Data.ManagedDesktopItems.Count==1,"managed desktop items survive an unreadable structure");
        check(store.LastError.Contains("恢复记录"),"an unreadable structure explains that only the recovery records were kept ["+store.LastError+"]");
    }
    static void SnapshotsStayBounded(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"snapshot-store"));
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<35;i++)names.Add(Path.GetFileName(store.Snapshot()));
        check(names.Count==35,"snapshots taken in the same millisecond do not overwrite each other");
        int manual=Directory.GetFiles(store.SnapshotRoot,"*.json").Count(p=>!Path.GetFileName(p).StartsWith("auto-",StringComparison.OrdinalIgnoreCase));
        check(manual==30,"only the 30 newest manual snapshots are kept ["+manual+"]");
    }
}
}
