# QiDesk 开发约定

## 构建与验证

在 Windows x64 的 PowerShell 中，从仓库根目录执行。需要系统提供 .NET Framework 4.x 编译器和 WPF 程序集，无需下载研究资料或本地辅助工具。

```powershell
& .\code\build.ps1
& .\code\start.ps1
```

构建产物位于 `dist/`，包括主程序、MCP 程序、AI 组件和独立测试程序。启动脚本读取构建生成的最新版本指针。

应用所需源码、资源和运行依赖统一位于 `code/`。其中 `vendor/everything/` 是附带许可的第三方运行文件，构建按固定清单复制到 `dist/Everything/`；不在构建过程中联网下载，也不复制个人配置或索引数据库。只下载仓库中的 `code/` 即可编译，根目录文档与 `.githooks/` 用于开发检查和提交约定。

运行自测：

```powershell
$testExe = (Get-Content .\dist\latest-tests.txt).Trim()
$testRun = Start-Process -FilePath $testExe -ArgumentList '--self-test' -PassThru -Wait
$testRun.ExitCode
```

涉及 Win32 窗口、桌面挂接或 Shell 的修改，还应运行 `--self-test --native-test` 并检查真实交互。原生测试可能短暂显示窗口。

## 模块边界

- `code/src/Core.cs` 保存状态模型、迁移与持久化入口。界面事件不得直接修改桌面文件属性。
- `code/src/DesktopOrganization.cs` 负责已整理桌面项目的隐藏、受管移动与恢复。进入和退出桌面模式必须经由 `Store.Save`、`Store.RestoreDesktop`。
- `code/src/Controller.cs` 协调运行状态、面板和桌面集成；WPF 窗口只处理显示与用户输入。
- 多种实现确有不同生命周期或行为时再定义接口或基类；相同行为优先抽成小函数，避免为了“多态”增加空壳层。

## 修改规则

1. 先确定行为归属，只在拥有该行为的模块修改；不在多个窗口复制同一段文件或布局逻辑。
2. 新增持久化字段时，在 `State` 中给出默认值，在 `Store.Validate` 中兼容旧配置，并写迁移验证。
3. 文件移动前保存恢复映射；恢复失败保留映射并报告错误。停止、退出和异常退出都必须能恢复 Windows 桌面。
4. 不用紧凑的一行代码写复杂分支。方法应有明确职责，异常处理只覆盖预期的外部失败。
5. 修复行为时添加针对用户场景的测试。提交前运行 `code/build.ps1` 和自测，并检查 `git diff`。
6. 提交前代码守卫 `code/check-code.ps1` 必须通过。它按文件记录既有违规额度，只允许减少；新增违规要改代码，不要改基线。规矩和理由见 [CODE-RULES.md](CODE-RULES.md)。

## 版本管理

产品源码、资源、测试和构建脚本位于 `code/`，连同根目录开发说明、Git 配置文件和 `.githooks/` 由 Git 跟踪。`dist/`、`output/`、`research/`、`references/`、`tools/` 和整理笔记不提交；本地资料仍可保留。每项独立改动使用描述行为的提交信息，不提交用户桌面文件或本机配置。

提交钩子在仓库内，每个克隆启用一次：

```powershell
git config core.hooksPath .githooks
```

`-Accept` 更新守卫基线时应单独成一次提交，说明为什么数字变了，不要混在功能提交里。
