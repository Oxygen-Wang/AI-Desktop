using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace QiDesk {
public sealed class PendingFileRename {
    public string Source {get;set;}
    public string Destination {get;set;}
    public string OriginalDestination {get;set;}
    public bool Directory {get;set;}
}
// A persisted journal protects hidden/managed desktop paths between the native rename
// and saving the new references. Recovery only reconciles metadata; it never guesses a move.
public static class FileRenaming {
    public static string Rename(Store store,Window owner,string source,string name){
        source=Path.GetFullPath(source);
        if(!Store.Exists(source))throw new FileNotFoundException("项目已经移动或删除，请刷新面板。",source);
        string destination=ValidateName(source,name);
        if(source==destination)return source;
        var pending=new PendingFileRename(){Source=source,Destination=destination,Directory=Directory.Exists(source)};
        string original;
        if(store.Data.ManagedDesktopItems.TryGetValue(source,out original)){
            pending.OriginalDestination=ValidateName(original,name);
        }
        if(store.Data.PendingFileRenames.Any(r=>String.Equals(r.Source,source,StringComparison.OrdinalIgnoreCase)))throw new IOException("此项目还有未完成的重命名恢复记录，请先解决同名冲突后重试。");
        store.Data.PendingFileRenames.Add(pending);
        try{store.PersistFileRecovery();}
        catch{store.Data.PendingFileRenames.Remove(pending);throw;}
        try{
            if(!ShellActions.Rename(owner,source,name)){
                store.Data.PendingFileRenames.Remove(pending);store.PersistFileRecovery();return source;
            }
            Complete(store.Data,pending);
            store.RemapFileHistory(pending);
            store.PersistFileRecovery();
            return destination;
        }catch{
            if(Store.Exists(source)&&!Store.Exists(destination)){
                store.Data.PendingFileRenames.Remove(pending);
                store.PersistFileRecovery();
            }
            throw;
        }
    }
    public static string ValidateName(string source,string name){
        if(String.IsNullOrWhiteSpace(name)||name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith(".")||name.EndsWith(" "))throw new ArgumentException("名称不能包含路径符号，末尾不能是空格或点。");
        string stem=name.Split('.')[0];
        if(System.Text.RegularExpressions.Regex.IsMatch(stem,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))throw new ArgumentException("这个名称是 Windows 的保留名称。");
        string destination=Path.Combine(Path.GetDirectoryName(source),name);
        if(!String.Equals(source,destination,StringComparison.OrdinalIgnoreCase)&&Store.Exists(destination))throw new IOException("这里已有同名项目，请换一个名称。");
        return destination;
    }
    static void Complete(State state,PendingFileRename pending){
        UpdateReferences(state,pending);
        state.PendingFileRenames.Remove(pending);
    }
    internal static void UpdateReferences(State state,PendingFileRename pending){
        ShellActions.UpdateReferences(state,pending.Source,pending.Destination,pending.Directory);
        state.HiddenDesktopItems=state.HiddenDesktopItems.Select(path=>ShellActions.UpdatePath(path,pending.Source,pending.Destination,pending.Directory)).ToList();
        state.ManagedDesktopItems=state.ManagedDesktopItems.ToDictionary(
            pair=>ShellActions.UpdatePath(pair.Key,pending.Source,pending.Destination,pending.Directory),
            pair=>ShellActions.UpdatePath(pair.Value,pending.Source,pending.Destination,pending.Directory),StringComparer.OrdinalIgnoreCase);
        if(!String.IsNullOrEmpty(pending.OriginalDestination)&&state.ManagedDesktopItems.ContainsKey(pending.Destination))state.ManagedDesktopItems[pending.Destination]=pending.OriginalDestination;
    }
    public static void Recover(State state){
        foreach(var pending in state.PendingFileRenames.ToArray()){
            bool source=Store.Exists(pending.Source),destination=Store.Exists(pending.Destination);
            if(destination&&(!source||String.Equals(pending.Source,pending.Destination,StringComparison.OrdinalIgnoreCase)))Complete(state,pending);
            else if(source&&!destination)state.PendingFileRenames.Remove(pending);
        }
    }
}
}
