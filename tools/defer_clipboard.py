from pathlib import Path
root=Path(__file__).resolve().parents[1]
def edit(name,old,new):
 p=root/'src'/name;s=p.read_text(encoding='utf-8-sig')
 if old not in s:raise RuntimeError(name+' '+old[:70])
 p.write_text(s.replace(old,new),encoding='utf-8-sig')
edit('ManagerWindow.cs','ClipboardHistory=new ClipboardHub(controller,hwnd.Handle);FileActivity.Initialize(controller.Store.Root);bool clipboardKey=Shell.RegisterHotKey(hwnd.Handle,4,2|0x4000,0xC0);if(!clipboardKey)SetStatus("Ctrl＋` 已被占用，请在剪贴板页面打开侧边栏。");','FileActivity.Initialize(controller.Store.Root);')
edit('ManagerWindow.cs','menu.Items.Add("剪贴板 · Ctrl＋`",null,delegate{ToggleClipboard();});','')
edit('SettingsPages.cs','"最近文件","剪贴板","截图与图像"','"最近文件","截图与图像"')
edit('SettingsPages.cs','case "剪贴板":panel=ClipboardPage();break;','')
edit('Core.cs','ClipboardEnabled=true;','ClipboardEnabled=false;')
edit('Core.cs','if(s.Version<3){s.PortalNavigate=true;','if(s.Version<3){if(s.Version<2)s.RollupOnDoubleClick=false;s.PortalNavigate=true;')
edit('Core.cs','foreach(var g in s.Groups)if(g.Sort=="修改时间")g.SortDescending=true;','foreach(var g in s.Groups){if(g.Sort=="修改时间")g.SortDescending=true;if(g.TintStrength<.3)g.TintStrength=.55;}')
edit('AiSettings.cs','"ClipboardEnabled","ClipboardPinned","ClipboardLimit","ClipboardSide",','')
edit('FileFeatureTests.cs','old.Version==2&&!old.PortalNavigate&&!old.RollupOnDoubleClick','old.Version==3&&old.PortalNavigate&&old.HideOriginalIcons&&!old.RollupOnDoubleClick')
edit('FileFeatureTests.cs','old configuration migrates double click to system opening and initializes new preferences','old configuration enables folder navigation and duplicate icon hiding while preserving legacy rollup migration')
p=root/'tools/test_qidesk_mcp.py';s=p.read_text(encoding='utf-8');s=s.replace('len(listed.tools) == 13','len(listed.tools) == 20').replace('discovery of thirteen tools','discovery of twenty tools');p.write_text(s,encoding='utf-8')
print('Clipboard deferred; desktop core remains active')
