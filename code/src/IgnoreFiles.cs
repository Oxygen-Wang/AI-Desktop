using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace QiDesk {
public sealed class IgnoreFileSet {
    public string Id {get;set;}public string Root {get;set;}public string Source {get;set;}public string ScopeId {get;set;}
    public bool Enabled {get;set;}public bool Automatic {get;set;}public string Revision {get;set;}public List<string> Lines {get;set;}
    public IgnoreFileSet(){Id=Guid.NewGuid().ToString("N");Root="";Source="";ScopeId="";Enabled=true;Revision="";Lines=new List<string>();}
}
public sealed class IgnorePattern {
    public readonly bool Negated,DirectoryOnly;readonly Regex regex;readonly bool anchored;
    public IgnorePattern(string input){
        string line=TrimSpaces(input);if(line.StartsWith("!")){Negated=true;line=line.Substring(1);}if(line.Length==0)throw new InvalidDataException("反向忽略规则不能为空。");DirectoryOnly=line.EndsWith("/");if(DirectoryOnly)line=line.Substring(0,line.Length-1);anchored=line.StartsWith("/")||line.Contains("/");if(line.StartsWith("/"))line=line.Substring(1);if(line.Length==0)throw new InvalidDataException("忽略规则不能为空。");
        var pattern=new StringBuilder("^");for(int i=0;i<line.Length;i++){char c=line[i];if(c=='\\'){if(++i>=line.Length)throw new InvalidDataException("规则不能以单独的反斜杠结尾。");pattern.Append(Regex.Escape(line[i].ToString()));}else if(c=='*'){if(i+1<line.Length&&line[i+1]=='*'){bool recursive=(i==0||line[i-1]=='/')&&(i+2==line.Length||line[i+2]=='/');i++;if(!recursive){pattern.Append("[^/]*");continue;}if(i+1<line.Length&&line[i+1]=='/'){i++;pattern.Append("(?:[^/]+/)*");}else pattern.Append(".*");}else pattern.Append("[^/]*");}else if(c=='?')pattern.Append("[^/]");else if(c=='['){int end=line.IndexOf(']',i+1);if(end<0)throw new InvalidDataException("字符范围缺少 ]。");string chars=line.Substring(i+1,end-i-1);if(chars.Length==0||chars.Contains("/"))throw new InvalidDataException("字符范围无效。");if(chars[0]=='!')chars="^"+chars.Substring(1);pattern.Append("[").Append(chars.Replace("\\","\\\\")).Append("]");i=end;}else pattern.Append(Regex.Escape(c.ToString()));}pattern.Append("$");regex=new Regex(pattern.ToString(),RegexOptions.CultureInvariant|RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(30));
    }
    public bool Matches(string relative,bool directory){if(DirectoryOnly&&!directory)return false;try{return regex.IsMatch(anchored?relative:relative.Substring(relative.LastIndexOf('/')+1));}catch(RegexMatchTimeoutException){return false;}}
    public static string TrimSpaces(string text){int end=text.Length;while(end>0&&text[end-1]==' '){int slashes=0;for(int i=end-2;i>=0&&text[i]=='\\';i--)slashes++;if(slashes%2!=0)break;end--;}return text.Substring(0,end);}
}
public static class IgnoreFiles {
    public static readonly string[] Names={".gitignore",".cursorignore",".cursorindexingignore",".ignore"};
    sealed class Cached {public string Revision;public List<IgnorePattern> Patterns;}
    static readonly Dictionary<string,Cached> cache=new Dictionary<string,Cached>(StringComparer.Ordinal);
    public static IgnoreFileSet Read(string path,string scope,string root,bool automatic){
        var file=new FileInfo(path);if(!file.Exists||file.Length>1024*1024)throw new InvalidDataException("忽略文件不存在或超过 1 MB。");if((file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("忽略文件不能是文件链接。");string content=File.ReadAllText(path,Encoding.UTF8);var lines=Parse(content);return new IgnoreFileSet(){Source=Path.GetFullPath(path),Root=Path.GetFullPath(root??Path.GetDirectoryName(path)),ScopeId=scope??"",Lines=lines,Revision=file.LastWriteTimeUtc.Ticks+":"+file.Length,Automatic=automatic};
    }
    public static List<string> Parse(string content){if(content.Length>1024*1024)throw new InvalidDataException("忽略文件超过 1 MB。");var result=new List<string>();int n=0;foreach(string raw in content.Replace("\r\n","\n").Split('\n')){n++;string line=IgnorePattern.TrimSpaces(raw.TrimEnd('\r')).TrimStart('\uFEFF');if(line.Length==0||line[0]=='#')continue;if(line.Length>1024)throw new InvalidDataException("第 "+n+" 行超过 1024 字符。");try{new IgnorePattern(line);}catch(Exception ex){throw new InvalidDataException("第 "+n+" 行："+ex.Message);}result.Add(line);if(result.Count>2000)throw new InvalidDataException("每个忽略文件最多支持 2000 条规则。");}return result;}
    static List<IgnorePattern> Patterns(IgnoreFileSet set){string revision=String.Join("\n",set.Lines);lock(cache){Cached entry;if(!cache.TryGetValue(set.Id,out entry)||entry.Revision!=revision){entry=new Cached(){Revision=revision,Patterns=set.Lines.Select(x=>new IgnorePattern(x)).ToList()};if(cache.Count>128)cache.Clear();cache[set.Id]=entry;}return entry.Patterns;}}
    public static bool Matches(IgnoreFileSet set,string path){
        if(!set.Enabled||!WorkDetection.Under(path,set.Root))return false;string full=IgnoreRule.Canonical(path),root=IgnoreRule.Canonical(set.Root);if(full.Equals(root,StringComparison.OrdinalIgnoreCase))return false;string relative=full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\','/');string[] parts=relative.Split('/');var patterns=Patterns(set);string current="";for(int i=0;i<parts.Length;i++){current+=(i==0?"":"/")+parts[i];bool directory=i<parts.Length-1||Directory.Exists(path),ignored=false;foreach(var pattern in patterns)if(pattern.Matches(current,directory))ignored=!pattern.Negated;if(ignored)return true;}return false;
    }
    public static bool Ignored(State state,string path,Group group){return state.IgnoreFiles.Any(s=>(!s.Automatic||state.AutoImportIgnoreFiles)&&(String.IsNullOrEmpty(s.ScopeId)||(group!=null&&s.ScopeId==group.Id))&&Matches(s,path));}
    public static void Validate(List<IgnoreFileSet> sets){if(sets.Count>120||sets.Where(s=>s!=null&&s.Lines!=null).Sum(s=>(long)s.Lines.Sum(line=>line==null?0:line.Length))>4*1024*1024)throw new InvalidDataException("忽略文件最多 120 个，规则总量最多 4 MB。");var ids=new HashSet<string>();foreach(var set in sets){if(set==null||String.IsNullOrEmpty(set.Id)||!ids.Add(set.Id)||String.IsNullOrEmpty(set.Root)||!Path.IsPathRooted(set.Root))throw new InvalidDataException("忽略文件范围无效。");set.Lines=set.Lines??new List<string>();if(set.Lines.Count>2000)throw new InvalidDataException("忽略文件规则超过上限。");Patterns(set);}}
    public static int Import(State state,IgnoreFileSet incoming){var existing=state.IgnoreFiles.FirstOrDefault(s=>String.Equals(s.Source,incoming.Source,StringComparison.OrdinalIgnoreCase)&&s.ScopeId==incoming.ScopeId);if(existing!=null){incoming.Id=existing.Id;state.IgnoreFiles.Remove(existing);}else if(state.IgnoreFiles.Count>=120)throw new InvalidDataException("忽略文件数量已达到上限。");state.IgnoreFiles.Add(incoming);return incoming.Lines.Count;}
    // 自动导入 .gitignore / .cursorignore。除了 AI 工作目录，工作区面板打开的文件夹
    // 也要读，这样面板里看到的内容和 PathCat 一样遵循项目自己的忽略文件。
    public static List<string> Roots(State state){
        var roots=new List<string>();
        foreach(string root in state.WorkFolders)if(!String.IsNullOrEmpty(root)&&!roots.Any(p=>String.Equals(p,root,StringComparison.OrdinalIgnoreCase)))roots.Add(root);
        foreach(var tab in state.PanelTabs)if(tab!=null&&tab.IsFolder&&!roots.Any(p=>String.Equals(p,tab.Folder,StringComparison.OrdinalIgnoreCase)))roots.Add(tab.Folder);
        return roots;
    }
    public static bool Sync(State state){
        if(!state.AutoImportIgnoreFiles)return false;
        var roots=Roots(state);
        bool changed=state.IgnoreFiles.RemoveAll(s=>s.Automatic&&!roots.Any(p=>String.Equals(p,s.Root,StringComparison.OrdinalIgnoreCase)))>0;
        foreach(string root in roots.Take(60)){
            var paths=Names.Select(name=>Path.Combine(root,name)).Concat(new[]{Path.Combine(root,".git","info","exclude")});
            foreach(string path in paths){
                var existing=state.IgnoreFiles.FirstOrDefault(s=>String.IsNullOrEmpty(s.ScopeId)&&String.Equals(s.Source,path,StringComparison.OrdinalIgnoreCase));
                if(existing!=null&&!existing.Automatic)continue;
                if(!File.Exists(path)){if(existing!=null){state.IgnoreFiles.Remove(existing);changed=true;}continue;}
                try{
                    if(!WorkspaceConnections.SafeDirectory(root)||!WorkspaceConnections.SafeDirectory(Path.GetDirectoryName(path)))continue;
                    var info=new FileInfo(path);string revision=info.LastWriteTimeUtc.Ticks+":"+info.Length;
                    if(existing!=null&&existing.Revision==revision)continue;
                    var set=Read(path,"",root,true);if(existing!=null)set.Enabled=existing.Enabled;Import(state,set);changed=true;
                }catch{}
            }
        }
        return changed;
    }
}
public static class WorkspaceConnections {
    public static bool SafeDirectory(string path){if(!Path.IsPathRooted(path)||path.StartsWith("\\\\")||!Directory.Exists(path)||Directory.GetParent(path)==null)return false;string current=Path.GetFullPath(path);while(!String.IsNullOrEmpty(current)){if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;var parent=Directory.GetParent(current);if(parent==null)break;current=parent.FullName;}return true;}
    public static bool Connect(State state,string path,string source,bool automatic){path=IgnoreRule.Canonical(path);if(!SafeDirectory(path))throw new InvalidDataException("工作目录需为有效的本机文件夹，且不能是磁盘根目录或目录链接。");if(automatic&&(!state.AutoDetectWorkspaces||state.IgnoredWorkFolders.Contains(path,StringComparer.OrdinalIgnoreCase)))return false;if(!automatic)state.IgnoredWorkFolders.RemoveAll(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase));bool added=!state.WorkFolders.Any(p=>String.Equals(IgnoreRule.Canonical(p),path,StringComparison.OrdinalIgnoreCase));if(added){if(state.WorkFolders.Count>=30)throw new InvalidDataException("工作目录已达到上限。");state.WorkFolders.Add(path);}state.WorkSources[path]=String.IsNullOrEmpty(source)?"MCP":source.Substring(0,Math.Min(source.Length,80));state.WorkConnectedAt[path]=DateTime.UtcNow.ToString("o");state.CurrentWorkspace=path;IgnoreFiles.Sync(state);return added;}
    public static string RootFromUri(string text){Uri uri;if(!Uri.TryCreate(text,UriKind.Absolute,out uri)||!uri.IsFile||!String.IsNullOrEmpty(uri.Host)||uri.IsUnc)throw new InvalidDataException("项目根目录必须是本机 file URI。");return uri.LocalPath;}
}
}
