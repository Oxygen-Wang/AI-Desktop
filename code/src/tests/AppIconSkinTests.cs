using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QiDesk {
public static class AppIconSkinTests {
    public static void Run(string root,Action<bool,string> check){
        foreach(string style in AppIconSkins.Choices.Where(AppIconSkins.IsPack)){
            var pack=AppIconSkins.Find(style);
            check(pack!=null&&pack.License=="GPL-3.0"&&pack.Revision.Length==40,style+" ships a pinned source revision and its license");
            check(AppIconSkins.Match(pack,"renamed.lnk",@"C:\Apps\Browser\CHROME.EXE",false).Key=="chrome",style+" matches renamed shortcuts by executable target with Windows case semantics");
            check(AppIconSkins.Match(pack,"Google Chrome.lnk","",false).Key=="chrome",style+" uses explicit shortcut aliases when a target cannot be resolved");
            check(AppIconSkins.Match(pack,"Google Chrome.lnk",@"C:\Apps\other.exe",false)==null,style+" never overrides a known different shortcut target using its display name");
            check(AppIconSkins.Match(pack,"chrome-helper.exe","",false)==null&&AppIconSkins.Match(pack,"Chrome notes.txt","",false).Key=="document",style+" avoids substring and document-name application matches");
            check(AppIconSkins.Match(pack,"Google Chrome","",true).Key=="folder",style+" distinguishes folders from similarly named apps");
            check(AppIconSkins.Match(pack,"report.XLSX","",false).Key=="excel",style+" maps Office file extensions consistently");
            check(AppIconSkins.Match(pack,"unknown-app.exe","",false)==null,style+" leaves unknown applications available for system-icon fallback");
            foreach(var entry in pack.Entries){
                var bitmap=(BitmapSource)AppIconSkins.Image(pack,entry.Key);
                check(bitmap.PixelWidth==128&&bitmap.PixelHeight==128&&bitmap.IsFrozen,style+" decodes immutable 128px resource "+entry.Key);
            }
        }
        var store=new Store(Path.Combine(root,"skin-state"));store.Data.Groups[0].IconSkin="Papirus 应用";store.Save();
        check(new Store(store.Root).Data.Groups[0].IconSkin=="Papirus 应用","application skin selection persists across restart");
        var controller=new Controller(store);string groupId=store.Data.Groups[0].Id;
        var request=new Dictionary<string,object>{{"name","qidesk_update_group"},{"arguments",new Dictionary<string,object>{{"groupId",groupId},{"settings",new Dictionary<string,object>{{"IconSkin","Fluent 应用"},{"IconColor","#ABCDEF"}}}}}};
        McpBridge.Execute(controller,request);
        var saved=new Store(store.Root).Data.Groups[0];
        check(saved.IconSkin=="Fluent 应用"&&saved.IconColor=="#ABCDEF","the settings interface can apply and persist an application icon skin and color");
        var exposed=(Dictionary<string,object>)Store.Json.DeserializeObject(Store.Json.Serialize(AiSettings.Get(store)));
        var exposedGroup=(Dictionary<string,object>)((object[])exposed["groups"])[0];
        var exposedSettings=(Dictionary<string,object>)exposedGroup["settings"];
        check(Convert.ToString(exposedSettings["IconSkin"])=="Fluent 应用"&&Convert.ToString(exposedSettings["IconColor"])=="#ABCDEF","the settings interface reports the currently active skin and icon color");
        var arguments=(Dictionary<string,object>)request["arguments"];
        arguments["settings"]=new Dictionary<string,object>{{"IconColor","#102030"},{"IconSkin","unsupported-pack"}};
        bool invalidSettings=false;try{McpBridge.Execute(controller,request);}catch(Exception){invalidSettings=true;}
        check(invalidSettings&&store.Data.Groups[0].IconSkin=="Fluent 应用"&&store.Data.Groups[0].IconColor=="#ABCDEF","invalid icon skins reject and roll back the entire settings request");
        arguments["settings"]=new Dictionary<string,object>{{"IconColor","not-a-color"}};
        invalidSettings=false;try{McpBridge.Execute(controller,request);}catch(Exception){invalidSettings=true;}
        check(invalidSettings&&store.Data.Groups[0].IconColor=="#ABCDEF","the settings interface rejects invalid icon colors before saving them");
        var old=Store.Json.Deserialize<State>("{\"Version\":8,\"Groups\":[{\"Name\":\"old\"}],\"Rules\":[]}");Store.Validate(old);
        check(old.Groups[0].IconSkin=="经典","old configurations retain their original default icon style");
        var broken=new Group(){IconSkin="untrusted-theme"};Store.NormalizeStyle(broken);check(broken.IconSkin=="经典","unknown icon styles safely normalize to the classic style");
        string folder=Path.Combine(root,"invalid-skin");Directory.CreateDirectory(folder);
        var invalid=new AppIconPack(){Id="fixture",Name="Fixture",License="MIT",Entries=new List<AppIconEntry>{new AppIconEntry(){Key="test",Icon="..\\outside.png"}}};
        File.WriteAllText(Path.Combine(folder,"pack.json"),Store.Json.Serialize(invalid));bool rejected=false;
        try{AppIconSkins.Load(folder);}catch(InvalidDataException){rejected=true;}
        check(rejected,"icon pack loader rejects path traversal before decoding any image");
        invalid.Entries[0].Icon="broken.png";File.WriteAllText(Path.Combine(folder,"broken.png"),"not an image");File.WriteAllText(Path.Combine(folder,"pack.json"),Store.Json.Serialize(invalid));
        var corrupt=AppIconSkins.Load(folder);
        check(AppIconSkins.Image(corrupt,"test")==null&&!String.IsNullOrEmpty(AppIconSkins.LastError),"a corrupt skin image falls back with a diagnostic instead of breaking the file view");
    }
    static string Shortcut(string folder,string target,string name){
        string path=Path.Combine(folder,name);object shell=null,link=null;
        try{
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
            link.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});
            link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);return path;
        }finally{if(link!=null)Marshal.ReleaseComObject(link);if(shell!=null)Marshal.ReleaseComObject(shell);}
    }
    public static void Native(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"native-skins");Directory.CreateDirectory(folder);
        string executable=Path.Combine(folder,"chrome.exe");File.WriteAllText(executable,"fixture, never executed");
        string link=Shortcut(folder,executable,"My browser.lnk");byte[] original=File.ReadAllBytes(link);
        var source=AppIconSkins.Icon(link,"Fluent 应用");
        check(source!=null&&Object.ReferenceEquals(source,AppIconSkins.Image(AppIconSkins.Find("Fluent 应用"),"chrome")),"native shortcut targets select their app skin independently of the visible name");
        AppIconSkins.Icon(link,"Papirus 应用");AppIconSkins.Icon(link,"经典");
        check(original.SequenceEqual(File.ReadAllBytes(link)),"switching and restoring icon skins never writes the real shortcut");
        string folderLink=Shortcut(folder,folder,"Google Chrome.lnk");
        check(Object.ReferenceEquals(AppIconSkins.Icon(folderLink,"Fluent 应用"),AppIconSkins.Image(AppIconSkins.Find("Fluent 应用"),"folder")),"a shortcut to a folder uses a folder skin even if its label names an app");
        var image=new Image(){Width=64,Height=64};var window=new Window(){Content=image,Width=160,Height=160,Left=-15000,Top=-15000};
        try{
            window.Show();window.UpdateLayout();Thumbnails.Bind(image,link,64);Thumbnails.RequestVisible(window);
            FileIconPresentation.Bind(image,link,64,source);Thumbnails.Drain(1800);
            check(Object.ReferenceEquals(image.Source,source),"pending native thumbnail callbacks cannot overwrite a skin icon");
            FileIconPresentation.Bind(image,link,64,null);Thumbnails.Drain(1800);
            check(image.Source!=null&&!Object.ReferenceEquals(image.Source,source),"restoring a system icon re-enables normal thumbnail rendering");
        }finally{window.Close();}
        var store=new Store(Path.Combine(root,"skin-window-state"));
        var group=new Group(){Name="Fixture",IconSkin="Fluent 应用"};group.Items.Add(link);store.Data.Groups.Clear();store.Data.Groups.Add(group);
        var controller=new Controller(store);var fence=new FenceWindow(controller,group){Left=-15000,Top=-15000};
        try{
            fence.Show();fence.UpdateLayout();Thumbnails.RequestVisible(fence);Thumbnails.Drain(1800);
            check(Images(fence).Any(i=>Object.ReferenceEquals(i.Source,source)),"desktop partition keeps the chosen skin after thumbnails settle");
        }finally{fence.Close();}
        var manager=new ManagerWindow(controller){Left=-15000,Top=-15000};
        try{
            manager.Show();manager.ShowSettingsPage("颜色和外观");manager.UpdateLayout();
            var expander=Descendants<Expander>(manager).First(e=>Convert.ToString(e.Header).StartsWith("应用图标皮肤"));expander.IsExpanded=true;manager.UpdateLayout();
            var buttons=Descendants<Button>(expander).ToArray();
            buttons.First(b=>Convert.ToString(b.Content)=="应用这套图标").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(group.IconSkin=="Fluent 应用","appearance preview applies an app skin through its real button");
            buttons.First(b=>Convert.ToString(b.Content)=="恢复系统图标").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check(group.IconSkin=="经典"&&original.SequenceEqual(File.ReadAllBytes(link)),"restore button changes the partition style and preserves the shortcut bytes");
        }finally{manager.Quit();}
    }
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject{
        if(root is T)yield return (T)root;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    static IEnumerable<Image> Images(DependencyObject root){return Descendants<Image>(root);}
    public static int Verify(bool native){
        string root=Path.Combine(Path.GetTempPath(),"QiDesk-skin-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var results=new List<string>();Application app=null;
        Action<bool,string> check=delegate(bool valid,string name){if(!valid)throw new Exception("FAILED: "+name);results.Add("PASS: "+name);};
        try{Run(root,check);if(native){app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};Native(root,check);}return 0;}
        catch(Exception ex){results.Add(ex.ToString());return 1;}
        finally{File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"icon-skin-test-results.txt"),results);if(app!=null)app.Shutdown();}
    }
    public static int Render(){
        string root=Path.Combine(Path.GetTempPath(),"QiDesk-skin-preview-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};var store=new Store(root);var controller=new Controller(store);
        var group=new Group(){Name="应用",Width=570,Height=390,IconSize=44,Color="#77BFA3"};
        foreach(string name in new[]{"chrome.exe","msedge.exe","firefox.exe","wechat.exe","matlab.exe","code.exe","winword.exe","excel.exe","powerpnt.exe","cloudmusic.exe","qq.exe","unknown-app.exe"}){
            string file=Path.Combine(root,name);File.WriteAllText(file,"fixture, never executed");group.Items.Add(file);
        }
        store.Data.Groups.Clear();store.Data.Groups.Add(group);
        try{
            foreach(string skin in new[]{"经典","Fluent 应用","Papirus 应用"}){
                group.IconSkin=skin;var fence=new FenceWindow(controller,group){Left=-15000,Top=-15000};
                try{fence.Show();fence.UpdateLayout();Thumbnails.RequestVisible(fence);Thumbnails.Drain(1500);Capture(fence,skin=="经典"?"icon-skin-classic.png":skin=="Fluent 应用"?"icon-skin-fluent.png":"icon-skin-papirus.png");}finally{fence.Close();}
            }
            var manager=new ManagerWindow(controller){Left=-15000,Top=-15000};
            try{manager.Show();manager.ShowSettingsPage("颜色和外观");manager.UpdateLayout();foreach(var expander in Descendants<Expander>(manager).Where(e=>Convert.ToString(e.Header).StartsWith("应用图标皮肤")))expander.IsExpanded=true;Capture(manager,"icon-skin-settings.png");}finally{manager.Quit();}
            return 0;
        }finally{app.Shutdown();}
    }
    static void Capture(Window window,string name){window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name)))png.Save(stream);}
}
}
