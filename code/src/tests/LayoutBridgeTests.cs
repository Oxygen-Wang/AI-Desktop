using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QiDesk {
// CLI 与 MCP 必须走同一条布局分发路径，否则两条分支会慢慢走偏。
public static class LayoutBridgeTests {
    public static void Run(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"layout-bridge","store"));
        var controller=new Controller(store);
        foreach(var definition in LayoutCommands.Tools()){
            var map=(Dictionary<string,object>)definition;
            string name=Convert.ToString(map["name"]);
            check(LayoutCommands.Handles(name),"layout tool is dispatched by name: "+name);
            check(McpBridge.Tools().Cast<Dictionary<string,object>>().Any(t=>Convert.ToString(t["name"])==name),"layout tool is published for MCP: "+name);
        }
        object listed=LayoutBridge.Dispatch(controller,"qidesk_layout_list",new Dictionary<string,object>());
        check(listed!=null,"the bridge and the MCP tools share one dispatch path");
        bool rejected=false;
        try{LayoutBridge.Dispatch(controller,"qidesk_layout_list",new Dictionary<string,object>{{"bogus",1}});}catch{rejected=true;}
        check(rejected,"the bridge rejects undeclared layout arguments before applying anything");
        bool unknown=false;
        try{LayoutBridge.Dispatch(controller,"qidesk_layout_missing",new Dictionary<string,object>());}catch{unknown=true;}
        check(unknown,"the bridge rejects unknown layout commands");
    }
}
}
