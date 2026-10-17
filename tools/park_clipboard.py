from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/ClipboardHub.cs';s=p.read_text(encoding='utf-8-sig')
start=s.index('    public string[] Screenshots()')
part=s[start:]
part=part.replace('if(ClipboardHistory!=null)found.AddRange(ClipboardHistory.Entries.Where(e=>e.Kind=="image").Select(ClipboardHistory.ImagePath));','')
part=part.replace('按时间整理截图文件夹与剪贴板图片，保留原文件；剪贴板图片可能来自截图，也可能来自其他应用。','按时间预览截图文件夹中的图片，保留原文件。')
part=part.replace('还没有图片。截图后复制，或选择已有截图文件夹。','还没有图片。选择已有截图文件夹，或截图时保存到系统截图文件夹。')
(root/'src/Screenshots.cs').write_text('using System;\nusing System.IO;\nusing System.Linq;\nusing System.Collections.Generic;\nusing System.Windows;\nusing System.Windows.Controls;\nusing System.Windows.Media;\nnamespace QiDesk {\npublic partial class ManagerWindow {\n'+part,encoding='utf-8-sig')
dest=root/'experimental';dest.mkdir(exist_ok=True)
target=dest/'ClipboardHub.cs'
assert p.resolve().is_relative_to(root.resolve()) and target.resolve().is_relative_to(root.resolve())
p.rename(target)
(dest/'README.md').write_text('剪贴板 / Ditto 扩展已按用户要求暂停。此目录不参与构建，不注册剪贴板监听或快捷键。恢复范围需由用户提出。\n',encoding='utf-8')
p=root/'src/ManagerWindow.cs';s=p.read_text(encoding='utf-8-sig').replace('if(ClipboardHistory!=null)ClipboardHistory.Dispose();','').replace('Shell.UnregisterHotKey(hwnd.Handle,4);','').replace('if(msg==0x31D&&ClipboardHistory!=null)ClipboardHistory.Notify();','').replace('case 4:ToggleClipboard();break;','');p.write_text(s,encoding='utf-8-sig')
p=root/'src/AiSettings.cs';s=p.read_text(encoding='utf-8-sig').replace('new[]{"保持位置","保持比例"}','new[]{"保持位置","按显示器恢复","按工作区缩放"}');p.write_text(s,encoding='utf-8-sig')
p=root/'src/IgnoreFiles.cs';s=p.read_text(encoding='utf-8-sig').replace('string revision=String.Join("\\n",set.Lines);Cached entry;','lock(cache){string revision=String.Join("\\n",set.Lines);Cached entry;').replace('return entry.Patterns;}','return entry.Patterns;}}');p.write_text(s,encoding='utf-8-sig')
print('Clipboard code parked outside build')
