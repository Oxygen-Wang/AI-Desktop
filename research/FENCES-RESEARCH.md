# Fences 3 技术调查

日期：2026-10-13。范围：本机安装文件的只读静态分析，以及独立产品的实现准备。

## 已确认的本地证据

- 用户给出的快捷方式使用 `steam://rungameid/607380`。
- 实际产品安装目录为 `C:\Program Files (x86)\Stardock\Fences`；Steam common 目录主要保存安装器和启动器。
- `Fences.exe` 包含 CLR 元数据，直接 PE 导入为 `mscoree.dll`。已提取 195 个名称或方法与桌面、规则、布局等相关的类型。这些类型包括 `DesktopDockUI.RulesUI`、`DesktopDockUI.Layouts.LayoutBuilder`、`DesktopDockUI.Simulation.RuleSettings` 和 `DesktopDockUI.NewUI.Panels.FeaturesSnapshots`。
- `DesktopDock.dll` 是原生 x86；`DesktopDock64.dll` 和 `FencesMenu64.dll` 是原生 x64。
- DesktopDock64 导出 `FindDesktopWindowSlow`、`GetResolutionFingerprint`、`ShellGraphics_LoadIconOfFile`、`Shell_GetLnkPath`、`Shell_GetName`、`Shell_GetUrlPath`、`DllRegisterServer` 等。已提取导入表、导出表、相关字符串和部分入口反汇编。
- 布局模板 `.fencelayout` 是 XML，包含 `LayoutArea`、相对宽高、对齐、方向、偏置和间距修正参数。

原始证据位于 `evidence/Fences.exe.json`、`evidence/DesktopDock.dll.json`、`evidence/DesktopDock64.dll.json`、`evidence/FencesMenu64.dll.json`。运行 `tools/analyze_fences.py` 可以重新生成。证据只保存在本机；产品文件未上传。

## 推断及边界

结构支持“托管配置程序 + 原生桌面 / Shell 模块”的判断。后续已经通过 ILSpy-MCP 反编译配置、规则、分组、图标、布局和快照相关方法；通过 GhidraMCP 完成 DesktopDock64.dll、FencesMenu64.dll 的自动分析和关键入口反编译。新的证据与边界见 [第一轮实现规格](FENCES-IMPLEMENTATION-SPEC.md)。仍未完成全部原生函数控制流分析或动态行为验证。

所用逆向技能：https://github.com/juank115/reverse-engineerskill 。按技能执行只读 PE、CLR、反汇编与反编译分析；未启动分析对象，因此未进入动态分析阶段。该调查不是恶意软件分析，不应用 IOC 或 ATT&CK 分类。

## 对应工具

| 目标 | 适合项目 | 下一步 |
|---|---|---|
| Fences.exe 托管类型 | https://github.com/cervonwong/ILSpy-MCP | 反编译布局、规则、快照相关类型，确认配置协议和调用关系 |
| DesktopDock64.dll / FencesMenu64.dll | https://github.com/LaurieWired/GhidraMCP | 导入 Ghidra，沿桌面查找、Shell 图标、COM 注册和窗口处理交叉引用分析 |
| 独立桌面整理软件的源码参考 | https://github.com/limbo666/DesktopFramesPlus | 阅读桌面框、文件夹映射、配置和规则实现，并保留采用代码的许可证 |

没有查到经核实的 Fences 专用逆向 MCP。上述前两项是按本机证据选择的通用工具。

## 产品实现状态

项目已生成可运行的核心预览版 0.1，包含管理界面、分组窗口、配置与快照、基本规则、文件夹映射、导入和建议预览，已完成核心测试和本软件 UI 渲染。原版图标接管、完整规则条件、桌面空白双击隐藏等仍未完成，尚未达到 Fences 3 功能等价。详细范围见项目 README。

Fences 3 功能基线参考：

- https://wiki.stardocksoftware.com/index.php/3_Tips_for_Using_Stardock%27s_Fences_3
- https://wiki.stardocksoftware.com/index.php/Fences_History
- https://store.steampowered.com/app/607380/Fences_3/
