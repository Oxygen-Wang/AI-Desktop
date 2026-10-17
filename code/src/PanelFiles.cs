using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QiDesk {
// 工作区面板用的文件操作辅助：只做按名称的递归搜索、唯一命名与忽略规则登记，
// 不涉及任何向量或语义模型。
public static class PanelFiles {
    public const int SearchLimit=2000;
    public const int SearchBudget=400000;
    // 在当前文件夹及其子目录里按名称匹配。遵循栖桌现有忽略规则与 .gitignore/.cursorignore。
    public static string[] Search(State state,string root,string query,List<IgnoreRule> localRules=null){
        var found=new List<string>();
        if(String.IsNullOrEmpty(query)||String.IsNullOrEmpty(root)||!Directory.Exists(root))return found.ToArray();
        var pending=new Queue<string>();pending.Enqueue(Path.GetFullPath(root));
        int visited=0;
        while(pending.Count>0&&found.Count<SearchLimit&&visited<SearchBudget){
            string current=pending.Dequeue();string[] entries;
            try{entries=Directory.GetFileSystemEntries(current);}catch{continue;}
            foreach(string path in entries){
                if(++visited>SearchBudget)break;
                try{if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;}catch{continue;}
                string name=Path.GetFileName(path);
                if(name.Equals("desktop.ini",StringComparison.OrdinalIgnoreCase))continue;
                if(Ignored(state,path,localRules))continue;
                if(name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)found.Add(path);
                if(found.Count>=SearchLimit)break;
                if(Directory.Exists(path))pending.Enqueue(path);
            }
        }
        return found.ToArray();
    }
    public static bool Ignored(State state,string path,List<IgnoreRule> localRules=null){
        return state.IgnoreRules.Any(r=>r.Matches(path))||(localRules!=null&&localRules.Any(r=>r.Matches(path)))||IgnoreFiles.Ignored(state,path,null);
    }
    public static string Relative(string root,string path){
        try{
            string full=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string target=Path.GetFullPath(path);
            return target.StartsWith(full,StringComparison.OrdinalIgnoreCase)?target.Substring(full.Length):target;
        }catch{return path;}
    }
    // 同名时保留两份：name (2).ext
    public static string Unique(string folder,string name){
        string candidate=Path.Combine(folder,name);
        if(!Store.Exists(candidate))return candidate;
        string stem=Path.GetFileNameWithoutExtension(name),extension=Path.GetExtension(name);
        for(int i=2;i<10000;i++){candidate=Path.Combine(folder,stem+" ("+i+")"+extension);if(!Store.Exists(candidate))return candidate;}
        throw new InvalidOperationException("目标文件夹里同名文件过多。");
    }
    public static bool CanPasteInto(string[] paths,string destination){
        if(paths==null||paths.Length==0||!Directory.Exists(destination))return false;
        foreach(string path in paths){
            if(!Store.Exists(path))return false;
            if(Directory.Exists(path)&&WorkDetection.Under(destination,path))return false;
            if(String.Equals(IgnoreRule.Canonical(Path.GetDirectoryName(path)),IgnoreRule.Canonical(destination),StringComparison.OrdinalIgnoreCase))return false;
        }
        return true;
    }
    // 拖放忽略只针对该路径；名称通配符规则由设置页单独创建。
    public static IgnoreRule MakeIgnoreRule(string path){
        return new IgnoreRule(){Pattern=IgnoreRule.Canonical(path),Kind="路径",IncludeChildren=Directory.Exists(path),Enabled=true};
    }
    public static bool HasExactIgnore(IEnumerable<IgnoreRule> rules,string path){
        string exact=IgnoreRule.Canonical(path);
        return rules.Any(r=>r.Enabled&&r.Kind=="路径"&&String.Equals(IgnoreRule.Canonical(r.Pattern),exact,StringComparison.OrdinalIgnoreCase));
    }
    public static bool IsIgnoredPath(State state,string path){
        return state.IgnoreRules.Any(r=>r.Matches(path));
    }
    public static readonly string[] IgnoreIcons={"⊘","⛔","✖","☒","⚑","◌"};
}
}
