using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;

namespace QiDesk {
// MCP 与 CLI 共用的组件工具组：自定义插件（组件类型实例）的安装与移除、
// 桌面组合包（ComponentPack）的保存、应用与移除。组件只新增桌面窗口，不碰真实文件。
public static class McpComponents {
    static readonly string[] names={"qidesk_list_plugins","qidesk_install_plugin","qidesk_remove_plugin","qidesk_list_component_packs","qidesk_save_component_pack","qidesk_apply_component_pack","qidesk_remove_component_pack"};
    public static bool Handles(string name){return Array.IndexOf(names,name)>=0;}
    static string Text(Dictionary<string,object> args,string key){object value;return args.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):"";}
    public static object Execute(Controller controller,string name,Dictionary<string,object> args){
        args=args??new Dictionary<string,object>();
        var store=controller.Store;
        if(name=="qidesk_list_plugins")return WidgetPlugins.All(store.Data).Select(p=>new {id=p.Id,name=p.Name,kind=p.Kind,description=p.Description,enabled=p.Enabled,builtin=WidgetPlugins.Builtins().Any(b=>b.Id==p.Id),text=p.Text,accent=p.Accent}).ToArray();
        if(name=="qidesk_install_plugin")return Install(controller,args);
        if(name=="qidesk_remove_plugin")return RemovePlugin(controller,args);
        if(name=="qidesk_list_component_packs")return store.Data.ComponentPacks.Select(p=>new {id=p.id,name=p.name,format=p.format,version=p.version,components=p.components==null?0:p.components.Count}).ToArray();
        if(name=="qidesk_save_component_pack")return SavePack(controller,args);
        if(name=="qidesk_apply_component_pack")return ApplyPack(controller,args);
        if(name=="qidesk_remove_component_pack")return RemovePack(controller,args);
        throw new Exception("Unknown tool.");
    }
    static object Install(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string json=Text(args,"pluginJson");
        if(json.Length==0)throw new Exception("pluginJson is required.");
        if(json.Length>64*1024)throw new Exception("插件配置不能超过 64 KB。");
        var plugin=WidgetPlugins.Parse(json);
        store.Checkpoint();
        WidgetPlugins.Import(store.Data,plugin);
        store.Save();
        controller.RecordAiChange("安装组件插件",plugin.Name,plugin.Kind,null);
        controller.Refresh();
        return new {pluginId=plugin.Id};
    }
    static object RemovePlugin(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string pluginId=Text(args,"pluginId");
        if(WidgetPlugins.Builtins().Any(p=>p.Id==pluginId))throw new Exception("内置组件不能移除。");
        var plugin=store.Data.Plugins.FirstOrDefault(p=>p.Id==pluginId);
        if(plugin==null)throw new Exception("Unknown pluginId.");
        store.Checkpoint();
        int widgets=store.Data.Widgets.RemoveAll(w=>w.PluginId==pluginId);
        store.Data.Plugins.Remove(plugin);
        store.Save();
        controller.RecordAiChange("移除组件插件",plugin.Name,widgets>0?widgets+" 个组件已一并移除":"",null);
        controller.Rebuild();controller.Refresh();
        return new {removed=true,widgetsRemoved=widgets};
    }
    static object SavePack(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string packName=Text(args,"name");
        if(packName.Length==0)throw new Exception("name is required.");
        if(packName.Length>80)throw new Exception("组合名称不能超过 80 字符。");
        store.Checkpoint();
        var pack=ComponentPackages.Export(store.Data,store.Data.Widgets,packName);
        ComponentPackages.Install(store.Data,pack);
        store.Save();
        controller.RecordAiChange("保存桌面组合",pack.name,pack.components.Count+" 个组件",null);
        controller.Refresh();
        return new {packId=pack.id,components=pack.components.Count};
    }
    static object ApplyPack(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string packId=Text(args,"packId");
        var pack=store.Data.ComponentPacks.FirstOrDefault(p=>p.id==packId);
        if(pack==null)throw new Exception("Unknown packId.");
        store.Checkpoint();
        var areas=controller.Manager==null?new List<Rect>{SystemParameters.WorkArea}:Shell.WorkingAreas(controller.Manager);
        var added=ComponentPackages.Add(store.Data,pack,areas,null);
        store.Save();
        controller.RecordAiChange("应用桌面组合",pack.name,added.Count+" 个组件",null);
        controller.Rebuild();controller.Refresh();
        return new {packId=packId,added=added.Count};
    }
    static object RemovePack(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string packId=Text(args,"packId");
        var pack=store.Data.ComponentPacks.FirstOrDefault(p=>p.id==packId);
        if(pack==null)throw new Exception("Unknown packId.");
        store.Checkpoint();
        store.Data.ComponentPacks.Remove(pack);
        store.Save();
        controller.RecordAiChange("移除桌面组合",pack.name,"",null);
        controller.Refresh();
        return new {removed=true};
    }
    static Dictionary<string,object> Field(string type){return new Dictionary<string,object>{{"type",type}};}
    static Dictionary<string,object> Tool(string name,string description,bool readOnly,Dictionary<string,object> properties,string[] required){
        var schema=new Dictionary<string,object>{{"type","object"},{"properties",properties},{"required",required},{"additionalProperties",false}};
        return new Dictionary<string,object>{{"name",name},{"description",description},{"inputSchema",schema},{"annotations",new {readOnlyHint=readOnly,destructiveHint=false,openWorldHint=false}}};
    }
    public static object[] Tools(){
        var empty=new Dictionary<string,object>();
        return new object[]{
            Tool("qidesk_list_plugins","List every widget component type: built-ins and installed custom plugins, with kind, description and enabled state.",true,empty,new string[0]),
            Tool("qidesk_install_plugin","Install a custom widget plugin from a JSON object with id, name, kind, description, text and accent.",false,
                new Dictionary<string,object>{{"pluginJson",Field("string")}},new[]{"pluginJson"}),
            Tool("qidesk_remove_plugin","Remove an installed custom plugin; its desktop instances are removed with it. Built-ins cannot be removed.",false,
                new Dictionary<string,object>{{"pluginId",Field("string")}},new[]{"pluginId"}),
            Tool("qidesk_list_component_packs","List saved desktop component packs with their component counts.",true,empty,new string[0]),
            Tool("qidesk_save_component_pack","Save the current desktop widgets as a reusable component pack. Widgets only; no files are touched.",false,
                new Dictionary<string,object>{{"name",Field("string")}},new[]{"name"}),
            Tool("qidesk_apply_component_pack","Add every widget of a saved component pack to the desktop, placing them on free space.",false,
                new Dictionary<string,object>{{"packId",Field("string")}},new[]{"packId"}),
            Tool("qidesk_remove_component_pack","Remove a saved component pack from the library. Existing widgets stay.",false,
                new Dictionary<string,object>{{"packId",Field("string")}},new[]{"packId"}),
        };
    }
}
}
