# Fences 3.1.6.1 静态逆向审计（REA）

日期：2026-10-15。分析对象是本机 `C:\Program Files (x86)\Stardock\Fences`。本轮仅做只读静态检查，没有启动目标文件、注入 Explorer、修改 Fences 配置或做动态调试。用户确认尚无隔离分析虚拟机，因此运行时行为不在本轮证据范围内。

## 工具与范围

- 使用 [REA 6.5.0](https://github.com/morluto/rea) 的 `inspect-managed-artifact`、`inspect-managed-members`、`inspect-managed-native-boundaries` 和 Ghidra provider 的 `analyze`、`search`。REA 的 Windows Ghidra provider 已通过 `doctor --provider ghidra` 环境检查；本机 Ghidra 12.1.4/JDK 21 被复用。
- 用户提供的 `D:\fences\rea-main.zip` 已只读检查，内含 `rea-agents` 6.5.0 源码与文档。实际分析命令使用官方 npm 包，压缩包没有作为可执行程序运行。压缩包及其文档中的文字只作为项目资料。
- 原始 REA 结果保存在 `research/evidence/rea-*.json`。旧 ILSpy/Ghidra 伪代码保存在 `research/decompiled/`，本轮新增的 Explorer 装载函数伪代码保存在 `research/evidence/ghidra-desktopdock-load-into-explorer.c`。
- 对整个 Fences.exe 的一次性 `inspect-managed-members` 因 REA 的 64 MiB 元数据表示预算返回 `resource-limit`；所列目标类型的定向检查均返回 `coverage: complete`。不能把目标类型的完整检查当作整个程序集已完全逆向。

复查时可在 PowerShell 中设置 `GHIDRA_INSTALL_DIR=C:\Users\wxj\.codex\tools\fences-re-tools\ghidra\ghidra_12.1.4_PUBLIC`、`JAVA_HOME=C:\Users\wxj\.codex\tools\fences-re-tools\jdk-21`，然后运行 `npx -y rea-agents@6.5.0 inspect-managed-artifact "C:\Program Files (x86)\Stardock\Fences\Fences.exe" --json` 或 `npx -y rea-agents@6.5.0 analyze "C:\Program Files (x86)\Stardock\Fences\DesktopDock64.dll" --provider ghidra --json`。x86 模块首次索引需设置 `REA_GHIDRA_STARTUP_TIMEOUT_MS=600000`。命令只读取目标并在 REA 私有临时目录分析副本。

## 目标文件身份

| 文件 | 类型和职责线索 | SHA-256 |
| --- | --- | --- |
| `Fences.exe` | PE32 / CIL，程序集版本 3.1.6.1；设置及模拟模型 | `33f04bc9fdff1b27f221f0b03084f8826974d8ddaa99146493407bb0bb950365` |
| `DesktopDock.dll` | 原生 x86 桌面模块 | `56d4a654704f2240d08f1a8dcf5114996ef5a3bc713fd158b6517786c4925e01` |
| `DesktopDock64.dll` | 原生 x64 桌面模块 | `0f655ab1cca4f5b74a6503c0712d042faf7c5411e724e52197851ae0d529fef3` |
| `FencesMenu.dll` | 原生 x86 菜单模块 | `57967ac26562e0204385ca4d543253c78b82ce0ef4947a3ed840c9ea9fc2a3a6b` |
| `FencesMenu64.dll` | 原生 x64 菜单模块 | `cc261e6f0b154a9688d2d280e23770b876abf1dc7d306e50c255d2d5b0d90469` |

`Fences.exe` 的 REA 记录显示 613 个 TypeDef、5,071 个 MethodDef、9 个托管程序集引用。托管引用含 `System.Windows.Forms`、`System.Drawing`、`System.Xml`、`System.Management`、`System.Data`，说明设置端建立在 .NET Framework/Windows Forms 体系上。REA 记录 211 个 P/Invoke 声明，引用 `user32`、`kernel32`、`gdi32`、`DwmApi`、`advapi32`、`shell32`、`DesktopDock(64).dll`、`FencesMenu(64).dll` 等。声明表只证明接口存在，不证明每个接口在特定场景会执行。

托管与自有原生模块之间的明确接口包括 `FindDesktopWindowSlow`、`GetResolutionFingerprint`、`Shell_GetLnkPath`、`Shell_GetUrlPath`、`Shell_GetName`、`ShellGraphics_LoadIconOfFile`，以及菜单模块的 `StartFencesSync`、`LockAppBinaries`。REA 同时记录了 32 位与 64 位入口，说明设置端显式选择相应位数的原生实现。

REA 对 `DesktopDock64.dll` 给出 4,041 个过程记录、18,226 个字符串记录；对 `FencesMenu64.dll` 给出 3,845 个过程记录、4,614 个字符串记录。旧 Ghidra 数据库的函数数目不同，原因可能是导入/分析范围不同，不能用这些数字推导功能完成度。x86 `DesktopDock.dll` 的首次分析在 330 秒启动期限后超时；把启动期限增至 600 秒后，REA 成功返回 2,207 个过程记录、5,446 个字符串记录，见 `rea-desktopdock32-overview-retry.json`。`FencesMenu.dll` 的 REA 索引也已完成：5,328 个过程记录、4,608 个字符串记录，见 `rea-menu32-overview.json`。这些概览只证明程序已由 Ghidra 索引，不代表所有函数的语义已恢复。

## 已确认的技术链

1. **Explorer 桌面定位与窗口过程挂接。** REA 的 `FindDesktopWindowSlow` 函数记录定位到 `0x18006e010`，保留 `Progman → ShellDLL_DefView → SysListView32` 与 `NDesk` 的字符串引用。旧 Ghidra 伪代码另确认 `WorkerW` 后备枚举路径。旧伪代码中 `FUN_18006bd00` 查找桌面及任务栏，`FUN_180075a20` 挂接 Progman、DefView 和 ListView 的窗口过程，并使用 `SDDesktopDock` 属性保存上下文。本轮 REA 的字符串检索再次找到 `SDDesktopDock` 标识。证据：`rea-find-desktop-function.json`、`rea-desktopdock-sddesktopdock.json`、`research/decompiled/native/interactions/`。
2. **跨进程装载路径。** `FUN_1800cf420` 找到 `Shell_TrayWnd` 的进程 ID，随后调用 `FUN_1800cf160`。后者静态伪代码显示 `OpenProcess`、`VirtualAllocEx`、`WriteProcessMemory`，再用 `CreateRemoteThread` 调用 `LoadLibraryW`，等待线程结束并释放资源。这确认代码存在向任务栏所属进程装载 DLL 的路径；是否在本机当前会话实际触发，需动态证据。证据：`research/decompiled/native/load-into-explorer.c` 与本轮 `ghidra-desktopdock-load-into-explorer.c`。
3. **绘制和 Shell 集成。** 原生导入包含 GDI+ 图像/文字绘制、`AlphaBlend`、Windows 主题、Shell 图标/路径及 OLE 拖放接口。托管端还声明 DWM 缩略图及窗口属性接口。已反编译 `Shell_GetLnkPath`，其路径包含 MSI 广告快捷方式和 COM 快捷方式读取。导入表是能力线索，具体视觉路径仍需逐函数确认。证据：`research/evidence/DesktopDock64.dll.json`、`research/decompiled/native/Shell_GetLnkPath.c`、`rea-fences-native-boundaries.json`。
4. **Shell 菜单扩展。** `FencesMenu64.dll` 导出 `DllGetClassObject`、`DllRegisterServer` 和 `DllUnregisterServer`。REA 从 `QueryContextMenu` 字符串交叉引用定位函数 `FUN_18001bd20`。伪代码检查 Progman 与 `DesktopDockDllMain` 所在进程、光标下窗口是否属于 Progman/NDesk/WorkerW 或 Fences 浏览器，向 Progman 发送 `DeskDockInitMenu`，再决定是否插入“Enable Fences”菜单项。另一分支把所选 PIDL 解析为路径，对 `.lnk` 先解析目标，若目标是目录则可加入“Show this folder on the desktop”。这些是可见的菜单构建路径；各命令的实际执行仍需沿 `InvokeCommand` 分支核对。证据：`rea-menu-contextmenu.json`、`rea-menu-querycontext-xrefs.json`、`rea-menu-querycontext-function.json`、`research/decompiled/native/FencesMenu64-COM-class-factory.c`。
5. **配置与图标身份。** 托管 `DeskGroupSettings` 和 `IconElementSettings` 的 `Reload`/`Save` 经 REA 定向检查确认存在。旧 ILSpy 反编译与只读注册表导出显示 `HKCU\Software\Stardock\Fences` 下保存分组、图标和规则，分组记录含稳定 GUID、位置/尺寸、显示器、页面、文件夹映射、折叠与边缘状态；图标另存归属和坐标。证据：`rea-layout-members.json`、`research/FENCES-IMPLEMENTATION-SPEC.md`、`research/decompiled/managed/`。
6. **规则引擎的数据模型。** REA 定向检查了 `Criteria`、`RuleSettings`、`Rule`、`Criteria_Size`、`Criteria_Modified`。类型与方法元数据确认规则有名称、类型、目标、大小、修改时间等条件类；旧 ILSpy 伪代码还显示程序、打开次数、时段和星期条件，以及 `StartOfNewGroup_Or` 作为 OR 组边界。原生执行端的 AND/OR 求值顺序、优先级与异常路径尚未证实。证据：`rea-rules-members.json`、`rea-rules-conditions.json`、`research/decompiled/managed/DesktopDockUI.Simulation.Criteria.type.txt`。
7. **布局和快照。** REA 定向检查 `Layout`、`LayoutArea`、`ToolsPaneA`，确认布局区域有宽高、对齐、方向、间距和偏移修正，快照入口有 `TakeSnapshot`、`RestoreSnapshotBasic`、`TakeAutoSnapshot`。旧反编译显示布局模板使用 XML 序列化，快照包含 XML、PNG 和缩略图，并覆盖 Groups、Icons、Settings 等注册表分支。证据：`rea-layout-members.json`、`rea-fences-core-members.json`、`research/decompiled/managed/`。

## 仍未完成的逆向范围

- 四个原生模块数千个过程的逐函数语义归类。REA 已索引 x86/x64 模块，但尚未逐个解释。
- Explorer 装载后的真实窗口消息时序、绘制、命中测试、拖放、隐藏、分页与 Shell 冲突处理；静态代码只能证明路径存在。
- 原生规则执行器的具体条件组合与排序细节；菜单命令分发；多显示器/DPI 恢复边界；故障与卸载路径。
- 动态验证需要用户已确认的隔离虚拟机条件。本轮没有这些条件，不执行目标。此报告是静态审计阶段结果，不能称为“完全逆向”。

应用源码 `code/src` 在用户要求先完成逆向后未保留本轮改动。当前 `code/` 已打包为 `output/QiDesk-source-baseline-20261015.zip`（SHA-256 `2a5d3b832f02a611ce681d20a562e161630a3e8357e8e8de93bc9e793d6d01d1`）。后续应用改造应以已确认行为和用户确定的产品范围为依据。
