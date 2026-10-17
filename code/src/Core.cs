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
    public string ActiveGroupId { get; set; }
    public string WorkView { get; set; }
    public List<string> Folders { get; set; }
    public Dictionary<string,List<IgnoreRule>> FolderIgnoreRules {get;set;}
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
    public Group() { Id=Guid.NewGuid().ToString("N"); Color="#77BFA3"; Folder=""; Width=320; Height=340; Opacity=.90; Sort="名称"; Items=new List<string>();TitleMode="总是";LabelColor="#E8EDF6";LabelSize=14;LabelFont="Microsoft YaHei UI";IconSize=34;IconSkin="经典";IconColor="#92AAE5";CornerStyle="方角";WorkView="文档";TintStrength=.55;BackgroundStyle="跟随颜色";BackgroundImage="";ImageMode="填充";ViewMode="图标";Edge="";BackgroundOpacity=.76;BorderOpacity=.75;IgnoreRules=new List<IgnoreRule>();Kind="普通";Folders=new List<string>();FolderIgnoreRules=new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);FolderIndex=0; }
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
    public bool ClipboardEnabled {get;set;}
    public bool ClipboardPinned {get;set;}
    public int ClipboardLimit {get;set;}
    public string ClipboardSide {get;set;}
    public string ScreenshotFolder {get;set;}
    public bool UseEverything {get;set;}
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
    public State(){ComponentPacks=new List<ComponentPack>();ClipboardEnabled=false;ClipboardLimit=50;ClipboardSide="右侧";UseEverything=true;EverythingPath="";ScreenshotFolder="";HideOriginalIcons=false;HideDesktopIcons=false;PushIconsAside=true;Version=8;Pages=3;AutoRules=false;DoubleClickHide=true;DesktopDragCreate=false;RollupEnabled=true;RollupOnDoubleClick=false;HoverExpand=true;PortalNavigate=true;SnapEnabled=true;SnapGrid=8;SnapGap=16;SnapshotKeepDays=3;LayoutMode="保持位置";LastLayoutKey="";LayoutProfiles=new Dictionary<string,List<LayoutPlacement>>();AiEndpoint="http://127.0.0.1:11434";AiModel="qwen2.5:7b";Groups=new List<Group>();Rules=new List<Rule>();Excluded=new List<string>();HiddenDesktopItems=new List<string>();ManagedDesktopItems=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);IgnoreRules=new List<IgnoreRule>();WorkFolders=new List<string>();McpEnabled=true;WorkAutoIgnore=true;IgnoreFiles=new List<IgnoreFileSet>();AutoImportIgnoreFiles=true;AutoDetectWorkspaces=true;WorkSources=new Dictionary<string,string>();WorkConnectedAt=new Dictionary<string,string>();Widgets=new List<DesktopWidget>();Plugins=new List<WidgetPlugin>();IgnoredWorkFolders=new List<string>();PanelFolders=new List<string>();PanelTabs=new List<PanelTab>();SelectedWorkspaceGroups=new List<string>();PanelStyle=new Group(){Id="panel-style",Name="工作区面板",CornerStyle="方角",IconSize=44,BackgroundOpacity=.82,TintStrength=.55};PanelFollowGroupId="";PinnedGroupId="";PanelIndex=-1;PanelWidth=520;PanelHeight=620;PanelPositioned=false;PanelViewMode="图标";PanelIconSize=44;PanelIgnoreIcon="⊘";PetScale=1;PetVisible=true;PetSkin="默认猫";}
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
        if(File.Exists(StatePath))try{var s=Json.Deserialize<State>(File.ReadAllText(StatePath));Validate(s);return s;}catch(Exception ex){LastError="配置读取失败，已保留原文件："+ex.Message;File.Copy(StatePath,StatePath+".corrupt-"+DateTime.Now.ToString("yyyyMMddHHmmss"),true);}
        var data=new State();string[] names={"应用与入口","工作文档","图片与媒体","收件箱"};string[] colors={"#77BFA3","#92AAE5","#D9AA75","#BA9DDD"};
        for(int i=0;i<names.Length;i++){var group=new Group(){Name=names[i],Color=colors[i],X=40+(i%3)*340,Y=65+(i/3)*370};if(names[i]=="收件箱"){group.X=16;group.Y=Math.Max(16,System.Windows.SystemParameters.WorkArea.Bottom-group.Height-16);}data.Groups.Add(group);}
        data.Rules.Add(new Rule(){Name="应用快捷方式",Extensions="lnk,url,exe",GroupId=data.Groups[0].Id});
        data.Rules.Add(new Rule(){Name="文档资料",Extensions="pdf,doc,docx,xls,xlsx,ppt,pptx,txt,md",GroupId=data.Groups[1].Id});
        data.Rules.Add(new Rule(){Name="图片和影音",Extensions="png,jpg,jpeg,gif,svg,webp,mp4,mov,mp3,wav",GroupId=data.Groups[2].Id});
        return data;
    }
    public static void Validate(State s){
        if(s==null||s.Groups==null||s.Rules==null)throw new InvalidDataException("缺少分组或规则");
        s.Pages=Math.Max(1,Math.Min(20,s.Pages));s.Page=Math.Max(0,Math.Min(s.Pages-1,s.Page));
        s.Excluded=s.Excluded??new List<string>();s.HiddenDesktopItems=s.HiddenDesktopItems??new List<string>();s.ManagedDesktopItems=s.ManagedDesktopItems??new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(var group in s.Groups){group.Items=group.Items??new List<string>();group.IgnoreRules=group.IgnoreRules??new List<IgnoreRule>();}
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
        s.TutorialVersion=Math.Max(0,Math.Min(1,s.TutorialVersion));
        if(s.Version<3){if(s.Version<2)s.RollupOnDoubleClick=false;s.PortalNavigate=true;s.HideOriginalIcons=true;s.ClipboardEnabled=false;s.ClipboardLimit=50;s.ClipboardSide="右侧";s.UseEverything=true;s.Version=3;foreach(var g in s.Groups){if(g.Sort=="修改时间")g.SortDescending=true;if(g.TintStrength<.3)g.TintStrength=.55;}}
        if(s.Version<4){s.HideOriginalIcons=false;s.AutoRules=false;s.Version=4;}
        if(s.Version<5){foreach(var widget in s.Widgets)if(widget!=null)widget.CornerStyle="方角";s.Version=5;}
        if(s.Version<6){MigrateDesktopPanels(s);s.Version=6;}
        if(s.Version<7){OrganizeDefaultPanels(s,false,true);s.Version=7;}
        if(s.Version<8){ConsolidateDefaultPanels(s);s.Version=8;}
        s.ClipboardLimit=Math.Max(10,Math.Min(200,s.ClipboardLimit));s.ClipboardSide=s.ClipboardSide=="左侧"?"左侧":"右侧";s.ScreenshotFolder=s.ScreenshotFolder??"";s.EverythingPath=s.EverythingPath??"";
        IgnoreRule.Validate(s.IgnoreRules);
        s.LayoutProfiles=s.LayoutProfiles??new Dictionary<string,List<LayoutPlacement>>();
        s.SnapGrid=(int)Bound(s.SnapGrid,1,64);s.SnapGap=(int)Bound(s.SnapGap,0,80);s.SnapshotKeepDays=(int)Bound(s.SnapshotKeepDays,1,30);s.IdleHideSeconds=(int)Bound(s.IdleHideSeconds,0,3600);
        var ids=new HashSet<string>();
        foreach(var g in s.Groups){if(String.IsNullOrEmpty(g.Id)||!ids.Add(g.Id))throw new InvalidDataException("分组 ID 重复或为空");g.Items=g.Items??new List<string>();g.IgnoreRules=g.IgnoreRules??new List<IgnoreRule>();IgnoreRule.Validate(g.IgnoreRules);g.FolderIgnoreRules=g.FolderIgnoreRules??new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);foreach(var rules in g.FolderIgnoreRules.Values)IgnoreRule.Validate(rules);g.Width=Bound(g.Width,220,2000);g.Height=Bound(g.Height,100,2000);g.X=Bound(g.X,-20000,20000);g.Y=Bound(g.Y,-20000,20000);g.Opacity=Bound(g.Opacity,.3,1);NormalizeStyle(g);g.Page=Math.Max(0,Math.Min(s.Pages-1,g.Page));if(String.IsNullOrEmpty(g.Color))g.Color="#77BFA3";if(String.IsNullOrEmpty(g.LabelColor))g.LabelColor="#E8EDF6";if(String.IsNullOrEmpty(g.TitleMode))g.TitleMode="总是";if(String.IsNullOrEmpty(g.ViewMode))g.ViewMode="图标";g.Folders=g.Folders??new List<string>();if(g.Folders.Count==0&&!String.IsNullOrEmpty(g.Folder))g.Folders.Add(g.Folder);if(g.Kind!="文件夹"&&g.Kind!="工作"&&g.Kind!="应用")g.Kind=!String.IsNullOrEmpty(g.Folder)?"文件夹":"普通";if(g.Kind=="文件夹"){g.FolderIndex=g.Folders.Count==0?-1:Math.Max(String.IsNullOrEmpty(g.Folder)?-1:0,Math.Min(g.Folders.Count-1,g.FolderIndex));}else{g.Folder="";g.FolderIndex=g.Folders.Count==0?-1:Math.Max(-1,Math.Min(g.Folders.Count-1,g.FolderIndex));}}
        foreach(var g in s.Groups)g.FolderIgnoreRules=new Dictionary<string,List<IgnoreRule>>(g.FolderIgnoreRules,StringComparer.OrdinalIgnoreCase);
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
            if(target!=null&&!state.IgnoreRules.Any(r=>r.Matches(path))&&!target.IgnoreRules.Any(r=>r.Matches(path)))target.Items.Add(path);
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
        g.IconSkin=new[]{"经典","Fluent 线条","Fluent 填充"}.Contains(g.IconSkin)?g.IconSkin:"经典";
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
    public bool Ignored(string path,Group group){return Data.IgnoreRules.Any(r=>r.Matches(path))||(group!=null&&group.IgnoreRules.Any(r=>r.Matches(path)))||(group!=null&&group.FolderIgnoreRules!=null&&group.FolderIgnoreRules.Any(entry=>WorkDetection.Under(path,entry.Key)&&entry.Value.Any(r=>r.Matches(path))))||WorkDetection.AutoIgnored(Data,path)||IgnoreFiles.Ignored(Data,path,group);}
    public Group Match(string path){if(Ignored(path,null))return null;foreach(var rule in Data.Rules){if(!rule.Matches(path))continue;var g=Data.Groups.FirstOrDefault(x=>x.Id==rule.GroupId&&String.IsNullOrEmpty(x.Folder));if(g!=null)return Ignored(path,g)?null:g;}var fallback=Data.Groups.FirstOrDefault(g=>String.IsNullOrEmpty(g.Folder)&&g.Name=="收件箱")??Data.Groups.FirstOrDefault(g=>String.IsNullOrEmpty(g.Folder));return Ignored(path,fallback)?null:fallback;}
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
    public string Snapshot(){string p=Path.Combine(SnapshotRoot,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json");File.WriteAllText(p,Json.Serialize(Data));return p;}
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

