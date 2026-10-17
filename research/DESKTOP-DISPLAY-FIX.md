# 桌面显示故障修复 · 2026-10-13

## 观察到的故障

旧进程选择了一个包含 SHELLDLL_DefView 的小型 Shell 文件视图作为桌面承载窗口。其窗口尺寸约 455 × 457，而真正 Explorer WorkerW 桌面的范围为 `[-1680, 0, 1920, 1080]`。旧分组被作为子窗口连接到错误视图，同时把原生 LocationChanged 事件写回配置，导致保存的坐标持续偏移。诊断时分组原生矩形已处于约 x = -13625 的屏幕外区域。

导入错误日志还显示 OpenFileDialog 在解析初始目录时抛出异常。原目录包含未规范化的 `..`，对话框调用也位于异常处理范围之外。

## 修复

- 桌面候选必须是 Explorer 进程的可见 Progman 或 WorkerW，具有直接的桌面 DefView 和 ListView，并满足桌面尺寸下限；不再接受任意带 DefView 的窗口。
- WPF 透明分组保持顶层窗口，通过 GWLP_HWNDPARENT 设置桌面窗口所有者，再降低到普通应用窗口之下。避免跨进程 SetParent 带来的坐标转换和子窗口裁剪。隐藏后显示再次设置桌面位置关系。
- 只在用户完成拖动或缩放时保存位置，原生窗口事件不写回分组坐标。
- 检查分组标题是否能在任何显示器工作区触及。不可触及时移回可见工作区；有效副屏负坐标保持不变。
- 导入对话框使用完整目录及文件路径，找不到目录时回退到文档目录，整个过程纳入异常处理。
- 修复启动脚本首次构建后仍使用旧 EXE 路径的问题。

## 验证与限制

本机 26 项自动检查通过。原生检查使用本软件的真实透明 FenceWindow，验证选中桌面 HWND、顶层样式和所有者、挂接前后原生矩形相同、显示状态、位于显示器范围、配置坐标不被窗口事件修改、隐藏后显示和自身内容渲染。

检查时没有关闭 Fences，也没有重启或修改 Explorer。自身内容通过 WPF 渲染验证，不等于已经确认最终桌面合成画面。仍需要用户在新版中确认可见效果；Win+D、不同 DPI、Explorer 重启、与 Fences 共存的 Z 顺序属于待实测场景。

## 真实配置启动验证

新增 `--desktop` 启动参数，直接进入桌面模式。使用真实配置启动前，已另存 `snapshots/before-display-check-20261013-133932.json`，原配置未丢失。四个屏幕外分组恢复到 `(16,48)`、`(352,48)`、`(688,48)`、`(1024,48)`。

原生窗口检查确认四个分组的所有者均为真正的 WorkerW 桌面，窗口可见标志为真，DWM cloaked 为 0，矩形位于屏幕范围，进程正常响应；未新增错误日志。上述检查确认运行状态和位置，最终视觉效果仍以用户的桌面观察为准。

`tools/diagnose_desktop.py --pid <栖桌进程 ID>` 可重新生成只读诊断结果 `dist/desktop-diagnostics.json`，不截取桌面画面，也不读取文件内容。

Microsoft 平台参考：[SetParent 的样式与 DPI 行为](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)、[窗口所有权与 Z 顺序](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)、[分层窗口支持](https://learn.microsoft.com/en-us/windows/win32/winmsg/using-windows)。
