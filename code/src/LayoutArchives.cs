using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace QiDesk {
// 一份可读、可被 AI 编辑的桌面布局配置。
// 一个 desktop-layouts.json 里放多份存档；每份存档包含分区、规则、位置和模板。
public sealed class LayoutConfig {
    public string Schema {get;set;}
    public int Revision {get;set;}
    public string UpdatedAt {get;set;}
    public string Active {get;set;}
    public LayoutSettings Settings {get;set;}
    public List<LayoutArchive> Archives {get;set;}
    public LayoutConfig(){Schema="qidesk.layouts/1";Revision=1;Active="";Archives=new List<LayoutArchive>();Settings=new LayoutSettings();}
}
public sealed class LayoutSettings {public LayoutAi Ai {get;set;}public LayoutSettings(){Ai=new LayoutAi();}}
public sealed class LayoutAi {public string Endpoint {get;set;}public string Model {get;set;}}
public sealed class LayoutArchive {
    public string Id {get;set;}
    public string Name {get;set;}
    public string UpdatedAt {get;set;}
    public int Pages {get;set;}
    public LayoutFallback Fallback {get;set;}
    public LayoutTemplate Layout {get;set;}
    public List<LayoutPartition> Partitions {get;set;}
    public List<LayoutRule> Rules {get;set;}
    public LayoutArchive(){Pages=1;Partitions=new List<LayoutPartition>();Rules=new List<LayoutRule>();}
}
public sealed class LayoutFallback {public string Target {get;set;}public string Unmatched {get;set;}}
public sealed class LayoutTemplate {public string Template {get;set;}public double Gap {get;set;}public double Margin {get;set;}}
public sealed class LayoutPartition {
    public string Id {get;set;}
    public string Name {get;set;}
    public int Page {get;set;}
    public double X {get;set;}
    public double Y {get;set;}
    public double Width {get;set;}
    public double Height {get;set;}
    public string Color {get;set;}
    public LayoutSource Source {get;set;}
    public LayoutAppearance Appearance {get;set;}
    public List<string> Items {get;set;}
    public string Sort {get;set;}
    public bool Locked {get;set;}
    public LayoutPartition(){Source=new LayoutSource();Items=new List<string>();}
}
public sealed class LayoutSource {public string Type {get;set;}public List<string> Folders {get;set;}public LayoutSource(){Type="rules";Folders=new List<string>();}}
public sealed class LayoutAppearance {
    public string CornerStyle {get;set;}public string LabelColor {get;set;}public string LabelFont {get;set;}
    public string IconSkin {get;set;}public string IconColor {get;set;}public string BackgroundStyle {get;set;}
    public string BackgroundImage {get;set;}public string ImageMode {get;set;}public string ViewMode {get;set;}
    public double Opacity {get;set;}public double BackgroundOpacity {get;set;}public double TintStrength {get;set;}
    public double LabelSize {get;set;}public double IconSize {get;set;}public double BorderOpacity {get;set;}
}
public sealed class LayoutRule {
    public string Id {get;set;}public string Name {get;set;}public string Target {get;set;}
    public bool Enabled {get;set;}public LayoutMatch Match {get;set;}
    public LayoutRule(){Enabled=true;Match=new LayoutMatch();}
}
public sealed class LayoutMatch {public List<string> Extensions {get;set;}public string Kind {get;set;}public LayoutNameMatch Name {get;set;}public LayoutMatch(){Extensions=new List<string>();Kind="any";}}
public sealed class LayoutNameMatch {public string Contains {get;set;}public string Prefix {get;set;}public string Suffix {get;set;}}

public sealed class LayoutApplyResult {
    public string ArchiveId {get;set;}public string ArchiveName {get;set;}
    public int Revision {get;set;}public bool Preview {get;set;}
    public int PartitionsAdded {get;set;}public int PartitionsRemoved {get;set;}
    public int RuleCount {get;set;}
    public int Assigned {get;set;}public int Moved {get;set;}public int Unmatched {get;set;}public int Released {get;set;}
    public string Snapshot {get;set;}
    public string[] Warnings {get;set;}
    public string[] Moves {get;set;}
    public LayoutApplyResult(){Warnings=new string[0];Moves=new string[0];}
}

// 档案服务：读取/校验/保存配置，把存档映射到 State，计算整理计划并应用。
// 只改分区归属与布局，不移动、不删除真实文件。
public static class LayoutArchives {
    public static string ConfigPath(Store store){return Path.Combine(store.Root,"desktop-layouts.json");}
    static readonly string[] ArchiveTemplates={"three-columns","two-columns","focus-inbox","grid","stack-right"};

    public static LayoutConfig Load(Store store,out string error){
        error="";
        string path=ConfigPath(store);
        if(!File.Exists(path))return null;
        try{
            string json=File.ReadAllText(path,Encoding.UTF8);
            ValidateShape(json);
            var config=Store.Json.Deserialize<LayoutConfig>(json);
            Validate(config);
            return config;
        }catch(Exception ex){error=ex.Message;return null;}
    }
    public static string Raw(Store store){
        string path=ConfigPath(store);
        if(!File.Exists(path))throw new InvalidDataException("还没有布局配置文件。先用“从当前布局保存”或 --layout-export 生成一份。");
        return File.ReadAllText(path,Encoding.UTF8);
    }
    public static void Save(Store store,LayoutConfig config){
        Validate(config);
        config.Revision=Math.Max(1,config.Revision);
        config.UpdatedAt=DateTime.Now.ToString("o");
        string json=LayoutJson.Serialize(config);
        Functions.WriteAtomic(ConfigPath(store),JsonPretty.Format(json),ConfigPath(store)+".bak");
    }
    public static object SaveJson(Store store,string json){
        if(String.IsNullOrWhiteSpace(json))throw new InvalidDataException("配置 JSON 为空");
        ValidateShape(json);
        var config=Store.Json.Deserialize<LayoutConfig>(json);
        Save(store,config);
        return new {saved=true,revision=config.Revision,active=config.Active,archives=config.Archives.Count,path=ConfigPath(store)};
    }

    public static object List(Store store){
        string error;
        var config=Load(store,out error);
        object archives=config==null?(object)new object[0]:config.Archives.Select(a=>new {
            id=a.Id,name=a.Name,partitions=a.Partitions.Count,rules=a.Rules.Count,
            pages=a.Pages,updatedAt=a.UpdatedAt,active=String.Equals(a.Id,config.Active,StringComparison.OrdinalIgnoreCase)
        }).ToArray();
        return new {
            path=ConfigPath(store),
            exists=config!=null,
            error=error,
            revision=config==null?0:config.Revision,
            active=config==null?"":config.Active,
            archives=archives
        };
    }

    // 从当前运行时布局生成/更新一份存档，供 AI 读取和修改。
    public static object ExportCurrent(Store store,string archiveId,string name){
        if(String.IsNullOrWhiteSpace(archiveId))archiveId="work";
        archiveId=SafeKey(archiveId);
        string error;
        var config=Load(store,out error);if(config==null)config=new LayoutConfig();
        var archive=config.Archives.FirstOrDefault(a=>String.Equals(a.Id,archiveId,StringComparison.OrdinalIgnoreCase));
        if(archive==null){archive=new LayoutArchive(){Id=archiveId};config.Archives.Add(archive);}
        archive.Name=String.IsNullOrWhiteSpace(name)?(String.IsNullOrEmpty(archive.Name)?"当前布局":archive.Name):name.Trim();
        archive.UpdatedAt=DateTime.Now.ToString("o");
        archive.Pages=store.Data.Pages;
        archive.Partitions=new List<LayoutPartition>();
        var groupKey=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        int index=0;
        foreach(var group in store.Data.Groups){
            string key=String.IsNullOrEmpty(group.ArchiveKey)?("p"+(++index)+"-"+SafeKey(group.Name)):group.ArchiveKey;
            while(archive.Partitions.Any(p=>String.Equals(p.Id,key,StringComparison.OrdinalIgnoreCase)))key=key+"x";
            groupKey[group.Id]=key;
            var part=new LayoutPartition(){Id=key,Name=group.Name,Page=group.Page,X=group.X,Y=group.Y,Width=group.Width,Height=group.Height,Color=group.Color,Sort=group.Sort,Locked=group.Locked};
            bool folder=group.Kind=="文件夹"||!String.IsNullOrEmpty(group.Folder);
            part.Source=new LayoutSource(){Type=folder?"folder":"rules",Folders=PartitionTabs.Folders(group)};
            part.Items=new List<string>(group.Items??new List<string>());
            part.Appearance=new LayoutAppearance(){CornerStyle=group.CornerStyle,LabelColor=group.LabelColor,LabelFont=group.LabelFont,IconSkin=group.IconSkin,IconColor=group.IconColor,BackgroundStyle=group.BackgroundStyle,BackgroundImage=group.BackgroundImage,ImageMode=group.ImageMode,ViewMode=group.ViewMode,Opacity=group.Opacity,BackgroundOpacity=group.BackgroundOpacity,TintStrength=group.TintStrength,LabelSize=group.LabelSize,IconSize=group.IconSize,BorderOpacity=group.BorderOpacity};
            archive.Partitions.Add(part);
        }
        archive.Rules=store.Data.Rules.Select(rule=>new LayoutRule(){Id=SafeKey(rule.Name),Name=rule.Name,Target=groupKey.ContainsKey(rule.GroupId)?groupKey[rule.GroupId]:"",Enabled=rule.Enabled,Match=LayoutMatchFromRule(rule)}).Where(r=>r.Target.Length>0).ToList();
        var usedRuleKeys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var rule in archive.Rules){string key=rule.Id;while(!usedRuleKeys.Add(key))key=key+"x";rule.Id=key;}
        archive.Fallback=new LayoutFallback(){Unmatched="desktop"};
        archive.Layout=new LayoutTemplate(){Template="three-columns",Gap=16,Margin=40};
        config.Active=archiveId;
        config.Revision=Math.Max(2,config.Revision+1);
        config.Settings=config.Settings??new LayoutSettings();
        config.Settings.Ai=config.Settings.Ai??new LayoutAi();
        config.Settings.Ai.Endpoint=store.Data.AiEndpoint;
        config.Settings.Ai.Model=store.Data.AiModel;
        Save(store,config);
        return new {archiveId=archive.Id,name=archive.Name,partitions=archive.Partitions.Count,rules=archive.Rules.Count,path=ConfigPath(store)};
    }

    // 预览或应用一份存档。previewOnly=true 时只在克隆状态上计算，不改动运行时。
    public static LayoutApplyResult Apply(Controller controller,string archiveId,bool organize,bool includeExcluded,bool previewOnly,IEnumerable<string> candidates=null){
        var store=controller.Store;
        string error;
        var config=Load(store,out error);
        if(config==null)throw new InvalidDataException("读取布局配置失败："+error);
        LayoutArchive archive;
        ArchiveChoice(config,archiveId,out archive);
        var live=store.Data;
        var data=Store.Json.Deserialize<State>(Store.Json.Serialize(live));
        var result=new LayoutApplyResult(){ArchiveId=archive.Id,ArchiveName=archive.Name,Revision=config.Revision,Preview=previewOnly};
        var warnings=new List<string>();
        var moves=new List<string>();
        var oldGroups=(data.Groups??new List<Group>()).ToList();
        var previousOwner=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var previousItems=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach(var group in oldGroups){
            previousItems[group.Id]=new List<string>(group.Items??new List<string>());
            foreach(string path in group.Items??new List<string>())previousOwner[path]=group.Id;
        }

        var nextGroups=new List<Group>();
        var keyToGroup=new Dictionary<string,Group>(StringComparer.OrdinalIgnoreCase);
        var needsPlacement=new List<Group>();
        foreach(var part in archive.Partitions){
            Group group=oldGroups.FirstOrDefault(g=>!String.IsNullOrEmpty(g.ArchiveKey)&&String.Equals(g.ArchiveKey,part.Id,StringComparison.OrdinalIgnoreCase));
            if(group==null)group=oldGroups.FirstOrDefault(g=>String.Equals(g.Name,part.Name,StringComparison.OrdinalIgnoreCase)&&String.IsNullOrEmpty(g.HostGroupId));
            if(group==null)group=oldGroups.FirstOrDefault(g=>String.Equals(g.Name,part.Name,StringComparison.OrdinalIgnoreCase));
            bool added=group==null;
            if(group==null)group=new Group(){Id=Guid.NewGuid().ToString("N")};
            else if(group.HostGroupId!=null)group.HostGroupId="";
            group.ArchiveKey=part.Id;
            group.Name=part.Name;
            group.Page=Math.Max(0,Math.Min(archive.Pages-1,part.Page));
            group.Color=String.IsNullOrWhiteSpace(part.Color)?(String.IsNullOrWhiteSpace(group.Color)?"#77BFA3":group.Color):part.Color;
            group.Sort=String.IsNullOrWhiteSpace(part.Sort)?"名称":part.Sort;
            group.Locked=part.Locked;
            group.Rolled=false;
            group.ActiveGroupId=group.Id;
            group.HostGroupId="";
            group.Items=new List<string>();
            bool folder=part.Source!=null&&String.Equals(part.Source.Type,"folder",StringComparison.OrdinalIgnoreCase);
            if(folder){
                var folders=(part.Source.Folders??new List<string>()).Where(f=>!String.IsNullOrWhiteSpace(f)).ToList();
                group.Kind="文件夹";group.Folder=folders.FirstOrDefault()??"";group.Folders=folders;group.FolderIndex=folders.Count==0?-1:0;
            }else{
                group.Kind="普通";group.Folder="";group.Folders=new List<string>();group.FolderIndex=-1;
            }
            ApplyAppearance(group,part.Appearance);
            if(part.Width>0&&part.Height>0){group.X=part.X;group.Y=part.Y;group.Width=part.Width;group.Height=part.Height;}
            else{group.Width=0;group.Height=0;needsPlacement.Add(group);}
            nextGroups.Add(group);keyToGroup[part.Id]=group;
            if(added)result.PartitionsAdded++;
        }
        int removedCount=0;
        foreach(var old in oldGroups)if(!nextGroups.Any(g=>Object.ReferenceEquals(g,old)))removedCount++;
        result.PartitionsRemoved=removedCount;
        data.Groups=nextGroups;
        data.Rules=new List<Rule>();
        foreach(var lr in archive.Rules){
            Group target;
            if(!keyToGroup.TryGetValue(lr.Target??"",out target))continue;
            if(String.Equals(target.Kind,"文件夹",StringComparison.OrdinalIgnoreCase))continue;
            data.Rules.Add(RuleFromLayout(lr,target.Id));
        }
        result.RuleCount=data.Rules.Count;
        data.Pages=Math.Max(1,Math.Min(20,Math.Max(archive.Pages,nextGroups.Count==0?1:nextGroups.Max(g=>g.Page)+1)));
        data.Page=Math.Max(0,Math.Min(data.Pages-1,data.Page));

        // 没有写坐标的分区由模板排布；显式坐标只做屏幕外修正。
        IList<Rect> areas=controller.Manager==null?new List<Rect>{SystemParameters.WorkArea}:Shell.WorkingAreas(controller.Manager);
        PlaceTemplate(needsPlacement,archive.Layout,areas);
        int offscreen=0;
        for(int i=0;i<nextGroups.Count;i++)if(Layout.EnsureVisible(nextGroups[i],areas,i))offscreen++;
        if(offscreen>0)warnings.Add(offscreen+" 个分区在屏幕外，已移回工作区");

        if(organize){
            var pool=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(candidates==null)foreach(string path in Store.AllDesktopItems())pool.Add(path);
            else foreach(string path in candidates)if(!String.IsNullOrEmpty(path))pool.Add(path);
            foreach(string path in previousOwner.Keys)if(Store.Exists(path))pool.Add(path);
            foreach(var group in nextGroups)group.Items.Clear();
            Group fallback=null;
            if(archive.Fallback!=null&&String.Equals(archive.Fallback.Unmatched,"partition",StringComparison.OrdinalIgnoreCase))keyToGroup.TryGetValue(archive.Fallback.Target??"",out fallback);
            foreach(string path in pool){
                if(!Store.Exists(path))continue;
                if(!includeExcluded&&data.Excluded.Contains(path,StringComparer.OrdinalIgnoreCase)){result.Unmatched++;continue;}
                Group target;
                Rule matched=MatchRule(data,path,out target);
                if(target==null&&fallback!=null&&!String.Equals(fallback.Kind,"文件夹",StringComparison.OrdinalIgnoreCase))target=fallback;
                if(target==null){result.Unmatched++;continue;}
                if(!target.Items.Contains(path,StringComparer.OrdinalIgnoreCase))target.Items.Add(path);
                string oldId;
                if(!previousOwner.TryGetValue(path,out oldId)||oldId!=target.Id)result.Moved++;
                if(moves.Count<20)moves.Add(Path.GetFileName(path)+" → "+target.Name);
            }
            result.Assigned=nextGroups.Sum(g=>g.Items.Count);
            foreach(string path in previousOwner.Keys){
                if(!nextGroups.Any(g=>g.Items.Contains(path,StringComparer.OrdinalIgnoreCase)))result.Released++;
            }
            if(includeExcluded)data.Excluded.Clear();
        }else{
            // 只应用布局：保留原有归属，按名称/键映射到新分区。
            foreach(var group in oldGroups){
                string key;
                Group target=groupKeyForOld(group,nextGroups,out key);
                if(target==null)continue;
                if(String.Equals(target.Kind,"文件夹",StringComparison.OrdinalIgnoreCase))continue;
                List<string> old;
                if(!previousItems.TryGetValue(group.Id,out old))continue;
                foreach(string path in old)if(Store.Exists(path)&&!target.Items.Contains(path,StringComparer.OrdinalIgnoreCase))target.Items.Add(path);
            }
            result.Assigned=nextGroups.Sum(g=>g.Items.Count);
            result.Moved=0;
        }
        result.Warnings=warnings.ToArray();
        result.Moves=moves.ToArray();

        if(previewOnly)return result;
        string previous=Store.Json.Serialize(live);
        try{
            store.Checkpoint();
            result.Snapshot=store.Snapshot();
            data.ActiveLayoutArchiveId=archive.Id;
            data.LastAppliedLayoutRevision=config.Revision;
            data.LastAppliedLayoutAt=DateTime.Now.ToString("o");
            store.Data=data;
            Store.Validate(data);
            controller.SaveAndRefresh(true);
            return result;
        }catch(Exception ex){
            store.Data=Store.Json.Deserialize<State>(previous);
            try{store.Save();controller.Rebuild();}catch{}
            throw new InvalidDataException("应用布局失败，已回滚："+ex.Message);
        }
    }

    static Group groupKeyForOld(Group old,List<Group> next,out string key){
        key=old.ArchiveKey??"";
        if(!String.IsNullOrEmpty(key)){
            string wanted=key;
            var byKey=next.FirstOrDefault(g=>String.Equals(g.ArchiveKey,wanted,StringComparison.OrdinalIgnoreCase));
            if(byKey!=null)return byKey;
        }
        key="";
        return next.FirstOrDefault(g=>String.Equals(g.Name,old.Name,StringComparison.OrdinalIgnoreCase));
    }
    static void ArchiveChoice(LayoutConfig config,string archiveId,out LayoutArchive archive){
        string id=String.IsNullOrWhiteSpace(archiveId)?config.Active:archiveId;
        archive=config.Archives.FirstOrDefault(a=>String.Equals(a.Id,id,StringComparison.OrdinalIgnoreCase));
        if(archive==null)archive=config.Archives.FirstOrDefault(a=>String.Equals(a.Id,config.Active,StringComparison.OrdinalIgnoreCase));
        if(archive==null)throw new InvalidDataException("找不到存档："+(String.IsNullOrEmpty(id)?"（未指定）":id));
    }
    static Rule MatchRule(State data,string path,out Group target){
        target=null;
        foreach(var rule in data.Rules){
            if(!rule.Enabled||!rule.Matches(path))continue;
            var candidate=data.Groups.FirstOrDefault(g=>g.Id==rule.GroupId&&!String.Equals(g.Kind,"文件夹",StringComparison.OrdinalIgnoreCase));
            if(candidate==null)continue;
            if(PartitionIgnores.Ignored(data,path,candidate))continue;
            target=candidate;return rule;
        }
        return null;
    }
    static Rule RuleFromLayout(LayoutRule source,string groupId){
        var match=source.Match??new LayoutMatch();
        string mode="包含",contains="";
        if(match.Name!=null){
            if(!String.IsNullOrWhiteSpace(match.Name.Contains)){mode="包含";contains=match.Name.Contains.Trim();}
            else if(!String.IsNullOrWhiteSpace(match.Name.Prefix)){mode="开头";contains=match.Name.Prefix.Trim();}
            else if(!String.IsNullOrWhiteSpace(match.Name.Suffix)){mode="结尾";contains=match.Name.Suffix.Trim();}
        }
        string kind=match.Kind=="folder"?"文件夹":match.Kind=="file"?"文件":"全部";
        return new Rule(){Name=source.Name,Extensions=String.Join(",",(match.Extensions??new List<string>()).Select(SafeExtension).Where(e=>e.Length>0)),Contains=contains,NameMode=mode,Kind=kind,GroupId=groupId,Enabled=source.Enabled};
    }
    static LayoutMatch LayoutMatchFromRule(Rule rule){
        var match=new LayoutMatch(){Kind=rule.Kind=="文件"?"file":rule.Kind=="文件夹"?"folder":"any",Extensions=(rule.Extensions??"").Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries).Select(SafeExtension).Where(e=>e.Length>0).ToList()};
        if(!String.IsNullOrWhiteSpace(rule.Contains))match.Name=new LayoutNameMatch(){Contains=rule.NameMode=="开头"?null:rule.NameMode=="结尾"?null:rule.Contains,Prefix=rule.NameMode=="开头"?rule.Contains:null,Suffix=rule.NameMode=="结尾"?rule.Contains:null};
        return match;
    }
    static string SafeExtension(string value){return (value??"").Trim().TrimStart('.').ToLowerInvariant();}
    static string SafeKey(string value){
        string key=new string((value??"").ToLowerInvariant().Select(c=>(c>='a'&&c<='z')||(c>='0'&&c<='9')||c=='-'||c=='_'?c:'-').ToArray()).Trim('-');
        if(key.Length==0)key="p";
        if(key.Length>40)key=key.Substring(0,40).TrimEnd('-');
        return key;
    }

    static void ApplyAppearance(Group group,LayoutAppearance a){
        if(a==null)return;
        if(!String.IsNullOrEmpty(a.CornerStyle))group.CornerStyle=a.CornerStyle=="方角"?"方角":"圆角";
        if(a.Opacity>0)group.Opacity=Math.Max(.3,Math.Min(1,a.Opacity));
        if(a.BackgroundOpacity>0)group.BackgroundOpacity=Math.Max(0,Math.Min(1,a.BackgroundOpacity));
        if(a.TintStrength>0)group.TintStrength=Math.Max(0,Math.Min(1,a.TintStrength));
        if(a.BorderOpacity>0)group.BorderOpacity=Math.Max(0,Math.Min(1,a.BorderOpacity));
        if(!String.IsNullOrEmpty(a.LabelColor))group.LabelColor=a.LabelColor;
        if(a.LabelSize>0)group.LabelSize=Math.Max(10,Math.Min(24,a.LabelSize));
        if(!String.IsNullOrEmpty(a.LabelFont))group.LabelFont=a.LabelFont;
        if(a.IconSize>0)group.IconSize=Math.Max(20,Math.Min(96,a.IconSize));
        if(!String.IsNullOrEmpty(a.IconSkin)&&AppIconSkins.Choices.Contains(a.IconSkin))group.IconSkin=a.IconSkin;
        if(!String.IsNullOrEmpty(a.IconColor))group.IconColor=a.IconColor;
        if(!String.IsNullOrEmpty(a.BackgroundStyle)&&Store.BackgroundStyles.Contains(a.BackgroundStyle))group.BackgroundStyle=a.BackgroundStyle;
        if(!String.IsNullOrEmpty(a.BackgroundImage))group.BackgroundImage=a.BackgroundImage;
        if(!String.IsNullOrEmpty(a.ImageMode)&&Store.ImageModes.Contains(a.ImageMode))group.ImageMode=a.ImageMode;
        if(!String.IsNullOrEmpty(a.ViewMode)&&new[]{"图标","列表","详细信息"}.Contains(a.ViewMode))group.ViewMode=a.ViewMode;
    }

    // 内置布局模板：只为没有写坐标的分区计算位置。写了 x/y/width/height 的分区保持原值。
    static void PlaceTemplate(List<Group> groups,LayoutTemplate layout,IList<Rect> areas){
        if(groups==null||groups.Count==0)return;
        if(areas==null||areas.Count==0)areas=new List<Rect>{SystemParameters.WorkArea};
        Rect area=areas.OrderByDescending(r=>r.Width*r.Height).First();
        double margin=layout!=null&&layout.Margin>0?Math.Min(200,layout.Margin):40;
        double gap=layout!=null&&layout.Gap>0?Math.Min(80,layout.Gap):16;
        string template=layout==null||String.IsNullOrEmpty(layout.Template)?"three-columns":layout.Template;
        if(template=="focus-inbox"&&groups.Count>1){
            Group first=groups[0];
            double leftW=Math.Min(900,Math.Max(420,area.Width*.55));
            first.X=area.Left+margin;first.Y=area.Top+margin;first.Width=leftW;first.Height=Math.Min(640,area.Height-2*margin);
            double rightW=Math.Min(320,Math.Max(220,area.Width*.26));
            int rest=groups.Count-1;
            double each=Math.Max(120,Math.Min(300,(area.Height-2*margin-(rest-1)*gap)/Math.Max(1,rest)));
            for(int i=0;i<rest;i++){
                var g=groups[i+1];g.Width=rightW;g.Height=each;g.X=area.Right-margin-rightW;g.Y=area.Top+margin+i*(each+gap);
            }
            return;
        }
        int columns=template=="two-columns"?2:template=="grid"?4:template=="stack-right"?1:3;
        double baseW=template=="two-columns"?520:template=="grid"?300:template=="stack-right"?360:360;
        double baseH=template=="two-columns"?320:template=="grid"?260:template=="stack-right"?260:300;
        int cols=Math.Max(1,Math.Min(columns,groups.Count));
        int rows=(int)Math.Ceiling(groups.Count/(double)cols);
        while(cols<groups.Count&&(area.Height-2*margin-(rows-1)*gap)/Math.Max(1,rows)<160){cols++;rows=(int)Math.Ceiling(groups.Count/(double)cols);}
        double cellW=Math.Min(baseW,(area.Width-2*margin-(cols-1)*gap)/cols);
        double cellH=Math.Min(baseH,(area.Height-2*margin-(rows-1)*gap)/rows);
        cellW=Math.Max(220,cellW);cellH=Math.Max(100,cellH);
        for(int i=0;i<groups.Count;i++){
            int row=i/cols,col=i%cols;
            var g=groups[i];g.Width=cellW;g.Height=cellH;
            g.X=template=="stack-right"?area.Right-margin-cellW:area.Left+margin+col*(cellW+gap);
            g.Y=area.Top+margin+row*(cellH+gap);
        }
    }

    // ---- 校验 ----
    static void Validate(LayoutConfig config){
        if(config==null)throw new InvalidDataException("配置为空");
        if(config.Schema!="qidesk.layouts/1")throw new InvalidDataException("schema 必须是 qidesk.layouts/1");
        if(config.Revision<1)throw new InvalidDataException("revision 必须是正整数");
        config.Archives=config.Archives??new List<LayoutArchive>();
        if(config.Archives.Count==0)throw new InvalidDataException("archives 不能为空");
        if(config.Archives.Count>50)throw new InvalidDataException("存档最多 50 份");
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var archive in config.Archives){
            if(archive==null)throw new InvalidDataException("存档不能为空");
            if(!Regex.IsMatch(archive.Id??"","^[a-z0-9_-]{1,40}$"))throw new InvalidDataException("存档 id 只能是小写字母、数字、- 和 _，长度 1–40");
            if(!ids.Add(archive.Id))throw new InvalidDataException("存档 id 重复："+archive.Id);
            if(String.IsNullOrWhiteSpace(archive.Name)||archive.Name.Length>80)throw new InvalidDataException("存档 name 必须是 1–80 字");
            archive.Pages=Math.Max(1,Math.Min(20,archive.Pages));
            archive.Partitions=archive.Partitions??new List<LayoutPartition>();
            archive.Rules=archive.Rules??new List<LayoutRule>();
            if(archive.Partitions.Count==0)throw new InvalidDataException("存档 "+archive.Id+" 至少需要一个分区");
            if(archive.Partitions.Count>200)throw new InvalidDataException("存档 "+archive.Id+" 的分区超过 200 个");
            if(archive.Rules.Count>100)throw new InvalidDataException("存档 "+archive.Id+" 的规则超过 100 条");
            var partIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var part in archive.Partitions){
                if(part==null)throw new InvalidDataException("分区不能为空");
                if(!Regex.IsMatch(part.Id??"","^[a-z0-9_-]{1,40}$"))throw new InvalidDataException("分区 id 无效："+part.Id);
                if(!partIds.Add(part.Id))throw new InvalidDataException("分区 id 重复："+part.Id);
                if(String.IsNullOrWhiteSpace(part.Name)||part.Name.Length>80)throw new InvalidDataException("分区 "+part.Id+" 的 name 必须是 1–80 字");
                if(part.Page<0||part.Page>=archive.Pages)throw new InvalidDataException("分区 "+part.Id+" 的 page 超出范围");
                if(!String.IsNullOrWhiteSpace(part.Color))ValidColor(part.Color,"分区 "+part.Id+" 的 color");
                string source=part.Source==null?"":(part.Source.Type??"").ToLowerInvariant();
                if(source!="rules"&&source!="folder"&&source!="manual")throw new InvalidDataException("分区 "+part.Id+" 的 source.type 必须是 rules、folder 或 manual");
                if(source=="folder"){
                    part.Source.Folders=(part.Source.Folders??new List<string>()).Where(f=>!String.IsNullOrWhiteSpace(f)).ToList();
                    if(part.Source.Folders.Count==0)throw new InvalidDataException("文件夹分区 "+part.Id+" 需要 source.folders");
                    foreach(string folder in part.Source.Folders)if(!Directory.Exists(folder))throw new InvalidDataException("文件夹不存在："+folder);
                }
                bool anyGeometry=part.Width!=0||part.Height!=0;
                if(anyGeometry){
                    if(part.Width<1||part.Height<1)throw new InvalidDataException("分区 "+part.Id+" 的 width/height 必须同时大于 0");
                    if(part.Width<220||part.Width>2000||part.Height<100||part.Height>2000)throw new InvalidDataException("分区 "+part.Id+" 的尺寸超出范围（220–2000 × 100–2000）");
                    if(part.X<-20000||part.X>20000||part.Y<-20000||part.Y>20000)throw new InvalidDataException("分区 "+part.Id+" 的坐标超出范围");
                }
            }
            var ruleIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var rule in archive.Rules){
                if(rule==null)throw new InvalidDataException("规则不能为空");
                if(String.IsNullOrWhiteSpace(rule.Id)||rule.Id.Length>80)throw new InvalidDataException("规则 id 无效");
                if(!ruleIds.Add(rule.Id))throw new InvalidDataException("规则 id 重复："+rule.Id);
                if(String.IsNullOrWhiteSpace(rule.Name)||rule.Name.Length>80)throw new InvalidDataException("规则 name 必须是 1–80 字");
                if(String.IsNullOrWhiteSpace(rule.Target)||!partIds.Contains(rule.Target))throw new InvalidDataException("规则 "+rule.Id+" 的 target 不存在："+rule.Target);
                var target=archive.Partitions.First(p=>String.Equals(p.Id,rule.Target,StringComparison.OrdinalIgnoreCase));
                if(String.Equals(target.Source.Type,"folder",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("规则 "+rule.Id+" 不能指向文件夹分区");
                if(rule.Match==null)throw new InvalidDataException("规则 "+rule.Id+" 缺少 match");
                var exts=rule.Match.Extensions??new List<string>();
                if(exts.Count>64)throw new InvalidDataException("规则 "+rule.Id+" 的扩展名太多");
                foreach(string extension in exts){
                    string clean=SafeExtension(extension);
                    if(clean.Length==0||clean.Length>20||clean.IndexOfAny(new[]{'\\','/',':','*','?','\"','<','>','|'})>=0)throw new InvalidDataException("规则 "+rule.Id+" 的扩展名无效："+extension);
                }
                if(rule.Match.Name!=null){
                    int names=new[]{rule.Match.Name.Contains,rule.Match.Name.Prefix,rule.Match.Name.Suffix}.Count(v=>!String.IsNullOrWhiteSpace(v));
                    if(names>1)throw new InvalidDataException("规则 "+rule.Id+" 的 name 只能写 contains、prefix、suffix 之一");
                }
            }
            if(archive.Fallback!=null&&String.Equals(archive.Fallback.Unmatched,"partition",StringComparison.OrdinalIgnoreCase)){
                if(String.IsNullOrWhiteSpace(archive.Fallback.Target)||!partIds.Contains(archive.Fallback.Target))throw new InvalidDataException("fallback.target 不存在："+archive.Fallback.Target);
            }else if(archive.Fallback!=null&&!String.IsNullOrWhiteSpace(archive.Fallback.Unmatched)&&!String.Equals(archive.Fallback.Unmatched,"desktop",StringComparison.OrdinalIgnoreCase)){
                throw new InvalidDataException("fallback.unmatched 只能是 desktop 或 partition");
            }
            if(archive.Layout!=null&&!String.IsNullOrWhiteSpace(archive.Layout.Template)&&!ArchiveTemplates.Contains(archive.Layout.Template))throw new InvalidDataException("未知的布局模板："+archive.Layout.Template);
        }
        if(String.IsNullOrWhiteSpace(config.Active)||!config.Archives.Any(a=>String.Equals(a.Id,config.Active,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("active 必须指向已存在的存档 id");
    }
    static void ValidColor(string color,string path){try{ColorConverter.ConvertFromString(color);}catch{throw new InvalidDataException(path+" 不是有效颜色："+color);}}

    // 未知字段会被 JavaScriptSerializer 静默忽略；这里先按结构检查，避免 AI/AI 客户端写错字段却“看起来应用成功”。
    static void ValidateShape(string json){
        var root=Store.Json.Deserialize<Dictionary<string,object>>(json);
        if(root==null)throw new InvalidDataException("配置必须是 JSON 对象");
        Shape(root,"root",new[]{"schema","revision","updatedAt","active","settings","archives"},new[]{"schema","revision","archives"});
        object settings;
        if(root.TryGetValue("settings",out settings)&&settings!=null){
            var map=settings as Dictionary<string,object>;if(map==null)throw new InvalidDataException("settings 必须是对象");
            Shape(map,"settings",new[]{"ai"},new string[0]);
            object ai;if(map.TryGetValue("ai",out ai)&&ai!=null){var aim=ai as Dictionary<string,object>;if(aim==null)throw new InvalidDataException("settings.ai 必须是对象");Shape(aim,"settings.ai",new[]{"endpoint","model"},new string[0]);}
        }
        var archives=root["archives"] as ArrayList;
        if(archives==null)throw new InvalidDataException("archives 必须是数组");
        for(int i=0;i<archives.Count;i++){
            string path="archives["+i+"]";
            var archive=archives[i] as Dictionary<string,object>;if(archive==null)throw new InvalidDataException(path+" 必须是对象");
            Shape(archive,path,new[]{"id","name","updatedAt","pages","fallback","layout","partitions","rules"},new[]{"id","name","partitions","rules"});
            object fallback;if(archive.TryGetValue("fallback",out fallback)&&fallback!=null)Shape(fallback as Dictionary<string,object>,path+".fallback",new[]{"target","unmatched"},new string[0]);
            object layout;if(archive.TryGetValue("layout",out layout)&&layout!=null)Shape(layout as Dictionary<string,object>,path+".layout",new[]{"template","gap","margin"},new string[0]);
            var parts=archive["partitions"] as ArrayList;if(parts==null)throw new InvalidDataException(path+".partitions 必须是数组");
            for(int p=0;p<parts.Count;p++){
                string pp=path+".partitions["+p+"]";
                var part=parts[p] as Dictionary<string,object>;if(part==null)throw new InvalidDataException(pp+" 必须是对象");
                Shape(part,pp,new[]{"id","name","page","x","y","width","height","color","source","appearance","items","sort","locked"},new[]{"id","name","source"});
                object source;if(!part.TryGetValue("source",out source)||source==null)throw new InvalidDataException(pp+".source 缺失");
                var sourceMap=source as Dictionary<string,object>;if(sourceMap==null)throw new InvalidDataException(pp+".source 必须是对象");
                Shape(sourceMap,pp+".source",new[]{"type","folders"},new[]{"type"});
                object appearance;if(part.TryGetValue("appearance",out appearance)&&appearance!=null)Shape(appearance as Dictionary<string,object>,pp+".appearance",new[]{"cornerStyle","labelColor","labelFont","iconSkin","iconColor","backgroundStyle","backgroundImage","imageMode","viewMode","opacity","backgroundOpacity","tintStrength","labelSize","iconSize","borderOpacity"},new string[0]);
            }
            var rules=archive["rules"] as ArrayList;if(rules==null)throw new InvalidDataException(path+".rules 必须是数组");
            for(int r=0;r<rules.Count;r++){
                string rp=path+".rules["+r+"]";
                var rule=rules[r] as Dictionary<string,object>;if(rule==null)throw new InvalidDataException(rp+" 必须是对象");
                Shape(rule,rp,new[]{"id","name","enabled","target","match"},new[]{"id","name","target","match"});
                object match;if(!rule.TryGetValue("match",out match)||match==null)throw new InvalidDataException(rp+".match 缺失");
                var matchMap=match as Dictionary<string,object>;if(matchMap==null)throw new InvalidDataException(rp+".match 必须是对象");
                Shape(matchMap,rp+".match",new[]{"extensions","kind","name"},new string[0]);
                object name;if(matchMap.TryGetValue("name",out name)&&name!=null){var nameMap=name as Dictionary<string,object>;if(nameMap==null)throw new InvalidDataException(rp+".match.name 必须是对象");Shape(nameMap,rp+".match.name",new[]{"contains","prefix","suffix"},new string[0]);}
            }
        }
    }
    static void Shape(Dictionary<string,object> map,string path,string[] allowed,string[] required){
        if(map==null)throw new InvalidDataException(path+" 必须是对象");
        foreach(var key in map.Keys)if(!allowed.Contains(key))throw new InvalidDataException("未知字段："+path+"."+key);
        foreach(string key in required)if(!map.ContainsKey(key)||map[key]==null)throw new InvalidDataException("缺少字段："+path+"."+key);
    }
}

// 外部 schema 使用小写字段名，避免把 State/Group 的内部 PascalCase 暴露给 AI。
public static class LayoutJson {
    public static string Serialize(LayoutConfig config){
        var root=new Dictionary<string,object>{
            {"schema",config.Schema},
            {"revision",config.Revision},
            {"updatedAt",config.UpdatedAt??""},
            {"active",config.Active??""},
            {"settings",SettingsMap(config.Settings)},
            {"archives",Archives(config.Archives)}
        };
        return Store.Json.Serialize(root);
    }
    static Dictionary<string,object> SettingsMap(LayoutSettings settings){
        var ai=settings==null?null:settings.Ai;
        return new Dictionary<string,object>{{"ai",new Dictionary<string,object>{{"endpoint",ai==null?(object)"" : (ai.Endpoint??"")},{"model",ai==null?(object)"" : (ai.Model??"")}}}};
    }
    static List<object> Archives(List<LayoutArchive> archives){
        var list=new List<object>();
        if(archives==null)return list;
        foreach(var archive in archives)list.Add(ArchiveMap(archive));
        return list;
    }
    static Dictionary<string,object> ArchiveMap(LayoutArchive archive){
        var map=new Dictionary<string,object>{
            {"id",archive.Id??""},{"name",archive.Name??""},{"updatedAt",archive.UpdatedAt??""},
            {"pages",archive.Pages},{"partitions",Partitions(archive.Partitions)},{"rules",Rules(archive.Rules)}
        };
        if(archive.Fallback!=null)map["fallback"]=new Dictionary<string,object>{{"target",archive.Fallback.Target??""},{"unmatched",String.IsNullOrEmpty(archive.Fallback.Unmatched)?"desktop":archive.Fallback.Unmatched}};
        if(archive.Layout!=null)map["layout"]=new Dictionary<string,object>{{"template",archive.Layout.Template??"three-columns"},{"gap",archive.Layout.Gap>0?archive.Layout.Gap:16},{"margin",archive.Layout.Margin>0?archive.Layout.Margin:40}};
        return map;
    }
    static List<object> Partitions(List<LayoutPartition> parts){
        var list=new List<object>();
        if(parts==null)return list;
        foreach(var part in parts){
            var map=new Dictionary<string,object>{
                {"id",part.Id??""},{"name",part.Name??""},{"page",part.Page},
                {"x",part.X},{"y",part.Y},{"width",part.Width},{"height",part.Height},
                {"color",part.Color??""},
                {"source",new Dictionary<string,object>{{"type",part.Source==null?"rules":(part.Source.Type??"rules")},{"folders",part.Source==null||part.Source.Folders==null?new List<string>():part.Source.Folders}}},
                {"locked",part.Locked}
            };
            if(part.Appearance!=null)map["appearance"]=AppearanceMap(part.Appearance);
            if(part.Items!=null&&part.Items.Count>0)map["items"]=part.Items;
            if(!String.IsNullOrEmpty(part.Sort))map["sort"]=part.Sort;
            list.Add(map);
        }
        return list;
    }
    static Dictionary<string,object> AppearanceMap(LayoutAppearance a){
        return new Dictionary<string,object>{
            {"cornerStyle",a.CornerStyle??""},{"labelColor",a.LabelColor??""},{"labelFont",a.LabelFont??""},
            {"iconSkin",a.IconSkin??""},{"iconColor",a.IconColor??""},{"backgroundStyle",a.BackgroundStyle??""},
            {"backgroundImage",a.BackgroundImage??""},{"imageMode",a.ImageMode??""},{"viewMode",a.ViewMode??""},
            {"opacity",a.Opacity},{"backgroundOpacity",a.BackgroundOpacity},{"tintStrength",a.TintStrength},
            {"labelSize",a.LabelSize},{"iconSize",a.IconSize},{"borderOpacity",a.BorderOpacity}
        };
    }
    static List<object> Rules(List<LayoutRule> rules){
        var list=new List<object>();
        if(rules==null)return list;
        foreach(var rule in rules){
            var match=new Dictionary<string,object>{{"extensions",rule.Match==null||rule.Match.Extensions==null?new List<string>():rule.Match.Extensions},{"kind",rule.Match==null||String.IsNullOrEmpty(rule.Match.Kind)?"any":rule.Match.Kind}};
            if(rule.Match!=null&&rule.Match.Name!=null){
                var name=new Dictionary<string,object>();
                if(!String.IsNullOrEmpty(rule.Match.Name.Contains))name["contains"]=rule.Match.Name.Contains;
                if(!String.IsNullOrEmpty(rule.Match.Name.Prefix))name["prefix"]=rule.Match.Name.Prefix;
                if(!String.IsNullOrEmpty(rule.Match.Name.Suffix))name["suffix"]=rule.Match.Name.Suffix;
                if(name.Count>0)match["name"]=name;
            }
            list.Add(new Dictionary<string,object>{{"id",rule.Id??""},{"name",rule.Name??""},{"enabled",rule.Enabled},{"target",rule.Target??""},{"match",match}});
        }
        return list;
    }
}

// 紧凑 JSON 的缩进版本，便于 AI/用户直接阅读和 diff。
public static class JsonPretty {
    public static string Format(string json){
        if(String.IsNullOrEmpty(json))return json??"";
        var output=new StringBuilder(json.Length+256);int indent=0;bool inString=false,escape=false;
        for(int i=0;i<json.Length;i++){
            char c=json[i];
            if(inString){
                output.Append(c);
                if(escape)escape=false;else if(c=='\\')escape=true;else if(c=='"')inString=false;
                continue;
            }
            if(c=='"'){inString=true;output.Append(c);continue;}
            if(c=='{'||c=='['){output.Append(c);indent++;NewLine(output,indent);continue;}
            if(c=='}'||c==']'){indent=Math.Max(0,indent-1);NewLine(output,indent);output.Append(c);continue;}
            if(c==','){output.Append(c);NewLine(output,indent);continue;}
            if(c==':'){output.Append(": ");continue;}
            if(Char.IsWhiteSpace(c))continue;
            output.Append(c);
        }
        return output.ToString();
    }
    static void NewLine(StringBuilder output,int indent){output.Append("\r\n");output.Append(' ',indent*2);}
}
}
