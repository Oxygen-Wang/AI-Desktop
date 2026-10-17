using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QiDesk {
public sealed class AppIconEntry {
    public string Key {get;set;}
    public string Icon {get;set;}
    public string Kind {get;set;}
    public string[] Executables {get;set;}
    public string[] Names {get;set;}
    public string[] Extensions {get;set;}
    public AppIconEntry(){Kind="";Executables=new string[0];Names=new string[0];Extensions=new string[0];}
}
public sealed class AppIconPack {
    public string Id {get;set;}
    public string Name {get;set;}
    public string License {get;set;}
    public string Source {get;set;}
    public string Revision {get;set;}
    public List<AppIconEntry> Entries {get;set;}
    internal string Root;
}
// Pure app matching and bounded local PNG loading. No changes to .lnk files or
// executable resources are needed to apply or undo a partition's icon skin.
public static class AppIconSkins {
    public static readonly string[] Choices={"经典","Fluent 线条","Fluent 填充","Fluent 应用","Papirus 应用"};
    static readonly Dictionary<string,AppIconPack> packs=new Dictionary<string,AppIconPack>();
    static readonly Dictionary<string,ImageSource> images=new Dictionary<string,ImageSource>();
    static readonly Dictionary<string,Tuple<string,string>> targets=new Dictionary<string,Tuple<string,string>>(StringComparer.OrdinalIgnoreCase);
    public static string LastError {get;private set;}
    public static bool IsPack(string style){return style=="Fluent 应用"||style=="Papirus 应用";}
    static string Id(string style){return style=="Fluent 应用"?"fluent":style=="Papirus 应用"?"papirus":"";}
    static string AssetPath(string root,string name){
        if(String.IsNullOrWhiteSpace(name)||Path.IsPathRooted(name)||Path.GetFileName(name)!=name||!name.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("图标包只能引用同目录的 PNG 文件。");
        string path=Path.Combine(root,name);
        if(!File.Exists(path)||new FileInfo(path).Length>4*1024*1024||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("图标素材缺失、过大或是目录链接。");
        return path;
    }
    public static AppIconPack Load(string directory){
        string root=Path.GetFullPath(directory),file=Path.Combine(root,"pack.json");
        if(!File.Exists(file)||new FileInfo(file).Length>1024*1024)throw new InvalidDataException("图标包清单缺失或超过 1 MB。");
        var pack=Store.Json.Deserialize<AppIconPack>(File.ReadAllText(file));
        if(pack==null||String.IsNullOrWhiteSpace(pack.Id)||String.IsNullOrWhiteSpace(pack.Name)||pack.Name.Length>80||String.IsNullOrWhiteSpace(pack.License)||pack.Entries==null||pack.Entries.Count>1000)throw new InvalidDataException("图标包缺少名称、授权或图标映射。");
        var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in pack.Entries){
            if(entry==null||String.IsNullOrWhiteSpace(entry.Key)||!keys.Add(entry.Key))throw new InvalidDataException("图标包映射名称缺失或重复。");
            entry.Executables=entry.Executables??new string[0];entry.Names=entry.Names??new string[0];entry.Extensions=entry.Extensions??new string[0];
            if(entry.Executables.Length+entry.Names.Length+entry.Extensions.Length>100||entry.Executables.Concat(entry.Names).Any(s=>String.IsNullOrWhiteSpace(s)||s.Length>100||s.IndexOfAny(new[]{'\\','/',':'})>=0)||entry.Extensions.Any(s=>!System.Text.RegularExpressions.Regex.IsMatch(s??"","^\\.[a-zA-Z0-9-]{1,16}$")))throw new InvalidDataException("图标包的程序或类型映射无效。");
            AssetPath(root,entry.Icon);
        }
        pack.Root=root;return pack;
    }
    public static AppIconPack Find(string style){
        if(!IsPack(style))return null;
        AppIconPack pack;if(packs.TryGetValue(style,out pack))return pack;
        try{pack=Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"icon-skins",Id(style)));packs[style]=pack;LastError="";return pack;}
        catch(Exception ex){if(!(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException||ex is InvalidOperationException))throw;LastError="应用图标皮肤不可用："+ex.Message;return null;}
    }
    public static AppIconEntry Match(AppIconPack pack,string path,string target,bool directory){
        if(pack==null)return null;
        if(directory)return pack.Entries.FirstOrDefault(e=>e.Kind=="folder");
        if(FilePresentation.IsApplication(path)){
            string executable=Path.GetFileName(String.IsNullOrEmpty(target)?path:target);
            var exact=pack.Entries.FirstOrDefault(e=>e.Executables.Contains(executable,StringComparer.OrdinalIgnoreCase));
            if(exact!=null)return exact;
            if(!String.IsNullOrEmpty(target))return null;
            string name=Path.GetFileNameWithoutExtension(path);
            foreach(string suffix in new[]{" - 快捷方式"," - Shortcut"})if(name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-suffix.Length);
            return pack.Entries.FirstOrDefault(e=>e.Names.Contains(name,StringComparer.OrdinalIgnoreCase));
        }
        return pack.Entries.FirstOrDefault(e=>e.Extensions.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase));
    }
    static string Target(string path){
        if(!String.Equals(Path.GetExtension(path),".lnk",StringComparison.OrdinalIgnoreCase))return path;
        string revision=Thumbnails.Revision(path);Tuple<string,string> saved;
        if(targets.TryGetValue(path,out saved)&&saved.Item1==revision)return saved.Item2;
        string target=NativeFileOperations.ResolveShortcut(path);
        if(targets.Count>=512)targets.Clear();targets[path]=Tuple.Create(revision,target);return target;
    }
    public static ImageSource Image(AppIconPack pack,string key){
        try{return ReadImage(pack,key);}
        catch(Exception ex){
            if(!(ex is IOException||ex is UnauthorizedAccessException||ex is NotSupportedException||ex is ArgumentException))throw;
            LastError="图标无法读取，已保留原图："+ex.Message;return null;
        }
    }
    static ImageSource ReadImage(AppIconPack pack,string key){
        if(pack==null)return null;
        var entry=pack.Entries.FirstOrDefault(e=>e.Key==key);if(entry==null)return null;
        string path=AssetPath(pack.Root,entry.Icon);ImageSource source;
        if(images.TryGetValue(path,out source))return source;
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=128;bitmap.UriSource=new Uri(path,UriKind.Absolute);bitmap.EndInit();bitmap.Freeze();
        if(images.Count>=128)images.Clear();images[path]=bitmap;return bitmap;
    }
    public static ImageSource Icon(string path,string style){
        var pack=Find(style);if(pack==null)return null;
        string target=Target(path);
        var entry=Match(pack,path,target,Directory.Exists(path)||Directory.Exists(target));if(entry==null)return null;
        return Image(pack,entry.Key);
    }
}
public static class FileIconPresentation {
    public static ImageSource Themed(string path,Group style){return Themed(path,style,null,64);}
    // 自定义显示图标优先于皮肤与系统图标：用户显式指定过就一直用它。
    public static ImageSource Themed(string path,Group style,State state,double size){
        var custom=CustomIcons.Icon(state,path,size);if(custom!=null)return custom;
        var app=AppIconSkins.Icon(path,style.IconSkin);if(app!=null)return app;
        return FilePresentation.IsApplication(path)?null:FluentIcons.Icon(path,style.IconSkin,((SolidColorBrush)Ui.Brush(style.IconColor)).Color);
    }
    public static void Bind(Image image,string path,double size,ImageSource themed){
        if(themed==null)Thumbnails.Bind(image,path,size);
        else Thumbnails.UseStatic(image,themed);
    }
}
}
