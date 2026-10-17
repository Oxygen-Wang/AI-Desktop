using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QiDesk {
// MCP 与 CLI 共用的图标工具组：SVG 生成 .ico、自定义显示图标、快捷方式图标位置、
// 应用图标皮肤。与 LayoutCommands 同一模式：Handles 判定、Execute 执行、Tools 供 schema。
// 边界：自定义显示图标只动栖桌内的呈现；快捷方式图标写真实 .lnk（有回收站之外的手动恢复途径，
// 因此记入 AI 修改回执）；皮肤只改分区设置，从不改写快捷方式文件。
public static class McpIcons {
    static readonly string[] names={"qidesk_build_icon","qidesk_set_custom_icon","qidesk_reset_custom_icon","qidesk_list_custom_icons","qidesk_get_shortcut_icon","qidesk_set_shortcut_icon","qidesk_reset_shortcut_icon","qidesk_list_icon_skins","qidesk_set_icon_skin"};
    static readonly string[] pictureSources={".ico",".png",".jpg",".jpeg",".bmp",".gif",".webp",".exe",".dll",".icl"};
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
    static bool Same(string left,string right){return String.Equals(IgnoreRule.Canonical(left),IgnoreRule.Canonical(right),StringComparison.OrdinalIgnoreCase);}
    static bool WritableRoot(Store store,string svg,string directory){
        if(Same(directory,store.Root))return true;
        if(Same(directory,Path.GetTempPath()))return true;
        if(Same(directory,Path.GetDirectoryName(Path.GetFullPath(svg))))return true;
        return store.Data.WorkFolders.Any(root=>Same(directory,root));
    }
    public static object Execute(Controller controller,string name,Dictionary<string,object> args){
        args=args??new Dictionary<string,object>();
        var store=controller.Store;
        if(name=="qidesk_build_icon")return BuildIcon(store,args);
        if(name=="qidesk_set_custom_icon")return SetCustomIcon(controller,args);
        if(name=="qidesk_reset_custom_icon")return ResetCustomIcon(controller,args);
        if(name=="qidesk_list_custom_icons")return store.Data.CustomIcons==null?new object[0]:store.Data.CustomIcons.Select(p=>new {path=p.Key,source=p.Value.Source,index=p.Value.Index,sourceExists=File.Exists(p.Value.Source)}).ToArray();
        if(name=="qidesk_get_shortcut_icon")return ShortcutIcon(controller,args);
        if(name=="qidesk_set_shortcut_icon")return SetShortcutIcon(controller,args);
        if(name=="qidesk_reset_shortcut_icon")return ResetShortcutIcon(controller,args);
        if(name=="qidesk_list_icon_skins")return AppIconSkins.Choices.Select(DescribeSkin).ToArray();
        if(name=="qidesk_set_icon_skin")return SetIconSkin(controller,args);
        throw new Exception("Unknown tool.");
    }
    static object DescribeSkin(string style){
        var pack=AppIconSkins.Find(style);
        // 非 pack 样式（经典）就是系统图标，不依赖任何文件，永远可用，也不该背上
        // 某次 pack 加载失败留下的陈旧错误——否则 AI 会以为经典样式不可用。
        bool isPack=AppIconSkins.IsPack(style);
        int apps=pack==null?0:pack.Entries.Count(e=>e.Executables.Length>0||e.Names.Length>0);
        return new {skin=style,isPack=isPack,available=!isPack||pack!=null,
            license=pack==null?"":pack.License,appMappings=apps,error=isPack&&pack==null?AppIconSkins.LastError:""};
    }
    static object BuildIcon(Store store,Dictionary<string,object> args){
        string svg=Full(args,"svgPath"),output=Full(args,"outputPath");
        if(!File.Exists(svg))throw new Exception("SVG 文件不存在。");
        if(!String.Equals(Path.GetExtension(svg),".svg",StringComparison.OrdinalIgnoreCase))throw new Exception("只支持 .svg 源文件。");
        if(new FileInfo(svg).Length>1024*1024)throw new Exception("SVG 不能超过 1 MB。");
        if(!String.Equals(Path.GetExtension(output),".ico",StringComparison.OrdinalIgnoreCase))throw new Exception("输出必须是 .ico 文件。");
        string directory=Path.GetDirectoryName(output);
        if(!Directory.Exists(directory))throw new Exception("输出目录不存在。");
        if(!WritableRoot(store,svg,directory))throw new Exception("图标只能写入已连接的工作区、临时目录、SVG 所在目录或栖桌数据目录。");
        // Generate 为程序徽标构建会在输出目录旁多写一张 qidesk.png；这里不是徽标构建，预先记录、生成后清理。
        string stray=Path.Combine(directory,"qidesk.png");bool strayExisted=File.Exists(stray);
        AppIcon.Generate(svg,output);
        if(!strayExisted&&File.Exists(stray))try{File.Delete(stray);}catch(IOException){/* 删不掉也不让出图失败：这是 Generate 的副产物，留着不致命。 */}
        return new {output=output,bytes=new FileInfo(output).Length,sizes="16,24,32,48,64,128,256"};
    }
    static object SetCustomIcon(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string path=Full(args,"path"),source=Full(args,"source");int index=(int)Number(args,"index",0);
        if(!Store.Exists(path))throw new Exception("桌面项目不存在，无法设置显示图标。");
        if(!File.Exists(source))throw new Exception("图标来源文件不存在。");
        if(Array.IndexOf(pictureSources,Path.GetExtension(source).ToLowerInvariant())<0)throw new Exception("图标来源只支持 .ico、图片或 .exe/.dll/.icl。");
        if(new FileInfo(source).Length>8*1024*1024)throw new Exception("图标来源不能超过 8 MB。");
        store.Checkpoint();CustomIcons.Set(store.Data,path,source,index);store.Save();
        controller.RecordAiChange("更换显示图标",Path.GetFileName(path),Path.GetFileName(source),null);
        controller.Rebuild();controller.Refresh();
        return new {path=path,source=source,index=index};
    }
    static object ResetCustomIcon(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string path=Full(args,"path");
        store.Checkpoint();
        if(!CustomIcons.Reset(store.Data,path))throw new Exception("这个项目没有自定义显示图标。");
        store.Save();
        controller.RecordAiChange("恢复系统图标",Path.GetFileName(path),"",null);
        controller.Rebuild();controller.Refresh();
        return new {removed=true,path=path};
    }
    static object ShortcutIcon(Controller controller,Dictionary<string,object> args){
        string path=Full(args,"path");
        if(!ShortcutIcons.IsShortcut(path))throw new Exception("只有 .lnk 快捷方式记录了自己的图标位置。");
        string current=ShortcutIcons.Current(path);
        int cut=current.LastIndexOf(',');string source=cut>0?current.Substring(0,cut):"";int index=0;
        if(cut>0&&!Int32.TryParse(current.Substring(cut+1),out index))index=0;
        return new {path=path,hasCustomIcon=source.Length>0,source=source,index=index};
    }
    static object SetShortcutIcon(Controller controller,Dictionary<string,object> args){
        string path=Full(args,"path"),source=Full(args,"source");int index=(int)Number(args,"index",0);
        if(!ShortcutIcons.IsShortcut(path))throw new Exception("只有 .lnk 快捷方式可以写入图标位置。");
        ShortcutIcons.Apply(path,source,index);
        controller.RecordAiChange("更换快捷方式图标",Path.GetFileName(path),Path.GetFileName(source),null);
        controller.Refresh();
        return new {path=path,source=source,index=index};
    }
    static object ResetShortcutIcon(Controller controller,Dictionary<string,object> args){
        string path=Full(args,"path");
        if(!ShortcutIcons.IsShortcut(path))throw new Exception("只有 .lnk 快捷方式可以写入图标位置。");
        ShortcutIcons.Reset(path);
        controller.RecordAiChange("恢复快捷方式原图标",Path.GetFileName(path),"",null);
        controller.Refresh();
        return new {removed=true,path=path};
    }
    static object SetIconSkin(Controller controller,Dictionary<string,object> args){
        var store=controller.Store;string skin=Text(args,"skin"),groupId=Text(args,"groupId").Trim();
        if(Array.IndexOf(AppIconSkins.Choices,skin)<0)throw new Exception("未知图标皮肤，可选："+String.Join("、",AppIconSkins.Choices));
        List<Group> targets;
        if(groupId.Length==0)targets=store.Data.Groups.ToList();
        else{
            var group=store.Data.Groups.FirstOrDefault(g=>g.Id==groupId);
            if(group==null)throw new Exception("Unknown groupId.");
            targets=new List<Group>{group};
        }
        store.Checkpoint();
        foreach(var group in targets)group.IconSkin=skin;
        store.Save();
        controller.RecordAiChange("更换图标皮肤",groupId.Length==0?targets.Count+" 个分区":targets[0].Name,skin,null);
        controller.Rebuild();controller.Refresh();
        return new {skin=skin,groups=targets.Count};
    }
    static Dictionary<string,object> Field(string type){return new Dictionary<string,object>{{"type",type}};}
    // 原名 Path 会遮蔽同文件业务代码里的 System.IO.Path（Path.GetFileName 等被解析成对这个方法的调用，
    // 报 CS0119）。schema 辅助方法让位，改成 PathField。
    static Dictionary<string,object> PathField(string key){return new Dictionary<string,object>{{"type","string"},{"maxLength",1024}};}
    static Dictionary<string,object> Tool(string name,string description,bool readOnly,Dictionary<string,object> properties,string[] required){
        var schema=new Dictionary<string,object>{{"type","object"},{"properties",properties},{"required",required},{"additionalProperties",false}};
        return new Dictionary<string,object>{{"name",name},{"description",description},{"inputSchema",schema},{"annotations",new {readOnlyHint=readOnly,destructiveHint=false,openWorldHint=false}}};
    }
    public static object[] Tools(){
        var empty=new Dictionary<string,object>();
        return new object[]{
            Tool("qidesk_build_icon",
                "Render a supported SVG subset (root-level rect/path/circle/ellipse/line/polygon/polyline; "
                +"fill with colors or linearGradient ids; stroke/stroke-width) into a multi-size Windows .ico at 16-256 px. "
                +"The .ico may only be written into a connected workspace, the temp directory, the SVG's own directory or the QiDesk data directory.",
                false,
                new Dictionary<string,object>{{"svgPath",PathField("svgPath")},{"outputPath",PathField("outputPath")}},new[]{"svgPath","outputPath"}),
            Tool("qidesk_set_custom_icon","Show a chosen icon for one desktop item inside QiDesk only; never rewrites the item or any shortcut. Source may be an .ico, a picture, or an .exe/.dll/.icl plus an index.",false,
                new Dictionary<string,object>{{"path",PathField("path")},{"source",PathField("source")},{"index",Field("number")}},new[]{"path","source"}),
            Tool("qidesk_reset_custom_icon","Drop the custom display icon of one item so it returns to the system or skin icon.",false,
                new Dictionary<string,object>{{"path",PathField("path")}},new[]{"path"}),
            Tool("qidesk_list_custom_icons","List every custom display icon mapping with its source file and whether that source still exists.",true,empty,new string[0]),
            Tool("qidesk_get_shortcut_icon","Read the icon location stored inside a .lnk shortcut. An empty source means the shortcut uses the target program's own icon.",true,
                new Dictionary<string,object>{{"path",PathField("path")}},new[]{"path"}),
            Tool("qidesk_set_shortcut_icon","Write an icon location into a .lnk shortcut itself; this changes the real file and appears in the AI change receipt. Source must be .ico, .exe, .dll or .icl.",false,
                new Dictionary<string,object>{{"path",PathField("path")},{"source",PathField("source")},{"index",Field("number")}},new[]{"path","source"}),
            Tool("qidesk_reset_shortcut_icon","Clear the icon location inside a .lnk shortcut so it returns to the target program's own icon.",false,
                new Dictionary<string,object>{{"path",PathField("path")}},new[]{"path"}),
            Tool("qidesk_list_icon_skins","List available application icon skins with license and how many application mappings each pack ships.",true,empty,new string[0]),
            Tool("qidesk_set_icon_skin","Apply an application icon skin to one partition or every partition. Classic restores system icons; shortcut files are never modified.",false,
                new Dictionary<string,object>{{"skin",Field("string")},{"groupId",Field("string")}},new[]{"skin"}),
        };
    }
}
}
