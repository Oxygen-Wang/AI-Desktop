using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Collections.Generic;

namespace QiDesk {
public static class Program {
    [STAThread] public static int Main(string[] args){
        try{
            if(args.Length>=3&&args[0]=="--build-icon")return AppIcon.Generate(args[1],args[2]);
#if QIDESK_TESTS
            if(args.Contains("--render-icon-skins"))return AppIconSkinTests.Render();
            if(args.Contains("--render-quick-search"))return QuickSearchAndRenameTests.Render();
            if(args.Contains("--render-unified"))return UnifiedWorkspaceTests.Render();
            if(args.Contains("--render-search"))return WorkspaceSearchTests.Render();
            if(args.Contains("--render-panel"))return WorkspacePanelTests.Render();
            if(args.Contains("--render-components"))return ComponentFeatureTests.Render();
#endif
            int pipeOption=Array.IndexOf(args,"--pipe");string pipeName=pipeOption>=0&&pipeOption+1<args.Length?args[pipeOption+1]:McpBridge.PipeName;
            int workspaceOption=Array.IndexOf(args,"--workspace");string workspace=workspaceOption>=0&&workspaceOption+1<args.Length?args[workspaceOption+1]:Environment.GetEnvironmentVariable("QIDESK_WORKSPACE");if(args.Contains("--mcp"))return McpBridge.Serve(pipeName,workspace);
            if(args.Contains("--ai-serve"))return AiSidecar.Serve();
            if(args.Contains("--mcp-fixture")){int index=Array.IndexOf(args,"--mcp-fixture");var fixtureApp=new Application(){ShutdownMode=ShutdownMode.OnMainWindowClose};var fixtureStore=new Store(args[index+1]);var fixtureController=new Controller(fixtureStore);var fixtureWindow=new ManagerWindow(fixtureController){Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual,ShowActivated=false};fixtureApp.MainWindow=fixtureWindow;using(var fixtureBridge=new McpBridge(fixtureController,pipeName)){fixtureApp.Run(fixtureWindow);}return 0;}
            if(args.Length>0&&args[0]=="--icon-guard")return DesktopIcons.Guard(args);
            if(args.Length>0&&args[0]=="--desktop-guard")return DesktopOrganization.Guard(args);
#if QIDESK_TESTS
            if(args.Length>0&&args[0]=="--test-icon-owner")return InteractionTests.OwnerProbe(args);
            if(args.Contains("--self-test")&&args.Contains("--icon-skins"))return AppIconSkinTests.Verify(args.Contains("--native-test"));
            if(args.Contains("--self-test")&&args.Contains("--quick-features"))return QuickSearchAndRenameTests.Verify(args.Contains("--native-test"));
            if(args.Contains("--self-test"))return Tests.Run(args.Contains("--native-test"));
#endif
            if(args.Any(a=>a.StartsWith("--layout-")))return LayoutCli.Run(args);
            string shellAction=args.Contains("--organize-loose")?"--organize-loose":null;
            if(shellAction!=null&&DesktopShellActions.Signal(shellAction))return 0;
            bool render=args.Contains("--render")||args.Contains("--render-settings");
            bool created;using(var mutex=new Mutex(true,"Local\\QiDeskDesktopV1",out created)){
                if(!created&&!render){MessageBox.Show("栖桌已在运行，可从系统托盘打开。","栖桌");return 0;}
                var app=new Application(){ShutdownMode=ShutdownMode.OnMainWindowClose};
                string root=render?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"render-state"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QiDesk");
                var store=new Store(root);store.Scan(true);
                if(args.Contains("--render-settings")&&!store.Data.Groups.Any(g=>!String.IsNullOrEmpty(g.Folder))){string sampleRoot=Path.Combine(root,"portal-preview");Directory.CreateDirectory(Path.Combine(sampleRoot,"子文件夹"));File.WriteAllText(Path.Combine(sampleRoot,"示例文档.txt"),"This is an isolated rendering fixture.");store.Data.Groups.Add(new Group(){Name="映射示例",Folder=sampleRoot,Page=1,ViewMode="详细信息"});}
                store.Save();if(store.LastError.StartsWith("桌面图标整理未完成"))File.WriteAllText(Path.Combine(root,"visibility-error.txt"),store.LastError);var controller=new Controller(store);var window=new ManagerWindow(controller);app.MainWindow=window;
                app.DispatcherUnhandledException+=delegate(object s,DispatcherUnhandledExceptionEventArgs e){File.AppendAllText(Path.Combine(root,"errors.log"),DateTime.Now+" "+e.Exception+Environment.NewLine);e.Handled=true;window.SetStatus("操作失败，错误已记录："+e.Exception.Message);};
                window.Loaded+=delegate{
                    if(render){controller.AddStarterWidgets();window.Refresh();
                        // Render this application's own UI to an artifact, without desktop hooks.
                        window.Left=-20000;window.Top=-20000;
                        window.Dispatcher.BeginInvoke(new Action(delegate{Capture(window,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));if(args.Contains("--render-settings")){foreach(string name in window.SettingsPageNames){window.ShowSettingsPage(name);window.UpdateLayout();Capture(window,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-"+name+".png"));}window.ShowSettingsPage("主页");}var pictureGroup=store.Data.Groups.FirstOrDefault(g=>store.Items(g,"").Any(p=>new[]{".png",".jpg",".jpeg",".webp"}.Contains(Path.GetExtension(p).ToLowerInvariant())));if(pictureGroup!=null){var enlarged=Store.Json.Deserialize<Group>(Store.Json.Serialize(pictureGroup));enlarged.IconSize=96;enlarged.ViewMode="图标";enlarged.Width=460;enlarged.Height=420;CaptureFence(controller,enlarged,"picture-preview.png");}var sample=store.Data.Groups.FirstOrDefault();if(sample!=null){CaptureFence(controller,sample,"fence-preview.png");var transparent=Store.Json.Deserialize<Group>(Store.Json.Serialize(sample));transparent.BackgroundOpacity=0;CaptureFence(controller,transparent,"transparent-preview.png");var detailGroup=pictureGroup??sample;var details=new FileDetailsWindow(controller,detailGroup,detailGroup.Folder,window){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};details.Show();details.UpdateLayout();Thumbnails.RequestVisible(details);Thumbnails.Drain(4000);Capture(details,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"details-preview.png"));details.Close();}var portal=store.Data.Groups.FirstOrDefault(g=>!String.IsNullOrEmpty(g.Folder));if(args.Contains("--render-settings")&&portal!=null)CaptureFence(controller,portal,"portal-preview.png");foreach(var widget in store.Data.Widgets){var widgetWindow=new WidgetWindow(controller,widget){Left=-20000,Top=-20000};widgetWindow.Show();widgetWindow.UpdateLayout();Capture(widgetWindow,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"widget-"+widget.PluginId+".png"));widgetWindow.Close();}window.Quit();}),DispatcherPriority.ApplicationIdle);
                    }else{window.EnableTrayAndHotkeys();LayoutBridge.Start(controller);controller.UpdateMcp();controller.StartProjectDetection();controller.AddStarterWidgets();controller.EnsureDefaultPanels();if(args.Contains("--desktop"))controller.Start(true);else{ExplorerDesktop.RestoreInteraction();controller.RecoverDesktopIfNeeded();}if(store.LastError!="")window.SetStatus(store.LastError);if(shellAction!=null)window.Dispatcher.BeginInvoke(new Action(delegate{controller.ApplyRules();}),DispatcherPriority.ApplicationIdle);else window.Dispatcher.BeginInvoke(new Action(window.MaybeShowTutorial),DispatcherPriority.ApplicationIdle);if(args.Contains("--tray"))window.Hide();}
                };
                if(render){window.Left=-20000;window.Top=-20000;window.WindowStartupLocation=WindowStartupLocation.Manual;}
                app.Run(window);return 0;
            }
        }catch(Exception ex){string log=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.log");File.WriteAllText(log,ex.ToString());bool headless=args.Contains("--self-test")||args.Contains("--mcp")||args.Contains("--ai-serve")||args.Contains("--mcp-fixture")||args.Contains("--build-icon")||args.Contains("--icon-guard")||args.Contains("--test-icon-owner")||args.Any(a=>a.StartsWith("--render"))||args.Any(a=>a.StartsWith("--layout-"));if(!headless)MessageBox.Show(ex.Message,"栖桌启动失败");return 1;}
    }
    static void Capture(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(path))png.Save(output);}
    static void CaptureFence(Controller controller,Group group,string filename){var fence=new FenceWindow(controller,group);fence.Left=-20000;fence.Top=-20000;fence.Show();fence.UpdateLayout();Thumbnails.RequestVisible(fence);Thumbnails.Drain(4000);Capture(fence,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,filename));fence.Close();}
}
#if QIDESK_TESTS
public static class Tests {
    static List<string> results=new List<string>();
    static List<string> failedBlocks=new List<string>();
    static void Check(bool condition,string name){if(!condition)throw new Exception("FAILED: "+name);results.Add("PASS: "+name);File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-progress-results.txt"),results);}
    // 一个套件失败只终止它自己，记录后继续跑下一个。早先是裸调用：任何一条断言抛出都会冲出整个 native 段，
    // 让后面所有套件从结果里整段消失——看上去像"没有问题"，实际是根本没执行。
    // Check 仍然抛出，套件内部的后续断言依赖前面的状态，提前终止是对的；但绝不能跨出套件边界。
    static void RunBlock(string name,Action body){
        try{body();}
        catch(Exception ex){
            string message=ex.Message??"";
            results.Add(message.StartsWith("FAILED:",StringComparison.Ordinal)?message:"FAILED: "+name+" threw "+ex.GetType().Name+": "+message);
            failedBlocks.Add(name);
            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-progress-results.txt"),results);
        }
    }
    public static int Run(bool native){
        string testRoot=Path.Combine(Path.GetTempPath(),"QiDesk-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(testRoot);
        try{
            var store=new Store(testRoot);string file=Path.Combine(testRoot,"REPORT.PDF");File.WriteAllText(file,"test");
            Check(!new State().AutoRules&&!new State().HideOriginalIcons,"new desktops keep loose icons and Explorer interaction by default");
            var g=store.Match(file);Check(g!=null&&g.Name=="工作文档","case-insensitive rule classification");
            store.Assign(file,g);store.Assign(file,store.Data.Groups[0]);Check(g.Items.Count==0&&store.Data.Groups[0].Items.Count==1,"unique membership after reassignment");
            string loose=Path.Combine(testRoot,"loose.txt");File.WriteAllText(loose,"test");var organizer=new Controller(store);Check(organizer.OrganizeLoose(new[]{file,loose})==1&&store.Assigned(file)==store.Data.Groups[0]&&store.Assigned(loose)!=null,"manual loose-item organization preserves icons already placed in a group");
            store.Checkpoint();store.Assign(file,g);store.Save();Check(store.Undo()&&store.Data.Groups[0].Items.Contains(file),"undo restores membership");
            string snap=store.Snapshot();store.Data.Groups[0].Name="changed";store.Save();store.Restore(snap);Check(store.Data.Groups[0].Name=="应用与入口","snapshot restore");
            store.Save();var loaded=new Store(testRoot);Check(loaded.Data.Groups[0].Items.Contains(file),"restart persistence");
            loaded.RemoveFromGroups(file);loaded.Save();var excluded=new Store(testRoot);Check(excluded.Data.Excluded.Contains(file)&&excluded.Assigned(file)==null,"explicit removal survives restart");excluded.Assign(file,excluded.Data.Groups[0]);Check(!excluded.Data.Excluded.Contains(file),"explicit reassignment clears exclusion");
            var portal=new Group(){Folder=testRoot};Check(store.Items(portal,"").Contains(file),"folder portal enumeration");
            var controller=new Controller(store);string imported=Path.Combine(testRoot,"import.json");File.WriteAllText(imported,Store.Json.Serialize(new {groups=new[]{new {id=12,title="Import",x=22,y=33,width=350,height=240,folder="",page=2,rolled=0}},icons=new[]{new {path=file,group_id=12}}}));controller.ImportFences(imported);Check(store.Data.Groups.Count==1&&store.Data.Groups[0].Items.Contains(file)&&store.Data.Groups[0].Page==2,"Fences export imports identity and page");
            Check(!store.Data.AutoRules&&store.Data.Rules.Count==0,"import avoids stale rule target IDs");
            Check(store.Undo()&&store.Data.Groups.Count==4,"import undo restores complete previous layout");
            Check(!Shell.IsDesktopCandidate("CabinetWClass",7,7,true,true,900,700),"folder view is not a desktop host");
            Check(!Shell.IsDesktopCandidate("WorkerW",7,7,true,true,455,457),"small shell portal is not a desktop host");
            Check(!Shell.IsDesktopCandidate("WorkerW",8,7,true,true,3600,1080),"desktop host must belong to shell process");
            var areas=new List<Rect>{new Rect(-1680,0,1680,1050),new Rect(0,0,1920,1040)};
            var outside=new Group(){X=-4742,Y=1400,Width=320,Height=340};Check(Layout.EnsureVisible(outside,areas,0)&&areas[1].Contains(new Point(outside.X,outside.Y)),"offscreen group restored to primary working area");
            var secondary=new Group(){X=-1200,Y=100,Width=320,Height=340};Check(!Layout.EnsureVisible(secondary,areas,0)&&secondary.X==-1200,"valid negative secondary monitor position retained");
            var partial=new Group(){X=1850,Y=100,Width=320,Height=340};Check(!Layout.EnsureVisible(partial,areas,0),"reachable partially visible header retained");
            RunBlock("InteractionTests.Pure",delegate{InteractionTests.Pure(Check);});
            RunBlock("PageSwipeTests.Pure",delegate{PageSwipeTests.Pure(Check);});
            RunBlock("ReferenceTests",delegate{ReferenceTests.Run(testRoot,Check);});
            RunBlock("DragTests",delegate{DragTests.Run(testRoot,Check);});
            RunBlock("FileFeatureTests",delegate{FileFeatureTests.Run(testRoot,Check);});
            RunBlock("ShortcutIconTests",delegate{ShortcutIconTests.Run(testRoot,Check);});
            RunBlock("ProjectPluginTests",delegate{ProjectPluginTests.Run(testRoot,Check);});
            RunBlock("WorkspaceSearchTests",delegate{WorkspaceSearchTests.Run(testRoot,Check);});
            RunBlock("WorkspacePanelTests",delegate{WorkspacePanelTests.Run(testRoot,Check);});
            RunBlock("PartitionTabTests",delegate{PartitionTabTests.Run(testRoot,Check);});
            RunBlock("LayoutArchiveTests",delegate{LayoutArchiveTests.Run(testRoot,Check);});
            RunBlock("LayoutBridgeTests",delegate{LayoutBridgeTests.Run(testRoot,Check);});
            RunBlock("StoreRecoveryTests",delegate{StoreRecoveryTests.Run(testRoot,Check);});
            RunBlock("CustomIconTests",delegate{CustomIconTests.Run(testRoot,Check);});
            RunBlock("AiSidecarTests",delegate{AiSidecarTests.Run(testRoot,Check);});
            RunBlock("MultiSelectTests",delegate{MultiSelectTests.Run(testRoot,Check);});
            RunBlock("ComponentFeatureTests",delegate{ComponentFeatureTests.Run(testRoot,Check);});
            RunBlock("ProductivityTests",delegate{ProductivityTests.Run(testRoot,Check);});
            RunBlock("QuickSearchAndRenameTests",delegate{QuickSearchAndRenameTests.Run(testRoot,Check);});
            RunBlock("AppIconSkinTests",delegate{AppIconSkinTests.Run(testRoot,Check);});
            RunBlock("CodeRuleTests",delegate{CodeRuleTests.Run(testRoot,Check);});
            RunBlock("desktop preferences restart",delegate{
                store.Data.HideOriginalIcons=true;store.Data.DoubleClickHide=false;store.Save();
                var options=new Store(testRoot);
                Check(options.Data.HideOriginalIcons&&!options.Data.DoubleClickHide,"desktop interaction preferences survive restart");
            });
            if(native)Native(store,controller,testRoot,file);
            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),results);
            if(failedBlocks.Count>0){
                results.Add("SUMMARY "+failedBlocks.Count+" block(s) failed: "+String.Join(", ",failedBlocks.ToArray()));
                File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),results);
            }
            return failedBlocks.Count>0?1:0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),String.Join(Environment.NewLine,results)+Environment.NewLine+ex);return 1;}
        finally{try{Directory.Delete(testRoot,true);}catch{}}
    }
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
    // 原生段独立成方法。RunBlock 的每个 delegate{ 都会让所在行加深一层，
    // 继续写在 Run 的 if(native) 分支里会把 17 个套件调用全推过 8 层上限。
    static void Native(Store store,Controller controller,string testRoot,string file){
        IntPtr host=Shell.DesktopHost();
        var app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var liveGroup=new Group(){Name="Display regression check",X=32,Y=64,Width=320,Height=240};
        var live=new FenceWindow(controller,liveGroup);
        try{
            RunBlock("native live window regression",delegate{NativeLiveWindowRegression(store,controller,live,liveGroup,host,file);});
            RunBlock("InteractionTests.Native",delegate{InteractionTests.Native(live,host,Check);});
            RunBlock("PageSwipeTests.Native",delegate{PageSwipeTests.Native(testRoot,Check);});
            RunBlock("DragTests.Native",delegate{DragTests.Native(testRoot,Check);});
            RunBlock("FileFeatureTests.Native",delegate{FileFeatureTests.Native(controller,Check);});
            RunBlock("ShellFeatureTests.Native",delegate{ShellFeatureTests.Run(testRoot,controller,live,Check);});
            RunBlock("ProjectPluginTests.Native",delegate{ProjectPluginTests.Native(testRoot,controller,live,Check);});
            RunBlock("WorkspaceSearchTests.Native",delegate{WorkspaceSearchTests.Native(testRoot,Check);});
            RunBlock("WorkspacePanelTests.Native",delegate{WorkspacePanelTests.Native(testRoot,Check);});
            RunBlock("UnifiedWorkspaceTests.Native",delegate{UnifiedWorkspaceTests.Native(testRoot,Check);});
            RunBlock("PartitionTabTests.Native",delegate{PartitionTabTests.Native(testRoot,Check);});
            RunBlock("MultiSelectTests.Native",delegate{MultiSelectTests.Native(testRoot,Check);});
            RunBlock("QuickSearchAndRenameTests.Native",delegate{QuickSearchAndRenameTests.Native(testRoot,Check);});
            RunBlock("AppIconSkinTests.Native",delegate{AppIconSkinTests.Native(testRoot,Check);});
            RunBlock("McpToolsTests.Native",delegate{McpToolsTests.Native(testRoot,Check);});
            RunBlock("ComponentFeatureTests.Native",delegate{ComponentFeatureTests.Native(testRoot,Check);});
            RunBlock("ProductivityTests.Native",delegate{ProductivityTests.Native(testRoot,live,Check);});
        }finally{live.Close();app.Shutdown();}
    }
    // 原生前置回归：验证桌面挂接、坐标保持和 Alt＋Space 行为。这条链自带清理，所以放在独立方法里——
    // 直接写在 Run 的 if(native) 分支中会把嵌套推到 11 层，而且清理逻辑散在正常路径末尾，
    // 断言一失败就不再执行，多余的窗口和分组会漏给后面的套件。
    static void NativeLiveWindowRegression(Store store,Controller controller,FenceWindow live,Group liveGroup,IntPtr host,string file){
        Check(host!=IntPtr.Zero,"actual Explorer desktop host found");
        Check(Shell.Icon(file)!=null,"native file icon lookup");
        FenceWindow other=null;Group otherGroup=null;
        try{
            live.Show();Pump();Rect before=Shell.Bounds(live);
            Check(Shell.Attach(live,host)&&Shell.DesktopBindingValid(live,host),"transparent group has desktop owner and retains top-level style");
            Pump();Rect after=Shell.Bounds(live);Check(before==after,"desktop binding preserves native screen coordinates");
            Check(Shell.NativeVisible(live)&&Shell.WorkingAreas(live).Any(r=>r.IntersectsWith(new Rect(live.Left,live.Top,live.Width,live.Height))),"shown group is within a current monitor");
            Check(liveGroup.X==32&&liveGroup.Y==64,"native ownership events do not rewrite saved layout");
            live.Hide();live.Show();Shell.Attach(live,host);Pump();Check(Shell.Bounds(live)==before&&Shell.DesktopBindingValid(live,host),"hide and show retain desktop binding and coordinates");
            var view=new RenderTargetBitmap((int)live.ActualWidth,(int)live.ActualHeight,96,96,PixelFormats.Pbgra32);view.Render(live);byte[] pixels=new byte[320*240*4];view.CopyPixels(pixels,320*4,0);Check(pixels.Where((value,index)=>index%4==3).Any(value=>value>0),"bound transparent group renders nonempty content");
            store.Data.Groups.Add(liveGroup);
            controller.Windows.Add(live);
            otherGroup=new Group(){Name="workspace hotkey neighbour",X=420,Y=64,Width=320,Height=240};
            other=new FenceWindow(controller,otherGroup){Left=-20000,Top=-20000,ShowActivated=false};
            other.Show();Pump();
            other.PlaceOnDesktop(host);
            controller.Docked=true;
            controller.Windows.Add(other);
            controller.ToggleGroupSelection(liveGroup);
            controller.ToggleSelectedWorkspace();
            Check(live.IsVisible&&!live.Topmost&&Shell.FloatingBindingValid(live),"Alt+Space raises a selected partition as an ordinary foreground window");
            Check(other.IsVisible&&!other.Topmost,"raising a selected workspace leaves unselected partitions untouched");
            Check((Shell.ExtendedStyle(other)&8)==0&&Shell.DesktopBindingValid(other,host),"unselected native window remains non-topmost and owned by the desktop");
            controller.ToggleSelectedWorkspace();
            Check(live.IsVisible&&!live.Topmost&&Shell.DesktopBindingValid(live,host),"repeated Alt+Space returns the selected partition to the desktop without hiding it");
            Check(other.IsVisible&&!other.Topmost,"returning the selected partition leaves unselected partitions untouched");
            controller.ToggleSelectedWorkspace();
            Check(live.IsVisible&&!live.Topmost&&Shell.FloatingBindingValid(live),"Alt+Space can raise the partition again after returning it to the desktop");
            live.MiddleClick();Pump();
            Check(!live.IsVisible&&!live.Topmost,"middle-clicking the partition collapses it like a folder");
            Check(other.IsVisible&&!other.Topmost,"collapsing by middle click leaves unselected partitions untouched");
            controller.ToggleSelectedWorkspace();
            Check(live.IsVisible&&!live.Topmost&&Shell.FloatingBindingValid(live),"Alt+Space restores the workspace collapsed by middle click");
            controller.ClearGroupSelection();
            Check(live.IsVisible&&!live.Topmost,"clearing workspace selection restores the desktop partition without hiding it");
            Check(Shell.DesktopBindingValid(live,host),"clearing selection restores Explorer ownership after floating");
        }finally{
            controller.Docked=false;
            if(other!=null){controller.Windows.Remove(other);other.Close();}
            if(otherGroup!=null)store.Data.Groups.Remove(otherGroup);
            controller.Windows.Remove(live);
            store.Data.Groups.Remove(liveGroup);
            store.Save();
        }
    }
}
#endif
}
