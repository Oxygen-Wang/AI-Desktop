from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
def edit(name, old, new):
    p = ROOT / 'src' / name
    s = p.read_text(encoding='utf-8-sig')
    if old not in s:
        raise RuntimeError(f'missing target: {name} {old[:60]}')
    p.write_text(s.replace(old, new), encoding='utf-8-sig')

edit('Core.cs','public State(){Version=2;','public State(){ClipboardEnabled=true;ClipboardLimit=50;ClipboardSide="右侧";UseEverything=true;EverythingPath="";ScreenshotFolder="";HideOriginalIcons=true;Version=3;')
edit('Core.cs','PortalNavigate=false;SnapEnabled','PortalNavigate=true;SnapEnabled')
edit('Core.cs','TintStrength=.18;ViewMode','TintStrength=.55;ViewMode')
edit('ManagerWindow.cs','Shell.UnregisterHotKey(hwnd.Handle,3);','Shell.UnregisterHotKey(hwnd.Handle,3);Shell.UnregisterHotKey(hwnd.Handle,4);')
edit('ManagerWindow.cs','Closed+=delegate{controller.CloseMcp();','Closed+=delegate{if(ClipboardHistory!=null)ClipboardHistory.Dispose();controller.CloseMcp();')
edit('ManagerWindow.cs','bool hide=Shell.RegisterHotKey','ClipboardHistory=new ClipboardHub(controller,hwnd.Handle);FileActivity.Initialize(controller.Store.Root);bool clipboardKey=Shell.RegisterHotKey(hwnd.Handle,4,2|0x4000,0xC0);if(!clipboardKey)SetStatus("Ctrl＋` 已被占用，请在剪贴板页面打开侧边栏。");bool hide=Shell.RegisterHotKey')
edit('ManagerWindow.cs','if(msg==0x312){','if(msg==0x31D&&ClipboardHistory!=null)ClipboardHistory.Notify();if(msg==0x312){')
edit('ManagerWindow.cs','case 3:controller.Page(1);break;','case 3:controller.Page(1);break;case 4:ToggleClipboard();break;')
edit('ManagerWindow.cs','menu.Items.Add("退出",','menu.Items.Add("剪贴板 · Ctrl＋`",null,delegate{ToggleClipboard();});menu.Items.Add("文件搜索",null,delegate{OpenProductivity("文件搜索");});menu.Items.Add("退出",')
edit('ManagerWindow.cs','new[]{"名称","类型","修改时间"}','new[]{"名称","类型","修改时间","大小"}')
edit('SettingsPages.cs','"插件与组件","关于"','"插件与组件","文件搜索","最近文件","剪贴板","截图与图像","关于"')
edit('SettingsPages.cs','0.5.0','0.6.0')
edit('SettingsPages.cs','case "插件与组件":panel=PluginsPage();break;','case "插件与组件":panel=PluginsPage();break;case "文件搜索":panel=SearchPage();break;case "最近文件":panel=RecentPage();break;case "剪贴板":panel=ClipboardPage();break;case "截图与图像":panel=ScreenshotsPage();break;')
edit('SettingsPages.cs','AddColorSliders(p,color);','AddAppearancePreview(p,color,opacity,strength);AddColorSliders(p,color);')
edit('SettingsPages.cs','g.TintStrength=.18','g.TintStrength=.55')
edit('SettingsPages.cs','new[]{"名称","类型","修改时间"}','new[]{"名称","类型","修改时间","大小"}')
edit('SettingsPages.cs','string snapshot=path;var row=new WrapPanel();row.Children.Add(Ui.Text(Path.GetFileNameWithoutExtension(path),12,Ui.Muted));row.Children.Add(Ui.Button("恢复",delegate{controller.Store.Restore(snapshot);SaveSettings(true);ShowSettingsPage("布局和快照");}));p.Children.Add(row);','p.Children.Add(SnapshotCard(path));')
edit('Shell.cs','Process.Start(new ProcessStartInfo(path){UseShellExecute=true});','Process.Start(new ProcessStartInfo(path){UseShellExecute=true});FileActivity.Record(path);')
edit('ShellActions.cs','new[]{"名称","类型","修改时间"}','new[]{"名称","类型","修改时间","大小"}')
edit('ShellActions.cs','group.Sort=sort;controller.SaveAndRefresh','group.Sort=sort;group.SortDescending=sort=="修改时间"||sort=="大小";controller.SaveAndRefresh')
edit('ShellActions.cs','menu.AddBackgroundCommands(new NativeMenuCommand','sorts.Add(new NativeMenuCommand("升序",delegate{controller.Store.Checkpoint();group.SortDescending=false;controller.SaveAndRefresh(false);refresh();}){Checked=!group.SortDescending});sorts.Add(new NativeMenuCommand("降序",delegate{controller.Store.Checkpoint();group.SortDescending=true;controller.SaveAndRefresh(false);refresh();}){Checked=group.SortDescending});menu.AddBackgroundCommands(new NativeMenuCommand')
edit('ShellActions.cs','Clipboard.SetText(path);','Ui.CopyText(path);')
edit('ShellActions.cs','if(background||verb!="rename")return false;','if(!background&&(verb=="open"||verb=="openas"))FileActivity.Record(path);if(background||verb!="rename")return false;')
edit('McpSession.cs','0.5.0','0.6.0')
print('Productivity wiring updated')
