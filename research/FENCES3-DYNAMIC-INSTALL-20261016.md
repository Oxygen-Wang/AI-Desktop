# Fences 3.1.6.1 安装与动态观察

日期：2026-10-16。目标：`C:\1\rjaz\steam\steamapps\common\Fences\Fences-Steam-Setup.exe`，SHA-256 `53D28FA413119814CC52CD060D5C7841C6010B0DCF3702D2310388456FBAA4E4`，Stardock 有效签名，文件版本 3.1.6.1。此报告只记录 Fences 3；Fences 6 已按用户要求停止。

## 环境与方法

- Windows Sandbox，全新实例；剪贴板、共享目录、vGPU、音视频和打印机重定向关闭。
- 样本通过宿主虚拟网卡上的临时接收服务传入，来宾核对 SHA-256 后开启 Windows Performance Recorder `GeneralProfile`。
- 运行样本前将来宾网卡禁用，并记录禁用状态。外联防火墙缺省阻断，仅预留宿主虚拟网卡 `172.26.0.1:8765` 作为取证通道。
- 首轮因残留 Fences 进程保留网络隔离，完整 JSON/ETL 留在沙盒。第二轮改进进程清理后，完整 JSON 已回传；ETL 约 1.48 GB，未回传。用户要求停止后，沙盒与临时接收服务已关闭。

## 已观察到的行为

1. 用户确认安装向导显示完成。沙盒桌面出现三个 Fences 分区：“程序”“文件夹”“文件和文档”。这证明安装后桌面分区组件已运行，不能把后续报错解释为整个程序未启动。
2. 沙盒显示“找不到文件 SasSteam.exe”。这直接说明该启动路径缺少一个文件，不足以断言 Steam 授权验证失败。宿主安装目录中 `SasSteam.exe` 和 `steam_api.dll` 存在且签名有效，`SasSteam.ini` 标明 Steam App ID `607380`。
3. 18 个进程采样覆盖约 4 分钟，依次观察到安装器 `Fences-Steam-Setup.exe` 和 `Fences.exe` 的 `/install`、`/updateconfig`、`/startup`、`/saswelcome`、`/takeinitalsnapshot`、`/updatelogheader`、`/v5notice`、`/initalsetup` 命令行。首轮结束时还残留后三条提示类流程。参数语义仍需反编译交叉核对。
4. 第二轮安装目录枚举到 142 个文件（含 87 个 Images 文件、10 个 Layouts 文件），未列出 `SasSteam.exe` 或 `steam_api.dll`；宿主既有安装目录有这两个文件。沙盒提示缺少 `SasSteam.exe` 与该差异一致。安装器退出码为 145，但桌面分区已出现；因此不能把退出码单独解读为安装完全失败或完全成功。
5. 第二轮记录 `HKLM\SOFTWARE\Stardock\Fences` 的 `InstalledPath` 与 `IsRegistered=1`，以及 HKCU 下 Groups、Icons、InitialSnapshot、Rules、Settings、Skinning 等子键。未观察到名称包含 Fences/Stardock 的服务。来宾网卡在样本执行时报告 `Disabled`。
6. 未取得完整 ETL，不能据此断言具体文件和注册表事件的精确先后顺序，也不能确认 Shell 扩展的实际装载调用链。

## 证据

- `research/dynamic-prep/guest-uploads/Fences3-monitor-ready.json`：第二轮样本哈希与 WPR 就绪回执。
- `research/dynamic-prep/guest-uploads/Fences3-runtime.json`：第二轮安装目录、进程采样、服务、注册表与断网状态。
- `research/evidence/fences3-sandbox-20261016/Fences3-monitor-ready-first.json`：首轮就绪回执。
- `research/evidence/fences3-sandbox-20261016/desktop-groups-and-sassteam-error.png`：沙盒桌面分区和缺文件提示。
- `research/evidence/fences3-sandbox-20261016/remaining-processes.png`：首轮残留进程与命令行。
- `research/evidence/fences3-sandbox-20261016/missing-sassteam.png`：错误提示近照。

此报告的“已观察”与此前静态反编译分开记录。静态方法、注册表格式和原生模块分析见 `FENCES-IMPLEMENTATION-SPEC.md`；反编译文本不等于原始源码。
