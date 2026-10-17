using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace QiDesk {
// 本地命令行入口。只负责把命令发给运行中的栖桌实例；实际校验和应用在 McpBridge 及各
// Mcp* 模块中，CLI 与 MCP 因此永远只有一套行为。
// --call 可调用任意 MCP 工具；其余开关是高频命令的快捷方式，最终都汇成同一次工具调用。
public static class LayoutCli {
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int processId);
    static readonly string[] flags={
        "--call","--arg","--json","--json-file","--status","--groups","--items","--layout",
        "--custom-icons","--icon-skins","--skin","--panels","--plugins","--packs",
        "--screenshots","--music-now","--organize","--undo","--search","--page",
        "--desktop-mode","--hide","--set-icon","--reset-icon","--shortcut-icon",
        "--reset-shortcut-icon","--open","--reveal","--rename","--add-tab","--add-group-tab",
        "--remove-tab","--select-tab","--show-panel","--install-plugin","--remove-plugin",
        "--save-pack","--apply-pack","--remove-pack","--music"};
    public static bool Handles(string[] args){
        if(args==null)return false;
        foreach(string a in args){
            if(a==null)continue;
            if(a.StartsWith("--layout-")||Array.IndexOf(flags,a)>=0)return true;
        }
        return false;
    }
    public static int Run(string[] args){
        AttachConsole(-1);
        try{
            if(args.Any(a=>a!=null&&a.StartsWith("--layout-")))return Layout(args);
            if(Array.IndexOf(args,"--call")>=0)return Call(args);
            return Shortcut(args);
        }catch(Exception ex){
            try{System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-cli-error.txt"),ex.Message);}catch{}
            Console.Error.WriteLine("栖桌命令失败："+ex.Message);
            return 1;
        }
    }
    static int Layout(string[] args){
        string command;var payload=new Dictionary<string,object>();
        if(args.Contains("--layout-list"))command="qidesk_layout_list";
        else if(args.Contains("--layout-config"))command="qidesk_layout_config";
        else if(args.Contains("--layout-undo"))command="qidesk_layout_undo";
        else if(args.Contains("--layout-export")){
            command="qidesk_layout_export";
            payload["archiveId"]=Value(args,"--layout-export");
            string name=Value(args,"--name");if(name.Length>0)payload["name"]=name;
        }else if(args.Contains("--layout-preview")){
            command="qidesk_layout_preview";
            payload["archiveId"]=Value(args,"--layout-preview");
            payload["organize"]=!args.Contains("--layout-only");
            if(args.Contains("--include-excluded"))payload["includeExcluded"]=true;
        }else if(args.Contains("--layout-apply")){
            command="qidesk_layout_apply";
            payload["archiveId"]=Value(args,"--layout-apply");
            payload["organize"]=!args.Contains("--layout-only");
            if(args.Contains("--include-excluded"))payload["includeExcluded"]=true;
        }else return Usage();
        return Send(command,payload);
    }
    // 任意 MCP 工具：--call 工具名 --json '{...}' 或 --arg 键=值（可重复，后写覆盖先写）。
    static int Call(string[] args){
        int at=Array.IndexOf(args,"--call");string tool=at>=0&&at+1<args.Length?args[at+1]:"";
        if(tool.Length==0||tool.StartsWith("--"))return Usage();
        var payload=new Dictionary<string,object>();
        int file=Array.IndexOf(args,"--json-file");
        if(file>=0){
            if(file+1>=args.Length)return Usage();
            Merge(payload,Store.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[file+1])));
        }
        int inline=Array.IndexOf(args,"--json");
        if(inline>=0){
            if(inline+1>=args.Length)return Usage();
            Merge(payload,Store.Json.Deserialize<Dictionary<string,object>>(args[inline+1]));
        }
        for(int i=0;i<args.Length;i++){
            if(args[i]!="--arg"||i+1>=args.Length)continue;
            string pair=args[i+1];int cut=pair.IndexOf('=');
            if(cut<=0)return Usage();
            payload[pair.Substring(0,cut)]=Value(pair.Substring(cut+1));
        }
        return Send(tool,payload);
    }
    static int Shortcut(string[] args){
        string tool="";var payload=new Dictionary<string,object>();
        if(args.Contains("--status"))tool="qidesk_get_status";
        else if(args.Contains("--groups"))tool="qidesk_list_groups";
        else if(args.Contains("--items"))tool="qidesk_list_desktop_items";
        else if(args.Contains("--layout"))tool="qidesk_get_layout";
        else if(args.Contains("--custom-icons"))tool="qidesk_list_custom_icons";
        else if(args.Contains("--icon-skins"))tool="qidesk_list_icon_skins";
        else if(args.Contains("--panels"))tool="qidesk_list_panels";
        else if(args.Contains("--plugins"))tool="qidesk_list_plugins";
        else if(args.Contains("--packs"))tool="qidesk_list_component_packs";
        else if(args.Contains("--screenshots"))tool="qidesk_list_screenshots";
        else if(args.Contains("--music-now"))tool="qidesk_music_now";
        else if(args.Contains("--organize"))tool="qidesk_apply_rules";
        else if(args.Contains("--undo"))tool="qidesk_undo";
        else if(args.Contains("--search")){tool="qidesk_search_files";payload["query"]=Value(args,"--search");}
        else if(args.Contains("--page")){tool="qidesk_set_page";payload["page"]=Number(args,"--page",1);}
        else if(args.Contains("--desktop-mode")){tool="qidesk_set_desktop_mode";payload["enabled"]=Value(args,"--desktop-mode")=="on";}
        else if(args.Contains("--hide")){tool="qidesk_set_hidden";payload["hidden"]=Value(args,"--hide")=="on";}
        else if(args.Contains("--set-icon")){
            tool="qidesk_set_custom_icon";payload["path"]=Value(args,"--set-icon");payload["source"]=Value(args,"--set-icon",1);payload["index"]=Number(args,"--index",0);
        }
        else if(args.Contains("--reset-icon")){tool="qidesk_reset_custom_icon";payload["path"]=Value(args,"--reset-icon");}
        else if(args.Contains("--shortcut-icon")){
            tool="qidesk_set_shortcut_icon";payload["path"]=Value(args,"--shortcut-icon");payload["source"]=Value(args,"--shortcut-icon",1);payload["index"]=Number(args,"--index",0);
        }
        else if(args.Contains("--reset-shortcut-icon")){tool="qidesk_reset_shortcut_icon";payload["path"]=Value(args,"--reset-shortcut-icon");}
        else if(args.Contains("--skin")){tool="qidesk_set_icon_skin";payload["skin"]=Value(args,"--skin");string group=Value(args,"--group");if(group.Length>0)payload["groupId"]=group;}
        else if(args.Contains("--open")){tool="qidesk_open_item";payload["path"]=Value(args,"--open");}
        else if(args.Contains("--reveal")){tool="qidesk_reveal_item";payload["path"]=Value(args,"--reveal");}
        else if(args.Contains("--rename")){tool="qidesk_rename_item";payload["path"]=Value(args,"--rename");payload["name"]=Value(args,"--rename",1);}
        else if(args.Contains("--add-tab")){tool="qidesk_add_panel_tab";payload["path"]=Value(args,"--add-tab");}
        else if(args.Contains("--add-group-tab")){tool="qidesk_add_panel_tab";payload["groupId"]=Value(args,"--add-group-tab");}
        else if(args.Contains("--remove-tab")){tool="qidesk_remove_panel_tab";payload["index"]=Number(args,"--remove-tab",0);}
        else if(args.Contains("--select-tab")){tool="qidesk_select_panel_tab";payload["index"]=Number(args,"--select-tab",0);}
        else if(args.Contains("--show-panel")){tool="qidesk_show_panel";payload["show"]=Value(args,"--show-panel")!="off";}
        else if(args.Contains("--install-plugin")){
            tool="qidesk_install_plugin";string file=Value(args,"--install-plugin");
            if(file.Length==0||!File.Exists(file))return Usage();
            payload["pluginJson"]=File.ReadAllText(file);
        }
        else if(args.Contains("--remove-plugin")){tool="qidesk_remove_plugin";payload["pluginId"]=Value(args,"--remove-plugin");}
        else if(args.Contains("--save-pack")){tool="qidesk_save_component_pack";payload["name"]=Value(args,"--save-pack");}
        else if(args.Contains("--apply-pack")){tool="qidesk_apply_component_pack";payload["packId"]=Value(args,"--apply-pack");}
        else if(args.Contains("--remove-pack")){tool="qidesk_remove_component_pack";payload["packId"]=Value(args,"--remove-pack");}
        else if(args.Contains("--music")){tool="qidesk_music_control";payload["action"]=Value(args,"--music");}
        else return Usage();
        if(tool.Length==0)return Usage();
        return Send(tool,payload);
    }
    static int Send(string tool,Dictionary<string,object> payload){
        int timeout=(tool=="qidesk_layout_apply"||tool=="qidesk_set_desktop_mode")?60000:12000;
        object result=McpSession.Call(LayoutBridge.PipeName,tool,payload,timeout);
        string json=JsonPretty.Format(Store.Json.Serialize(result));
        try{System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-cli-result.json"),json);}catch{}
        Console.WriteLine(json);
        return 0;
    }
    static void Merge(Dictionary<string,object> target,Dictionary<string,object> source){
        if(source==null)return;
        foreach(var pair in source)target[pair.Key]=pair.Value;
    }
    // 值按 JSON 字面量解析：true/false、数字、数组与对象原样传入，其余按字符串。
    static object Value(string text){
        string t=(text??"").Trim();
        if(t=="true")return true;
        if(t=="false")return false;
        double number;
        if(t.Length>0&&Double.TryParse(t,NumberStyles.Float,CultureInfo.InvariantCulture,out number))return number;
        if(t.StartsWith("[")||t.StartsWith("{")){
            // 非法 JSON 不是命令行能修的错：按原字符串传下去，让工具自己去报参数错误。
            try{return Store.Json.DeserializeObject(t);}
            catch(ArgumentException){return text;}
        }
        return text;
    }
    static string Value(string[] args,string flag){int at=Array.IndexOf(args,flag);return at>=0&&at+1<args.Length&&!args[at+1].StartsWith("--")?args[at+1]:"";}
    static string Value(string[] args,string flag,int skip){
        int at=Array.IndexOf(args,flag);int next=at+1+skip;
        return at>=0&&next<args.Length&&!args[next].StartsWith("--")?args[next]:"";
    }
    static double Number(string[] args,string flag,double fallback){
        string text=Value(args,flag);double value;
        return text.Length>0&&Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)?value:fallback;
    }
    static int Usage(){
        string[] lines={
            "用法：QiDesk.exe --call <工具名> [--json '<参数JSON>'] [--json-file <文件>] [--arg 键=值 ...]",
            "  --call 可调用任意 MCP 工具，例如：--call qidesk_set_custom_icon --arg path=C:\\a.txt --arg source=C:\\b.ico",
            "布局：--layout-list | --layout-config | --layout-export <id> [--name 名称] | --layout-preview <id> | --layout-apply <id> [--layout-only] [--include-excluded] | --layout-undo",
            "查询：--status --groups --items --layout --panels --plugins --packs --custom-icons --icon-skins --screenshots --music-now --search <词>",
            "整理：--organize --undo --page <n> --desktop-mode on|off --hide on|off",
            "图标：--set-icon <项目> <来源> [--index n] --reset-icon <项目> --shortcut-icon <lnk> <来源> [--index n] --reset-shortcut-icon <lnk> --skin <名称> [--group <id>]",
            "文件：--open <项目> --reveal <项目> --rename <项目> <新名>",
            "面板：--add-tab <文件夹> --add-group-tab <groupId> --remove-tab <n> --select-tab <n> --show-panel on|off",
            "组件：--install-plugin <json文件> --remove-plugin <id> --save-pack <名称> --apply-pack <id> --remove-pack <id>",
            "媒体：--music <play-pause|next|previous>",
            "SVG 生成 .ico（无需运行栖桌）：QiDesk.exe --build-icon <svg> <ico>",
        };
        Console.Error.WriteLine(String.Join(Environment.NewLine,lines));
        return 2;
    }
}
}
