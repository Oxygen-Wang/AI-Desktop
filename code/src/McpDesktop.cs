using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QiDesk {
// MCP 与 CLI 共用的桌面操作工具组：桌面项列举与移出、打开/定位/重命名、翻页、
// 桌面模式与显隐、状态快照、撤销、截图列表与音乐控制。
// 重命名会改真实文件名，因此限死在桌面项、已连接工作区与面板目录内，并沿用
// FileRenaming 的未决日志（先落盘再改名，崩溃后可恢复）。
public static class McpDesktop {
    static readonly string[] names={
        "qidesk_list_desktop_items","qidesk_remove_items","qidesk_open_item","qidesk_reveal_item",
        "qidesk_rename_item","qidesk_set_page","qidesk_set_desktop_mode","qidesk_set_hidden",
        "qidesk_get_status","qidesk_undo","qidesk_list_screenshots","qidesk_music_control","qidesk_music_now"};
    public static bool Handles(string name){return Array.IndexOf(names,name)>=0;}
    static string Text(Dictionary<string,object> args,string key){object value;return args.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):"";}
    static string Full(Dictionary<string,object> args,string key){
        string path=Text(args,key).Trim().Trim('"');
        if(path.Length==0)throw new Exception(key+" is required.");
        try{return Path.GetFullPath(path);}catch{throw new Exception("路径无效："+path);}
    }
    static double Number(Dictionary<string,object> args,string key,double fallback){
        object raw;if(!args.TryGetValue(key,out raw)||raw==null)return fallback;
        double value;
        if(!Double.TryParse(Convert.ToString(raw,System.Globalization.CultureInfo.InvariantCulture),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))throw new Exception("Invalid number: "+key);
        return value;
    }
    static bool Flag(Dictionary<string,object> args,string key){
        object raw;if(!args.TryGetValue(key,out raw)||!(raw is bool))throw new Exception(key+" must be a boolean.");
        return (bool)raw;
    }
    // 只有桌面项、已连接工作区或面板目录内的项目允许改名：MCP 不该顺手的重命名系统文件。
    static bool Mutable(Store store,string path){
        if(Store.DesktopItems().Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)))return true;
        if(store.Data.WorkFolders.Any(root=>WorkDetection.Under(path,root)))return true;
        return store.Data.PanelTabs.Any(t=>t!=null&&t.IsFolder&&WorkDetection.Under(path,t.Folder));
    }
    public static object Execute(Controller controller,string name,Dictionary<string,object> args){
        args=args??new Dictionary<string,object>();
        var store=controller.Store;
        if(name=="qidesk_list_desktop_items")return ListItems(store);
        if(name=="qidesk_remove_items")return RemoveItems(controller,args);
        if(name=="qidesk_open_item")return OpenItem(controller,args);
        if(name=="qidesk_reveal_item")return RevealItem(controller,args);
        if(name=="qidesk_rename_item")return RenameItem(controller,args);
        if(name=="qidesk_set_page")return SetPage(controller,args);
        if(name=="qidesk_set_desktop_mode")return SetDesktopMode(controller,args);
        if(name=="qidesk_set_hidden")return SetHidden(controller,args);
        if(name=="qidesk_get_status")return Status(controller);
        if(name=="qidesk_undo")return Undo(controller);
        if(name=="qidesk_list_screenshots")return Screenshots(controller);
        if(name=="qidesk_music_control")return Music(args);
        if(name=="qidesk_music_now"){var now=NetEaseMusic.Now();return new {running=now.Running,title=now.Title??"",artist=now.Artist??""};}
        throw new Exception("Unknown tool.");
    }
    static object ListItems(Store store){
        var desktop=Store.DesktopItems();
        var items=desktop.Take(500).Select(p=>{
            var group=store.Assigned(p);
            return new {name=Path.GetFileName(p),path=p,type=FilePresentation.Type(p),size=FilePresentation.Size(p),loose=group==null,groupId=group==null?"":group.Id,group=group==null?"":group.Name,customIcon=CustomIcons.Has(store.Data,p)};
        }).ToArray();
        return new {total=desktop.Count(),items=items};
    }
    static object RemoveItems(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;object raw;
        if(!args.TryGetValue("paths",out raw))throw new Exception("paths is required.");
        var requested=raw as System.Collections.IEnumerable;
        if(requested==null||raw is string)throw new Exception("paths must be an array.");
        store.Checkpoint();
        int removed=0;var names=new List<string>();var skipped=new List<string>();
        foreach(object item in requested){
            string path=item as string;
            if(path==null){skipped.Add(Convert.ToString(item));continue;}
            try{path=Path.GetFullPath(path);}catch{skipped.Add(path);continue;}
            if(store.Assigned(path)==null){skipped.Add(path);continue;}
            store.RemoveFromGroups(path);removed++;names.Add(Path.GetFileName(path));
        }
        if(removed>0){
            store.Save();
            controller.RecordAiChange("移出分区",null,removed+" 个",names.ToArray());
            controller.Rebuild();controller.Refresh();
        }
        return new {removed=removed,skipped=skipped.Take(20).ToArray()};
    }
    static object OpenItem(Controller controller,Dictionary<string,object> args){
        string path=Full(args,"path");
        if(!Store.Exists(path))throw new Exception("项目不存在，无法打开。");
        Shell.Open(path,false);
        return new {opened=true,path=path};
    }
    static object RevealItem(Controller controller,Dictionary<string,object> args){
        string path=Full(args,"path");
        if(!Store.Exists(path))throw new Exception("项目不存在，无法定位。");
        Shell.Reveal(path,false);
        return new {revealed=true,path=path};
    }
    static object RenameItem(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string path=Full(args,"path"),name=Text(args,"name");
        if(controller.Manager==null)throw new Exception("Application window unavailable.");
        if(!Store.Exists(path))throw new Exception("项目不存在，无法重命名。");
        if(!Mutable(store,path))throw new Exception("只能重命名桌面项目、已连接工作区或面板目录内的文件。");
        string destination=FileRenaming.Rename(store,controller.Manager,path,name);
        store.Save();
        controller.RecordAiChange("重命名",Path.GetFileName(path),Path.GetFileName(destination),null);
        controller.Rebuild();controller.Refresh();
        return new {path=destination,renamed=!String.Equals(path,destination,StringComparison.OrdinalIgnoreCase)};
    }
    static object SetPage(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;int page=(int)Number(args,"page",1);
        if(page<1||page>store.Data.Pages)throw new Exception("page 必须在 1 到 "+store.Data.Pages+" 之间。");
        store.Checkpoint();store.Data.Page=page-1;
        controller.SaveAndRefresh();
        return new {page=page,pages=store.Data.Pages};
    }
    static object SetDesktopMode(Controller controller,Dictionary<string,object> args){
        bool enabled=Flag(args,"enabled");
        if(controller.Manager==null)throw new Exception("Application window unavailable.");
        if(enabled){if(!controller.Running)controller.Start(true);}
        else if(controller.Running)controller.Stop();
        controller.Refresh();
        return new {desktopMode=controller.Running&&controller.Docked};
    }
    static object SetHidden(Controller controller,Dictionary<string,object> args){
        bool hidden=Flag(args,"hidden");
        if(controller.Manager==null)throw new Exception("Application window unavailable.");
        if(!controller.Running)throw new Exception("请先启动桌面模式，再隐藏或显示分区。");
        if(controller.Hidden!=hidden)controller.ToggleHidden();
        return new {hidden=controller.Hidden};
    }
    static object Status(Controller controller){
        var store=controller.Store;var data=store.Data;
        return new {
            running=controller.Running,docked=controller.Docked,hidden=controller.Hidden,
            page=data.Page+1,pages=data.Pages,
            partitions=data.Groups.Count(g=>String.IsNullOrEmpty(g.HostGroupId)),widgets=data.Widgets.Count,
            desktopItems=Store.DesktopItems().Count(),looseItems=Store.DesktopItems().Count(p=>store.Assigned(p)==null),
            workspaces=data.WorkFolders.Count,currentWorkspace=WorkspaceSelection.Current(data),
            mcpEnabled=data.McpEnabled,aiConfigured=(data.AiEndpoint??"").Length>0,
            petVisible=data.PetVisible,panelTabs=data.PanelTabs.Count,
            customIcons=data.CustomIcons==null?0:data.CustomIcons.Count,layoutMode=data.LayoutMode
        };
    }
    static object Undo(Controller controller){
        if(!controller.Store.Undo())throw new Exception("没有可撤销的修改。");
        controller.SaveAndRefresh();controller.Refresh();
        return new {undone=true};
    }
    static object Screenshots(Controller controller){
        if(controller.Manager==null)throw new Exception("Application window unavailable.");
        return controller.Manager.Screenshots().Select(p=>new {name=Path.GetFileName(p),path=p,modified=File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm")}).ToArray();
    }
    static object Music(Dictionary<string,object> args){
        string action=Text(args,"action");
        if(action=="play-pause")NetEaseMusic.PlayPause();
        else if(action=="next")NetEaseMusic.Next();
        else if(action=="previous")NetEaseMusic.Previous();
        else throw new Exception("action 必须是 play-pause、next 或 previous。");
        return new {action=action};
    }
    static Dictionary<string,object> Field(string type){return new Dictionary<string,object>{{"type",type}};}
    static Dictionary<string,object> Paths(){return new Dictionary<string,object>{{"type","array"},{"items",Field("string")},{"minItems",1},{"maxItems",100}};}
    static Dictionary<string,object> Tool(string name,string description,bool readOnly,Dictionary<string,object> properties,string[] required){
        var schema=new Dictionary<string,object>{{"type","object"},{"properties",properties},{"required",required},{"additionalProperties",false}};
        return new Dictionary<string,object>{{"name",name},{"description",description},{"inputSchema",schema},{"annotations",new {readOnlyHint=readOnly,destructiveHint=false,openWorldHint=false}}};
    }
    public static object[] Tools(){
        var empty=new Dictionary<string,object>();
        return new object[]{
            Tool("qidesk_list_desktop_items","List real desktop items with their partition membership, type and size, so an item can be found by name. At most 500 items.",true,empty,new string[0]),
            Tool("qidesk_remove_items","Release desktop items from their partitions back to loose desktop icons. Display only; files are never moved or deleted.",false,
                new Dictionary<string,object>{{"paths",Paths()}},new[]{"paths"}),
            Tool("qidesk_open_item","Open a desktop item with its default application.",false,
                new Dictionary<string,object>{{"path",Field("string")}},new[]{"path"}),
            Tool("qidesk_reveal_item","Open File Explorer with a desktop item selected.",false,
                new Dictionary<string,object>{{"path",Field("string")}},new[]{"path"}),
            Tool("qidesk_rename_item","Rename a desktop item, a file inside a connected workspace or inside a panel folder. Renames the real file with a persisted recovery journal.",false,
                new Dictionary<string,object>{{"path",Field("string")},{"name",Field("string")}},new[]{"path","name"}),
            Tool("qidesk_set_page","Switch the visible desktop page. Page is one based.",false,
                new Dictionary<string,object>{{"page",Field("number")}},new[]{"page"}),
            Tool("qidesk_set_desktop_mode","Start or stop desktop mode. Starting attaches partitions to the Explorer desktop; stopping restores native desktop icons.",false,
                new Dictionary<string,object>{{"enabled",Field("boolean")}},new[]{"enabled"}),
            Tool("qidesk_set_hidden","Hide or show every partition and widget at once, like the tray command.",false,
                new Dictionary<string,object>{{"hidden",Field("boolean")}},new[]{"hidden"}),
            Tool("qidesk_get_status","Read the live runtime status: desktop mode, current page, counts, workspace, AI endpoint presence and pet visibility.",true,empty,new string[0]),
            Tool("qidesk_undo","Undo the most recent desktop change made through QiDesk, including AI changes.",false,empty,new string[0]),
            Tool("qidesk_list_screenshots","List recent screenshots from the configured folder and the system screenshot folders.",true,empty,new string[0]),
            Tool("qidesk_music_control","Send a media command to the music application: play-pause, next or previous.",false,
                new Dictionary<string,object>{{"action",Field("string")}},new[]{"action"}),
            Tool("qidesk_music_now","Read the track currently playing in the music application.",true,empty,new string[0]),
        };
    }
}
}
