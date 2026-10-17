using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace QiDesk {
public static class WorkspacePanelTests {
    static string Describe(Brush brush){
        if(brush==null)return "null";
        var solid=brush as SolidColorBrush;
        if(solid!=null)return "solid:"+solid.Color.ToString();
        var gradient=brush as LinearGradientBrush;
        if(gradient!=null)return "gradient:"+String.Join(",",gradient.GradientStops.Select(stop=>stop.Color.ToString()).ToArray());
        return brush.GetType().Name;
    }
    public static void Run(string root,Action<bool,string> check){
        string project=Path.Combine(root,"panel-project"),nested=Path.Combine(project,"docs");
        Directory.CreateDirectory(nested);
        string report=Path.Combine(project,"report.txt"),draft=Path.Combine(nested,"report-draft.md"),notes=Path.Combine(project,"notes.md");
        File.WriteAllText(report,"report");File.WriteAllText(draft,"draft");File.WriteAllText(notes,"notes");
        var state=new State();
        var hits=PanelFiles.Search(state,project,"report");
        check(hits.Length==2&&hits.Any(p=>p==report)&&hits.Any(p=>p==draft),"workspace panel searches file names in subfolders");
        check(PanelFiles.Relative(project,draft)==Path.Combine("docs","report-draft.md"),"search results show the path relative to the panel root");
        check(PanelFiles.Search(state,project,"REPORT").Length==2,"workspace panel search ignores letter case");
        state.IgnoreRules.Add(new IgnoreRule(){Pattern="report.txt"});
        check(PanelFiles.Search(state,project,"report").Length==1&&!PanelFiles.Search(state,project,"report").Any(p=>p==report),"workspace panel search honours the existing global ignore rules");
        check(PanelFiles.IsIgnoredPath(state,report)&&!PanelFiles.IsIgnoredPath(state,draft),"ignore state is recognised per item");
        var rule=PanelFiles.MakeIgnoreRule(draft);
        check(rule.Kind=="路径"&&rule.Pattern==IgnoreRule.Canonical(draft)&&rule.Enabled,"dropping on the ignore icon creates an enabled exact-path rule");
        string sameName=Path.Combine(project,"report-draft.md");File.WriteAllText(sameName,"keep");
        check(!rule.Matches(sameName),"drag ignore does not hide a same-named file elsewhere");
        IgnoreRule.Validate(new List<IgnoreRule>{rule});
        var tab=PanelTab.OfFolder(project);tab.IgnoreRules.Add(PanelFiles.MakeIgnoreRule(notes));
        var persisted=Store.Json.Deserialize<PanelTab>(Store.Json.Serialize(tab));
        check(persisted.IgnoreRules.Count==1&&PanelFiles.Search(new State(),project,"notes",persisted.IgnoreRules).Length==0&&PanelFiles.Search(new State(),project,"notes").Length==1,"each folder tab keeps its own ignore rules across serialization");
        var folderGroup=new Group(){Kind="文件夹",Folder=project,Folders=new List<string>{project}};
        folderGroup.FolderIgnoreRules[project]=persisted.IgnoreRules;
        Directory.CreateDirectory(Path.Combine(root,"scoped-panel"));var scopedStore=new Store(Path.Combine(root,"scoped-panel"),false);scopedStore.Data=new State();scopedStore.Data.Groups.Add(folderGroup);Store.Validate(scopedStore.Data);
        check(!scopedStore.ItemsAt(folderGroup,"",project).Contains(notes)&&scopedStore.ItemsAt(folderGroup,"",project).Contains(report),"pinned folder partition applies only that folder tab's ignore rules");
        var references=new State();var mapped=new Group(){Kind="文件夹",Folder=project,Folders=new List<string>{project}};
        mapped.FolderIgnoreRules[project]=new List<IgnoreRule>{PanelFiles.MakeIgnoreRule(notes)};references.Groups.Add(mapped);
        var mappedTab=PanelTab.OfFolder(project);mappedTab.IgnoreRules.Add(PanelFiles.MakeIgnoreRule(notes));references.PanelTabs.Add(mappedTab);
        string relocated=project+"-moved";ShellActions.UpdateReferences(references,project,relocated,true);
        check(mapped.FolderIgnoreRules.ContainsKey(relocated)&&mapped.FolderIgnoreRules[relocated][0].Pattern==Path.Combine(relocated,"notes.md")&&mappedTab.IgnoreRules[0].Pattern==Path.Combine(relocated,"notes.md"),"renaming a folder updates its scoped ignore rules and panel tab rules");
        var selectedController=new Controller(scopedStore);
        selectedController.ToggleGroupSelection(folderGroup);
        check(new Store(scopedStore.Root).Data.SelectedWorkspaceGroups.Contains(folderGroup.Id),"selected workspaces persist for Alt+Space across restarts");
        selectedController.ClearGroupSelection();
        check(new Store(scopedStore.Root).Data.SelectedWorkspaceGroups.Count==0&&scopedStore.Data.Groups.Contains(folderGroup),"clearing workspace selection leaves desktop partitions intact");
        File.WriteAllText(Path.Combine(project,"copy.txt"),"copy");
        check(Path.GetFileName(PanelFiles.Unique(project,"copy.txt"))=="copy (2).txt","duplicate names keep both files");
        check(PanelFiles.CanPasteInto(new[]{report},nested)&&!PanelFiles.CanPasteInto(new[]{report},project),"dragging rejects dropping a file onto its own folder");
        check(!PanelFiles.CanPasteInto(new[]{project},nested),"dragging rejects moving a folder into its own subtree");
        // 面板打开的文件夹要读它自己的 .gitignore / .cursorignore，和 PathCat 一致。
        string ignoredFile=Path.Combine(project,"gitignored.txt"),keptFile=Path.Combine(project,"kept.txt");
        File.WriteAllText(ignoredFile,"ignored");File.WriteAllText(keptFile,"kept");
        File.WriteAllText(Path.Combine(project,".gitignore"),"gitignored.txt\n");
        var panelState=new State();panelState.PanelTabs.Add(PanelTab.OfFolder(project));
        check(IgnoreFiles.Roots(panelState).Contains(project),"panel folders join the ignore-file roots");
        check(IgnoreFiles.Sync(panelState),"opening a folder in the panel imports its .gitignore");
        check(panelState.IgnoreFiles.Any(s=>s.Automatic&&String.Equals(s.Root,project,StringComparison.OrdinalIgnoreCase)),"the imported rule set is scoped to the panel folder");
        var panelStore=new Store(Path.Combine(root,"panel-ignore-state"),false);panelStore.Data=panelState;
        check(panelStore.Ignored(ignoredFile,null)&&!panelStore.Ignored(keptFile,null),"panel folders honour .gitignore while keeping other files visible");
        check(!PanelFiles.Search(panelState,project,"txt").Any(p=>p==ignoredFile),"workspace panel search skips files ignored by .gitignore");
        var editable=panelState.IgnoreFiles.First(s=>s.Automatic&&s.Root==project);
        editable.Lines[0]="kept.txt";editable.Automatic=false;editable.Revision="manual";
        File.WriteAllText(Path.Combine(project,".gitignore"),"gitignored.txt\nchanged.txt\n");
        IgnoreFiles.Sync(panelState);
        check(editable.Lines.SequenceEqual(new[]{"kept.txt"})&&IgnoreFiles.Matches(editable,keptFile)&&!IgnoreFiles.Matches(editable,ignoredFile),"editing an imported rule keeps a separate local copy when the source file changes");
        panelState.PanelTabs.Clear();
        IgnoreFiles.Sync(panelState);
        check(!panelState.IgnoreFiles.Any(s=>s.Automatic)&&panelState.IgnoreFiles.Contains(editable),"closing a panel tab removes automatic imports but retains the user's edited local copy");
        var clamped=new State();clamped.PetScale=99;clamped.PanelIconSize=5000;clamped.PanelIgnoreIcon="";clamped.PanelWidth=20;clamped.PanelHeight=20;clamped.PanelViewMode="乱写";
        Store.Validate(clamped);
        check(clamped.PetScale==2&&clamped.PanelIconSize==96&&clamped.PanelIgnoreIcon=="⊘"&&clamped.PanelWidth==340&&clamped.PanelHeight==260&&clamped.PanelViewMode=="图标"&&clamped.PanelFolders!=null,"panel and pet preferences are validated on load");
        var inherited=new State(){Version=8};Store.Validate(inherited);
        check(inherited.PetScale==1&&inherited.PetVisible&&inherited.PanelIndex==-1,"older configurations default to a visible 100% cat and no folder tabs");
        // 上一版把标签存成纯文件夹列表；升级后要能读出来。
        var legacy=new State();legacy.PanelFolders.Add(project);legacy.PanelFolders.Add(nested);Store.Validate(legacy);
        check(legacy.PanelTabs.Count==2&&legacy.PanelTabs.All(t=>t.IsFolder)&&legacy.PanelFolders.Count==0,"older folder-only panel tabs migrate into typed tabs");
        // 外观字段的默认值与容错。
        var style=new Group();Store.NormalizeStyle(style);
        check(style.CornerStyle=="方角"&&style.BackgroundStyle=="跟随颜色"&&style.ImageMode=="填充"&&style.BackgroundImage=="","new partitions default to square corners with the colour-following background");
        var broken=new Group(){BackgroundStyle="乱写",ImageMode="乱写",Color="",LabelColor="",IconColor="",IconSkin="乱写",CornerStyle="乱写"};
        Store.NormalizeStyle(broken);
        check(broken.BackgroundStyle=="跟随颜色","an unknown background style falls back to following the partition colour");
        check(broken.ImageMode=="填充","an unknown image fill mode falls back to fill");
        check(broken.Color=="#77BFA3","an empty partition colour falls back to the default");
        check(broken.LabelColor=="#E8EDF6","an empty label colour falls back to the default");
        check(broken.IconColor=="#92AAE5","an empty icon colour falls back to the default");
        check(broken.IconSkin=="经典","an unknown icon skin falls back to the classic skin");
        check(broken.CornerStyle=="圆角","an unknown corner style falls back to rounded corners");
        // 桌面分区的底色公式不能被这一轮改坏。
        string[] kinds={"跟随颜色","纯色","渐变","毛玻璃","图片"};
        check(kinds.SequenceEqual(Store.BackgroundStyles),"the five background styles stay stable");
        string plain=Describe(Appearance.FrameBackground(new Group(){BackgroundStyle="跟随颜色",Color="#77BFA3",TintStrength=.55},.8));
        string legacyBrush=Describe(Appearance.Background("#77BFA3",.8,.55));
        check(plain==legacyBrush,"the default partition background keeps the original formula");
        foreach(string kind in kinds){var brush=Appearance.FrameBackground(new Group(){BackgroundStyle=kind,Color="#80B8E8",TintStrength=.6,BackgroundImage=""},.8);check(brush!=null,"background style "+kind+" produces a usable brush");}
        // 缩放阶梯：大图标 → 中 → 小 → 列表 → 可折叠树。
        check(WorkspacePanel.ZoomLadder.Length==9&&WorkspacePanel.ZoomLadder[0]=="图标:96"&&WorkspacePanel.ZoomLadder[8]=="树","the zoom ladder runs from large icons down to the collapsible tree");
        var ladder=new State();WorkspacePanel.ApplyLevel(ladder,"图标:96");check(WorkspacePanel.LadderIndex(ladder.PanelViewMode,ladder.PanelIconSize)==0,"ladder index starts at the largest icon size");
        WorkspacePanel.ApplyLevel(ladder,"树");check(ladder.PanelViewMode=="树"&&WorkspacePanel.LadderIndex("树",ladder.PanelIconSize)==8,"the ladder ends in tree mode");
        VectorCatTests.Run(root,check);
        UnifiedWorkspaceTests.Run(root,check);
    }
    static void Pump(int milliseconds){
        var end=DateTime.UtcNow.AddMilliseconds(milliseconds);
        while(DateTime.UtcNow<end){
            var frame=new DispatcherFrame();
            var timer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(20)};
            timer.Tick+=delegate{timer.Stop();frame.Continue=false;};
            timer.Start();Dispatcher.PushFrame(frame);
        }
    }
    static void PumpUntil(Func<bool> done,int milliseconds){
        var end=DateTime.UtcNow.AddMilliseconds(milliseconds);
        while(!done()&&DateTime.UtcNow<end)Pump(25);
        if(!done())throw new Exception("Workspace panel UI timed out.");
    }
    public static void Native(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"panel-ui"));store.Data.UseEverything=false;
        string first=Path.Combine(root,"panel-alpha"),second=Path.Combine(root,"panel-beta");
        Directory.CreateDirectory(first);Directory.CreateDirectory(second);
        Directory.CreateDirectory(Path.Combine(first,"子文件夹"));
        string alpha=Path.Combine(first,"alpha-note.txt"),hidden=Path.Combine(first,"alpha-secret.txt"),beta=Path.Combine(second,"beta-note.txt");
        File.WriteAllText(alpha,"alpha");File.WriteAllText(hidden,"secret");File.WriteAllText(beta,"beta");
        File.WriteAllText(Path.Combine(first,"子文件夹","alpha-deep.md"),"deep");
        var controller=new Controller(store);var manager=new ManagerWindow(controller);var panel=manager.ShowWorkspacePanel(false);
        try{
            panel.Present(true);
            check(panel.FolderTabs.Length==0,"a fresh workspace panel starts without folder tabs");
            panel.AddFolders(new[]{first});
            check(panel.FolderTabs.Length==1&&String.Equals(panel.CurrentFolder,first,StringComparison.OrdinalIgnoreCase),"dropping a folder opens it as the first folder tab");
            panel.AddFolders(new[]{first});
            check(panel.FolderTabs.Length==1,"adding the same folder twice keeps a single tab");
            panel.AddFolders(new[]{second});
            check(panel.FolderTabs.Length==2&&String.Equals(panel.CurrentFolder,second,StringComparison.OrdinalIgnoreCase),"a second folder becomes its own active tab");
            check(panel.VisiblePaths.Contains(beta),"the panel lists the real files of the active folder");
            panel.AddFolders(new[]{first});
            store.Data.IgnoreRules.Add(new IgnoreRule(){Pattern="alpha-secret.txt"});
            panel.RefreshNow();
            check(panel.VisiblePaths.Contains(alpha)&&!panel.VisiblePaths.Contains(hidden),"ignored files disappear from the folder panel without touching the disk");
            check(File.Exists(hidden),"ignoring a file never deletes it");
            panel.ToggleSearchBar();panel.SearchInput.Text="alpha";
            PumpUntil(()=>panel.Summary.Contains("名称搜索「alpha」"),8000);
            check(panel.SearchBarVisible&&panel.VisiblePaths.Length==2&&panel.VisiblePaths.Contains(alpha)&&panel.VisiblePaths.Any(p=>p.EndsWith("alpha-deep.md")),"Ctrl+F search finds names in subfolders and lists them in the panel");
            check(panel.VisiblePaths.All(Store.Exists),"every search result exists on disk");
            panel.SearchInput.Text="不存在的名称";
            PumpUntil(()=>panel.VisiblePaths.Length==0&&panel.Summary.Contains("名称搜索"),8000);
            check(panel.Summary.Contains("0 项"),"an empty search reports zero matches instead of failing");
            panel.ToggleSearchBar();
            check(!panel.SearchBarVisible&&panel.VisiblePaths.Length==2,"closing the search restores the folder listing");
            // 面板打开的真实文件夹被重命名后，引用要跟着更新，磁盘文件不能丢。
            string renamed=first+"-renamed";Directory.Move(first,renamed);
            ShellActions.UpdateReferences(store.Data,first,renamed,true);
            check(store.Data.PanelTabs.Any(t=>t.IsFolder&&String.Equals(t.Folder,renamed,StringComparison.OrdinalIgnoreCase)),"renaming a panel folder keeps the tab pointing at the real path");
            check(File.Exists(Path.Combine(renamed,"alpha-note.txt")),"renaming the panel folder leaves its files in place");
            panel.Present(true);panel.RefreshNow();
            // Ctrl＋滚轮缩放：一路缩到可折叠树。
            for(int i=0;i<6;i++)panel.ZoomBy(-1);
            check(store.Data.PanelViewMode=="树"&&panel.TreeVisible,"zooming all the way out switches the panel to the collapsible tree");
            check(panel.VisibleNames.Contains("子文件夹")&&panel.VisibleNames.Contains("alpha-note.txt"),"the tree lists the folder and its files");
            check(!panel.VisibleNames.Contains("alpha-deep.md"),"collapsed tree rows hide their children");
            panel.ExpandRow(Path.Combine(renamed,"子文件夹"));
            check(panel.VisibleNames.Contains("alpha-deep.md"),"expanding a tree row loads its children");
            panel.ZoomBy(1);
            check(store.Data.PanelViewMode=="列表"&&!panel.TreeVisible,"zooming back in leaves the tree for the list view");
            store.Data.PanelViewMode="图标";panel.RefreshNow();
            // 钉到桌面：面板变回一个真正的分区。
            bool wasVisible=panel.IsVisible;
            panel.PinToDesktop();
            var pinned=store.Data.Groups.FirstOrDefault(g=>g.Kind=="文件夹"&&g.Folders.Any(f=>String.Equals(f,renamed,StringComparison.OrdinalIgnoreCase)));
            check(pinned!=null&&pinned.Folders.Count==2&&pinned.CornerStyle=="方角","pinning turns the panel into a real desktop folder partition with all its tabs");
            check(!panel.IsVisible&&wasVisible,"pinning closes the floating panel");
            check(store.Data.PanelTabs.Count==0,"pinning empties the floating panel tabs");
            // 分区弹成面板：桌面分区接进面板当标签。
            var host=new Group(){Name="分区甲",Page=store.Data.Page};store.Data.Groups.Add(host);
            host.Items.Add(Path.Combine(second,"beta-note.txt"));
            panel.Present(true);panel.OpenGroups(new[]{host.Id});
            check(panel.GroupTabs().Contains(host.Id)&&panel.CurrentGroup==host,"a desktop partition pops into the panel as a group tab");
            check(panel.VisiblePaths.Contains(Path.Combine(second,"beta-note.txt")),"the group tab lists the partition's own items");
            controller.ToggleGroupSelection(host);
            check(controller.SelectedGroups.Contains(host.Id),"Ctrl+click selection records the desktop partition");
            controller.ToggleGroupSelection(host);
            check(controller.SelectedGroups.Count==0,"clicking a selected partition again clears the selection");
            // The pet now uses only desktop partitions. Legacy-panel tests above remain
            // migration coverage and do not expose a second panel through the pet.
            panel.Dismiss();store.Data.PanelTabs.Clear();
            controller.Running=true;
            var pet=new DesktopPet(controller);
            try{
                check(controller.SelectedGroups.Count==0,"the workspace selection starts empty before the pet click");
                pet.ClickPanel();Pump(60);
                check(!manager.WorkspacePanelVisible,"clicking the pet without selection never creates an extra workspace panel");
                var fenceGroup=new Group(){Name="小猫测试分区",Page=store.Data.Page};
                store.Data.Groups.Add(fenceGroup);
                var fence=new FenceWindow(controller,fenceGroup);
                controller.Windows.Add(fence);
                try{
                    controller.ToggleGroupSelection(fenceGroup);
                    controller.ToggleSelectedWorkspace();
                    check(fence.IsVisible&&!fence.Topmost&&controller.WorkspaceVisible,"the selected partition starts raised in front of the desktop");
                    fence.MiddleClick();Pump(60);
                    check(!fence.IsVisible&&!controller.WorkspaceVisible,"middle click collapses the raised workspace like a folder");
                    pet.ClickPanel();Pump(60);
                    check(!manager.WorkspacePanelVisible,"clicking the pet with a selection never opens the workspace panel");
                    check(fence.IsVisible&&!fence.Topmost&&controller.WorkspaceVisible,"clicking the pet raises the collapsed partition back to the front");
                    pet.ClickPanel();Pump(60);
                    check(fence.IsVisible&&!fence.Topmost&&!controller.WorkspaceVisible,"clicking the pet again returns the raised partition to the desktop without hiding it");
                }finally{controller.Windows.Remove(fence);fence.Close();controller.ClearGroupSelection();}
                store.Data.Groups.Remove(fenceGroup);
            }finally{pet.Close();}
            panel.Dismiss();check(!panel.IsVisible,"Escape or the close button hides the floating workspace panel");
        }finally{panel.Close();if(manager.WorkspacePanelVisible)manager.ShowWorkspacePanel(true);manager.Quit();}
    }
    public static int Render(){
        string output=AppDomain.CurrentDomain.BaseDirectory,root=Path.Combine(output,"panel-preview-state");
        var app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var store=new Store(root);store.Data.UseEverything=false;
        string project=Path.Combine(root,"栖桌项目");Directory.CreateDirectory(Path.Combine(project,"src"));
        File.WriteAllText(Path.Combine(project,"README.md"),"preview");
        File.WriteAllText(Path.Combine(project,"PathCat 参考.md"),"preview");
        File.WriteAllText(Path.Combine(project,"面板设计.txt"),"preview");
        File.WriteAllText(Path.Combine(project,"src","Panel.cs"),"preview");
        Directory.CreateDirectory(Path.Combine(project,"assets"));
        var controller=new Controller(store);var manager=new ManagerWindow(controller);var panel=manager.ShowWorkspacePanel(false);
        DesktopPet pet=null;
        try{
            panel.AddFolders(new[]{project});
            panel.Present(true);panel.RefreshNow();
            panel.UpdateLayout();Thumbnails.RequestVisible(panel);Thumbnails.Drain(2500);panel.UpdateLayout();
            Capture(panel,Path.Combine(output,"workspace-panel-preview.png"));
            panel.ToggleSearchBar();panel.SearchInput.Text="面板";
            var end=DateTime.UtcNow.AddSeconds(8);
            while(panel.VisiblePaths.Length!=2&&DateTime.UtcNow<end)Pump(50);
            panel.UpdateLayout();Thumbnails.RequestVisible(panel);Thumbnails.Drain(2000);panel.UpdateLayout();
            Capture(panel,Path.Combine(output,"workspace-panel-search-preview.png"));
            panel.ToggleSearchBar();
            store.Data.PanelViewMode="树";panel.RefreshNow();
            panel.ExpandRow(Path.Combine(project,"src"));
            panel.UpdateLayout();Thumbnails.RequestVisible(panel);Thumbnails.Drain(1500);panel.UpdateLayout();
            Capture(panel,Path.Combine(output,"workspace-panel-tree-preview.png"));
            store.Data.PanelViewMode="图标";
            store.Data.PanelStyle.BackgroundStyle="毛玻璃";panel.RefreshNow();
            panel.UpdateLayout();Thumbnails.Drain(500);panel.UpdateLayout();
            Capture(panel,Path.Combine(output,"workspace-panel-acrylic-preview.png"));
            store.Data.PanelStyle.BackgroundStyle="跟随颜色";panel.RefreshNow();
            pet=new DesktopPet(controller){Left=-20000,Top=-20000,ShowActivated=false,Topmost=false};
            pet.Show();pet.UpdateLayout();Pump(600);pet.UpdateLayout();
            Capture(pet,Path.Combine(output,"desktop-pet-preview.png"));
            pet.ApplySkin("毛绒三花");Pump(240);pet.UpdateLayout();
            Capture(pet,Path.Combine(output,"desktop-pet-calico-preview.png"));
            pet.ApplyScale(2);Pump(120);pet.UpdateLayout();
            Capture(pet,Path.Combine(output,"desktop-pet-calico-200-preview.png"));
            pet.ApplyScale(1);
            pet.ApplySkin("矢量猫");Pump(240);pet.UpdateLayout();
            Capture(pet,Path.Combine(output,"desktop-pet-vector-preview.png"));
            var pinned=new Group(){Name="文件夹",Kind="文件夹",Folder=project,Folders=new List<string>{project,Path.Combine(project,"src")},Width=700,Height=420,FolderIndex=0};
            var child=new Group(){Name="文档",HostGroupId=pinned.Id};store.Data.Groups.Add(pinned);store.Data.Groups.Add(child);
            var fence=new FenceWindow(controller,pinned){Left=-20000,Top=-20000,ShowActivated=false};
            try{fence.Show();fence.UpdateLayout();Thumbnails.RequestVisible(fence);Thumbnails.Drain(1000);fence.UpdateLayout();Capture(fence,Path.Combine(output,"pinned-workspace-preview.png"));}finally{fence.Close();}
            return 0;
        }finally{if(pet!=null)pet.Close();panel.Close();manager.Quit();app.Shutdown();}
    }
    static void Capture(Window window,string path){
        window.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Max(1,window.ActualWidth),(int)Math.Max(1,window.ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using(var stream=File.Create(path))png.Save(stream);
    }
}
}
