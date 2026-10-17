using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace QiDesk {
// 本地命令行入口。只负责把命令发给运行中的栖桌实例；实际校验和应用在 LayoutArchives 中。
public static class LayoutCli {
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int processId);
    public static int Run(string[] args){
        AttachConsole(-1);
        try{
            string command;
            var payload=new Dictionary<string,object>();
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
            }else{
                Console.Error.WriteLine("用法：QiDesk.exe --layout-list | --layout-config | --layout-export <id> [--name 名称] | --layout-preview <id> | --layout-apply <id> [--layout-only] [--include-excluded] | --layout-undo");
                return 2;
            }
            object result=McpSession.Call(LayoutBridge.PipeName,command,payload,60000);
            string json=JsonPretty.Format(Store.Json.Serialize(result));
            try{System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-cli-result.json"),json);}catch{}
            Console.WriteLine(json);
            return 0;
        }catch(Exception ex){
            try{System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-cli-error.txt"),ex.Message);}catch{}
            Console.Error.WriteLine("栖桌布局命令失败："+ex.Message);
            return 1;
        }
    }
    static string Value(string[] args,string flag){int index=Array.IndexOf(args,flag);return index>=0&&index+1<args.Length&&!args[index+1].StartsWith("--")?args[index+1]:"";}
}
}
