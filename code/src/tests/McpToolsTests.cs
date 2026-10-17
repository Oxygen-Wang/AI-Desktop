using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QiDesk {
// 通过真实的 MCP 工具入口验证 AI 读写分区：建分区、读布局、引用文件、加规则、整理、删分区。
// 所有操作只改分区引用和布局；真实文件既不被复制也不被移动或删除。
public static class McpToolsTests {
    static Dictionary<string,object> Call(Controller controller,string name,Dictionary<string,object> arguments){
        object result=McpBridge.Execute(controller,new Dictionary<string,object>{{"name",name},{"arguments",arguments}});
        return Store.Json.Deserialize<Dictionary<string,object>>(Store.Json.Serialize(result));
    }
    public static void Native(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"mcp-tools-state"));store.Data.UseEverything=false;
        string folder=Path.Combine(root,"mcp-tools-workspace");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"useful.txt");File.WriteAllText(file,"fixture");
        store.Data.WorkFolders.Add(folder);
        var controller=new Controller(store);var manager=new ManagerWindow(controller){Left=-15000,Top=-15000};
        try{
            manager.Show();manager.UpdateLayout();
            var created=Call(controller,"qidesk_create_group",new Dictionary<string,object>{{"name","AI 整理区"},{"x",40.0},{"y",60.0},{"width",420.0},{"height",320.0}});
            string id=Convert.ToString(created["groupId"]);
            check(!String.IsNullOrEmpty(id),"MCP creates a desktop partition with screen geometry");
            var layout=Call(controller,"qidesk_get_layout",new Dictionary<string,object>());
            check(layout.ContainsKey("groups")&&Store.Json.Serialize(layout).Contains("AI 整理区"),"MCP reads the live layout including the new partition");
            var assigned=Call(controller,"qidesk_assign_items",new Dictionary<string,object>{{"groupId",id},{"paths",new[]{file}}});
            check(Convert.ToInt32(assigned["assigned"])==1&&File.Exists(file),"MCP references a workspace file into a partition without moving it");
            Call(controller,"qidesk_add_rule",new Dictionary<string,object>{{"groupId",id},{"extensions","txt"}});
            check(store.Data.Rules.Any(r=>r.GroupId==id&&r.Extensions=="txt"),"MCP adds a partition rule for future matching files");
            var applied=Call(controller,"qidesk_apply_rules",new Dictionary<string,object>());
            check(applied.ContainsKey("moved"),"MCP can run rule organization and report how many loose icons moved");
            var deleted=Call(controller,"qidesk_delete_group",new Dictionary<string,object>{{"groupId",id}});
            check(Convert.ToBoolean(deleted["deleted"])&&store.Data.Groups.All(g=>g.Id!=id)&&File.Exists(file),"MCP deletes a partition and releases its items without touching real files");
        }finally{manager.Quit();}
    }
}
}
