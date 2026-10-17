using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace QiDesk {
public class Group {
    public bool SortDescending {get;set;}
    public string Id { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }
    public string Folder { get; set; }
    public string Kind { get; set; }
    public string HostGroupId { get; set; }
    public string ArchiveKey { get; set; }
    public string ActiveGroupId { get; set; }
    public bool TabPinned { get; set; }
    public string WorkView { get; set; } // 旧“文档/图片”内置标签的当前视图，只用于兼容旧配置
    public List<string> Folders { get; set; }
    public Dictionary<string,List<IgnoreRule>> FolderIgnoreRules {get;set;}
    public List<string> DisabledGlobalIgnoreIds {get;set;}
    public int FolderIndex { get; set; }
    public int Page { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Opacity { get; set; }
    public bool Rolled { get; set; }
    public bool Locked { get; set; }
    public bool ExcludeFromHide { get; set; }
    public string Sort { get; set; }
    public List<string> Items { get; set; }
    public string TitleMode {get;set;}
    public string LabelColor {get;set;}
    public double LabelSize {get;set;}
    public string LabelFont {get;set;}
    public double IconSize {get;set;}
    public string IconSkin {get;set;}
    public string IconColor {get;set;}
    public string CornerStyle {get;set;}
    public double TintStrength {get;set;}
    public string BackgroundStyle {get;set;}
    public string BackgroundImage {get;set;}
    public string ImageMode {get;set;}
    public string ViewMode {get;set;}
    public string Edge {get;set;}
    public double BackgroundOpacity {get;set;}
    public double BorderOpacity {get;set;}
    public List<IgnoreRule> IgnoreRules {get;set;}
    public Group() { DisabledGlobalIgnoreIds=new List<string>(); Id=Guid.NewGuid().ToString("N"); Color="#77BFA3"; Folder=""; Width=320; Height=340; Opacity=.90; Sort="名称"; Items=new List<string>();TitleMode="总是";LabelColor="#E8EDF6";LabelSize=14;LabelFont="Microsoft YaHei UI";IconSize=34;IconSkin="经典";IconColor="#92AAE5";CornerStyle="方角";WorkView="";TintStrength=.55;BackgroundStyle="跟随颜色";BackgroundImage="";ImageMode="填充";ViewMode="图标";Edge="";BackgroundOpacity=.76;BorderOpacity=.75;IgnoreRules=new List<IgnoreRule>();ArchiveKey="";Kind="普通";Folders=new List<string>();FolderIgnoreRules=new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);FolderIndex=0; }
}
public class Rule {
    public string Name { get; set; }
    public string Extensions { get; set; }
    public string Contains { get; set; }
    public string GroupId { get; set; }
    public bool Enabled { get; set; }
    public string NameMode {get;set;}
    public string Kind {get;set;}
    public Rule() { Name="规则"; Extensions=""; Contains=""; Enabled=true;NameMode="包含";Kind="全部"; }
    public bool Matches(string path) {
        bool ext=String.IsNullOrWhiteSpace(Extensions) || Extensions.Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries).Any(e=>String.Equals("."+e.Trim().TrimStart('.'),Path.GetExtension(path),StringComparison.OrdinalIgnoreCase));
        string fileName=Path.GetFileName(path);bool name=String.IsNullOrWhiteSpace(Contains)||(NameMode=="开头"?fileName.StartsWith(Contains,StringComparison.OrdinalIgnoreCase):NameMode=="结尾"?fileName.EndsWith(Contains,StringComparison.OrdinalIgnoreCase):fileName.IndexOf(Contains,StringComparison.OrdinalIgnoreCase)>=0);
        bool kind=Kind=="文件夹"?Directory.Exists(path):Kind=="文件"?File.Exists(path):true;
        return Enabled && ext && name && kind;
    }
}
// 工作区面板顶栏的一个标签：绑定真实文件夹（Folder），或引用桌面上的一个分区（GroupId）。
public class PanelTab {
    public string Folder {get;set;}
    public string GroupId {get;set;}
    public List<IgnoreRule> IgnoreRules {get;set;}
    public PanelTab(){Folder="";GroupId="";IgnoreRules=new List<IgnoreRule>();}
    public static PanelTab OfFolder(string folder){return new PanelTab(){Folder=folder??""};}
    public static PanelTab OfGroup(string id){return new PanelTab(){GroupId=id??""};}
    public bool IsFolder{get{return !String.IsNullOrEmpty(Folder);}}
}
public class State {
    public string ScreenshotFolder {get;set;}
    public bool UseEverything {get;set;}
    public bool QuickSearchAll {get;set;}
    public string EverythingPath {get;set;}
    public int Version {get;set;}
    public int Page {get;set;}
    public int Pages {get;set;}
    public bool AutoRules {get;set;}
    public bool HideOriginalIcons {get;set;}
    public bool HideDesktopIcons {get;set;}
    public bool PushIconsAside {get;set;}
    public bool ShowFileExtensions {get;set;}
    public bool DoubleClickHide {get;set;}
    public bool DesktopDragCreate {get;set;}
    public bool RollupEnabled {get;set;}
    public bool RollupOnDoubleClick {get;set;}
    public bool HoverExpand {get;set;}
    public bool EdgeRollup {get;set;}
    public bool PortalNavigate {get;set;}
    public bool SnapEnabled {get;set;}
    public int SnapGrid {get;set;}
    public int SnapGap {get;set;}
    public bool AutoDailySnapshot {get;set;}
    public int SnapshotKeepDays {get;set;}
    public string LayoutMode {get;set;}
    public bool DefaultPanelsAdded {get;set;}
    public int IdleHideSeconds {get;set;}
    public bool AutoShowOnDesktop {get;set;}
    public Dictionary<string,List<LayoutPlacement>> LayoutProfiles {get;set;}
    public string LastLayoutKey {get;set;}
    public string ActiveLayoutArchiveId {get;set;}
    public int LastAppliedLayoutRevision {get;set;}
    public string LastAppliedLayoutAt {get;set;}
    public string AiEndpoint {get;set;}
    public string AiModel {get;set;}
    public List<Group> Groups {get;set;}
    public List<Rule> Rules {get;set;}
    public List<string> Excluded {get;set;}
    public List<string> HiddenDesktopItems {get;set;}
    public Dictionary<string,string> ManagedDesktopItems {get;set;}
    public List<IgnoreRule> IgnoreRules {get;set;}
    public List<string> WorkFolders {get;set;}
    public bool McpEnabled {get;set;}
    public bool WorkAutoIgnore {get;set;}
    public bool WorkIgnoreCaches {get;set;}
    public List<IgnoreFileSet> IgnoreFiles {get;set;}
    public bool AutoImportIgnoreFiles {get;set;}
    public bool AutoDetectWorkspaces {get;set;}
    public Dictionary<string,string> WorkSources {get;set;}
    public Dictionary<string,string> WorkConnectedAt {get;set;}
    public string CurrentWorkspace {get;set;}
    public double SearchPanelX {get;set;}
    public double SearchPanelY {get;set;}
    public bool SearchPanelPositioned {get;set;}
    public int TutorialVersion {get;set;}
    public List<ComponentPack> ComponentPacks {get;set;}
    public List<DesktopWidget> Widgets {get;set;}
    public List<WidgetPlugin> Plugins {get;set;}
    public bool StarterWidgetsAdded {get;set;}
    public List<string> IgnoredWorkFolders {get;set;}
    // 工作区面板（浮动面板）：顶部是多个文件夹面板标签，一次只显示一个。
    public List<string> PanelFolders {get;set;}
    public List<PanelTab> PanelTabs {get;set;}
    public List<string> SelectedWorkspaceGroups {get;set;}
    public Group PanelStyle {get;set;}
    public string PanelFollowGroupId {get;set;}
    public string PinnedGroupId {get;set;}
    public int PanelIndex {get;set;}
    public double PanelWidth {get;set;}
    public double PanelHeight {get;set;}
    public double PanelX {get;set;}
    public double PanelY {get;set;}
    public bool PanelPositioned {get;set;}
    public string PanelViewMode {get;set;}
    public double PanelIconSize {get;set;}
    public string PanelIgnoreIcon {get;set;}
    // 桌面小猫：代码绘制的矢量形象。
    public double PetX {get;set;}
    public double PetY {get;set;}
    public bool PetPositioned {get;set;}
    public double PetScale {get;set;}
    public bool PetVisible {get;set;}
    public string PetSkin {get;set;}
    public List<PendingFileRename> PendingFileRenames {get;set;}
    // 桌面项自定义显示图标：路径 -> 图标来源，只影响栖桌内的显示。
    public Dictionary<string,CustomIcon> CustomIcons {get;set;}
    public State(){PendingFileRenames=new List<PendingFileRename>();ComponentPacks=new List<ComponentPack>();UseEverything=true;QuickSearchAll=true;EverythingPath="";ScreenshotFolder="";HideOriginalIcons=false;HideDesktopIcons=false;PushIconsAside=true;Version=8;Pages=3;AutoRules=false;DoubleClickHide=true;DesktopDragCreate=false;RollupEnabled=true;RollupOnDoubleClick=false;HoverExpand=true;PortalNavigate=true;SnapEnabled=true;SnapGrid=8;SnapGap=16;SnapshotKeepDays=3;LayoutMode="保持位置";LastLayoutKey="";ActiveLayoutArchiveId="";LastAppliedLayoutRevision=0;LastAppliedLayoutAt="";LayoutProfiles=new Dictionary<string,List<LayoutPlacement>>();AiEndpoint="http://127.0.0.1:11434";AiModel="qwen2.5:7b";Groups=new List<Group>();Rules=new List<Rule>();Excluded=new List<string>();HiddenDesktopItems=new List<string>();ManagedDesktopItems=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);IgnoreRules=new List<IgnoreRule>();WorkFolders=new List<string>();McpEnabled=true;WorkAutoIgnore=true;IgnoreFiles=new List<IgnoreFileSet>();AutoImportIgnoreFiles=true;AutoDetectWorkspaces=true;WorkSources=new Dictionary<string,string>();WorkConnectedAt=new Dictionary<string,string>();Widgets=new List<DesktopWidget>();Plugins=new List<WidgetPlugin>();IgnoredWorkFolders=new List<string>();PanelFolders=new List<string>();PanelTabs=new List<PanelTab>();SelectedWorkspaceGroups=new List<string>();PanelStyle=new Group(){Id="panel-style",Name="工作区面板",CornerStyle="方角",IconSize=44,BackgroundOpacity=.82,TintStrength=.55};PanelFollowGroupId="";PinnedGroupId="";PanelIndex=-1;PanelWidth=520;PanelHeight=620;PanelPositioned=false;PanelViewMode="图标";PanelIconSize=44;PanelIgnoreIcon="⊘";PetScale=1;PetVisible=true;PetSkin="默认猫";CustomIcons=new Dictionary<string,CustomIcon>(StringComparer.OrdinalIgnoreCase);}
}
public class LayoutPlacement {public string Id {get;set;}public double X {get;set;}public double Y {get;set;}public double Width {get;set;}public double Height {get;set;}public string Edge {get;set;}public bool Rolled {get;set;}}
public class Suggestion {
    public string Path {get;set;}
    public string GroupId {get;set;}
    public string Reason {get;set;}
    public override string ToString(){return System.IO.Path.GetFileName(Path)+"  →  "+Reason;}
}
public class Store {
    public readonly string Root;
    public State Data;
    public string LastError="";
    public int UndoGeneration {get;private set;}
    public bool DesktopActive {get;set;}
    public HashSet<string> Known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    Stack<string> undo=new Stack<string>();
    // One serializer per thread: the UI thread, MCP pipe thread and background search
    // threads all use Store.Json, and JavaScriptSerializer is not thread-safe.
    [ThreadStatic] static JavaScriptSerializer threadJson;
    public static JavaScriptSerializer Json {get{if(threadJson==null)threadJson=new JavaScriptSerializer(){MaxJsonLength=16*1024*1024};return threadJson;}}
    public Store(string root):this(root,true){}
    public Store(string root,bool load){Root=root;if(load){Directory.CreateDirectory(root);Directory.CreateDirectory(SnapshotRoot);Data=Load();}else Data=new State();}
    public string StatePath {get{return Path.Combine(Root,"state.json");}}
    public string SnapshotRoot {get{return Path.Combine(Root,"snapshots");}}
    State Load(){
        if(!File.Exists(StatePath))return CreateDefault();
        string json;
        try{json=File.ReadAllText(StatePath);}
        catch(Exception ex){LastError="配置读取失败，已保留原文件："+ex.Message;return CreateDefault();}
        try{
            var state=Json.Deserialize<State>(json);
            Validate(state);FileRenaming.Recover(state);return state;
        }catch(Exception ex){
            string backup=StatePath+".corrupt-"+DateTime.Now.ToString("yyyyMMddHHmmss");
            try{File.Copy(StatePath,backup,true);}catch{}
            string note;
            var repaired=Repair(json,backup,out note)??SalvageRecovery(json,backup,out note);
            if(repaired!=null){LastError=note;return repaired;}
            LastError="配置读取失败，已保留原文件："+ex.Message;
            return CreateDefault();
        }
    }
    static State CreateDefault(){
        var data=new State();string[] names={"应用与入口","工作文档","图片与媒体","收件箱"};string[] colors={"#77BFA3","#92AAE5","#D9AA75","#BA9DDD"};
        for(int i=0;i<names.Length;i++){var group=new Group(){Name=names[i],Color=colors[i],X=40+(i%3)*340,Y=65+(i/3)*370};if(names[i]=="收件箱"){group.X=16;group.Y=Math.Max(16,System.Windows.SystemParameters.WorkArea.Bottom-group.Height-16);}data.Groups.Add(group);}
        data.Rules.Add(new Rule(){Name="应用快捷方式",Extensions="lnk,url,exe",GroupId=data.Groups[0].Id});
        data.Rules.Add(new Rule(){Name="文档资料",Extensions="pdf,doc,docx,xls,xlsx,ppt,pptx,txt,md",GroupId=data.Groups[1].Id});
        data.Rules.Add(new Rule(){Name="图片和影音",Extensions="png,jpg,jpeg,gif,svg,webp,mp4,mov,mp3,wav",GroupId=data.Groups[2].Id});
        return data;
    }
    // Validate 是整份校验，任何一条记录损坏都会让整份配置不可用。直接回退默认值会连同
    // HiddenDesktopItems / ManagedDesktopItems 一起丢掉，下次启动就找不到恢复映射，
    // 被隐藏或移走的桌面图标再也回不来。所以先逐项修复，只丢掉确实无效的记录；
    // 修不好时至少保住桌面恢复记录：宁可丢布局，也不能丢恢复映射。
    static State Repair(string json,string backup,out string note){
        note="";
        State state;
        try{state=Json.Deserialize<State>(json);}catch{return null;}
        if(state==null)return null;
        state.Groups=(state.Groups??new List<Group>()).Where(g=>g!=null).ToList();
        state.Rules=state.Rules??new List<Rule>();
        RepairGroupIds(state);
        state.PendingFileRenames=ValidRenames(state.PendingFileRenames);
        state.IgnoreRules=KeepValid(state.IgnoreRules,IgnoreRule.Validate);
        state.IgnoreFiles=KeepValid(state.IgnoreFiles,IgnoreFiles.Validate);
        state.Plugins=ValidPlugins(state.Plugins);
        state.ComponentPacks=ValidPacks(state.ComponentPacks);
        state.Widgets=ValidWidgets(state.Widgets,state.Groups.Select(g=>g.Id));
        state.PanelTabs=ValidTabs(state.PanelTabs);
        foreach(var group in state.Groups){
            group.IgnoreRules=KeepValid(group.IgnoreRules,IgnoreRule.Validate);
            group.FolderIgnoreRules=RepairFolderRules(group.FolderIgnoreRules);
        }
        try{Validate(state);}
        catch{
            // 分区数据仍不可用时，优先保住桌面图标恢复记录：宁可丢布局，也不能丢恢复映射。
            var recovery=new State();
            recovery.Excluded=state.Excluded??new List<string>();
            recovery.HiddenDesktopItems=state.HiddenDesktopItems??new List<string>();
            recovery.ManagedDesktopItems=NormalizePaths(state.ManagedDesktopItems);
            try{Validate(recovery);}catch{return null;}
            note="配置损坏，已保留桌面图标恢复记录并备份原文件："+Path.GetFileName(backup);
            return recovery;
        }
        FileRenaming.Recover(state);
        note="配置有损坏项，已修复并备份原文件："+Path.GetFileName(backup);
        return state;
    }
    // 结构无法按 State 反序列化时（字段类型写错、旧版本残片），直接读原始 JSON 只捞恢复映射。
    static State SalvageRecovery(string json,string backup,out string note){
        note="";
        Dictionary<string,object> root;
        try{root=Json.Deserialize<Dictionary<string,object>>(json);}catch{return null;}
        if(root==null)return null;
        var state=new State();
        state.Excluded=Strings(Field(root,"Excluded"));
        state.HiddenDesktopItems=Strings(Field(root,"HiddenDesktopItems"));
        object managed=Field(root,"ManagedDesktopItems");
        var pairs=managed as Dictionary<string,object>;
        if(pairs!=null)foreach(var pair in pairs)if(!String.IsNullOrEmpty(pair.Key))state.ManagedDesktopItems[pair.Key]=Convert.ToString(pair.Value);
        try{Validate(state);}catch{return null;}
        note="配置结构损坏，已保留桌面图标恢复记录并备份原文件："+Path.GetFileName(backup);
        return state;
    }
    static object Field(Dictionary<string,object> root,string name){
        foreach(var pair in root)if(String.Equals(pair.Key,name,StringComparison.OrdinalIgnoreCase))return pair.Value;
        return null;
    }
    static List<string> Strings(object value){
        var list=new List<string>();var items=value as System.Collections.IEnumerable;
        if(items==null||value is string)return list;
        foreach(object item in items){string text=Convert.ToString(item);if(!String.IsNullOrEmpty(text))list.Add(text);}
        return list;
    }
    // JavaScriptSerializer 会用默认比较器重建 Dictionary，重载后路径查找变成区分大小写；
    // 统一成 OrdinalIgnoreCase，避免恢复桌面项时因为路径大小写不同而找不到映射。
    static Dictionary<string,string> NormalizePaths(Dictionary<string,string> map){
        var normalized=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in map??new Dictionary<string,string>())if(!String.IsNullOrEmpty(pair.Key))normalized[pair.Key]=pair.Value;
        return normalized;
    }
    static List<PendingFileRename> ValidRenames(List<PendingFileRename> renames){
        var kept=new List<PendingFileRename>();
        foreach(var rename in renames??new List<PendingFileRename>()){
            if(rename==null)continue;
            if(!Path.IsPathRooted(rename.Source??"")||!Path.IsPathRooted(rename.Destination??""))continue;
            if(!String.Equals(Path.GetDirectoryName(rename.Source),Path.GetDirectoryName(rename.Destination),StringComparison.OrdinalIgnoreCase))continue;
            kept.Add(rename);
            if(kept.Count>=100)break;
        }
        return kept;
    }
    // 逐条累加校验：只把让校验失败的那一条丢掉，其余记录保留。
    static List<T> KeepValid<T>(List<T> items,Action<List<T>> validate){
        var kept=new List<T>();
        foreach(var item in items??new List<T>()){
            kept.Add(item);
            try{validate(kept);}catch{kept.RemoveAt(kept.Count-1);}
        }
        return kept;
    }
    static void RepairGroupIds(State state){
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var group in state.Groups){
            if(!String.IsNullOrEmpty(group.Id)&&ids.Add(group.Id))continue;
            group.Id=Guid.NewGuid().ToString("N");ids.Add(group.Id);
        }
    }
    static List<WidgetPlugin> ValidPlugins(List<WidgetPlugin> plugins){
        var kept=new List<WidgetPlugin>();var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var plugin in plugins??new List<WidgetPlugin>()){
            try{WidgetPlugins.ValidatePlugin(plugin);}catch{continue;}
            if(WidgetPlugins.Builtins().Any(p=>p.Id==plugin.Id)||!ids.Add(plugin.Id))continue;
            kept.Add(plugin);
            if(kept.Count>=30)break;
        }
        return kept;
    }
    static List<ComponentPack> ValidPacks(List<ComponentPack> packs){
        var kept=new List<ComponentPack>();
        foreach(var pack in packs??new List<ComponentPack>()){
            kept.Add(pack);
            try{ComponentPackages.ValidateLibrary(new State(){ComponentPacks=kept});}catch{kept.RemoveAt(kept.Count-1);}
            if(kept.Count>=20)break;
        }
        return kept;
    }
    static List<DesktopWidget> ValidWidgets(List<DesktopWidget> widgets,IEnumerable<string> groupIds){
        var ids=new HashSet<string>(groupIds??new string[0],StringComparer.OrdinalIgnoreCase);
        var kept=new List<DesktopWidget>();
        foreach(var widget in widgets??new List<DesktopWidget>()){
            if(widget==null)continue;
            if(String.IsNullOrEmpty(widget.Id)||!ids.Add(widget.Id)){widget.Id=Guid.NewGuid().ToString("N");ids.Add(widget.Id);}
            kept.Add(widget);
            if(kept.Count>=30)break;
        }
        return kept;
    }
    static List<PanelTab> ValidTabs(List<PanelTab> tabs){
        var kept=new List<PanelTab>();
        foreach(var tab in tabs??new List<PanelTab>()){
            if(tab==null||(String.IsNullOrEmpty(tab.Folder)&&String.IsNullOrEmpty(tab.GroupId)))continue;
            tab.IgnoreRules=KeepValid(tab.IgnoreRules,IgnoreRule.Validate);
            kept.Add(tab);
            if(kept.Count>=60)break;
        }
        return kept;
    }
    static Dictionary<string,List<IgnoreRule>> RepairFolderRules(Dictionary<string,List<IgnoreRule>> rules){
        var kept=new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in rules??new Dictionary<string,List<IgnoreRule>>())if(!String.IsNullOrEmpty(pair.Key))kept[pair.Key]=KeepValid(pair.Value,IgnoreRule.Validate);
        return kept;
    }
    public static void Validate(State s){
        if(s==null||s.Groups==null||s.Rules==null)throw new InvalidDataException("缺少分组或规则");
        s.PendingFileRenames=s.PendingFileRenames??new List<PendingFileRename>();
        if(s.PendingFileRenames.Count>100||s.PendingFileRenames.Any(r=>r==null||!Path.IsPathRooted(r.Source??"")||!Path.IsPathRooted(r.Destination??"")||!String.Equals(Path.GetDirectoryName(r.Source),Path.GetDirectoryName(r.Destination),StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("重命名恢复记录无效。");
        s.Pages=Math.Max(1,Math.Min(20,s.Pages));s.Page=Math.Max(0,Math.Min(s.Pages-1,s.Page));
        s.Excluded=s.Excluded??new List<string>();s.HiddenDesktopItems=s.HiddenDesktopItems??new List<string>();s.ManagedDesktopItems=NormalizePaths(s.ManagedDesktopItems);foreach(var group in s.Groups){group.Items=group.Items??new List<string>();group.IgnoreRules=group.IgnoreRules??new List<IgnoreRule>();}
        s.IgnoreRules=s.IgnoreRules??new List<IgnoreRule>();s.WorkFolders=s.WorkFolders??new List<string>();
        s.IgnoreFiles=s.IgnoreFiles??new List<IgnoreFileSet>();IgnoreFiles.Validate(s.IgnoreFiles);s.WorkSources=s.WorkSources??new Dictionary<string,string>();s.WorkConnectedAt=s.WorkConnectedAt??new Dictionary<string,string>();s.Widgets=s.Widgets??new List<DesktopWidget>();s.Plugins=s.Plugins??new List<WidgetPlugin>();WidgetPlugins.Validate(s);
        s.IgnoredWorkFolders=s.IgnoredWorkFolders??new List<string>();
        s.CurrentWorkspace=s.CurrentWorkspace??"";
        s.PanelFolders=s.PanelFolders??new List<string>();s.PanelTabs=s.PanelTabs??new List<PanelTab>();
        s.SelectedWorkspaceGroups=(s.SelectedWorkspaceGroups??new List<string>()).Where(id=>s.Groups.Any(g=>g.Id==id&&String.IsNullOrEmpty(g.HostGroupId))).Distinct(StringComparer.OrdinalIgnoreCase).Take(60).ToList();
        if(s.PanelTabs.Count==0&&s.PanelFolders.Count>0)foreach(string folder in s.PanelFolders)s.PanelTabs.Add(PanelTab.OfFolder(folder));
        s.PanelFolders=new List<string>();
        s.PanelTabs=s.PanelTabs.Where(t=>t!=null&&(!String.IsNullOrEmpty(t.Folder)||!String.IsNullOrEmpty(t.GroupId))).ToList();if(s.PanelTabs.Count>60)s.PanelTabs=s.PanelTabs.Take(60).ToList();foreach(var tab in s.PanelTabs){tab.IgnoreRules=tab.IgnoreRules??new List<IgnoreRule>();IgnoreRule.Validate(tab.IgnoreRules);}
        s.PanelStyle=s.PanelStyle??new Group(){Id="panel-style",Name="工作区面板",CornerStyle="方角",IconSize=44};
        s.PanelStyle.Id="panel-style";NormalizeStyle(s.PanelStyle);
        s.PanelFollowGroupId=s.PanelFollowGroupId??"";
        s.PinnedGroupId=s.PinnedGroupId??"";
        s.PanelIndex=Math.Max(-1,Math.Min(s.PanelTabs.Count-1,s.PanelIndex));s.PanelWidth=Bound(s.PanelWidth,340,1400);s.PanelHeight=Bound(s.PanelHeight,260,2000);s.PanelX=Bound(s.PanelX,-20000,20000);s.PanelY=Bound(s.PanelY,-20000,20000);if(s.PanelViewMode!="图标"&&s.PanelViewMode!="列表"&&s.PanelViewMode!="详细信息"&&s.PanelViewMode!="树")s.PanelViewMode="图标";s.PanelIconSize=Bound(s.PanelIconSize,20,96);if(String.IsNullOrEmpty(s.PanelIgnoreIcon)||s.PanelIgnoreIcon.Length>4)s.PanelIgnoreIcon="⊘";
        if(s.PetSkin!="毛绒三花"&&s.PetSkin!="矢量猫")s.PetSkin="默认猫";
        double petScale=s.PetScale;s.PetScale=petScale<.875?.75:petScale<1.25?1:petScale<1.75?1.5:2;if(petScale<=0)s.PetScale=1;s.PetX=Bound(s.PetX,-20000,20000);s.PetY=Bound(s.PetY,-20000,20000);
        s.ComponentPacks=s.ComponentPacks??new List<ComponentPack>();ComponentPackages.ValidateLibrary(s);
        CustomIcons.Validate(s);
        s.TutorialVersion=Math.Max(0,Math.Min(1,s.TutorialVersion));
        if(s.Version<3){if(s.Version<2)s.RollupOnDoubleClick=false;s.PortalNavigate=true;s.HideOriginalIcons=true;s.UseEverything=true;s.QuickSearchAll=true;s.Version=3;foreach(var g in s.Groups){if(g.Sort=="修改时间")g.SortDescending=true;if(g.TintStrength<.3)g.TintStrength=.55;}}
        if(s.Version<4){s.HideOriginalIcons=false;s.AutoRules=false;s.Version=4;}
        if(s.Version<5){foreach(var widget in s.Widgets)if(widget!=null)widget.CornerStyle="方角";s.Version=5;}
        if(s.Version<6){MigrateDesktopPanels(s);s.Version=6;}
        if(s.Version<7){OrganizeDefaultPanels(s,false,true);s.Version=7;}
        if(s.Version<8){ConsolidateDefaultPanels(s);s.Version=8;}
        MigrateWorkSections(s);
        s.ScreenshotFolder=s.ScreenshotFolder??"";s.EverythingPath=s.EverythingPath??"";
        IgnoreRule.Validate(s.IgnoreRules);
        s.LayoutProfiles=s.LayoutProfiles??new Dictionary<string,List<LayoutPlacement>>();
        s.ActiveLayoutArchiveId=s.ActiveLayoutArchiveId??"";s.LastAppliedLayoutRevision=Math.Max(0,s.LastAppliedLayoutRevision);s.LastAppliedLayoutAt=s.LastAppliedLayoutAt??"";
        s.SnapGrid=(int)Bound(s.SnapGrid,1,64);s.SnapGap=(int)Bound(s.SnapGap,0,80);s.SnapshotKeepDays=(int)Bound(s.SnapshotKeepDays,1,30);s.IdleHideSeconds=(int)Bound(s.IdleHideSeconds,0,3600);
        var ids=new HashSet<string>();
        foreach(var g in s.Groups){if(String.IsNullOrEmpty(g.Id)||!ids.Add(g.Id))throw new InvalidDataException("分组 ID 重复或为空");g.Items=g.Items??new List<string>();g.IgnoreRules=g.IgnoreRules??new List<IgnoreRule>();IgnoreRule.Validate(g.IgnoreRules);g.FolderIgnoreRules=g.FolderIgnoreRules??new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);foreach(var rules in g.FolderIgnoreRules.Values)IgnoreRule.Validate(rules);g.Width=Bound(g.Width,220,2000);g.Height=Bound(g.Height,100,2000);g.X=Bound(g.X,-20000,20000);g.Y=Bound(g.Y,-20000,20000);g.Opacity=Bound(g.Opacity,.3,1);NormalizeStyle(g);g.Page=Math.Max(0,Math.Min(s.Pages-1,g.Page));if(String.IsNullOrEmpty(g.Color))g.Color="#77BFA3";if(String.IsNullOrEmpty(g.LabelColor))g.LabelColor="#E8EDF6";if(String.IsNullOrEmpty(g.TitleMode))g.TitleMode="总是";if(String.IsNullOrEmpty(g.ViewMode))g.ViewMode="图标";g.Folders=(g.Folders??new List<string>()).Where(f=>!String.IsNullOrEmpty(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(g.Folders.Count==0&&!String.IsNullOrEmpty(g.Folder))g.Folders.Add(g.Folder);if(g.Kind!="文件夹"&&g.Kind!="应用")g.Kind=!String.IsNullOrEmpty(g.Folder)?"文件夹":"普通";if(g.Kind=="文件夹"){g.FolderIndex=g.Folders.Count==0?-1:Math.Max(String.IsNullOrEmpty(g.Folder)?-1:0,Math.Min(g.Folders.Count-1,g.FolderIndex));}else{g.Folder="";g.FolderIndex=g.Folders.Count==0?-1:Math.Max(-1,Math.Min(g.Folders.Count-1,g.FolderIndex));}}
        foreach(var g in s.Groups){g.FolderIgnoreRules=new Dictionary<string,List<IgnoreRule>>(g.FolderIgnoreRules,StringComparer.OrdinalIgnoreCase);g.DisabledGlobalIgnoreIds=(g.DisabledGlobalIgnoreIds??new List<string>()).Where(id=>!String.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Take(4000).ToList();g.ArchiveKey=(g.ArchiveKey??"").Trim();if(g.ArchiveKey.Length>40)g.ArchiveKey=g.ArchiveKey.Substring(0,40);}
        foreach(var g in s.Groups){if(String.IsNullOrEmpty(g.HostGroupId))continue;var host=s.Groups.FirstOrDefault(x=>x.Id==g.HostGroupId);if(host==null||host==g||!String.IsNullOrEmpty(host.HostGroupId)){g.HostGroupId="";continue;}g.Page=host.Page;}
        foreach(var g in s.Groups.Where(x=>String.IsNullOrEmpty(x.HostGroupId)))if(g.ActiveGroupId!=g.Id&&!s.Groups.Any(x=>x.HostGroupId==g.Id&&x.Id==g.ActiveGroupId))g.ActiveGroupId=g.Id;
    }
    static void MigrateDesktopPanels(State state){OrganizeDefaultPanels(state,true);}
    static void ConsolidateDefaultPanels(State state){
        var hosts=state.Groups.Where(g=>g.Name=="文档"&&state.Groups.Any(child=>child.HostGroupId==g.Id&&child.Name=="图片")&&state.Groups.Where(child=>child.HostGroupId==g.Id).All(child=>child.Name=="图片")).ToArray();if(hosts.Length<2)return;
        var keep=hosts.OrderByDescending(g=>g.X).ThenByDescending(g=>g.Items.Count).First();var keepPicture=state.Groups.First(g=>g.HostGroupId==keep.Id&&g.Name=="图片");
        foreach(var old in hosts.Where(g=>g!=keep)){
            foreach(string path in old.Items)if(!keep.Items.Contains(path,StringComparer.OrdinalIgnoreCase))keep.Items.Add(path);
            foreach(var child in state.Groups.Where(g=>g.HostGroupId==old.Id).ToArray()){foreach(string path in child.Items)if(!keepPicture.Items.Contains(path,StringComparer.OrdinalIgnoreCase))keepPicture.Items.Add(path);state.Groups.Remove(child);}
            state.Groups.Remove(old);
        }
        keep.ActiveGroupId=keep.Id;
    }
    static void MigrateWorkSections(State state){
        // 旧“工作”分区自带文档/图片两个内置标签。现在所有分区平权，拆成普通分区 + 图片子分区；
        // 之后标签栏只认数据来源（Items 或文件夹），不再有 Kind=工作 的特例。
        foreach(var work in state.Groups.Where(g=>g.Kind=="工作"&&String.IsNullOrEmpty(g.Folder)).ToArray()){
            var pictures=state.Groups.FirstOrDefault(g=>g.HostGroupId==work.Id&&g.Name=="图片");
            if(pictures==null){
                pictures=new Group(){Name="图片",Color=work.Color,HostGroupId=work.Id,Page=work.Page,Width=work.Width,Height=work.Height,IconSize=work.IconSize,IconSkin=work.IconSkin,IconColor=work.IconColor};
                state.Groups.Add(pictures);
            }
            foreach(string path in work.Items.Where(p=>FilePresentation.MatchesKind(p,"图片")).ToArray()){
                work.Items.Remove(path);
                if(!pictures.Items.Contains(path,StringComparer.OrdinalIgnoreCase))pictures.Items.Add(path);
            }
            work.Kind="普通";work.ActiveGroupId=work.Id;work.WorkView="";
        }
    }
    public static void OrganizeDefaultPanels(State state,bool convertWork,bool hiddenOnly=false){
        var folder=state.Groups.FirstOrDefault(g=>g.Kind=="文件夹"&&String.IsNullOrEmpty(g.Folder));
        var work=state.Groups.FirstOrDefault(g=>(g.Kind=="工作"||g.Name=="文档"&&state.Groups.Any(child=>child.HostGroupId==g.Id&&child.Name=="图片"))&&String.IsNullOrEmpty(g.Folder));
        var apps=state.Groups.FirstOrDefault(g=>g.Kind=="应用"&&String.IsNullOrEmpty(g.Folder));
        Group pictures=null;
        if(work!=null&&convertWork){
            pictures=state.Groups.FirstOrDefault(g=>g.HostGroupId==work.Id&&g.Name=="图片");
            if(pictures==null){pictures=new Group(){Name="图片",Color=work.Color,HostGroupId=work.Id,Page=work.Page,Width=work.Width,Height=work.Height,IconSize=work.IconSize,IconSkin=work.IconSkin};state.Groups.Add(pictures);}
            foreach(string path in work.Items.Where(p=>FilePresentation.MatchesKind(p,"图片")).ToArray()){work.Items.Remove(path);if(!pictures.Items.Contains(path,StringComparer.OrdinalIgnoreCase))pictures.Items.Add(path);}
            work.Name="文档";work.Kind="普通";work.ActiveGroupId=work.Id;
        }else if(work!=null)pictures=state.Groups.FirstOrDefault(g=>g.HostGroupId==work.Id&&g.Name=="图片");
        foreach(string path in AllDesktopItems().Where(p=>!hiddenOnly||IsHidden(p))){
            if(state.Excluded.Contains(path,StringComparer.OrdinalIgnoreCase)||state.Groups.Any(g=>g.Items.Contains(path,StringComparer.OrdinalIgnoreCase)))continue;
            Group target=Directory.Exists(path)?folder:FilePresentation.MatchesKind(path,"应用")?apps:FilePresentation.MatchesKind(path,"图片")?pictures:FilePresentation.MatchesKind(path,"文档")?work:null;
            if(target!=null&&!PartitionIgnores.Ignored(state,path,target))target.Items.Add(path);
        }
    }
    static bool IsHidden(string path){try{return (File.GetAttributes(path)&FileAttributes.Hidden)!=0;}catch{return false;}}
    public static IEnumerable<string> AllDesktopItems(){var paths=new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)};var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string dir in paths.Distinct()){string[] entries;try{entries=Directory.GetFileSystemEntries(dir);}catch{continue;}foreach(string path in entries){if(Path.GetFileName(path).Equals("desktop.ini",StringComparison.OrdinalIgnoreCase))continue;if(found.Add(path))yield return path;}}}
    static double Bound(double x,double min,double max){return Double.IsNaN(x)||Double.IsInfinity(x)?min:Math.Max(min,Math.Min(max,x));}
    // 外表字段的统一校验：桌面分区和工作区面板共用同一套外观，避免两处走偏。
    public static readonly string[] BackgroundStyles={"跟随颜色","纯色","渐变","毛玻璃","图片"};
    public static readonly string[] ImageModes={"填充","适应","平铺","居中"};
    public static void NormalizeStyle(Group g){
        if(g==null)return;
        g.BackgroundOpacity=Bound(g.BackgroundOpacity,0,1);g.BorderOpacity=Bound(g.BorderOpacity,0,1);
        g.LabelSize=Bound(g.LabelSize,10,24);g.IconSize=Bound(g.IconSize,20,96);
        g.IconSkin=AppIconSkins.Choices.Contains(g.IconSkin)?g.IconSkin:"经典";
        if(String.IsNullOrEmpty(g.IconColor))g.IconColor="#92AAE5";
        g.CornerStyle=g.CornerStyle=="方角"?"方角":"圆角";
        g.TintStrength=Bound(g.TintStrength,0,1);
        if(String.IsNullOrEmpty(g.Color))g.Color="#77BFA3";
        if(String.IsNullOrEmpty(g.LabelColor))g.LabelColor="#E8EDF6";
        if(String.IsNullOrEmpty(g.ViewMode))g.ViewMode="图标";
        if(!BackgroundStyles.Contains(g.BackgroundStyle))g.BackgroundStyle="跟随颜色";
        if(!ImageModes.Contains(g.ImageMode))g.ImageMode="填充";
        g.BackgroundImage=g.BackgroundImage??"";
    }
    public void Save(){Validate(Data);if(DesktopActive&&String.Equals(Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QiDesk"),StringComparison.OrdinalIgnoreCase)){DesktopOrganization.Sync(Data,Root,SaveCore);if(DesktopOrganization.LastError.Length>0)LastError="桌面图标整理未完成："+DesktopOrganization.LastError;}SaveCore();}
    public void RestoreDesktop(){DesktopActive=false;DesktopOrganization.Restore(Data);if(DesktopOrganization.LastError.Length>0)LastError="桌面图标恢复未完成："+DesktopOrganization.LastError;Save();}
    internal void PersistFileRecovery(){Validate(Data);SaveCore();}
    internal void RemapFileHistory(PendingFileRename rename){
        // Layout undo never undoes a real Shell operation. Keep its references
        // aligned with the file so an older layout cannot lose recovery metadata.
        var remapped=new Stack<string>();
        foreach(string json in undo.Reverse()){
            var snapshot=Json.Deserialize<State>(json);
            FileRenaming.UpdateReferences(snapshot,rename);
            remapped.Push(Json.Serialize(snapshot));
        }
        undo=remapped;
    }
    void SaveCore(){Functions.WriteAtomic(StatePath,Json.Serialize(Data),StatePath+".bak");}
    public void Checkpoint(){undo.Push(Json.Serialize(Data));if(undo.Count>40)undo=new Stack<string>(undo.Take(40).Reverse());}
    public bool Undo(){if(undo.Count==0)return false;Data=Json.Deserialize<State>(undo.Pop());Save();UndoGeneration++;return true;}
    public static bool Exists(string path){return File.Exists(path)||Directory.Exists(path);}
    public static IEnumerable<string> DesktopItems(){
        var paths=new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)};
        var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string dir in paths.Distinct()) {string[] files;try{files=Directory.GetFileSystemEntries(dir);}catch{continue;}
            foreach(string f in files){if(Path.GetFileName(f).Equals("desktop.ini",StringComparison.OrdinalIgnoreCase))continue;try{if((File.GetAttributes(f)&FileAttributes.Hidden)!=0)continue;}catch{continue;}if(found.Add(f))yield return f;}}
    }
    public Group Assigned(string path){return Data.Groups.FirstOrDefault(g=>String.IsNullOrEmpty(g.Folder)&&g.Items.Contains(path,StringComparer.OrdinalIgnoreCase));}
    public bool Ignored(string path,Group group){return PartitionIgnores.Ignored(Data,path,group);}
    public Group Match(string path){foreach(var rule in Data.Rules){if(!rule.Matches(path))continue;var g=Data.Groups.FirstOrDefault(x=>x.Id==rule.GroupId&&String.IsNullOrEmpty(x.Folder));if(g!=null)return Ignored(path,g)?null:g;}var fallback=Data.Groups.FirstOrDefault(g=>String.IsNullOrEmpty(g.Folder)&&g.Name=="收件箱")??Data.Groups.FirstOrDefault(g=>String.IsNullOrEmpty(g.Folder));return Ignored(path,fallback)?null:fallback;}
    public void Assign(string path,Group target){if(!Exists(path)||!String.IsNullOrEmpty(target.Folder))return;path=Path.GetFullPath(path);foreach(var g in Data.Groups)g.Items.RemoveAll(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase));Data.Excluded.RemoveAll(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase));target.Items.Add(path);}
    public void RemoveFromGroups(string path){try{path=Path.GetFullPath(path);}catch{}string original=DesktopOrganization.Release(Data,path);foreach(var g in Data.Groups)g.Items.RemoveAll(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase));if(!Data.Excluded.Contains(original,StringComparer.OrdinalIgnoreCase))Data.Excluded.Add(original);}
    public int Scan(bool all){int n=0;foreach(var p in DesktopItems()){Known.Add(p);if(Assigned(p)==null&&Data.AutoRules&&!Data.Excluded.Contains(p,StringComparer.OrdinalIgnoreCase)){var g=Match(p);if(g!=null){Assign(p,g);n++;}}}return n;}
    public IEnumerable<string> Items(Group g,string search){
        return ItemsAt(g,search,g.Folder);
    }
    public IEnumerable<string> ItemsAt(Group g,string search,string folder){
        IEnumerable<string> result;
        if(!String.IsNullOrEmpty(folder)){try{result=Directory.GetFileSystemEntries(folder).Where(p=>!Path.GetFileName(p).Equals("desktop.ini",StringComparison.OrdinalIgnoreCase)).ToArray();}catch{result=new string[0];}}
        else result=g.Items.Where(Exists);
        result=result.Where(p=>!Ignored(p,g));
        if(!String.IsNullOrEmpty(search))result=result.Where(p=>Path.GetFileName(p).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0);
        return FilePresentation.Sorted(result,g);
    }
    public string Snapshot(){
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string path=Path.Combine(SnapshotRoot,stamp+".json");
        for(int i=1;File.Exists(path);i++)path=Path.Combine(SnapshotRoot,stamp+"-"+i+".json");
        File.WriteAllText(path,Json.Serialize(Data));
        PruneSnapshots(30);
        return path;
    }
    // 手动快照和每次应用布局都会生成一份快照；只保留最近若干份，避免 snapshots 无限增长。
    // 自动快照按天清理，由 DailySnapshot 自己负责，这里不动它们。
    public void PruneSnapshots(int keep){
        if(keep<1)keep=1;
        var manual=Directory.GetFiles(SnapshotRoot,"*.json").Where(p=>!Path.GetFileName(p).StartsWith("auto-",StringComparison.OrdinalIgnoreCase)).OrderByDescending(Path.GetFileName).Skip(keep);
        foreach(string old in manual)try{File.Delete(old);}catch{}
    }
    public void DailySnapshot(DateTime today){if(!Data.AutoDailySnapshot)return;string name="auto-"+today.ToString("yyyyMMdd")+".json",path=Path.Combine(SnapshotRoot,name);if(!File.Exists(path))File.WriteAllText(path,Json.Serialize(Data));foreach(string old in Directory.GetFiles(SnapshotRoot,"auto-????????.json").OrderByDescending(Path.GetFileName).Skip(Data.SnapshotKeepDays))File.Delete(old);}
    public void RecordLayout(string key){if(String.IsNullOrEmpty(key))return;Data.LayoutProfiles[key]=Data.Groups.Cast<Group>().Concat(Data.Widgets).Select(g=>new LayoutPlacement(){Id=g.Id,X=g.X,Y=g.Y,Width=g.Width,Height=g.Height,Edge=g.Edge,Rolled=g.Rolled}).ToList();Data.LastLayoutKey=key;if(Data.LayoutProfiles.Count>20)Data.LayoutProfiles.Remove(Data.LayoutProfiles.Keys.First(k=>k!=key));}
    public bool RestoreLayout(string key){List<LayoutPlacement> positions;if(!Data.LayoutProfiles.TryGetValue(key,out positions)||positions==null)return false;foreach(var pos in positions){var group=Data.Groups.Cast<Group>().Concat(Data.Widgets).FirstOrDefault(g=>g.Id==pos.Id);if(group==null)continue;group.X=pos.X;group.Y=pos.Y;group.Width=pos.Width;group.Height=pos.Height;group.Edge=pos.Edge;group.Rolled=pos.Rolled;}Validate(Data);Data.LastLayoutKey=key;return true;}
    public void Restore(string p){var s=Json.Deserialize<State>(File.ReadAllText(p));Validate(s);Checkpoint();Data=s;Save();}
    public List<Suggestion> Suggest(){var result=new List<Suggestion>();foreach(string p in DesktopItems()){
        Group g=Match(p);string reason="扩展名规则";
        string name=Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
        var work=Data.Groups.FirstOrDefault(x=>String.IsNullOrEmpty(x.Folder)&&(x.Name.Contains("工作")||x.Name.Contains("文档")));
        if(work!=null&&new[]{"报告","合同","论文","项目","report","proposal","thesis"}.Any(name.Contains)){g=work;reason="文件名包含工作资料关键词";}
        if(g!=null&&!Ignored(p,g)&&Assigned(p)!=g)result.Add(new Suggestion(){Path=p,GroupId=g.Id,Reason=g.Name+" · "+reason});
    }return result;}
}
}

