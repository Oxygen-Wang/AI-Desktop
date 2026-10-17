# Fences 逆向工具

本次使用静态分析：ILSpy 读取 CLR 元数据和 IL 并反编译 C#；Ghidra 导入原生 PE，反汇编、分析交叉引用并生成 C 伪代码。分析对象未被执行。

## 已安装

- ILSpy-MCP 1.2.1：`C:\Users\wxj\.codex\tools\fences-re-tools\ilspy\ILSpy.Mcp.exe`。
- Ghidra 12.1.4 + GhidraMCP 2.0：同目录下 `ghidra`、`ghidra-mcp-release` 和 `python`。
- 独立 Temurin JDK 21：同目录下 `jdk-21`；只供分析工具使用，没有修改系统 Java、PATH 或注册表。
- Codex MCP 注册名为 `ilspy-mcp` 和 `ghidra-mcp`。两者使用 stdio，由 Codex 启动。Ghidra 默认分析本机 `DesktopDock64.dll`。

下载来源、版本、摘要位于同目录 `install-manifest.json`。三个发布 ZIP 均按 GitHub 官方 release 的 SHA256 验证。Ghidra 扩展同时复制到本次独立 Ghidra 安装的扩展目录，GUI 可加载；静态后台模式通过 `--extension-jar` 显式选用同一个 JAR。

## 当前会话实测

为了在工具列表尚未重新加载时进行真实验证，已启动仅监听本机的临时 HTTP 服务：

- ILSpy：`http://127.0.0.1:3001/mcp`。
- Ghidra DesktopDock64：`http://127.0.0.1:8081/mcp`。
- Ghidra FencesMenu64：`http://127.0.0.1:8082/mcp`。

`mcp_client.py` 直接使用 MCP initialize / tools/list / tools/call，而不是模拟工具输出。原始结果与反编译文本保存在 `research/decompiled/managed` 和 `research/decompiled/native`。项目数据库位于 `research/ghidra-projects`，Codex 默认使用独立的 `research/ghidra-codex-projects`，避免当前研究服务锁住同一个项目。

```powershell
# 桌面核心
& D:\fences\code\tools\reverse\start-ghidra.ps1

# 另一个模块，使用独立端口与项目
& D:\fences\code\tools\reverse\start-ghidra.ps1 -Binary 'C:\Program Files (x86)\Stardock\Fences\FencesMenu64.dll' -Port 8082 -ProjectName FencesMenu64
```

启动脚本不会覆盖占用的端口。重启电脑后由 Codex 的 stdio 注册启动分析服务即可；无需恢复这些临时 HTTP 进程。Ghidra 第一次导入新二进制时自动分析需要一段时间；`NOT_READY` 表示尚在分析，不能据此宣称目标反编译已经完成。

## jadx-mcp 卸载

已通过 jadx 的插件管理命令卸载插件，通过 `codex mcp remove jadx-mcp` 移除注册，并停止已验证的托管后台进程。jadx CLI 验证显示 `Installed plugins: 0`；Codex 查询显示不存在 `jadx-mcp`。

对 `C:\Users\wxj\.codex\tools\jadx-mcp` 的递归缓存清理被自动审批拒绝，返回 `blocked by policy`。缓存留存；用户提供的 ZIP 与原有 jadx 没有被本次卸载删除。后续发现此前的系统 Java 27 目录已不存在，Ghidra 因此采用了独立 JDK 21。
