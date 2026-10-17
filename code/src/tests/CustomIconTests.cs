using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QiDesk {
// 自定义显示图标：只改栖桌内显示，配置随重命名迁移，来源失效时回退系统图标。
public static class CustomIconTests {
    public static void Run(string root,Action<bool,string> check){
        OnlyAbsolutePathsSurviveValidation(root,check);
        RenameAndMoveFollowTheItem(root,check);
        SourcesLoadAndFallBack(root,check);
        ThemedIconPrefersCustomIcon(root,check);
        ConfigurationRoundTrip(root,check);
    }
    static string Png(string folder,string name){
        Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,name);
        if(File.Exists(path))return path;
        byte[] pixels=new byte[8*8*4];
        for(int i=3;i<pixels.Length;i+=4)pixels[i]=255;
        var source=BitmapSource.Create(8,8,96,96,PixelFormats.Bgra32,null,pixels,8*4);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));
        using(var stream=File.Create(path))encoder.Save(stream);
        return path;
    }
    static void OnlyAbsolutePathsSurviveValidation(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"custom-icon-validate");
        string png=Png(folder,"icon.png");
        var state=new State();
        CustomIcons.Set(state,Path.Combine(folder,"a.txt"),png,0);
        CustomIcons.Set(state,"relative\\item.txt",png,0);
        CustomIcons.Set(state,Path.Combine(folder,"b.txt"),"relative.ico",0);
        CustomIcons.Set(state,Path.Combine(folder,"c.txt"),png,-4);
        CustomIcons.Validate(state);
        check(state.CustomIcons.Count==2,"validation keeps only absolute item paths with absolute icon sources");
        check(state.CustomIcons.ContainsKey(Path.Combine(folder,"a.txt")),"a valid custom icon survives validation");
        check(state.CustomIcons[Path.Combine(folder,"c.txt")].Index==0,"negative icon indexes clamp to the first icon");
        check(!state.CustomIcons.ContainsKey("relative\\item.txt"),"relative item paths are dropped before they point somewhere else");
        bool valid=true;try{Store.Validate(state);}catch{valid=false;}
        check(valid,"a state with custom icons passes validation");
    }
    static void RenameAndMoveFollowTheItem(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"custom-icon-move");
        string png=Png(folder,"icon.png");
        string before=Path.Combine(folder,"note.txt"),after=Path.Combine(folder,"renamed.txt");
        var state=new State();
        CustomIcons.Set(state,before,png,0);
        ShellActions.UpdateReferences(state,before,after,false);
        check(!state.CustomIcons.ContainsKey(before)&&state.CustomIcons.ContainsKey(after),"renaming an item moves its custom icon to the new path");
        string source=Path.Combine(folder,"work"),target=Path.Combine(folder,"work-moved");
        string child=Path.Combine(source,"sub","deep.txt"),moved=Path.Combine(target,"sub","deep.txt");
        CustomIcons.Set(state,child,png,0);
        ShellActions.UpdateReferences(state,source,target,true);
        check(state.CustomIcons.ContainsKey(moved),"moving a parent folder carries custom icons of nested items");
        check(!state.CustomIcons.ContainsKey(child),"the old nested path no longer holds a custom icon");
    }
    static void SourcesLoadAndFallBack(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"custom-icon-load");
        string png=Png(folder,"icon.png");
        var state=new State();
        CustomIcons.Set(state,Path.Combine(folder,"item.txt"),png,0);
        check(CustomIcons.Icon(state,Path.Combine(folder,"item.txt"),64)!=null,"an image source renders as the item icon");
        string library=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"shell32.dll");
        if(File.Exists(library)){
            CustomIcons.Set(state,Path.Combine(folder,"app.exe"),library,3);
            check(CustomIcons.Icon(state,Path.Combine(folder,"app.exe"),64)!=null,"an icon index inside a system library loads");
            check(CustomIcons.Count(library)>1,"icon libraries report how many icons they contain");
        }
        CustomIcons.Set(state,Path.Combine(folder,"gone.txt"),Path.Combine(folder,"missing.ico"),0);
        check(CustomIcons.Icon(state,Path.Combine(folder,"gone.txt"),64)==null,"a missing icon source falls back instead of throwing");
        check(CustomIcons.Icon(state,Path.Combine(folder,"plain.txt"),64)==null,"items without a custom icon use the normal presentation");
    }
    static void ThemedIconPrefersCustomIcon(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"custom-icon-themed");
        string png=Png(folder,"icon.png");
        var state=new State();var group=new Group(){IconSkin="经典",IconColor="#92AAE5"};
        string item=Path.Combine(folder,"report.txt");
        CustomIcons.Set(state,item,png,0);
        var custom=FileIconPresentation.Themed(item,group,state,64);
        check(custom!=null,"the shared icon presentation returns the custom icon first");
        check(custom==FileIconPresentation.Themed(item,group,state,64),"custom icons are reused from cache instead of decoded per tile");
        check(CustomIcons.Reset(state,item),"resetting reports that a custom icon was removed");
        check(FileIconPresentation.Themed(item,group,state,64)!=custom,"resetting drops the custom icon from the presentation");
        CustomIcons.Set(state,Path.Combine(folder,"other.txt"),png,0);
        check(CustomIcons.Clear(state)==1,"clearing reports how many custom icons were removed");
        check(state.CustomIcons.Count==0,"clearing leaves no custom icons behind");
    }
    static void ConfigurationRoundTrip(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"custom-icon-store");
        string png=Png(folder,"icon.png");
        var store=new Store(folder);
        string item=Path.Combine(folder,"file.txt");
        CustomIcons.Set(store.Data,item,png,0);
        store.Save();
        var reloaded=new Store(folder);
        check(reloaded.Data.CustomIcons.Count==1,"custom icons are persisted with the configuration");
        check(CustomIcons.Has(reloaded.Data,item),"a persisted custom icon is found again after reload");
        check(CustomIcons.Icon(reloaded.Data,item,64)!=null,"a persisted custom icon still renders after reload");
    }
}
}
