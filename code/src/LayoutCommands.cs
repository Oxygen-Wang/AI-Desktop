using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QiDesk {
// MCP 与 CLI 共用的布局档案命令。真正的校验/应用在 LayoutArchives，避免两套逻辑。
public static class LayoutCommands {
    public static bool Handles(string name){return !String.IsNullOrEmpty(name)&&name.StartsWith("qidesk_layout_",StringComparison.Ordinal);}
    public static object Execute(Controller controller,string name,Dictionary<string,object> args){
        args=args??new Dictionary<string,object>();
        if(name=="qidesk_layout_list")return LayoutArchives.List(controller.Store);
        if(name=="qidesk_layout_config")return new {path=LayoutArchives.ConfigPath(controller.Store),json=LayoutArchives.Raw(controller.Store)};
        if(name=="qidesk_layout_save")return LayoutArchives.SaveJson(controller.Store,Text(args,"configJson"));
        if(name=="qidesk_layout_export")return LayoutArchives.ExportCurrent(controller.Store,Text(args,"archiveId").Length==0?"work":Text(args,"archiveId"),Text(args,"name"));
        if(name=="qidesk_layout_preview")return LayoutArchives.Apply(controller,Text(args,"archiveId"),Bool(args,"organize",true),Bool(args,"includeExcluded",false),true);
        if(name=="qidesk_layout_apply"){
            var result=LayoutArchives.Apply(controller,Text(args,"archiveId"),Bool(args,"organize",true),Bool(args,"includeExcluded",false),false);
            controller.RecordAiChange("应用布局存档",result.ArchiveName,result.Assigned+" 个文件 · "+result.PartitionsAdded+" 个新分区",result.Moves.Take(12).ToArray());
            controller.Refresh();
            return result;
        }
        if(name=="qidesk_layout_undo"){
            if(!controller.Store.Undo())throw new InvalidDataException("没有可撤销的布局修改");
            controller.SaveAndRefresh(true);controller.Refresh();
            return new {undone=true};
        }
        throw new InvalidDataException("未知的布局工具："+name);
    }
    static string Text(Dictionary<string,object> args,string key){object value;return args!=null&&args.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):"";}
    static bool Bool(Dictionary<string,object> args,string key,bool fallback){object value;if(args==null||!args.TryGetValue(key,out value)||value==null)return fallback;if(value is bool)return (bool)value;bool parsed;return Boolean.TryParse(Convert.ToString(value),out parsed)?parsed:fallback;}
    static Dictionary<string,object> Field(string type){return new Dictionary<string,object>{{"type",type}};}
    static Dictionary<string,object> Tool(string name,string description,bool readOnly,Dictionary<string,object> properties,string[] required){
        return new Dictionary<string,object>{{"name",name},{"description",description},{"inputSchema",new Dictionary<string,object>{{"type","object"},{"properties",properties},{"required",required},{"additionalProperties",false}}},{"annotations",new {readOnlyHint=readOnly,destructiveHint=false,openWorldHint=false}}};
    }
    public static object[] Tools(){
        var empty=new Dictionary<string,object>();
        var preview=Tool("qidesk_layout_preview","Preview a named layout archive: partitions, rules, item moves and unmatched items. Does not change the desktop or files.",true,
            new Dictionary<string,object>{{"archiveId",Field("string")},{"organize",Field("boolean")},{"includeExcluded",Field("boolean")}},new string[0]);
        var apply=Tool("qidesk_layout_apply","Apply a layout archive: replace partitions/rules/layout, optionally reorganize desktop membership. Never moves or deletes files; undoable.",false,
            new Dictionary<string,object>{{"archiveId",Field("string")},{"organize",Field("boolean")},{"includeExcluded",Field("boolean")}},new string[0]);
        var export=Tool("qidesk_layout_export","Save the current desktop layout as a named archive in desktop-layouts.json.",false,
            new Dictionary<string,object>{{"archiveId",Field("string")},{"name",Field("string")}},new string[0]);
        var save=Tool("qidesk_layout_save","Validate and save a complete desktop-layouts.json. Does not apply it.",false,
            new Dictionary<string,object>{{"configJson",Field("string")}},new[]{"configJson"});
        return new object[]{
            Tool("qidesk_layout_list","List layout archives in desktop-layouts.json and which archive is active.",true,empty,new string[0]),
            Tool("qidesk_layout_config","Read the complete desktop-layouts.json text, including all archives and AI settings.",true,empty,new string[0]),
            preview,apply,export,save,
            Tool("qidesk_layout_undo","Undo the most recent layout change, including an applied archive.",false,empty,new string[0])
        };
    }
}
}
