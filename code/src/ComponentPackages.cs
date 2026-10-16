using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Windows;

namespace QiDesk {
public class PluginManifest {
    public string id {get;set;}public string name {get;set;}public string kind {get;set;}public string description {get;set;}public string text {get;set;}public string accent {get;set;}
    public WidgetPlugin ToPlugin(){return new WidgetPlugin(){Id=id,Name=name,Kind=kind,Description=description??"",Text=text??"",Accent=accent??"#77BFA3"};}
    public static PluginManifest From(WidgetPlugin p){return new PluginManifest(){id=p.Id,name=p.Name,kind=p.Kind,description=p.Description,text=p.Text,accent=p.Accent};}
}
public class ComponentPreset {
    public string pluginId {get;set;}public string name {get;set;}public string text {get;set;}public string timeZone {get;set;}public string targetDate {get;set;}
    public bool showSeconds {get;set;}public int focusMinutes {get;set;}public double x {get;set;}public double y {get;set;}public double width {get;set;}public double height {get;set;}public double backgroundOpacity {get;set;}public string color {get;set;}
    public ComponentPreset(){text="";timeZone="local";targetDate="";showSeconds=true;focusMinutes=25;width=280;height=230;backgroundOpacity=.76;color="#77BFA3";}
}
public class ComponentPack {
    public string format {get;set;}public int version {get;set;}public string id {get;set;}public string name {get;set;}
    public List<PluginManifest> plugins {get;set;}public List<ComponentPreset> components {get;set;}
    public ComponentPack(){format="qidesk.component-pack";version=1;id="pack-"+Guid.NewGuid().ToString("N");name="我的桌面组合";plugins=new List<PluginManifest>();components=new List<ComponentPreset>();}
}
public static class ComponentPackages {
    static readonly string[] packKeys={"format","version","id","name","plugins","components"};
    static readonly string[] pluginKeys={"id","name","kind","description","text","accent"};
    static readonly string[] presetKeys={"pluginId","name","text","timeZone","targetDate","showSeconds","focusMinutes","x","y","width","height","backgroundOpacity","color"};
    static void Fields(Dictionary<string,object> row,string[] allowed){if(row!=null)foreach(string integer in new[]{"version","focusMinutes"}){object numeric;if(row.TryGetValue(integer,out numeric)&&!(numeric is int)&&!(numeric is long))throw new InvalidDataException(integer+" 需为整数。");}if(row==null||row.Keys.Any(k=>!allowed.Contains(k)))throw new InvalidDataException("组件包包含不支持的字段。");foreach(var pair in row){if(new[]{"plugins","components"}.Contains(pair.Key))continue;if(new[]{"version","focusMinutes","x","y","width","height","backgroundOpacity"}.Contains(pair.Key)){if(!(pair.Value is int)&&!(pair.Value is long)&&!(pair.Value is double)&&!(pair.Value is decimal))throw new InvalidDataException("组件包数字字段无效。");}else if(pair.Key=="showSeconds"){if(!(pair.Value is bool))throw new InvalidDataException("showSeconds 需为布尔值。");}else if(!(pair.Value is string))throw new InvalidDataException("组件包文字字段无效。");}}
    public static ComponentPack Read(string path){
        if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("组件包不能超过 1 MB。");string json=File.ReadAllText(path);var raw=Store.Json.Deserialize<Dictionary<string,object>>(json);Fields(raw,packKeys);
        foreach(string key in new[]{"plugins","components"}){object value;if(!raw.TryGetValue(key,out value)||!(value is IList))throw new InvalidDataException("组件包缺少列表："+key);foreach(object entry in (IList)value)Fields(entry as Dictionary<string,object>,key=="plugins"?pluginKeys:presetKeys);}
        var pack=Store.Json.Deserialize<ComponentPack>(json);Validate(pack);return pack;
    }
    static bool Number(double n,double min,double max){return !Double.IsNaN(n)&&!Double.IsInfinity(n)&&n>=min&&n<=max;}
    public static void Validate(ComponentPack pack){
        if(pack==null||pack.format!="qidesk.component-pack"||pack.version!=1||!System.Text.RegularExpressions.Regex.IsMatch(pack.id??"","^[a-z0-9][a-z0-9.-]{0,63}$")||String.IsNullOrWhiteSpace(pack.name)||pack.name.Length>80||pack.plugins==null||pack.plugins.Count>30||pack.components==null||pack.components.Count<1||pack.components.Count>30)throw new InvalidDataException("组件包格式、名称或数量无效。");
        var ids=new HashSet<string>(WidgetPlugins.Builtins().Select(p=>p.Id));foreach(var manifest in pack.plugins){if(manifest==null)throw new InvalidDataException("插件为空。");WidgetPlugins.ValidatePlugin(manifest.ToPlugin());if(!ids.Add(manifest.id))throw new InvalidDataException("组件包插件 ID 重复或与内置插件重名。");}
        foreach(var p in pack.components){if(p==null||!ids.Contains(p.pluginId)||String.IsNullOrWhiteSpace(p.name)||p.name.Length>80||p.text==null||p.text.Length>2000||p.timeZone==null||p.targetDate==null||!Number(p.x,0,20000)||!Number(p.y,0,20000)||!Number(p.width,220,800)||!Number(p.height,130,900)||!Number(p.backgroundOpacity,0,1)||p.focusMinutes<1||p.focusMinutes>180)throw new InvalidDataException("组件包中的组件设置无效。");
            string kind=WidgetPlugins.Builtins().Select(x=>PluginManifest.From(x)).Concat(pack.plugins).First(x=>x.id==p.pluginId).kind;if(p.height<WidgetPlugins.MinimumHeight(kind))throw new InvalidDataException("组件高度不足以显示内容。");
            try{System.Windows.Media.ColorConverter.ConvertFromString(p.color);if(p.timeZone!="local")TimeZoneInfo.FindSystemTimeZoneById(p.timeZone);DateTime date;if(p.targetDate.Length>0&&!DateTime.TryParse(p.targetDate,out date))throw new Exception();}catch{throw new InvalidDataException("组件的颜色、时区或日期无效。");}
        }
    }
    public static void ValidateLibrary(State state){if(state.ComponentPacks.Count>20)throw new InvalidDataException("最多保留 20 套桌面组合。");var ids=new HashSet<string>();long length=0;foreach(var pack in state.ComponentPacks){Validate(pack);if(!ids.Add(pack.id))throw new InvalidDataException("桌面组合 ID 重复。");length+=Store.Json.Serialize(pack).Length;}if(length>4*1024*1024)throw new InvalidDataException("组合库总量超过 4 MB。");}
    public static ComponentPack Export(State state,IEnumerable<DesktopWidget> widgets,string name){
        var chosen=widgets.ToArray();if(chosen.Length==0)throw new InvalidDataException("先选择要分享的组件。");var pack=new ComponentPack(){name=name};double left=chosen.Min(w=>w.X),top=chosen.Min(w=>w.Y);
        foreach(var w in chosen){var plugin=WidgetPlugins.Find(state,w.PluginId);if(plugin==null)throw new InvalidDataException("组件插件已移除。");pack.components.Add(new ComponentPreset(){pluginId=w.PluginId,name=w.Name,text=w.Text??"",timeZone=w.TimeZone??"local",targetDate=w.TargetDate??"",showSeconds=w.ShowSeconds,focusMinutes=w.FocusMinutes>0?w.FocusMinutes:25,x=w.X-left,y=w.Y-top,width=w.Width,height=w.Height,backgroundOpacity=w.BackgroundOpacity,color=w.Color});}
        pack.plugins=state.Plugins.Where(p=>chosen.Any(w=>w.PluginId==p.Id)).Select(PluginManifest.From).ToList();Validate(pack);return pack;
    }
    public static ComponentPack FromPlugins(State state,IEnumerable<string> pluginIds,string name){var copy=Store.Json.Deserialize<State>(Store.Json.Serialize(state));copy.Groups.Clear();copy.Widgets.Clear();foreach(string id in pluginIds.Distinct()){if(copy.Widgets.Count>=30)throw new InvalidDataException("组合组件太多。");var w=WidgetPlugins.Create(copy,id,new[]{new Rect(0,0,1920,1080)});w.X=(copy.Widgets.Count-1)%3*300;w.Y=(copy.Widgets.Count-1)/3*380;}return Export(copy,copy.Widgets,name);}
    public static void Install(State state,ComponentPack pack){
        Validate(pack);var candidate=Store.Json.Deserialize<State>(Store.Json.Serialize(state));candidate.ComponentPacks=candidate.ComponentPacks??new List<ComponentPack>();
        foreach(var manifest in pack.plugins){var p=manifest.ToPlugin();var existing=candidate.Plugins.FirstOrDefault(x=>x.Id==p.Id);if(existing==null)WidgetPlugins.Import(candidate,p);else if(Store.Json.Serialize(PluginManifest.From(existing))!=Store.Json.Serialize(manifest))throw new InvalidDataException("插件“"+p.Name+"”的 ID 已被其他配置使用，请先移除旧插件。");}
        var previous=candidate.ComponentPacks.FirstOrDefault(p=>p.id==pack.id);if(previous!=null){if(Store.Json.Serialize(previous)!=Store.Json.Serialize(pack))throw new InvalidDataException("同名组合已有不同版本，请先移除旧组合。");}else candidate.ComponentPacks.Add(Store.Json.Deserialize<ComponentPack>(Store.Json.Serialize(pack)));
        Store.Validate(candidate);state.Plugins=candidate.Plugins;state.ComponentPacks=candidate.ComponentPacks;
    }
    public static List<DesktopWidget> Add(State state,ComponentPack pack,IList<Rect> areas,Rect? selected){
        var copy=Store.Json.Deserialize<State>(Store.Json.Serialize(state));var added=AddCore(copy,pack,areas,selected);Store.Validate(copy);state.Widgets=copy.Widgets;state.Pages=copy.Pages;state.Page=copy.Page;return added;
    }
    static List<DesktopWidget> AddCore(State state,ComponentPack pack,IList<Rect> areas,Rect? selected){
        Validate(pack);if(state.Widgets.Count+pack.components.Count>30)throw new InvalidDataException("桌面组件最多 30 个，请先移除不用的组件。");foreach(var p in pack.components){var plugin=WidgetPlugins.Find(state,p.pluginId);if(plugin==null||!plugin.Enabled)throw new InvalidDataException("请先安装并启用组合中的插件。");}
        var added=new List<DesktopWidget>();Rect area=areas.Count==0?new Rect(0,0,1920,1080):areas[0];Rect target=selected??new Rect(area.Left+20,area.Top+20,area.Width-40,area.Height-40);
        if(!selected.HasValue){
            double totalWidth=pack.components.Max(p=>p.x+p.width),totalHeight=pack.components.Max(p=>p.y+p.height);Point origin;
            if(totalWidth<=target.Width&&totalHeight<=target.Height){int foundPage=FindSpace(state,area,totalWidth,totalHeight,out origin);state.Page=foundPage;state.Pages=Math.Max(state.Pages,foundPage+1);target=new Rect(origin.X,origin.Y,totalWidth,totalHeight);}
            else {selected=target;int openingPage=state.Page;if(state.Groups.Any(g=>g.Page==openingPage)||state.Widgets.Any(w=>w.Page==openingPage)){openingPage++;if(openingPage>=20)throw new InvalidDataException("当前页面放不下这套组合，且已达到页面上限。");state.Page=openingPage;state.Pages=Math.Max(state.Pages,openingPage+1);}}
        }
        double cellWidth=Math.Min(310,Math.Max(220,target.Width)),x=target.X,y=target.Y,row=0;int page=state.Page;
        foreach(var p in pack.components){double width=Math.Min(p.width,Math.Max(220,Math.Min(target.Width,area.Width-20))),height=Math.Min(Math.Max(p.height,p.pluginId=="calendar"?300:130),area.Height-20);
            if(selected.HasValue){if(x+width>target.Right&&x>target.Left){x=target.Left;y+=row+16;row=0;}if(y+height>area.Bottom-10&&y>area.Top+20){page++;if(page>=20)throw new InvalidDataException("已达到桌面页面上限。");x=target.Left;y=area.Top+20;row=0;}}
            var w=WidgetPlugins.Create(state,p.pluginId,areas);w.Name=p.name;w.Text=p.text;w.TimeZone=p.timeZone;w.TargetDate=p.targetDate;w.ShowSeconds=p.showSeconds;w.FocusMinutes=p.focusMinutes;w.BackgroundOpacity=p.backgroundOpacity;w.Color=p.color;w.Width=width;w.Height=height;
            if(selected.HasValue){w.X=x;w.Y=y;w.Page=page;x+=Math.Max(width,cellWidth)+16;row=Math.Max(row,height);state.Pages=Math.Max(state.Pages,page+1);}else{w.X=target.X+p.x;w.Y=target.Y+p.y;}
            Layout.EnsureVisible(w,areas,0);added.Add(w);
        }if(added.Count>0)state.Page=added[0].Page;return added;
    }
    static int FindSpace(State state,Rect area,double width,double height,out Point origin){
        for(int page=state.Page;page<20;page++){var taken=state.Groups.Cast<Group>().Concat(state.Widgets).Where(g=>g.Page==page).Select(g=>new Rect(g.X-8,g.Y-8,g.Width+16,g.Height+16)).ToArray();
            for(double y=area.Top+20;y+height<=area.Bottom-20;y+=24)for(double x=area.Left+20;x+width<=area.Right-20;x+=24){var rect=new Rect(x,y,width,height);if(!taken.Any(r=>r.IntersectsWith(rect))){origin=new Point(x,y);return page;}}
        }throw new InvalidDataException("没有足够空间添加这套组件，请移除一些组件或重新框选。");
    }
}
}
