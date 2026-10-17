using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QiDesk {
public sealed class IgnoreRule {
    public string Id {get;set;}
    public string Pattern {get;set;}
    public string Kind {get;set;}
    public bool Enabled {get;set;}
    public bool IncludeChildren {get;set;}
    public IgnoreRule(){Id=Guid.NewGuid().ToString("N");Pattern="";Kind="名称";Enabled=true;}
    public bool Matches(string path){
        if(!Enabled||String.IsNullOrWhiteSpace(Pattern))return false;
        if(Kind=="路径")return String.Equals(Canonical(path),Canonical(Pattern),StringComparison.OrdinalIgnoreCase)||(IncludeChildren&&WorkDetection.Under(path,Pattern));
        string pattern=Regex.Escape(Pattern.Trim()).Replace("\\*",".*").Replace("\\?",".");
        return Regex.IsMatch(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),"^"+pattern+"$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    }
    public static string Canonical(string path){try{return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}catch{return "";}}
    public static void Validate(List<IgnoreRule> rules){if(rules.Count>500)throw new InvalidDataException("每个范围最多支持 500 条忽略规则。");var ids=new HashSet<string>();foreach(var r in rules){if(r==null||String.IsNullOrWhiteSpace(r.Pattern)||r.Pattern.Length>1024)throw new InvalidDataException("忽略规则不能为空且最多 1024 字符。");if(r.Kind!="名称"&&r.Kind!="路径"&&r.Kind!="Git")throw new InvalidDataException("忽略规则类型无效。");if(r.Kind=="Git")new IgnorePattern(r.Pattern);if(r.Kind=="名称"&&(r.Pattern.Contains("/")||r.Pattern.Contains("\\")))throw new InvalidDataException("名称规则仅匹配文件名；路径请使用指定项目。");if(r.Kind=="路径"&&(String.IsNullOrEmpty(Canonical(r.Pattern))||!Path.IsPathRooted(r.Pattern)))throw new InvalidDataException("指定项目必须使用有效绝对路径。");if(String.IsNullOrEmpty(r.Id)||!ids.Add(r.Id))throw new InvalidDataException("忽略规则标识重复。");}}
}
public static class IgnoreRuleInput {
    public static string[] Split(string input){
        return (input??"").Split(new[]{',','，',';','；',' ','\t','\r','\n','　'},StringSplitOptions.RemoveEmptyEntries)
            .Select(value=>value.Trim()).Where(value=>value.Length>0).ToArray();
    }
}
public static class FilePresentation {
    public static string DisplayName(string path,bool showExtension){string name=Path.GetFileName(path);if(showExtension||Directory.Exists(path)||String.IsNullOrEmpty(name)||name[0]=='.'&&name.IndexOf('.',1)<0)return name;string stem=Path.GetFileNameWithoutExtension(name);return String.IsNullOrEmpty(stem)?name:stem;}
    public static bool IsApplication(string path){string ext=Path.GetExtension(path);return ext.Equals(".exe",StringComparison.OrdinalIgnoreCase)||ext.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||ext.Equals(".url",StringComparison.OrdinalIgnoreCase)||ext.Equals(".appref-ms",StringComparison.OrdinalIgnoreCase);}
    static readonly string[] WorkKinds={"doc","docx","xls","xlsx","ppt","pptx","pdf","txt","md","rtf","csv","png","jpg","jpeg","gif","svg","webp","bmp","tif","tiff","ico","mp4","mov","avi","mkv","webm","mp3","wav","flac","m4a"};
    static readonly string[] PictureKinds={"png","jpg","jpeg","gif","svg","webp","bmp","tif","tiff","ico"};
    static readonly string[] AppKinds={"lnk","url","exe","msi","bat","cmd","appref-ms"};
    public static bool MatchesKind(string path,string kind){if(Directory.Exists(path))return false;string ext=Path.GetExtension(path).TrimStart('.').ToLowerInvariant();return kind=="应用"?AppKinds.Contains(ext):kind=="图片"?PictureKinds.Contains(ext):kind=="文档"?WorkKinds.Contains(ext)&&!PictureKinds.Contains(ext):kind=="工作"?WorkKinds.Contains(ext):false;}
    public static long Bytes(string path){try{return File.Exists(path)?new FileInfo(path).Length:0;}catch{return 0;}}
    public static IEnumerable<string> Sorted(IEnumerable<string> paths,Group g){Func<string,IComparable> key=p=>g.Sort=="修改时间"?(IComparable)File.GetLastWriteTime(p):g.Sort=="大小"?(IComparable)Bytes(p):g.Sort=="类型"?(IComparable)Type(p):(IComparable)Path.GetFileName(p);return (g.SortDescending?paths.OrderByDescending(key):paths.OrderBy(key)).ThenBy(Path.GetFileName,StringComparer.CurrentCultureIgnoreCase);}
    public static bool Zoom(Group group,int direction){
        string before=group.ViewMode+group.IconSize;
        if(direction>0){if(group.ViewMode=="详细信息")group.ViewMode="列表";else if(group.ViewMode=="列表"){group.ViewMode="图标";group.IconSize=20;}else group.IconSize=Math.Min(96,group.IconSize+8);}
        else if(direction<0){if(group.ViewMode=="图标"&&group.IconSize>20)group.IconSize=Math.Max(20,group.IconSize-8);else if(group.ViewMode=="图标")group.ViewMode="列表";else if(group.ViewMode=="列表")group.ViewMode="详细信息";}
        return before!=group.ViewMode+group.IconSize;
    }
    public static string FolderName(string path){string name=Path.GetFileName((path??"").TrimEnd(Path.DirectorySeparatorChar));return String.IsNullOrEmpty(name)?"文件夹":name;}
    public static string Size(string path){if(Directory.Exists(path))return "文件夹";try{double n=new FileInfo(path).Length;string[] units={"B","KB","MB","GB","TB"};int i=0;while(n>=1024&&i<4){n/=1024;i++;}return n.ToString(i==0?"0":"0.#")+" "+units[i];}catch{return "不可用";}}
    public static string Modified(string path){try{return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm");}catch{return "不可用";}}
    public static string Type(string path){return Directory.Exists(path)?"文件夹":String.IsNullOrEmpty(Path.GetExtension(path))?"文件":Path.GetExtension(path).TrimStart('.').ToUpperInvariant()+" 文件";}
    public static string Detail(string path){return Size(path)+" · "+Type(path)+" · "+Modified(path);}
    public static string OpenFolder(Group group,string current){return !String.IsNullOrEmpty(current)&&Directory.Exists(current)?current:String.IsNullOrEmpty(group.Folder)?Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory):group.Folder;}
}
public sealed class WorkCandidate {
    public string Path {get;set;}
    public string Name {get;set;}
    public string Workspace {get;set;}
    public string Reason {get;set;}
}
public static class WorkDetection {
    // Names are hints only. They do not prove which program produced a file.
    static readonly string[] markers={"AGENTS.md","CLAUDE.md",".codex",".claude",".cursor",".git",".cursorignore",".gitignore"};
    public static string[] Markers(string folder){return markers.Where(m=>Store.Exists(Path.Combine(folder,m))).ToArray();}
    public static bool Under(string path,string root){string p=IgnoreRule.Canonical(path),r=IgnoreRule.Canonical(root);return p.Length>0&&r.Length>0&&(String.Equals(p,r,StringComparison.OrdinalIgnoreCase)||p.StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));}
    public static bool Generated(string name){string n=name.ToLowerInvariant();return n.StartsWith("chatgpt-")||n.StartsWith("chatgpt_")||n.StartsWith("chatgpt image")||n.StartsWith("codex-")||n.StartsWith("codex_")||n.StartsWith("claude-")||n.StartsWith("claude_")||n.StartsWith("generated-")||n.StartsWith("generated_");}
    public static bool AutoIgnored(State state,string path){if(!state.WorkAutoIgnore)return false;foreach(string root in state.WorkFolders){if(!Under(path,root)||String.Equals(IgnoreRule.Canonical(root),IgnoreRule.Canonical(path),StringComparison.OrdinalIgnoreCase))continue;string relative=IgnoreRule.Canonical(path).Substring(IgnoreRule.Canonical(root).Length).TrimStart(Path.DirectorySeparatorChar);foreach(string part in relative.Split(Path.DirectorySeparatorChar)){if(Generated(part)||(state.WorkIgnoreCaches&&Reason(part).Length>0))return true;}}return false;}
    public static string Reason(string name){string n=name.ToLowerInvariant();if(n.StartsWith("chatgpt-")||n.StartsWith("chatgpt_")||n.StartsWith("chatgpt image"))return "名称包含 ChatGPT 产物前缀";if(n.StartsWith("codex-")||n.StartsWith("codex_"))return "名称包含 Codex 产物前缀";if(n.StartsWith("claude-")||n.StartsWith("claude_"))return "名称包含 Claude 产物前缀";if(n.StartsWith("generated-")||n.StartsWith("generated_"))return "名称包含 generated 产物前缀";if(n.EndsWith(".tmp")||n.EndsWith(".temp"))return "临时文件";if(n=="__pycache__"||n==".pytest_cache"||n=="node_modules")return "缓存或依赖目录";return "";}
    public static List<WorkCandidate> Scan(State state){var result=new List<WorkCandidate>();foreach(string root in state.WorkFolders.Take(30)){int visited=0;var pending=new Queue<Tuple<string,int>>();if(Directory.Exists(root))pending.Enqueue(Tuple.Create(root,0));while(pending.Count>0&&visited<3000&&result.Count<300){var entry=pending.Dequeue();string[] files;try{files=Directory.GetFileSystemEntries(entry.Item1);}catch{continue;}foreach(string path in files){if(++visited>3000)break;try{if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;}catch{continue;}string reason=Reason(System.IO.Path.GetFileName(path));if(reason.Length>0&&!state.IgnoreRules.Any(r=>r.Matches(path))&&!IgnoreFiles.Ignored(state,path,null))result.Add(new WorkCandidate(){Path=path,Name=System.IO.Path.GetFileName(path),Workspace=FilePresentation.FolderName(root),Reason=reason});if(result.Count>=300)break;if(entry.Item2<2&&Directory.Exists(path)&&reason.Length==0&&!IgnoreFiles.Ignored(state,path,null)&&!state.IgnoreRules.Any(r=>r.Matches(path))&&!System.IO.Path.GetFileName(path).StartsWith("."))pending.Enqueue(Tuple.Create(path,entry.Item2+1));}}}return result;}
}
}
