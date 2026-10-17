using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace QiDesk {
public static class UnifiedWorkspaceTests {
    [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
    public static void Run(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"unified-files");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"draft.tmp");File.WriteAllText(file,"test");
        var store=new Store(Path.Combine(root,"unified-state"));
        var a=new Group(){Folder=folder,Kind="文件夹",Name="A"};a.Folders.Add(folder);
        var b=new Group(){Folder=folder,Kind="文件夹",Name="B"};b.Folders.Add(folder);
        store.Data.Groups=new List<Group>{a,b};store.Data.IgnoreRules.Clear();store.Data.IgnoreFiles.Clear();
        var global=new IgnoreRule(){Kind="Git",Pattern="*.tmp"};store.Data.IgnoreRules.Add(global);
        check(store.Ignored(file,a)&&store.Ignored(file,b),"global display rules apply to both partitions");
        a.DisabledGlobalIgnoreIds.Add(global.Id);
        check(!store.Ignored(file,a)&&store.Ignored(file,b),"removing an inherited rule affects only the current partition");
        global.Pattern="draft.*";
        check(!store.Ignored(file,a),"a local inheritance exception survives editing the global pattern");
        a.DisabledGlobalIgnoreIds.Clear();a.IgnoreRules.Add(new IgnoreRule(){Kind="Git",Pattern="!draft.tmp"});
        check(!store.Ignored(file,a)&&store.Ignored(file,b),"local Git negation overrides a global match");
        var ordinary=new Group(){Name="自动整理分区"};ordinary.IgnoreRules.Add(new IgnoreRule(){Kind="Git",Pattern="!draft.tmp"});store.Data.Groups.Add(ordinary);
        store.Data.Rules=new List<Rule>{new Rule(){Extensions="tmp",GroupId=ordinary.Id}};
        check(store.Match(file)==ordinary,"automatic organization honors a partition exception to global ignore rules");
        a.IgnoreRules.Clear();
        var child=new Group(){Name="child",HostGroupId=a.Id};store.Data.Groups.Add(child);
        child.DisabledGlobalIgnoreIds.Add(global.Id);
        check(!store.Ignored(file,a)&&!store.Ignored(file,child),"combined tabs use one inheritance policy");
        child.IgnoreRules.Add(PanelFiles.MakeIgnoreRule(file));
        check(store.Ignored(file,a)&&store.Ignored(file,child),"combined tabs share exact path ignore rules");
        string second=Path.Combine(root,"another-root");Directory.CreateDirectory(second);string same=Path.Combine(second,"draft.tmp");File.WriteAllText(same,"test");a.Folders.Add(second);
        check(!store.Ignored(same,a),"a dragged exact path does not hide a same-named file in another root");
        string source=Path.Combine(folder,".gitignore");File.WriteAllText(source,"*.log\n!keep.log\n");
        var imported=IgnoreFiles.Read(source,a.Id,folder,false);IgnoreFiles.Import(store.Data,imported);
        imported.Lines.RemoveAt(0);store.Save();
        check(File.ReadAllText(source).Contains("*.log"),"editing imported partition rules never writes the source ignore file");
        var loaded=new Store(store.Root);
        check(loaded.Data.Groups.Single(g=>g.Id==child.Id).DisabledGlobalIgnoreIds.Contains(global.Id),"partition inheritance exceptions survive restart");
        var legacy=new State();legacy.Groups.Add(new Group(){DisabledGlobalIgnoreIds=null});Store.Validate(legacy);
        check(legacy.Groups[0].DisabledGlobalIgnoreIds!=null,"old configuration initializes local inheritance exceptions");
        store.Data.PanelTabs.Add(PanelTab.OfFolder(folder));var controller=new Controller(store);
        string beforeColor=a.Color;double beforeOpacity=a.BackgroundOpacity;
        check(controller.MigrateWorkspaceTabs()&&store.Data.PanelTabs.Count==0&&controller.SelectedGroups.Contains(a.Id),"old workspace tabs migrate to existing desktop partitions");
        check(!controller.MigrateWorkspaceTabs()&&a.Color==beforeColor&&a.BackgroundOpacity==beforeOpacity,"workspace migration is idempotent and preserves desktop appearance");
        a=store.Data.Groups.Single(g=>g.Id==a.Id);child=store.Data.Groups.Single(g=>g.Id==child.Id);
        b=store.Data.Groups.Single(g=>g.Id==b.Id);
        child.TabPinned=true;store.Save();
        check(new Store(store.Root).Data.Groups.Single(g=>g.Id==child.Id).TabPinned,"pinned merged tabs survive restart");
        check(controller.MoveMergedTab(a,child,b)&&child.HostGroupId==b.Id&&b.ActiveGroupId==child.Id,"merged tab can move into another partition");
        check(controller.MoveMergedTab(b,child,a)&&child.HostGroupId==a.Id&&a.ActiveGroupId==child.Id,"merged tab can move back to its original partition");
        a.IgnoreRules.Add(new IgnoreRule(){Kind="Git",Pattern="*.private"});
        PartitionIgnores.CopySharedRulesForDetach(store.Data,child);child.HostGroupId="";
        var copied=child.IgnoreRules.Single(r=>r.Pattern=="*.private");copied.Pattern="*.secret";
        check(a.IgnoreRules.Any(r=>r.Pattern=="*.private")&&child.DisabledGlobalIgnoreIds.Contains(global.Id),"detaching copies shared rules and inheritance exceptions without sharing mutable state");
        check(store.Data.IgnoreFiles.Any(f=>f.ScopeId==child.Id&&f.Source==source),"detaching keeps an independent copy of imported shared rules");
        string exception=Path.Combine(folder,"keep.log");File.WriteAllText(exception,"test");
        a.IgnoreRules.Add(PanelFiles.MakeIgnoreRule(exception));
        check(store.Ignored(exception,a),"explicit partition ignore overrides an imported inclusion exception");
        var broad=new IgnoreFileSet(){Root=root,Source=Path.Combine(root,".ignore"),Lines=new List<string>{"*.unrelated"}};store.Data.IgnoreFiles.Add(broad);
        a.IgnoreRules.Add(new IgnoreRule(){Kind="Git",Pattern="/anchored.txt"});string anchored=Path.Combine(folder,"anchored.txt");File.WriteAllText(anchored,"test");
        check(store.Ignored(anchored,a),"anchored partition rules keep their folder root when global imports use a broader root");
        var automatic=new IgnoreFileSet(){Root=folder,Source=Path.Combine(folder,".cursorignore"),Automatic=true,Lines=new List<string>{"*.cache"}};
        store.Data.IgnoreFiles.Add(automatic);store.Data.PanelTabs.Add(PanelTab.OfFolder(folder));
        check(controller.MigrateWorkspaceTabs(),"migration accepts legacy automatically linked ignore files");
        IgnoreFiles.Sync(store.Data);
        check(store.Data.IgnoreFiles.Any(f=>f.ScopeId==a.Id&&!f.Automatic&&f.Lines.Contains("*.cache")),"migrated ignore rules survive removal of legacy panel roots");
        var limited=new Store(Path.Combine(root,"limited-migration"));
        limited.Data.Groups=new List<Group>{new Group(){Name="unchanged",Folder=folder}};
        limited.Data.PanelTabs.Add(PanelTab.OfFolder(folder));
        for(int i=0;i<120;i++)limited.Data.IgnoreFiles.Add(new IgnoreFileSet(){Root=folder,Source=Path.Combine(folder,"source-"+i),Automatic=true});
        var limitedController=new Controller(limited);bool rejected=false;
        try{limitedController.MigrateWorkspaceTabs();}catch(InvalidDataException){rejected=true;}
        check(rejected&&limited.Data.PanelTabs.Count==1&&limited.Data.IgnoreFiles.Count==120&&limited.Data.Groups.Count==1,"migration validates limits before replacing the old configuration");
        using(var cancel=new System.Threading.CancellationTokenSource()){
            cancel.Cancel();bool cancelled=false;
            try{PanelFiles.Search(store.Data,folder,"draft",null,a,cancel.Token);}catch(OperationCanceledException){cancelled=true;}
            check(cancelled,"partition search stops when its window or query is cancelled");
        }
    }

    public static void Native(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"unified-native"));
        string folder=Path.Combine(root,"unified-search");Directory.CreateDirectory(Path.Combine(folder,"nested"));
        string match=Path.Combine(folder,"nested","find-me.txt");File.WriteAllText(match,"test");
        var a=new Group(){Name="搜索与折叠",Kind="文件夹",Folder=folder,X=40,Y=80,Width=420,Height=300,Page=0};a.Folders.Add(folder);
        var b=new Group(){Name="其他分区",X=520,Y=80,Page=0};
        store.Data.Groups=new List<Group>{a,b};store.Data.Pages=1;store.Data.PanelTabs.Clear();
        var controller=new Controller(store){Running=true,Docked=false};var manager=new ManagerWindow(controller);
        try{
            controller.Rebuild();controller.ToggleGroupSelection(a);
            controller.Rebuild();var first=controller.Windows.Single(w=>w.Group.Id==a.Id);
            check(!first.Topmost&&!controller.WorkspaceVisible,"rebuilding a selected but unopened partition does not raise it");
            manager.ToggleWorkspaceFromPet();
            check(first.IsVisible&&!first.Topmost&&controller.WorkspaceVisible&&!manager.WorkspacePanelVisible,"pet opens the original partition as an ordinary foreground window with no extra panel");
            check(controller.Windows.Where(w=>w.Group.Id!=a.Id).All(w=>w.IsVisible&&!w.Topmost),"pet leaves unselected partitions on the desktop instead of raising them");
            controller.ToggleGroupSelection(b);manager.ToggleWorkspaceFromPet();
            controller.HideWorkspaceGroup(a);manager.ToggleWorkspaceFromPet();
            check(controller.Windows.All(w=>w.IsVisible&&!w.Topmost)&&controller.WorkspaceVisible,"mixed hidden and visible selected partitions are restored together");
            first.ToggleSearchBar();first.SearchInput.Text="find-me";
            PumpUntil(()=>first.SearchResultPaths.Contains(match),4000);
            check(first.SearchVisible&&first.SearchResultPaths.Contains(match),"desktop partition search finds nested files in place");
            first.ToggleSearchBar();
            check(!first.SearchVisible,"a second search toggle closes search in the same partition");
            controller.ToggleGroupSelection(a);
            check(first.IsVisible&&!first.Topmost,"deselecting a raised partition immediately demotes it");
            controller.HideWorkspaceGroup(b);controller.ClearGroupSelection();
            check(controller.Windows.All(w=>w.IsVisible&&!w.Topmost),"clearing selection restores hidden partitions to desktop state");
            a.Rolled=true;controller.Rebuild();first=controller.Windows.Single(w=>w.Group.Id==a.Id);first.UpdateLayout();
            check(first.Height>=48&&TextNodes(first).Any(t=>t.Text==FilePresentation.FolderName(folder)&&t.IsVisible),"a folded folder partition keeps a readable title");
            check(TextNodes(first).Any(t=>t.Text=="⠿"&&t.IsVisible),"folded partition has an explicit drag handle");
            DragFoldedPartition(first,check);
            check(a.Rolled&&a.Height==300&&(a.X!=40||a.Y!=80),"native folded-bar drag preserves folded state and expanded height");
            var saved=new Store(store.Root).Data.Groups.Single(g=>g.Id==a.Id);
            check(saved.Rolled&&saved.X==a.X&&saved.Y==a.Y,"native folded-bar drag persists its new position");
            controller.HideWorkspaceGroup(a);controller.Rebuild();
            check(!controller.Windows.Single(w=>w.Group.Id==a.Id).IsVisible&&store.Data.Groups.Contains(a)&&File.Exists(match),"closing a partition survives rebuild without removing configuration or files");
        }finally{manager.Quit();}
    }

    static IEnumerable<TextBlock> TextNodes(DependencyObject node){
        var text=node as TextBlock;if(text!=null)yield return text;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in TextNodes(VisualTreeHelper.GetChild(node,i)))yield return child;
    }
    static void DragFoldedPartition(FenceWindow window,Action<bool,string> check){
        var original=System.Windows.Forms.Cursor.Position;
        bool pressed=false;
        try{
            Shell.Float(window);window.Topmost=true;Shell.BeginInlineInput(window);window.UpdateLayout();
            var grip=TextNodes(window).First(t=>t.Text=="⠿");
            Point location=grip.PointToScreen(new Point(12,12));
            System.Windows.Forms.Cursor.Position=new System.Drawing.Point((int)location.X,(int)location.Y);
            PumpFor(30);
            check(DesktopGesture.TargetAt(new DesktopGesture.NativePoint((int)location.X,(int)location.Y))==new WindowInteropHelper(window).Handle,"folded drag fixture is the native mouse target");
            mouse_event(2,0,0,0,UIntPtr.Zero);pressed=true;PumpFor(30);
            System.Windows.Forms.Cursor.Position=new System.Drawing.Point((int)location.X+80,(int)location.Y+100);
            PumpFor(60);
            mouse_event(4,0,0,0,UIntPtr.Zero);pressed=false;PumpFor(60);
        }finally{
            if(pressed)mouse_event(4,0,0,0,UIntPtr.Zero);
            System.Windows.Forms.Cursor.Position=original;
            window.Topmost=false;
        }
    }
    static void PumpFor(int milliseconds){var end=DateTime.UtcNow.AddMilliseconds(milliseconds);PumpUntil(()=>DateTime.UtcNow>=end,milliseconds+100);}
    public static int Render(){
        string root=Path.Combine(Path.GetTempPath(),"QiDesk-unified-preview-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var store=new Store(root);string folder=Path.Combine(root,"工作资料");Directory.CreateDirectory(folder);
        foreach(string name in new[]{"需求说明.md","会议记录.txt","设计笔记.txt"})File.WriteAllText(Path.Combine(folder,name),"preview");
        string pictures=Path.Combine(root,"图片素材");Directory.CreateDirectory(pictures);
        foreach(string name in new[]{"封面.png","截图.png"})File.WriteAllText(Path.Combine(pictures,name),"preview");
        var group=new Group(){Name="文档",Kind="普通",Width=620,Height=340,Folders=new List<string>{folder,pictures},FolderIndex=0};
        var child=new Group(){Name="图片",HostGroupId=group.Id};
        store.Data.Groups=new List<Group>{group,child};store.Data.IgnoreRules.Add(new IgnoreRule(){Kind="Git",Pattern="*.tmp"});
        var controller=new Controller(store);var manager=new ManagerWindow(controller){Left=-20000,Top=-20000,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
        FenceWindow fence=null;
        try{
            manager.Show();manager.ShowSettingsPage("颜色和外观");Capture(manager,"unified-appearance-preview.png");
            manager.ShowIgnoreRules(group);Capture(manager,"unified-ignore-preview.png");
            manager.ShowSettingsPage("主页");Capture(manager,"unified-control-preview.png");
            fence=new FenceWindow(controller,group){Left=-20000,Top=-20000,ShowActivated=false};fence.Show();
            var grid=(Grid)((Border)fence.Content).Child;
            var header=(Grid)((Border)grid.Children[0]).Child;
            header.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseEnterEvent});
            Capture(fence,"unified-partition-preview.png");
            fence.ToggleSearchBar();Capture(fence,"unified-search-preview.png");fence.ToggleSearchBar();fence.Close();
            string loose=Path.Combine(root,"桌面便签.txt");File.WriteAllText(loose,"preview");
            var folderPanel=new Group(){Name="文件夹",Kind="文件夹",Width=620,Height=340,Folders=new List<string>{folder,pictures},FolderIndex=-1};
            folderPanel.Items.Add(loose);store.Data.Groups.Add(folderPanel);
            fence=new FenceWindow(controller,folderPanel){Left=-20000,Top=-20000,ShowActivated=false};fence.Show();
            Capture(fence,"unified-desktop-tabs-preview.png");fence.Close();
            group.Rolled=true;
            fence=new FenceWindow(controller,group){Left=-20000,Top=-20000,ShowActivated=false};fence.Show();
            grid=(Grid)((Border)fence.Content).Child;header=(Grid)((Border)grid.Children[0]).Child;
            header.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseEnterEvent});
            Capture(fence,"unified-folded-preview.png");
            return 0;
        }finally{if(fence!=null)fence.Close();manager.Quit();app.Shutdown();}
    }
    static void Capture(Window window,string name){
        window.UpdateLayout();
        var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name)))encoder.Save(stream);
    }
    static void PumpUntil(Func<bool> ready,int milliseconds){
        var end=DateTime.UtcNow.AddMilliseconds(milliseconds);
        while(!ready()&&DateTime.UtcNow<end){var frame=new DispatcherFrame();var timer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    }
}
}
