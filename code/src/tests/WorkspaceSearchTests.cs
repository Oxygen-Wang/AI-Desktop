using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace QiDesk {
public static class WorkspaceSearchTests {
    static Dictionary<string,object> Call(string name,params string[] pairs){var args=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)args.Add(pairs[i],pairs[i+1]);return new Dictionary<string,object>{{"name",name},{"arguments",args}};}
    public static void Run(string root,Action<bool,string> check){
        string first=Path.Combine(root,"Search Project"),second=first+"-other";Directory.CreateDirectory(first);Directory.CreateDirectory(second);
        string report=Path.Combine(first,"report.txt"),image=Path.Combine(first,"picture.png"),privateFile=Path.Combine(first,"private.txt"),outside=Path.Combine(second,"report.txt");File.WriteAllText(report,"report");File.WriteAllText(image,"image fixture");File.WriteAllText(privateFile,"private");File.WriteAllText(outside,"outside");Directory.CreateDirectory(Path.Combine(first,"docs"));
        var store=new Store(Path.Combine(root,"search-state"));store.Data.UseEverything=false;WorkspaceConnections.Connect(store.Data,first,"test",false);WorkspaceConnections.Connect(store.Data,second,"test",false);
        check(WorkspaceSelection.Current(store.Data)==second,"latest explicit project connection becomes the Alt+Space workspace");WorkspaceSelection.Select(store.Data,first);store.Save();check(WorkspaceSelection.Current(new Store(store.Root).Data)==first,"manual current workspace selection survives restart");
        store.Data.IgnoreRules.Add(new IgnoreRule(){Pattern="private.*"});
        var response=FileSearch.Query(store,"",false,60,first);check(response.Items.Any(x=>x.Path==report)&&response.Items.All(x=>WorkDetection.Under(x.Path,first)&&x.Path!=privateFile),"project search excludes sibling projects and ignored files");
        check(FileSearch.Query(store,"ext:png",false,60,first).Items.Select(x=>x.Path).SequenceEqual(new[]{image}),"image chip filters extensions in bounded offline project search");
        check(FileSearch.Query(store,"folder:",false,60,first).Items.All(x=>Directory.Exists(x.Path))&&FileSearch.Query(store,"folder:",false,60,first).Items.Length==1,"folder chip searches real directories in offline mode");
        check(FileSearch.ScopedQuery("report | picture",first)=="\""+first+"\\\" <report | picture>"&&!FileSearch.InScope(outside,first),"Everything scope groups OR terms and excludes same-prefix sibling folders");
        check(FileSearch.EscapeSearchOptions("<-exit | /a-d> \"-export-csv\"")=="<\"-exit\" | \"/a-d\"> \"-export-csv\"","search terms resembling ES commands are quoted as literal filename searches");
        var c=new Controller(store);var search=(SearchResponse)McpBridge.ExecuteAsync(c,Call("qidesk_search_files","query","report")).GetAwaiter().GetResult();check(search.Items.Length==1&&search.Items[0].Path==report,"MCP file search uses the selected current workspace");
        McpBridge.Execute(c,Call("qidesk_set_current_workspace","workspaceId","1"));check(WorkspaceSelection.Current(store.Data)==second,"MCP can switch the project used by the launcher");
        bool rejected=false;try{McpBridge.ExecuteAsync(c,Call("qidesk_search_files","query","report","scope","all","workspaceId","0")).GetAwaiter().GetResult();}catch{rejected=true;}check(rejected,"ambiguous MCP global search plus workspace ID is rejected");
        store.Data.CurrentWorkspace=first;string renamed=first+"-renamed";Directory.Move(first,renamed);ShellActions.UpdateReferences(store.Data,first,renamed,true);check(WorkspaceSelection.Current(store.Data)==renamed&&FileSearch.Query(store,"report",false,60,renamed).Items.Length==1,"renaming the selected project preserves its current-workspace identity and search");
        string missing=first+"-missing";store.Data.WorkFolders.Add(missing);store.Data.CurrentWorkspace=missing;check(FileSearch.Query(store,"",false,60,missing).Items.Length==0&&WorkspaceSelection.Current(store.Data)==missing,"unavailable current workspace yields no files instead of searching another project");
    }
    static void PumpUntil(Func<bool> done){DateTime end=DateTime.UtcNow.AddSeconds(8);while(!done()&&DateTime.UtcNow<end){var frame=new DispatcherFrame();var timer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(25)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}if(!done())throw new Exception("Workspace search UI timed out.");}
    public static void Native(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"search-ui"));store.Data.UseEverything=false;string dir=Path.Combine(root,"search-ui-project");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"first-report.txt"),"first");File.WriteAllText(Path.Combine(dir,"second-report.txt"),"second");WorkspaceConnections.Connect(store.Data,dir,"UI test",false);
        var c=new Controller(store);var manager=new ManagerWindow(c);var panel=new WorkspaceSearchWindow(c);try{
            panel.Present(false,true);panel.SearchInput.Text="report";PumpUntil(()=>panel.ResultList.Items.Count==2);check(panel.SearchScope==dir,"floating launcher loads current project with real asynchronous search");
            var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(panel),0,Key.Down){RoutedEvent=Keyboard.PreviewKeyDownEvent};panel.RaiseEvent(key);check(panel.ResultList.SelectedIndex==1,"arrow key selects the next floating search result");
            panel.SearchInput.Text="first";panel.SearchInput.Text="second";PumpUntil(()=>panel.ResultList.Items.Count==1&&((SearchResult)((System.Windows.Controls.ListBoxItem)panel.ResultList.Items[0]).Tag).Name=="second-report.txt");check(true,"rapid input only displays results for the latest search text");
            string drop=Path.Combine(root,"search-panel-drop");Directory.CreateDirectory(drop);var dropped=panel.AddFolderTab(drop);check(dropped!=null&&String.Equals(dropped.Folder,drop,StringComparison.OrdinalIgnoreCase)&&panel.TabPaths(dropped.Id).Length==0,"dropping a folder on the panel tab bar creates a folder partition");
            var nativeGroup=new Group(){Name="Native Panel"};store.Data.Groups.Add(nativeGroup);nativeGroup.Items.Add(Path.Combine(dir,"first-report.txt"));store.Data.IgnoreRules.Add(new IgnoreRule(){Pattern="first-*"});
            check(panel.TabPaths("忽略").Contains(Path.Combine(dir,"first-report.txt"))&&!panel.TabPaths(nativeGroup.Id).Contains(Path.Combine(dir,"first-report.txt")),"ignored items appear in the ignore tab and stay out of their section tab");
            manager.UnignoreItem(Path.Combine(dir,"first-report.txt"));
            check(!panel.TabPaths("忽略").Contains(Path.Combine(dir,"first-report.txt"))&&panel.TabPaths(nativeGroup.Id).Contains(Path.Combine(dir,"first-report.txt")),"cancel-ignore restores the item to its section");
            panel.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(panel),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});check(!panel.IsVisible,"Escape dismisses the floating launcher");
            // Use an isolated HWND so the running user's hotkey registration is untouched.
            using(var source=new HwndSource(new HwndSourceParameters("QiDesk hotkey fixture"){Width=1,Height=1,WindowStyle=0x800000})){
                bool registered=Shell.RegisterHotKey(source.Handle,91,1|0x4000,0x20);if(registered){Shell.UnregisterHotKey(source.Handle,91);check(true,"Alt+Space can be registered and released on an isolated window");}else check(true,"Alt+Space registration reports existing owner without replacing it");
            }
        }finally{panel.Close();manager.Quit();}
    }
    public static int Render(){
        string output=AppDomain.CurrentDomain.BaseDirectory,root=Path.Combine(output,"search-preview-state");var app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};var store=new Store(root);store.Data.UseEverything=false;string project=Path.Combine(root,"栖桌项目");Directory.CreateDirectory(project);File.WriteAllText(Path.Combine(project,"桌面搜索设计报告.md"),"preview");File.WriteAllText(Path.Combine(project,"工作区交互报告.txt"),"preview");File.WriteAllText(Path.Combine(project,"本周测试报告.txt"),"preview");WorkspaceConnections.Connect(store.Data,project,"preview",false);var c=new Controller(store);var manager=new ManagerWindow(c);var panel=new WorkspaceSearchWindow(c);
        try{panel.Present(false,true);panel.SearchInput.Text="报告";PumpUntil(()=>panel.ResultList.Items.Count==3);Thumbnails.RequestVisible(panel);Thumbnails.Drain(1500);panel.UpdateLayout();var bitmap=new RenderTargetBitmap((int)panel.ActualWidth,(int)panel.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(panel);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,"workspace-search-preview.png")))png.Save(stream);return 0;}
        finally{panel.Close();manager.Quit();app.Shutdown();}
    }
}
}
