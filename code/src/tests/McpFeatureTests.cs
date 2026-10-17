using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QiDesk {
// 覆盖新增的四个 MCP 工具组：图标（McpIcons）、桌面（McpDesktop）、面板（McpPanels）
// 与组件（McpComponents）。所有调用都经 McpBridge.Execute，与 MCP 管道和 CLI 共用
// 同一套参数校验和分发路径。不访问真实桌面内容、不启动真实程序、不播放媒体。
public static class McpFeatureTests {
    // 覆盖 rect/path 之外的 SVG 子集：圆形、椭圆、直线、多边形、渐变与描边。
    const string FixtureSvg="<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 512 512'>"
        +"<defs><linearGradient id='sheen' x1='0' y1='0' x2='1' y2='1'>"
        +"<stop offset='0' stop-color='#4285F4'/><stop offset='1' stop-color='#34A853'/></linearGradient></defs>"
        +"<rect x='36' y='36' width='440' height='440' rx='96' fill='url(#sheen)'/>"
        +"<circle cx='256' cy='236' r='118' fill='#FFFFFF'/>"
        +"<ellipse cx='256' cy='392' rx='96' ry='34' fill='#EA4335'/>"
        +"<line x1='150' y1='150' x2='214' y2='150' stroke='#FBBC05' stroke-width='18'/>"
        +"<polygon points='256,96 306,206 206,206' fill='#FFFFFF'/>"
        +"<polyline points='150,420 214,356 278,420 342,356' stroke='#FFFFFF' stroke-width='16' fill='none'/>"
        +"</svg>";
    // 路径必须带 fill 或 stroke 才会被解析：两者都没有时 DrawElement 直接跳过，
    // 坏的 d 根本进不了 Geometry.Parse，问题收集不到，图标会被当成成功写出。
    const string BrokenSvg="<svg xmlns='http://www.w3.org/2000/svg'><path d='M 10 10 L bad bad' fill='#000000'/></svg>";
    static readonly string[] newTools={
        "qidesk_build_icon","qidesk_set_custom_icon","qidesk_reset_custom_icon","qidesk_list_custom_icons",
        "qidesk_get_shortcut_icon","qidesk_set_shortcut_icon","qidesk_reset_shortcut_icon",
        "qidesk_list_icon_skins","qidesk_set_icon_skin","qidesk_list_desktop_items","qidesk_remove_items",
        "qidesk_open_item","qidesk_reveal_item","qidesk_rename_item","qidesk_set_page",
        "qidesk_set_desktop_mode","qidesk_set_hidden","qidesk_get_status","qidesk_undo",
        "qidesk_list_screenshots","qidesk_music_control","qidesk_music_now",
        "qidesk_list_panels","qidesk_add_panel_tab","qidesk_remove_panel_tab",
        "qidesk_select_panel_tab","qidesk_show_panel","qidesk_list_plugins",
        "qidesk_install_plugin","qidesk_remove_plugin","qidesk_list_component_packs",
        "qidesk_save_component_pack","qidesk_apply_component_pack","qidesk_remove_component_pack"};
    static object Call(Controller controller,string name,Dictionary<string,object> arguments){
        object result=McpBridge.Execute(controller,new Dictionary<string,object>{{"name",name},{"arguments",arguments}});
        return Store.Json.Deserialize<object>(Store.Json.Serialize(result));
    }
    // CLI 管道走的就是 LayoutBridge.Dispatch：必须与 McpBridge.Execute 是同一条分发路径，
    // 否则命令行对新工具组一律报"未知的布局工具"。
    static object DispatchCall(Controller controller,string name,Dictionary<string,object> arguments){
        object result=LayoutBridge.Dispatch(controller,name,arguments);
        return Store.Json.Deserialize<object>(Store.Json.Serialize(result));
    }
    static Dictionary<string,object> Args(params object[] pairs){
        var map=new Dictionary<string,object>();
        for(int i=0;i+1<pairs.Length;i+=2)map[Convert.ToString(pairs[i])]=pairs[i+1];
        return map;
    }
    static Dictionary<string,object> Map(object value){return (Dictionary<string,object>)value;}
    static Dictionary<string,object>[] Rows(object value){return ((object[])value).Cast<Dictionary<string,object>>().ToArray();}
    static string[] Texts(object value){return ((object[])value).Cast<object>().Select(Convert.ToString).ToArray();}
    static string Fail(Action action){
        try{action();return "";}
        catch(Exception ex){return ex.Message;}
    }
    static int IconCount(string path){
        var bytes=File.ReadAllBytes(path);
        return bytes.Length>=6?bytes[4]+bytes[5]*256:0;
    }
    static string Png(string folder,string name){
        Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,name);
        if(File.Exists(path))return path;
        byte[] pixels=new byte[8*8*4];
        for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;
        var source=BitmapSource.Create(8,8,96,96,PixelFormats.Bgra32,null,pixels,8*4);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));
        using(var stream=File.Create(path))encoder.Save(stream);
        return path;
    }
    static string Shortcut(string folder,string target,string name){
        string path=Path.Combine(folder,name);object shell=null,link=null;
        try{
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
            link.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});
            link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
            return path;
        }finally{
            if(link!=null&&Marshal.IsComObject(link))Marshal.ReleaseComObject(link);
            if(shell!=null&&Marshal.IsComObject(shell))Marshal.ReleaseComObject(shell);
        }
    }
    public static void Run(string root,Action<bool,string> check){
        BuildIconTools(root,check);
        CustomIconTools(root,check);
        ShortcutIconTools(root,check);
        IconSkinTools(root,check);
        DesktopItemTools(root,check);
        PageAndMediaTools(root,check);
        StatusAndGuards(root,check);
        PluginTools(root,check);
        ComponentPackTools(root,check);
        PanelTools(root,check);
        ToolSurface(root,check);
    }
    static void BuildIconTools(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"build-icon");Directory.CreateDirectory(folder);
        string svg=Path.Combine(folder,"badge.svg");File.WriteAllText(svg,FixtureSvg);
        string ico=Path.Combine(folder,"badge.ico");
        var store=new Store(Path.Combine(root,"build-icon-state"));
        var controller=new Controller(store);
        var built=Map(Call(controller,"qidesk_build_icon",Args("svgPath",svg,"outputPath",ico)));
        check(Convert.ToString(built["output"])==ico,"build_icon reports the icon path it wrote");
        check(Convert.ToString(built["sizes"]).Split(',').Length==7,"build_icon renders seven sizes from 16 to 256 px");
        check(File.Exists(ico)&&IconCount(ico)==7,"the written ico header lists seven images");
        check(!File.Exists(Path.Combine(folder,"qidesk.png")),"build_icon cleans the badge png generated beside the icon");
        string work=Path.Combine(root,"build-icon-work");Directory.CreateDirectory(work);
        store.Data.WorkFolders.Add(work);store.Save();
        string shared=Path.Combine(work,"shared.ico");
        Call(controller,"qidesk_build_icon",Args("svgPath",svg,"outputPath",shared));
        check(File.Exists(shared),"build_icon may write into a connected workspace");
        string elsewhere=AppDomain.CurrentDomain.BaseDirectory;
        check(Fail(delegate{Call(controller,"qidesk_build_icon",Args("svgPath",svg,"outputPath",Path.Combine(elsewhere,"refused.ico")));}).Length>0,
            "build_icon refuses directories outside workspaces, temp and the QiDesk data root");
        check(Fail(delegate{Call(controller,"qidesk_build_icon",Args("svgPath",Path.Combine(folder,"missing.svg"),"outputPath",ico));})=="SVG 文件不存在。",
            "build_icon rejects a missing source file");
        string text=Path.Combine(folder,"note.txt");File.WriteAllText(text,"x");
        check(Fail(delegate{Call(controller,"qidesk_build_icon",Args("svgPath",text,"outputPath",ico));})=="只支持 .svg 源文件。",
            "build_icon rejects non-svg sources");
        check(Fail(delegate{Call(controller,"qidesk_build_icon",Args("svgPath",svg,"outputPath",Path.Combine(folder,"out.png")));})=="输出必须是 .ico 文件。",
            "build_icon rejects non-ico outputs");
        check(Fail(delegate{Call(controller,"qidesk_build_icon",Args("svgPath",svg,"outputPath",Path.Combine(folder,"gone","out.ico")));})=="输出目录不存在。",
            "build_icon rejects a missing output directory");
        string huge=Path.Combine(folder,"huge.svg");File.WriteAllText(huge,new string('x',1024*1024+8));
        check(Fail(delegate{Call(controller,"qidesk_build_icon",Args("svgPath",huge,"outputPath",ico));})=="SVG 不能超过 1 MB。",
            "build_icon rejects oversized sources");
        string broken=Path.Combine(folder,"broken.svg");File.WriteAllText(broken,BrokenSvg);
        string brokenOut=Fail(delegate{Call(controller,"qidesk_build_icon",Args("svgPath",broken,"outputPath",Path.Combine(folder,"broken.ico")));});
        check(brokenOut.StartsWith("SVG 包含无法解析的元素"),"build_icon reports unsupported svg elements instead of writing half an icon");
        check(!File.Exists(Path.Combine(folder,"broken.ico"))&&!File.Exists(Path.Combine(folder,"qidesk.png")),"a failed build leaves no files behind");
    }
    static void CustomIconTools(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"custom-icon-tools");Directory.CreateDirectory(folder);
        string item=Path.Combine(folder,"report.txt");File.WriteAllText(item,"fixture");
        string icon=Png(folder,"display.png");
        var store=new Store(Path.Combine(root,"custom-icon-state"));
        var controller=new Controller(store);
        var set=Map(Call(controller,"qidesk_set_custom_icon",Args("path",item,"source",icon)));
        check(Convert.ToString(set["source"])==icon&&store.Data.CustomIcons.ContainsKey(item),
            "set_custom_icon stores a display-only icon mapping");
        var listed=Rows(Call(controller,"qidesk_list_custom_icons",new Dictionary<string,object>()));
        check(listed.Length==1&&Convert.ToBoolean(listed[0]["sourceExists"])&&Convert.ToString(listed[0]["path"])==item,
            "list_custom_icons reports every mapping and whether its source still exists");
        store.Save();
        check(new Store(store.Root).Data.CustomIcons.ContainsKey(item),"the display icon mapping survives a restart");
        check(Fail(delegate{Call(controller,"qidesk_set_custom_icon",Args("path",Path.Combine(folder,"missing.txt"),"source",icon));})=="桌面项目不存在，无法设置显示图标。",
            "set_custom_icon rejects items that do not exist");
        check(Fail(delegate{Call(controller,"qidesk_set_custom_icon",Args("path",item,"source",Path.Combine(folder,"missing.png")));})=="图标来源文件不存在。",
            "set_custom_icon rejects missing icon sources");
        string text=Path.Combine(folder,"source.txt");File.WriteAllText(text,"x");
        check(Fail(delegate{Call(controller,"qidesk_set_custom_icon",Args("path",item,"source",text));}).Length>0,
            "set_custom_icon only accepts icon and picture sources");
        var reset=Map(Call(controller,"qidesk_reset_custom_icon",Args("path",item)));
        check(Convert.ToBoolean(reset["removed"])&&!store.Data.CustomIcons.ContainsKey(item),"reset_custom_icon drops the mapping");
        check(Fail(delegate{Call(controller,"qidesk_reset_custom_icon",Args("path",item));})=="这个项目没有自定义显示图标。",
            "reset_custom_icon reports items without a custom icon");
        check(controller.AiChanges.Any(c=>c.Action=="更换显示图标"||c.Action=="恢复系统图标"),
            "custom icon changes are recorded in the AI change receipt");
    }
    static void ShortcutIconTools(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"shortcut-icon-tools");Directory.CreateDirectory(folder);
        string svg=Path.Combine(folder,"badge.svg");File.WriteAllText(svg,FixtureSvg);
        string ico=Path.Combine(folder,"badge.ico");
        var store=new Store(Path.Combine(root,"shortcut-icon-state"));
        var controller=new Controller(store);
        Call(controller,"qidesk_build_icon",Args("svgPath",svg,"outputPath",ico));
        string target=Path.Combine(folder,"chrome.exe");File.WriteAllText(target,"fixture, never executed");
        string link=Shortcut(folder,target,"Google Chrome.lnk");
        string plain=Path.Combine(folder,"plain.txt");File.WriteAllText(plain,"x");
        check(Fail(delegate{Call(controller,"qidesk_get_shortcut_icon",Args("path",plain));})=="只有 .lnk 快捷方式记录了自己的图标位置。",
            "get_shortcut_icon refuses plain files");
        var before=Map(Call(controller,"qidesk_get_shortcut_icon",Args("path",link)));
        check(!Convert.ToBoolean(before["hasCustomIcon"])&&Convert.ToString(before["source"]).Length==0,
            "a fresh shortcut reports the target program own icon");
        var set=Map(Call(controller,"qidesk_set_shortcut_icon",Args("path",link,"source",ico,"index",0)));
        check(Convert.ToString(set["source"])==ico,"set_shortcut_icon writes the icon location into the shortcut");
        var after=Map(Call(controller,"qidesk_get_shortcut_icon",Args("path",link)));
        check(Convert.ToBoolean(after["hasCustomIcon"])&&Convert.ToString(after["source"])==ico&&Convert.ToInt32(after["index"])==0,
            "the shortcut reads back the generated icon location");
        check(File.ReadAllText(target)=="fixture, never executed","writing a shortcut icon leaves its target untouched");
        check(Fail(delegate{Call(controller,"qidesk_set_shortcut_icon",Args("path",plain,"source",ico));}).Length>0,
            "set_shortcut_icon refuses plain files instead of rewriting them");
        var reset=Map(Call(controller,"qidesk_reset_shortcut_icon",Args("path",link)));
        check(Convert.ToBoolean(reset["removed"])&&!Convert.ToBoolean(Map(Call(controller,"qidesk_get_shortcut_icon",Args("path",link)))["hasCustomIcon"]),
            "reset_shortcut_icon restores the target program own icon");
        check(controller.AiChanges.Any(c=>c.Action=="更换快捷方式图标"||c.Action=="恢复快捷方式原图标"),
            "shortcut icon changes are recorded in the AI change receipt");
    }
    static void IconSkinTools(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"skin-tools"));
        var controller=new Controller(store);
        var skins=Rows(Call(controller,"qidesk_list_icon_skins",new Dictionary<string,object>()));
        var classic=skins.First(s=>Convert.ToString(s["skin"])=="经典");
        check(!Convert.ToBoolean(classic["isPack"])&&Convert.ToBoolean(classic["available"]),
            "list_icon_skins reports the classic system style");
        var pack=skins.FirstOrDefault(s=>Convert.ToBoolean(s["isPack"]));
        check(pack!=null&&Convert.ToString(pack["license"]).Length>0&&Convert.ToInt32(pack["appMappings"])>0,
            "list_icon_skins reports each pack license and application mappings");
        string groupId=store.Data.Groups[0].Id;
        var applied=Map(Call(controller,"qidesk_set_icon_skin",Args("skin","Fluent 应用","groupId",groupId)));
        check(Convert.ToString(applied["skin"])=="Fluent 应用"&&Convert.ToInt32(applied["groups"])==1,
            "set_icon_skin applies a skin to one partition");
        check(store.Data.Groups[0].IconSkin=="Fluent 应用"&&store.Data.Groups.Skip(1).All(g=>g.IconSkin!="Fluent 应用"),
            "only the requested partition changes its skin");
        var all=Map(Call(controller,"qidesk_set_icon_skin",Args("skin","Papirus 应用")));
        check(Convert.ToInt32(all["groups"])==store.Data.Groups.Count&&store.Data.Groups.All(g=>g.IconSkin=="Papirus 应用"),
            "set_icon_skin without a groupId targets every partition");
        check(Fail(delegate{Call(controller,"qidesk_set_icon_skin",Args("skin","不存在的皮肤"));}).Contains("未知图标皮肤"),
            "set_icon_skin rejects unknown skins");
        check(Fail(delegate{Call(controller,"qidesk_set_icon_skin",Args("skin","经典","groupId","no-such-group"));})=="Unknown groupId.",
            "set_icon_skin rejects unknown partitions");
        check(new Store(store.Root).Data.Groups[0].IconSkin=="Papirus 应用","the chosen skin persists across restart");
    }
    static void DesktopItemTools(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"desktop-tools"));
        var controller=new Controller(store);
        var items=Map(Call(controller,"qidesk_list_desktop_items",new Dictionary<string,object>()));
        int total=Convert.ToInt32(items["total"]);
        var rows=Rows(items["items"]);
        check(total>=0&&rows.Length<=Math.Min(total,500),"list_desktop_items caps the listing and reports the desktop total");
        check(rows.All(r=>Convert.ToString(r["path"]).Length>0&&Convert.ToString(r["name"]).Length>0&&Convert.ToString(r["type"]).Length>0),
            "every listed desktop item carries its path name and type");
        check(rows.All(r=>r.ContainsKey("loose")&&r.ContainsKey("customIcon")),
            "desktop items report partition membership and custom icon state");
        string work=Path.Combine(root,"desktop-work");Directory.CreateDirectory(work);
        string file=Path.Combine(work,"report.txt");File.WriteAllText(file,"fixture");
        store.Data.WorkFolders.Add(work);store.Save();
        var group=store.Data.Groups[0];store.Assign(file,group);store.Save();
        var removed=Map(Call(controller,"qidesk_remove_items",Args("paths",new[]{file,Path.Combine(work,"missing.txt")})));
        check(Convert.ToInt32(removed["removed"])==1&&Texts(removed["skipped"]).Length==1,
            "remove_items releases assigned items and skips the rest");
        check(!group.Items.Contains(file,StringComparer.OrdinalIgnoreCase)&&File.Exists(file),
            "remove_items only changes partition membership and keeps the file");
        check(Fail(delegate{Call(controller,"qidesk_remove_items",Args("paths",file));})=="Invalid array: paths",
            "remove_items rejects a non-array paths argument");
    }
    static void PageAndMediaTools(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"page-tools"));
        var controller=new Controller(store);
        var page=Map(Call(controller,"qidesk_set_page",Args("page",2.0)));
        check(Convert.ToInt32(page["page"])==2&&Convert.ToInt32(page["pages"])==3&&store.Data.Page==1,
            "set_page switches the visible desktop page");
        check(Fail(delegate{Call(controller,"qidesk_set_page",Args("page",9.0));})=="page 必须在 1 到 3 之间。",
            "set_page rejects pages outside the range");
        var undone=Map(Call(controller,"qidesk_undo",new Dictionary<string,object>()));
        check(Convert.ToBoolean(undone["undone"])&&store.Data.Page==0,"undo returns to the previous page");
        check(Fail(delegate{Call(controller,"qidesk_music_control",Args("action","volume-up"));})=="action 必须是 play-pause、next 或 previous。",
            "music_control rejects unknown media actions");
        var now=Map(Call(controller,"qidesk_music_now",new Dictionary<string,object>()));
        check(now.ContainsKey("running")&&now.ContainsKey("title")&&now.ContainsKey("artist"),
            "music_now reads the current track without touching the player");
    }
    static void StatusAndGuards(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"status-tools"));
        var controller=new Controller(store);
        var status=Map(Call(controller,"qidesk_get_status",new Dictionary<string,object>()));
        check(!Convert.ToBoolean(status["running"])&&!Convert.ToBoolean(status["docked"])&&!Convert.ToBoolean(status["hidden"]),
            "get_status reports an idle desktop mode");
        check(Convert.ToInt32(status["page"])==1&&Convert.ToInt32(status["pages"])==3,
            "get_status reports the current page and page count");
        check(Convert.ToInt32(status["partitions"])==4&&Convert.ToInt32(status["widgets"])==0,
            "get_status counts partitions and widgets");
        check(Convert.ToInt32(status["desktopItems"])>=0&&Convert.ToInt32(status["looseItems"])>=0,
            "get_status counts real desktop items");
        // 全新配置自带本地 Ollama 端点预设，因此算已配置；清空端点后才算未配置。
        check(Convert.ToBoolean(status["mcpEnabled"])&&Convert.ToBoolean(status["aiConfigured"]),
            "get_status reports MCP and AI configuration presence");
        store.Data.AiEndpoint="";store.Save();
        var offline=Map(Call(controller,"qidesk_get_status",new Dictionary<string,object>()));
        check(!Convert.ToBoolean(offline["aiConfigured"]),"get_status reports a missing AI endpoint");
        check(Convert.ToInt32(status["panelTabs"])==0&&Convert.ToInt32(status["customIcons"])==0,
            "get_status reports panel tabs and custom icons");
        var viaCli=Map(DispatchCall(controller,"qidesk_get_status",new Dictionary<string,object>()));
        check(Convert.ToBoolean(viaCli["ok"])&&Map(viaCli["result"]).ContainsKey("mcpEnabled"),
            "layout bridge routes non-layout tools through the shared MCP dispatcher");
        var cliIcons=Map(DispatchCall(controller,"qidesk_list_custom_icons",new Dictionary<string,object>()));
        check(Convert.ToBoolean(cliIcons["ok"])&&cliIcons["result"] is object[],
            "layout bridge routes icon tools through the shared MCP dispatcher");
        check(Fail(delegate{Call(controller,"qidesk_undo",new Dictionary<string,object>());})=="没有可撤销的修改。",
            "undo reports when there is nothing to undo");
        check(Fail(delegate{Call(controller,"qidesk_rename_item",Args("path",Path.Combine(root,"any.txt"),"name","x"));})=="Application window unavailable.",
            "rename_item needs the application window");
        check(Fail(delegate{Call(controller,"qidesk_set_desktop_mode",Args("enabled",true));})=="Application window unavailable.",
            "set_desktop_mode needs the application window");
        check(Fail(delegate{Call(controller,"qidesk_set_hidden",Args("hidden",true));})=="Application window unavailable.",
            "set_hidden needs the application window");
        check(Fail(delegate{Call(controller,"qidesk_list_screenshots",new Dictionary<string,object>());})=="Application window unavailable.",
            "list_screenshots needs the application window");
        check(Fail(delegate{Call(controller,"qidesk_open_item",Args("path",Path.Combine(root,"missing-item.txt")));})=="项目不存在，无法打开。",
            "open_item reports missing items instead of launching anything");
        check(Fail(delegate{Call(controller,"qidesk_reveal_item",Args("path",Path.Combine(root,"missing-item.txt")));})=="项目不存在，无法定位。",
            "reveal_item reports missing items instead of opening Explorer");
    }
    static void PluginTools(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"plugin-tools"));
        var controller=new Controller(store);
        string json="{\"id\":\"test.note\",\"name\":\"工具便签\",\"kind\":\"note\",\"description\":\"通过 MCP 安装\",\"text\":\"你好\",\"accent\":\"#8CB5DE\"}";
        var installed=Map(Call(controller,"qidesk_install_plugin",Args("pluginJson",json)));
        check(Convert.ToString(installed["pluginId"])=="test.note","install_plugin reports the installed plugin id");
        var plugins=Rows(Call(controller,"qidesk_list_plugins",new Dictionary<string,object>()));
        var custom=plugins.First(p=>Convert.ToString(p["id"])=="test.note");
        check(!Convert.ToBoolean(custom["builtin"])&&Convert.ToBoolean(custom["enabled"]),
            "list_plugins marks the custom plugin as user installed");
        check(plugins.Count(p=>Convert.ToBoolean(p["builtin"]))==14,"list_plugins includes the fourteen built-in components");
        check(Fail(delegate{Call(controller,"qidesk_remove_plugin",Args("pluginId","clock"));})=="内置组件不能移除。",
            "built-in components cannot be removed");
        check(Fail(delegate{Call(controller,"qidesk_remove_plugin",Args("pluginId","no-such-plugin"));})=="Unknown pluginId.",
            "remove_plugin rejects unknown plugin ids");
        check(Fail(delegate{Call(controller,"qidesk_install_plugin",Args("pluginJson","{\"id\":\"bad.kind\",\"name\":\"x\",\"kind\":\"spaceship\"}"));}).Length>0,
            "install_plugin rejects unsupported component kinds");
        check(Fail(delegate{Call(controller,"qidesk_install_plugin",Args("pluginJson","{\"id\":\"clock\",\"name\":\"x\",\"kind\":\"note\"}"));})=="插件 id 与内置组件重名。",
            "install_plugin refuses built-in id collisions");
        check(Fail(delegate{Call(controller,"qidesk_install_plugin",Args("pluginJson","{\"id\":\"extra.one\",\"name\":\"x\",\"kind\":\"note\",\"command\":\"run.exe\"}"));})=="插件配置包含不支持的字段。",
            "install_plugin rejects unknown plugin fields");
        check(Fail(delegate{Call(controller,"qidesk_install_plugin",Args("pluginJson",new string('x',70*1024)));})=="Invalid argument: pluginJson",
            "the bridge rejects oversized plugin configurations before parsing");
        WidgetPlugins.Create(store.Data,"test.note",new[]{new Rect(0,0,1920,1080)});
        store.Save();
        var removed=Map(Call(controller,"qidesk_remove_plugin",Args("pluginId","test.note")));
        check(Convert.ToBoolean(removed["removed"])&&Convert.ToInt32(removed["widgetsRemoved"])==1,
            "remove_plugin removes the plugin together with its desktop instances");
        check(store.Data.Widgets.Count==0&&store.Data.Plugins.Count==0,
            "removing a plugin leaves no widgets or plugin records behind");
    }
    static void ComponentPackTools(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"pack-tools"));
        var controller=new Controller(store);
        var areas=new[]{new Rect(0,0,1920,1080)};
        WidgetPlugins.Create(store.Data,"clock",areas);
        WidgetPlugins.Create(store.Data,"note",areas);
        store.Save();
        var saved=Map(Call(controller,"qidesk_save_component_pack",Args("name","工具组合")));
        check(Convert.ToInt32(saved["components"])==2,"save_component_pack exports the current widgets");
        var packs=Rows(Call(controller,"qidesk_list_component_packs",new Dictionary<string,object>()));
        check(packs.Length==1&&Convert.ToInt32(packs[0]["components"])==2,"list_component_packs reports the saved pack");
        var applied=Map(Call(controller,"qidesk_apply_component_pack",Args("packId",Convert.ToString(saved["packId"]))));
        check(Convert.ToInt32(applied["added"])==2&&store.Data.Widgets.Count==4,
            "apply_component_pack adds every widget of the pack");
        check(Fail(delegate{Call(controller,"qidesk_apply_component_pack",Args("packId","no-such-pack"));})=="Unknown packId.",
            "apply_component_pack rejects unknown packs");
        var removed=Map(Call(controller,"qidesk_remove_component_pack",Args("packId",Convert.ToString(saved["packId"]))));
        check(Convert.ToBoolean(removed["removed"])&&store.Data.ComponentPacks.Count==0&&store.Data.Widgets.Count==4,
            "remove_component_pack keeps existing widgets and drops the pack");
    }
    static void PanelTools(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"panel-tools"));
        var controller=new Controller(store);
        string folder=Path.Combine(root,"panel-folder");Directory.CreateDirectory(folder);
        var listed=Map(Call(controller,"qidesk_list_panels",new Dictionary<string,object>()));
        check(Rows(listed["tabs"]).Length==0&&Convert.ToInt32(listed["activeIndex"])==-1&&!Convert.ToBoolean(listed["visible"]),
            "list_panels reports an empty panel without the application window");
        var added=Map(Call(controller,"qidesk_add_panel_tab",Args("path",folder)));
        check(Convert.ToBoolean(added["added"])&&Convert.ToString(added["kind"])=="folder",
            "add_panel_tab binds a local folder");
        check(Fail(delegate{Call(controller,"qidesk_add_panel_tab",Args("path",folder));})=="这个文件夹已经在面板标签中。",
            "the same folder cannot become two tabs");
        check(Fail(delegate{Call(controller,"qidesk_add_panel_tab",Args("path",folder,"groupId",store.Data.Groups[0].Id));})=="path 和 groupId 只能提供一个。",
            "add_panel_tab refuses both a folder and a partition");
        check(Fail(delegate{Call(controller,"qidesk_add_panel_tab",new Dictionary<string,object>());})=="请提供文件夹 path 或分区 groupId。",
            "add_panel_tab requires one of the two targets");
        check(Fail(delegate{Call(controller,"qidesk_add_panel_tab",Args("path",Path.Combine(root,"no-such-folder")));})=="文件夹不存在。",
            "add_panel_tab rejects missing folders");
        string groupId=store.Data.Groups[1].Id;
        var groupTab=Map(Call(controller,"qidesk_add_panel_tab",Args("groupId",groupId)));
        check(Convert.ToString(groupTab["kind"])=="group"&&Convert.ToInt32(groupTab["index"])==1,
            "add_panel_tab binds a desktop partition and activates it");
        check(Fail(delegate{Call(controller,"qidesk_add_panel_tab",Args("groupId",groupId));})=="这个分区已经在面板标签中。",
            "the same partition cannot become two tabs");
        var tabs=Rows(Map(Call(controller,"qidesk_list_panels",new Dictionary<string,object>()))["tabs"]);
        check(tabs.Length==2&&Convert.ToString(tabs[0]["kind"])=="folder"&&Convert.ToString(tabs[0]["name"]).Length>0,
            "list_panels describes each tab with its bound target and label");
        check(Convert.ToString(tabs[1]["groupId"])==groupId&&Convert.ToString(tabs[1]["name"])==store.Data.Groups[1].Name,
            "a partition tab reports the partition name");
        var selected=Map(Call(controller,"qidesk_select_panel_tab",Args("index",0.0)));
        check(Convert.ToInt32(selected["activeIndex"])==0,"select_panel_tab switches the active tab");
        check(Fail(delegate{Call(controller,"qidesk_select_panel_tab",Args("index",9.0));}).Contains("标签序号必须在"),
            "select_panel_tab rejects indexes outside the tab range");
        var gone=Map(Call(controller,"qidesk_remove_panel_tab",Args("index",0.0)));
        check(Convert.ToBoolean(gone["removed"])&&Convert.ToInt32(gone["activeIndex"])==0,
            "remove_panel_tab keeps the active index inside range");
        check(Fail(delegate{Call(controller,"qidesk_show_panel",Args("show",true));})=="Application window unavailable.",
            "show_panel needs the application window");
        check(new Store(store.Root).Data.PanelTabs.Count==1,"panel tab changes persist across restart");
    }
    static void ToolSurface(string root,Action<bool,string> check){
        var published=McpBridge.Tools().Cast<Dictionary<string,object>>().ToList();
        var names=published.Select(t=>Convert.ToString(t["name"])).ToList();
        foreach(string name in newTools){
            bool known=McpIcons.Handles(name)||McpDesktop.Handles(name)||McpPanels.Handles(name)||McpComponents.Handles(name);
            check(known&&names.Contains(name),"the new tool is dispatched and published for MCP: "+name);
        }
        check(names.Distinct().Count()==names.Count,"every published tool name is unique");
        foreach(string name in new[]{"qidesk_list_custom_icons","qidesk_get_shortcut_icon","qidesk_list_icon_skins","qidesk_list_desktop_items","qidesk_get_status","qidesk_list_screenshots","qidesk_music_now","qidesk_list_panels","qidesk_list_plugins","qidesk_list_component_packs"}){
            var tool=Map(Store.Json.Deserialize<object>(Store.Json.Serialize(published.First(t=>Convert.ToString(t["name"])==name))));
            var annotations=Map(tool["annotations"]);
            check(Convert.ToBoolean(annotations["readOnlyHint"]),"read-only tools advertise the read-only hint: "+name);
        }
        var store=new Store(Path.Combine(root,"surface-state"));var controller=new Controller(store);
        check(Fail(delegate{Call(controller,"qidesk_build_icon",Args("svgPath","x","outputPath","y","bogus",1));})=="Unexpected argument: bogus",
            "the bridge rejects undeclared arguments before running a tool");
        check(Fail(delegate{Call(controller,"qidesk_build_icon",Args("outputPath","y"));})=="Missing argument: svgPath",
            "the bridge rejects calls without required arguments");
        check(Fail(delegate{Call(controller,"qidesk_set_icon_skin",Args("skin",new string('x',1500)));})=="Invalid argument: skin",
            "the bridge caps string arguments");
        check(Fail(delegate{Call(controller,"qidesk_set_desktop_mode",Args("enabled","true"));})=="enabled must be a boolean.",
            "boolean arguments must arrive as JSON booleans");
    }
    public static void Native(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"feature-native");Directory.CreateDirectory(folder);
        string work=Path.Combine(folder,"workspace");Directory.CreateDirectory(work);
        string target=Path.Combine(work,"chrome.exe");File.WriteAllText(target,"fixture, never executed");
        string link=Shortcut(work,target,"Google Chrome.lnk");
        string svg=Path.Combine(work,"chrome.svg");File.WriteAllText(svg,FixtureSvg);
        string ico=Path.Combine(work,"chrome.ico");
        var store=new Store(Path.Combine(root,"feature-native-state"));
        store.Data.Groups.Clear();
        var group=new Group(){Name="应用",Width=420,Height=300,X=40,Y=60};
        store.Data.Groups.Add(group);
        store.Data.WorkFolders.Add(work);store.Save();
        var controller=new Controller(store);
        var manager=new ManagerWindow(controller){Left=-15000,Top=-15000};
        try{
            manager.Show();manager.UpdateLayout();
            var status=Map(Call(controller,"qidesk_get_status",new Dictionary<string,object>()));
            check(!Convert.ToBoolean(status["running"])&&Convert.ToInt32(status["partitions"])==1&&Convert.ToInt32(status["workspaces"])==1,
                "get_status reports the live partition and workspace counts");
            var page=Map(Call(controller,"qidesk_set_page",Args("page",2.0)));
            check(Convert.ToInt32(page["page"])==2&&Convert.ToInt32(Map(Call(controller,"qidesk_get_status",new Dictionary<string,object>()))["page"])==2,
                "set_page moves the live status to the requested page");
            // 标志性场景：AI 生成 SVG 图标，替换谷歌快捷方式的显示图标与真实图标位置。
            Call(controller,"qidesk_build_icon",Args("svgPath",svg,"outputPath",ico));
            check(File.Exists(ico),"build_icon writes the generated icon into the connected workspace");
            var assigned=Map(Call(controller,"qidesk_assign_items",Args("groupId",group.Id,"paths",new[]{link})));
            check(Convert.ToInt32(assigned["assigned"])==1,"assign_items references the workspace shortcut into a partition");
            var custom=Map(Call(controller,"qidesk_set_custom_icon",Args("path",link,"source",ico)));
            check(Convert.ToString(custom["source"])==ico&&store.Data.CustomIcons.ContainsKey(link),
                "AI gives the Chrome shortcut a generated display icon");
            var written=Map(Call(controller,"qidesk_set_shortcut_icon",Args("path",link,"source",ico,"index",0)));
            check(Convert.ToString(written["source"])==ico,"AI writes the generated icon into the shortcut itself");
            var read=Map(Call(controller,"qidesk_get_shortcut_icon",Args("path",link)));
            check(Convert.ToBoolean(read["hasCustomIcon"])&&Convert.ToString(read["source"])==ico,
                "the shortcut reports the generated icon location");
            Call(controller,"qidesk_reset_shortcut_icon",Args("path",link));
            check(!Convert.ToBoolean(Map(Call(controller,"qidesk_get_shortcut_icon",Args("path",link)))["hasCustomIcon"]),
                "resetting restores the shortcut target icon");
            Call(controller,"qidesk_reset_custom_icon",Args("path",link));
            check(!store.Data.CustomIcons.ContainsKey(link),"resetting drops the generated display icon");
            var shown=Map(Call(controller,"qidesk_show_panel",Args("show",true)));
            check(Convert.ToBoolean(shown["visible"]),"show_panel opens the workspace panel");
            var dismissed=Map(Call(controller,"qidesk_show_panel",Args("show",false)));
            check(!Convert.ToBoolean(dismissed["visible"]),"show_panel dismisses the workspace panel");
            check(Fail(delegate{Call(controller,"qidesk_set_hidden",Args("hidden",true));})=="请先启动桌面模式，再隐藏或显示分区。",
                "set_hidden refuses to run before desktop mode starts");
            var mode=Map(Call(controller,"qidesk_set_desktop_mode",Args("enabled",false)));
            check(!Convert.ToBoolean(mode["desktopMode"])&&!controller.Running,
                "set_desktop_mode reports a stopped desktop mode");
            string old=Path.Combine(work,"old-name.txt");File.WriteAllText(old,"rename fixture");
            var renamed=Map(Call(controller,"qidesk_rename_item",Args("path",old,"name","new-name.txt")));
            string fresh=Path.Combine(work,"new-name.txt");
            check(Convert.ToString(renamed["path"])==fresh&&!File.Exists(old)&&File.Exists(fresh),
                "rename_item renames a real workspace file through MCP");
            check(store.Data.PendingFileRenames.Count==0,"a completed rename leaves no recovery journal behind");
            check(controller.AiChanges.Any(c=>c.Action=="重命名"),"the rename is recorded in the AI change receipt");
            string outside=Path.Combine(root,"outside.txt");File.WriteAllText(outside,"outside");
            check(Fail(delegate{Call(controller,"qidesk_rename_item",Args("path",outside,"name","moved.txt"));}).Length>0,
                "rename_item refuses files outside desktop items, workspaces and panel folders");
            check(Fail(delegate{Call(controller,"qidesk_rename_item",Args("path",fresh,"name","CON.txt"));}).Contains("保留名称"),
                "rename_item rejects Windows reserved device names");
            check(Fail(delegate{Call(controller,"qidesk_open_item",Args("path",Path.Combine(work,"missing.txt")));})=="项目不存在，无法打开。",
                "open_item reports missing items instead of launching anything");
            var reassigned=Map(Call(controller,"qidesk_assign_items",Args("groupId",group.Id,"paths",new[]{fresh})));
            check(Convert.ToInt32(reassigned["assigned"])==1,"assign_items references the renamed workspace file");
            var released=Map(Call(controller,"qidesk_remove_items",Args("paths",new[]{fresh})));
            check(Convert.ToInt32(released["removed"])==1&&!group.Items.Contains(fresh,StringComparer.OrdinalIgnoreCase)&&File.Exists(fresh),
                "remove_items releases the file from the partition and keeps it on disk");
            WidgetPlugins.Create(store.Data,"clock",new[]{new Rect(0,0,1920,1080)});
            store.Save();
            var pack=Map(Call(controller,"qidesk_save_component_pack",Args("name","原生组合")));
            var added=Map(Call(controller,"qidesk_apply_component_pack",Args("packId",Convert.ToString(pack["packId"]))));
            check(Convert.ToInt32(added["added"])==1&&store.Data.Widgets.Count==2,
                "apply_component_pack places widgets with the live application window");
            check(Fail(delegate{Call(controller,"qidesk_show_panel",Args("show","yes"));})=="show must be a boolean.",
                "show_panel validates the boolean argument");
        }finally{controller.ClearAiChanges();manager.Quit();}
    }
}
}
