using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace QiDesk {
// 验证「只改快捷方式图标」这条链路：写入、读回、恢复、以及各种越界输入的拒绝。
public static class ShortcutIconTests {
    public static void Run(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"shortcut-icons");Directory.CreateDirectory(folder);
        string target=Path.Combine(folder,"target.txt");File.WriteAllText(target,"shortcut icon fixture");
        string link=Create(folder,target,"fixture.lnk");
        check(File.Exists(link)&&ShortcutIcons.IsShortcut(link),"a native shortcut is recognized for icon replacement");
        string icon=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"shell32.dll");
        check(File.Exists(icon),"system icon library is available as an icon source");
        ShortcutIcons.Apply(link,icon,3);
        string stored=ShortcutIcons.Current(link);
        check(stored.StartsWith(icon,StringComparison.OrdinalIgnoreCase)&&stored.EndsWith(",3"),"shortcut records the chosen icon source and index");
        check(File.ReadAllText(target)=="shortcut icon fixture","icon replacement leaves the shortcut target untouched");
        ShortcutIcons.Reset(link);
        check(ShortcutIcons.Current(link)=="","resetting a shortcut clears the custom icon back to the target default");
        bool refused=false;try{ShortcutIcons.Apply(target,icon,0);}catch(ArgumentException){refused=true;}check(refused,"non shortcut files are refused instead of being rewritten");
        refused=false;try{ShortcutIcons.Apply(link,Path.Combine(folder,"missing.ico"),0);}catch(FileNotFoundException){refused=true;}check(refused,"a missing icon source is refused before the shortcut is saved");
        refused=false;try{ShortcutIcons.Apply(link,icon,-1);}catch(ArgumentOutOfRangeException){refused=true;}check(refused,"negative icon indexes are refused");
        check(!ShortcutIcons.IsShortcut(target),"plain files are not treated as shortcuts");
    }
    static string Create(string folder,string target,string name){
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
}
}
