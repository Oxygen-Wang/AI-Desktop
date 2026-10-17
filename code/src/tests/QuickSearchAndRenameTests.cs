using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace QiDesk {
public static class QuickSearchAndRenameTests {
    public static int Verify(bool native){
        string root=Path.Combine(Path.GetTempPath(),"QiDesk-quick-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var lines=new System.Collections.Generic.List<string>();Application app=null;
        Action<bool,string> check=delegate(bool valid,string name){if(!valid)throw new Exception("FAILED: "+name);lines.Add("PASS: "+name);};
        try{
            Run(root,check);
            if(native){app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};Native(root,check);}
            return 0;
        }catch(Exception ex){lines.Add(ex.ToString());return 1;}
        finally{File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"quick-feature-results.txt"),lines);if(app!=null)app.Shutdown();}
    }
    public static void Run(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"quick-search");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"search-fixture.tmp");File.WriteAllText(file,"fixture");
        var store=new Store(Path.Combine(root,"quick-search-state"));store.Data.UseEverything=false;
        string portable=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tools","Everything-1.4.1.1032.x86","Everything.exe"));
        if(File.Exists(portable))check(String.Equals(FileSearch.Executable(store.Data),portable,StringComparison.OrdinalIgnoreCase),"repository portable Everything is discovered before installed copies");
        store.Data.WorkFolders.Add(folder);store.Data.IgnoreRules.Add(new IgnoreRule(){Kind="Git",Pattern="*.tmp"});
        check(!FileSearch.Query(store,"search-fixture",false,20,folder).Items.Any(),"existing scoped search still respects partition ignore policy");
        check(FileSearch.Query(store,"search-fixture",false,20,folder,false).Items.Any(r=>r.Path==file),"search-all includes ignored files from the same provider");
        var cancelled=new CancellationToken(true);bool stopped=false;
        try{FileSearch.Query(store,"search-fixture",false,20,folder,false,cancelled);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"cancelled quick searches stop before filesystem enumeration");
        check(Store.Json.Deserialize<State>("{\"Version\":8}").QuickSearchAll,"old configurations enable the new search shortcut by default");
        check(ManagerWindow.DescribeRule(new Rule(){Kind="文件",Extensions="pdf,docx",Contains="报告",NameMode="包含"})=="文件 · pdf,docx · 包含 报告","partition rules describe their extension and name conditions like ignore rules");
        store.Data.QuickSearchAll=false;store.Save();check(!new Store(store.Root).Data.QuickSearchAll,"disabled quick-search shortcut survives restart");
        var plugin=WidgetPlugins.Find(store.Data,"qidesk.search");
        check(plugin!=null&&plugin.Kind=="search"&&WidgetPlugins.SupportedKinds.Contains("search"),"desktop search is available through the component library");
        string renamed=Path.Combine(folder,"renamed.tmp");
        bool rejected=false;try{FileRenaming.ValidateName(file,"CON.txt");}catch(ArgumentException){rejected=true;}
        check(rejected,"inline renaming rejects Windows reserved device names");
        File.WriteAllText(renamed,"conflict");rejected=false;try{FileRenaming.ValidateName(file,"renamed.tmp");}catch(IOException){rejected=true;}
        check(rejected&&File.ReadAllText(renamed)=="conflict","inline rename collision validation preserves the existing file");
        var group=store.Data.Groups[0];group.Items.Add(file);store.Data.HiddenDesktopItems.Add(file);
        var pending=new PendingFileRename(){Source=file,Destination=Path.Combine(folder,"recovered.tmp")};
        store.Data.PendingFileRenames.Add(pending);store.PersistFileRecovery();File.Move(file,pending.Destination);
        var recovered=new Store(store.Root);
        check(recovered.Data.Groups[0].Items.Contains(pending.Destination)&&recovered.Data.HiddenDesktopItems.Contains(pending.Destination)&&recovered.Data.PendingFileRenames.Count==0,"restart reconciles renamed hidden files after an interrupted save");
        pending=new PendingFileRename(){Source=renamed,Destination=Path.Combine(folder,"not-moved.tmp")};recovered.Data.PendingFileRenames.Add(pending);
        FileRenaming.Recover(recovered.Data);check(recovered.Data.PendingFileRenames.Count==0&&File.Exists(renamed),"unperformed rename journal entries are safely discarded");
        pending=new PendingFileRename(){Source=renamed,Destination=Path.Combine(folder,"both.tmp")};File.WriteAllText(pending.Destination,"other");recovered.Data.PendingFileRenames.Add(pending);
        FileRenaming.Recover(recovered.Data);check(recovered.Data.PendingFileRenames.Count==1&&File.ReadAllText(renamed)=="conflict","ambiguous rename recovery keeps its journal and both files");
        recovered.Data.PendingFileRenames.Clear();
    }
    public static void Native(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"inline-native");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"before.txt"),conflict=Path.Combine(folder,"existing.txt");File.WriteAllText(file,"native fixture");File.WriteAllText(conflict,"existing");
        var store=new Store(Path.Combine(root,"inline-native-state"));store.Data.UseEverything=false;
        var group=new Group(){Name="Rename fixture",Folder=folder,Width=560,Height=320};store.Data.Groups.Clear();store.Data.Groups.Add(group);
        var controller=new Controller(store);var fence=new FenceWindow(controller,group){Left=-15000,Top=-15000};
        try{
            fence.Show();fence.UpdateLayout();int windows=Application.Current.Windows.Count;
            check(fence.BeginFileRename(file),"desktop partition opens its filename editor in place");
            var editor=Find<InlineFileName>(fence).First(x=>x.Editing);var input=editor.Input;
            check(input.Text=="before.txt"&&input.SelectedText=="before","inline rename selects the stem and preserves the visible extension");
            fence.Refresh();check(editor.Input==input,"periodic partition refresh preserves an active rename editor");
            input.Text="existing.txt";editor.Commit();
            check(editor.Editing&&File.ReadAllText(file)=="native fixture"&&File.ReadAllText(conflict)=="existing"&&Application.Current.Windows.Count==windows,"rename errors stay inline without opening a modal dialog");
            input.Text="after.txt";editor.Commit();
            check(!File.Exists(file)&&File.ReadAllText(Path.Combine(folder,"after.txt"))=="native fixture"&&!editor.Editing,"Enter commits the inline file rename through native Shell operations");
            check(fence.BeginFileRename(conflict),"another file can be renamed after the first editor closes");
            editor=Find<InlineFileName>(fence).First(x=>x.Editing);editor.Input.Text="cancelled.txt";editor.Cancel();
            check(File.Exists(conflict)&&!File.Exists(Path.Combine(folder,"cancelled.txt")),"Escape-style cancellation leaves the real file untouched");
        }finally{fence.Close();}
        string managed=Path.Combine(folder,"managed.txt"),original=Path.Combine(folder,"restored","managed.txt");Directory.CreateDirectory(Path.GetDirectoryName(original));File.WriteAllText(managed,"restore me");
        store.Data.Groups[0].Items.Add(managed);store.Data.ManagedDesktopItems[managed]=original;store.PersistFileRecovery();
        store.Checkpoint();store.Data.Groups[0].Name="Changed layout";
        var owner=new Window(){Width=200,Height=100,Left=-15000,Top=-15000};owner.Show();
        try{
            string moved=FileRenaming.Rename(store,owner,managed,"new-name.txt");
            check(store.Data.ManagedDesktopItems.ContainsKey(moved)&&store.Data.ManagedDesktopItems[moved]==Path.Combine(Path.GetDirectoryName(original),"new-name.txt"),"managed file rename updates both stored and restored desktop paths");
            store.Undo();
            check(store.Data.Groups[0].Name!="Changed layout"&&store.Data.Groups[0].Items.Contains(moved)&&store.Data.ManagedDesktopItems.ContainsKey(moved),"undoing a layout after renaming retains real file paths and recovery mappings");
            DesktopOrganization.Restore(store.Data);
            check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(original),"new-name.txt"))=="restore me"&&store.Data.ManagedDesktopItems.Count==0,"renamed managed file restores to its desktop destination");
        }finally{owner.Close();}
        string popupFolder=Path.Combine(root,"popup-search");Directory.CreateDirectory(popupFolder);
        string popupHit=Path.Combine(popupFolder,"popup-target.txt");File.WriteAllText(popupHit,"popup hit");
        store.Data.WorkFolders.Add(popupFolder);
        var popup=new QuickSearchWindow(controller);var second=new Window(){Width=200,Height=100,Left=-15000,Top=-15000};
        try{
            popup.Present();Pump(100);
            check(popup.Height<90&&popup.Search.Input.IsKeyboardFocused,"quick-search popup starts as a compact focused search bar");
            popup.Search.Input.Text="popup-target";
            check(popup.Search.ResultCount==0,"typing alone keeps the panel empty until Enter is pressed");
            popup.Search.Input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(popup),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            PumpUntil(()=>popup.Search.ResultCount>0,10000);
            check(popup.Search.ResultPaths.Contains(popupHit),"pressing Enter runs the search and lists the matching file");
            popup.Search.Reset();
            popup.Search.Input.Text="before";
            popup.Search.Present(new SearchResponse(){Provider="Fixture",Message="fixture results",Items=new[]{new SearchResult(){Path=conflict,Changed=DateTime.Today}}});
            check(popup.Height>300&&popup.Search.ResultCount==1,"search results expand the same compact window");
            popup.UpdateLayout();
            check(popup.Search.ResultPaths.Contains(conflict)&&Find<Border>(popup.Search).Any(border=>border.Tag is SearchResult),"search results reuse the desktop partition tile for each file");
            var grip=Find<Thumb>(popup).FirstOrDefault(thumb=>Convert.ToString(thumb.Tag)=="qidesk-search-resize");
            check(grip!=null,"search panel exposes a resize grip like desktop partitions");
            double narrow=popup.Width,tall=popup.Height;
            grip.RaiseEvent(new DragDeltaEventArgs(60,40){RoutedEvent=Thumb.DragDeltaEvent});
            check(popup.Width>narrow&&popup.Height>tall,"dragging the grip resizes the search results panel");
            var popupFrame=Find<Border>(popup).FirstOrDefault(border=>Object.ReferenceEquals(border.Child,popup.Search));
            check(popupFrame!=null&&popupFrame.CornerRadius.TopLeft>0,"search popup uses rounded corners");
            popup.Search.Input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(popup),0,Key.LeftCtrl){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            check(popup.Search.PreviewVisible,"holding Ctrl opens a larger selected-file preview");
            popup.Search.Input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(popup),0,Key.LeftCtrl){RoutedEvent=Keyboard.PreviewKeyUpEvent});
            check(!popup.Search.PreviewVisible,"releasing Ctrl closes the file preview");
            popup.Search.Reset();check(popup.Search.ResultCount==0&&popup.Height<90,"clearing the query collapses stale search results");
            second.Show();second.Activate();Pump(100);check(!popup.IsVisible,"quick-search popup hides when another window takes focus");
            popup.Present();popup.Search.Input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(popup),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            check(!popup.IsVisible,"Escape closes the search popup without closing desktop partitions");
        }finally{second.Close();popup.Close();}
        var widget=WidgetPlugins.Create(store.Data,"qidesk.search",new[]{new Rect(0,0,1920,1040)});
        var widgetWindow=new WidgetWindow(controller,widget){Left=-15000,Top=-15000};
        try{widgetWindow.Show();widgetWindow.UpdateLayout();check(Find<QuickSearchView>(widgetWindow).Count()==1,"desktop search plugin hosts the same search control as the shortcut popup");check(((Border)widgetWindow.Content).CornerRadius.TopLeft>0,"desktop search plugin defaults to rounded corners");}finally{widgetWindow.Close();}
        var manager=new ManagerWindow(controller){Left=-15000,Top=-15000};
        try{
            manager.Show();manager.ShowQuickSearch();check(manager.QuickSearchVisible,"manager opens the dedicated search bar");
            manager.ShowQuickSearch(true);check(!manager.QuickSearchVisible,"repeating the quick-search command toggles the same bar closed");
        }finally{manager.Quit();}
        var tabStore=new Store(Path.Combine(root,"tab-close-state"));tabStore.Data.UseEverything=false;
        var tabGroup=new Group(){Name="TabClose",Width=360,Height=240};tabStore.Data.Groups.Clear();tabStore.Data.Groups.Add(tabGroup);
        string tabKeep=Path.Combine(root,"tab-keep.txt");File.WriteAllText(tabKeep,"keep");tabGroup.Items.Add(tabKeep);
        var tabController=new Controller(tabStore);var tabManager=new ManagerWindow(tabController){Left=-15000,Top=-15000};var tabFence=new FenceWindow(tabController,tabGroup){Left=-15000,Top=-15000};tabController.Windows.Add(tabFence);
        try{
            tabFence.Show();tabFence.UpdateLayout();
            var selfTab=Find<Border>(tabFence).FirstOrDefault(border=>Convert.ToString(border.ToolTip).Contains("双击改名"));
            var closeGlyph=selfTab==null?null:Find<TextBlock>(selfTab).FirstOrDefault(text=>text.Text=="×");
            var closeBorder=closeGlyph==null?null:VisualTreeHelper.GetParent(closeGlyph) as Border;
            check(selfTab!=null&&closeBorder!=null,"the partition's own tab has a folder-style close button");
            selfTab.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.MouseEnterEvent});
            check(closeBorder.Opacity==1,"hovering the partition tab reveals its close button");
            closeBorder.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
            Pump(180);
            check(!tabStore.Data.Groups.Contains(tabGroup)&&File.Exists(tabKeep),"clicking the partition tab close button deletes the partition and keeps the real files");
        }finally{tabFence.Close();tabManager.Quit();}
        try{
            var indexed=EverythingSdk.Query(store.Data,"\""+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"notepad.exe")+"\"",false,10,CancellationToken.None);
            check(indexed.All(x=>x.Path.Length>0),"live Everything SDK replies contain valid result paths");
        }catch(Exception ex){
            if(!(ex is DllNotFoundException||ex is IOException||ex is TimeoutException||ex is BadImageFormatException))throw;
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"everything-sdk-check.txt"),"Unavailable: "+ex.Message);
        }
    }
    static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject {
        if(root is T)yield return (T)root;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Find<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static void PumpUntil(Func<bool> done,int milliseconds){var end=DateTime.UtcNow.AddMilliseconds(milliseconds);while(!done()&&DateTime.UtcNow<end)Pump(25);if(!done())throw new Exception("Quick search UI timed out.");}
    public static int Render(){
        string root=Path.Combine(Path.GetTempPath(),"QiDesk-search-preview-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};var store=new Store(root);var controller=new Controller(store);
        string file=Path.Combine(root,"项目需求说明.pdf");File.WriteAllText(file,"fixture");
        string picture=Path.Combine(root,"界面设计.png");
        var pixels=new byte[72*72*4];
        for(int y=0;y<72;y++)for(int x=0;x<72;x++){int i=(y*72+x)*4;pixels[i]=(byte)(80+x*2);pixels[i+1]=(byte)(110+y);pixels[i+2]=195;pixels[i+3]=255;}
        var bitmap=BitmapSource.Create(72,72,96,96,PixelFormats.Bgra32,null,pixels,72*4);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(picture))encoder.Save(stream);
        var popup=new QuickSearchWindow(controller);FenceWindow fence=null;WidgetWindow widget=null;
        try{
            popup.Present();Capture(popup,"quick-search-idle.png");
            popup.Search.Input.Text="需求";popup.Search.Present(new SearchResponse(){Provider="Everything",Message="索引全部文件",Items=new[]{new SearchResult(){Path=file,Changed=DateTime.Today},new SearchResult(){Path=picture,Changed=DateTime.Today}}});popup.UpdateLayout();Thumbnails.RequestVisible(popup);Pump(300);Capture(popup,"quick-search-results.png");popup.Hide();
            var group=new Group(){Name="文档",Width=560,Height=320};group.Items.Add(file);store.Data.Groups.Clear();store.Data.Groups.Add(group);
            fence=new FenceWindow(controller,group){Left=-15000,Top=-15000};fence.Show();fence.BeginFileRename(file);Capture(fence,"inline-rename-preview.png");
            var item=WidgetPlugins.Create(store.Data,"qidesk.search",new[]{new Rect(0,0,1920,1040)});widget=new WidgetWindow(controller,item){Left=-15000,Top=-15000};widget.Show();Capture(widget,"search-widget-preview.png");
            return 0;
        }finally{popup.Close();if(fence!=null)fence.Close();if(widget!=null)widget.Close();app.Shutdown();}
    }
    static void Capture(Window window,string name){window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name)))png.Save(stream);}
}
}
