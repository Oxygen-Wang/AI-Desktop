using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QiDesk {
// One display-rule evaluator for partition listings and partition search. A combined
// partition owns its children's rules while retaining imported directory boundaries.
public static class PartitionIgnores {
    static readonly Dictionary<string,IgnorePattern> patterns=new Dictionary<string,IgnorePattern>();
    public static Group Owner(State state,Group group){
        if(group==null||String.IsNullOrEmpty(group.HostGroupId))return group;
        return state.Groups.FirstOrDefault(g=>g.Id==group.HostGroupId)??group;
    }
    public static Group[] Members(State state,Group group){
        var owner=Owner(state,group);
        return owner==null?new Group[0]:new[]{owner}.Concat(state.Groups.Where(g=>g.HostGroupId==owner.Id)).ToArray();
    }
    public static void CopySharedRulesForDetach(State state,Group group){
        var members=Members(state,group);
        var rules=members.SelectMany(g=>g.IgnoreRules).GroupBy(r=>r.Id).Select(r=>Clone(r.First())).ToList();
        IgnoreRule.Validate(rules);
        var folders=new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in members.SelectMany(g=>g.FolderIgnoreRules)){
            List<IgnoreRule> target;
            if(!folders.TryGetValue(entry.Key,out target)){target=new List<IgnoreRule>();folders[entry.Key]=target;}
            foreach(var rule in entry.Value)if(!target.Any(r=>r.Id==rule.Id))target.Add(Clone(rule));
        }
        foreach(var list in folders.Values)IgnoreRule.Validate(list);
        var imports=state.IgnoreFiles.Where(f=>f.ScopeId!=group.Id&&members.Any(g=>g.Id==f.ScopeId)).Select(f=>{
            var copy=Store.Json.Deserialize<IgnoreFileSet>(Store.Json.Serialize(f));
            copy.Id=Guid.NewGuid().ToString("N");copy.ScopeId=group.Id;copy.Automatic=false;return copy;
        }).ToArray();
        IgnoreFiles.Validate(state.IgnoreFiles.Concat(imports).ToList());
        group.IgnoreRules=rules;group.FolderIgnoreRules=folders;
        group.DisabledGlobalIgnoreIds=members.SelectMany(g=>g.DisabledGlobalIgnoreIds).Distinct(StringComparer.Ordinal).ToList();
        state.IgnoreFiles.AddRange(imports);
    }
    static IgnoreRule Clone(IgnoreRule rule){return Store.Json.Deserialize<IgnoreRule>(Store.Json.Serialize(rule));}
    static IgnorePattern Pattern(string text){
        lock(patterns){IgnorePattern result;if(!patterns.TryGetValue(text,out result)){result=new IgnorePattern(text);if(patterns.Count>4000)patterns.Clear();patterns[text]=result;}return result;}
    }
    static bool MatchPattern(string text,string root,string path,bool directory,ref bool ignored){
        if(!WorkDetection.Under(path,root)||String.Equals(path,root,StringComparison.OrdinalIgnoreCase))return false;
        var pattern=Pattern(text);
        string relative=PanelFiles.Relative(root,path).Replace('\\','/');
        if(!pattern.Matches(relative,directory))return false;
        ignored=!pattern.Negated;return true;
    }
    static void ApplyRules(IEnumerable<IgnoreRule> rules,string root,string path,bool directory,ref bool ignored){
        foreach(var rule in rules){
            if(!rule.Enabled)continue;
            if(rule.Kind=="Git")MatchPattern(rule.Pattern,root,path,directory,ref ignored);
            else if(rule.Matches(path))ignored=true;
        }
    }
    static void ApplyFiles(IEnumerable<IgnoreFileSet> files,string path,bool directory,ref bool ignored){
        foreach(var file in files)foreach(string line in file.Lines)MatchPattern(line,file.Root,path,directory,ref ignored);
    }
    public static bool Ignored(State state,string path,Group group){
        var members=Members(state,group);
        var suppressed=new HashSet<string>(members.SelectMany(g=>g.DisabledGlobalIgnoreIds),StringComparer.Ordinal);
        var globalFiles=state.IgnoreFiles.Where(s=>s.Enabled&&(!s.Automatic||state.AutoImportIgnoreFiles)&&String.IsNullOrEmpty(s.ScopeId)&&!suppressed.Contains(s.Id)).ToArray();
        var localFiles=state.IgnoreFiles.Where(s=>s.Enabled&&members.Any(g=>g.Id==s.ScopeId)).ToArray();
        string full=IgnoreRule.Canonical(path);
        var mappedRoots=members.SelectMany(g=>g.Folders.Concat(new[]{g.Folder}).Concat(g.Items.Select(Path.GetDirectoryName)))
            .Where(r=>!String.IsNullOrEmpty(r)&&WorkDetection.Under(full,r)&&!String.Equals(full,IgnoreRule.Canonical(r),StringComparison.OrdinalIgnoreCase)).ToArray();
        var roots=mappedRoots.Concat(globalFiles.Concat(localFiles).Select(f=>f.Root))
            .Where(r=>!String.IsNullOrEmpty(r)&&WorkDetection.Under(full,r)&&!String.Equals(full,IgnoreRule.Canonical(r),StringComparison.OrdinalIgnoreCase));
        string root=roots.OrderBy(r=>r.Length).FirstOrDefault()??Path.GetDirectoryName(full);
        string ruleRoot=mappedRoots.OrderByDescending(r=>r.Length).FirstOrDefault()??root;
        if(String.IsNullOrEmpty(root))return false;
        string relative=PanelFiles.Relative(root,full);
        string current=IgnoreRule.Canonical(root);
        var parts=relative.Split(new[]{'\\','/'},StringSplitOptions.RemoveEmptyEntries);
        for(int i=0;i<parts.Length;i++){
            current=Path.Combine(current,parts[i]);
            bool directory=i<parts.Length-1||Directory.Exists(current);
            bool ignored=WorkDetection.AutoIgnored(state,current);
            ApplyRules(state.IgnoreRules.Where(r=>!suppressed.Contains(r.Id)),ruleRoot,current,directory,ref ignored);
            ApplyFiles(globalFiles,current,directory,ref ignored);
            ApplyFiles(localFiles,current,directory,ref ignored);
            foreach(var member in members){
                foreach(var scoped in member.FolderIgnoreRules.Where(f=>WorkDetection.Under(current,f.Key)))ApplyRules(scoped.Value,scoped.Key,current,directory,ref ignored);
                ApplyRules(member.IgnoreRules,ruleRoot,current,directory,ref ignored);
            }
            if(ignored)return true;
        }
        return false;
    }
}
}
