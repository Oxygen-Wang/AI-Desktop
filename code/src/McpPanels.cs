using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QiDesk {
// MCP 与 CLI 共用的工作区面板工具组：面板标签即 PanelTabs，可绑定真实文件夹或桌面分区。
// 只改状态与索引；面板窗口若正开着，通过 ManagerWindow.RefreshWorkspacePanel 立即重排。
public static class McpPanels {
    static readonly string[] names={"qidesk_list_panels","qidesk_add_panel_tab","qidesk_remove_panel_tab","qidesk_select_panel_tab","qidesk_show_panel"};
    const int MaxTabs=30;
    public static bool Handles(string name){return Array.IndexOf(names,name)>=0;}
    static string Text(Dictionary<string,object> args,string key){object value;return args.TryGetValue(key,out value)&&value!=null?Convert.ToString(value).Trim():"";}
    static int Index(Dictionary<string,object> args,string key){
        object raw;if(!args.TryGetValue(key,out raw))throw new Exception(key+" is required.");
        double value;
        if(!Double.TryParse(Convert.ToString(raw,System.Globalization.CultureInfo.InvariantCulture),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value))throw new Exception("Invalid number: "+key);
        return (int)value;
    }
    public static object Execute(Controller controller,string name,Dictionary<string,object> args){
        args=args??new Dictionary<string,object>();
        var store=controller.Store;var data=store.Data;
        if(name=="qidesk_list_panels")return List(data,controller);
        if(name=="qidesk_add_panel_tab")return Add(controller,args);
        if(name=="qidesk_remove_panel_tab")return Remove(controller,args);
        if(name=="qidesk_select_panel_tab")return Select(controller,args);
        if(name=="qidesk_show_panel")return Show(controller,args);
        throw new Exception("Unknown tool.");
    }
    static object List(State data,Controller controller){
        var tabs=data.PanelTabs.Select((tab,i)=>{
            string kind="empty",folder="",groupId="",label="";
            if(tab!=null){
                if(tab.IsFolder){kind="folder";folder=tab.Folder;label=FilePresentation.FolderName(tab.Folder);}
                else{kind="group";groupId=tab.GroupId;var group=data.Groups.FirstOrDefault(g=>g.Id==tab.GroupId);label=group==null?"":group.Name;}
            }
            return new {index=i,kind=kind,folder=folder,groupId=groupId,name=label};
        }).ToArray();
        return new {activeIndex=data.PanelIndex,tabs=tabs,visible=controller.Manager!=null&&controller.Manager.WorkspacePanelVisible};
    }
    static object Add(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;var data=store.Data;
        string folder=Text(args,"path"),groupId=Text(args,"groupId");
        if(folder.Length>0&&groupId.Length>0)throw new Exception("path 和 groupId 只能提供一个。");
        if(folder.Length==0&&groupId.Length==0)throw new Exception("请提供文件夹 path 或分区 groupId。");
        if(data.PanelTabs.Count>=MaxTabs)throw new Exception("面板标签最多 "+MaxTabs+" 个。");
        PanelTab tab;
        if(folder.Length>0){
            string full;try{full=Path.GetFullPath(folder);}catch{throw new Exception("路径无效："+folder);}
            if(!Directory.Exists(full))throw new Exception("文件夹不存在。");
            if(data.PanelTabs.Any(t=>t!=null&&t.IsFolder&&String.Equals(IgnoreRule.Canonical(t.Folder),IgnoreRule.Canonical(full),StringComparison.OrdinalIgnoreCase)))throw new Exception("这个文件夹已经在面板标签中。");
            tab=PanelTab.OfFolder(full);
        }else{
            if(!data.Groups.Any(g=>g.Id==groupId))throw new Exception("Unknown groupId.");
            if(data.PanelTabs.Any(t=>t!=null&&!t.IsFolder&&t.GroupId==groupId))throw new Exception("这个分区已经在面板标签中。");
            tab=PanelTab.OfGroup(groupId);
        }
        store.Checkpoint();
        data.PanelTabs.Add(tab);data.PanelIndex=data.PanelTabs.Count-1;
        store.Save();
        if(controller.Manager!=null)controller.Manager.RefreshWorkspacePanel();
        return new {added=true,index=data.PanelIndex,kind=tab.IsFolder?"folder":"group"};
    }
    static object Remove(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;var data=store.Data;
        int index=Index(args,"index");
        if(index<0||index>=data.PanelTabs.Count)throw new Exception("标签序号必须在 0 到 "+(data.PanelTabs.Count-1)+" 之间。");
        store.Checkpoint();
        data.PanelTabs.RemoveAt(index);
        if(data.PanelTabs.Count==0)data.PanelIndex=-1;
        else if(data.PanelIndex>=data.PanelTabs.Count)data.PanelIndex=0;
        store.Save();
        if(controller.Manager!=null)controller.Manager.RefreshWorkspacePanel();
        return new {removed=true,activeIndex=data.PanelIndex};
    }
    static object Select(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;var data=store.Data;
        int index=Index(args,"index");
        if(index<0||index>=data.PanelTabs.Count)throw new Exception("标签序号必须在 0 到 "+(data.PanelTabs.Count-1)+" 之间。");
        store.Checkpoint();
        data.PanelIndex=index;store.Save();
        if(controller.Manager!=null)controller.Manager.RefreshWorkspacePanel();
        return new {activeIndex=index};
    }
    static object Show(Controller controller,Dictionary<string,object> args){
        if(controller.Manager==null)throw new Exception("Application window unavailable.");
        object raw;bool show=true;
        if(args.TryGetValue("show",out raw)&&raw!=null){
            if(!(raw is bool))throw new Exception("show must be a boolean.");
            show=(bool)raw;
        }
        if(show)controller.Manager.ShowWorkspacePanel(false);
        else if(controller.Manager.WorkspacePanelVisible)controller.Manager.ShowWorkspacePanel(true);
        return new {visible=controller.Manager.WorkspacePanelVisible};
    }
    static Dictionary<string,object> Field(string type){return new Dictionary<string,object>{{"type",type}};}
    static Dictionary<string,object> Tool(string name,string description,bool readOnly,Dictionary<string,object> properties,string[] required){
        var schema=new Dictionary<string,object>{{"type","object"},{"properties",properties},{"required",required},{"additionalProperties",false}};
        return new Dictionary<string,object>{{"name",name},{"description",description},{"inputSchema",schema},{"annotations",new {readOnlyHint=readOnly,destructiveHint=false,openWorldHint=false}}};
    }
    public static object[] Tools(){
        var empty=new Dictionary<string,object>();
        return new object[]{
            Tool("qidesk_list_panels","List workspace panel tabs: bound folders or desktop partitions, the active tab and whether the panel is open.",true,empty,new string[0]),
            Tool("qidesk_add_panel_tab","Add a tab to the workspace panel, bound to a local folder or to an existing desktop partition. Provide either path or groupId.",false,
                new Dictionary<string,object>{{"path",Field("string")},{"groupId",Field("string")}},new string[0]),
            Tool("qidesk_remove_panel_tab","Remove a workspace panel tab by its zero based index. Never deletes files.",false,
                new Dictionary<string,object>{{"index",Field("number")}},new[]{"index"}),
            Tool("qidesk_select_panel_tab","Switch the workspace panel to a tab by its zero based index.",false,
                new Dictionary<string,object>{{"index",Field("number")}},new[]{"index"}),
            Tool("qidesk_show_panel","Show or dismiss the floating workspace panel, like Alt+Space.",false,
                new Dictionary<string,object>{{"show",Field("boolean")}},new string[0]),
        };
    }
}
}
