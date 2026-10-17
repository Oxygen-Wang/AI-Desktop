# 当前工作区与悬浮搜索

Alt＋Space 由栖桌通过 Windows RegisterHotKey 注册，打开/收起当前项目文件面板。MCP 提供项目上下文及唤起工具；快捷键本身不依赖 AI 每次响应。热键冲突时管理面板提示，托盘仍有入口。退出时释放热键和面板。

面板采用圆角搜索条、项目/全盘切换、项目选择和文档/图片/文件夹/今天筛选。220 ms 防抖，后台单次查询，丢弃过期结果；上下方向键选择，Enter 打开，Ctrl＋Enter 定位，Esc 或失焦收起。重复快捷键收起已显示的面板。

“当前项目”是最近由 MCP 显式连接、客户端 Roots 提供或用户选择的目录，不表示自动追踪前台编辑器。多个 Roots 以第一个有效根目录为当前项目。选择存入 state.json 的 CurrentWorkspace；旧配置按最近连接记录选择。目录不可用时提示，项目搜索不会自动扩大到全盘。

新增 MCP 工具：

| 工具 | 参数 | 效果 |
| --- | --- | --- |
| qidesk_get_current_workspace | 无 | 返回当前项目路径、ID 和可用状态 |
| qidesk_set_current_workspace | workspaceId | 选择已连接项目，保存选择 |
| qidesk_show_workspace | 可选 workspaceId | 唤起面板，可先选择项目 |
| qidesk_search_files | query，可选 workspaceId、scope | 默认搜索当前项目；scope=all 使用全盘索引/本机回退；最多 60 项 |

qidesk_search_files 在后台执行，避免阻塞桌面 UI。scope=all 与 workspaceId 不能同时提供。显示忽略规则同样适用于搜索；读搜索不会改变当前项目。

Everything ES 1.1 使用位置参数接收搜索表达式，不使用 -search。项目路径带尾部反斜杠并加引号，用户条件使用角括号分组，返回结果再按目录边界过滤，避免 OR 或相似目录名前缀扩大范围。查询通过 ProcessStartInfo 直接启动 ES，不经过 shell；看起来像 CLI 开关的搜索词按字面量加引号。结果导出到唯一临时 UTF-8 CSV 并清理，保留中文文件名和整数文件大小。

索引未连接时回退到桌面/已连接目录；项目模式只枚举该目录。枚举最多 5000 项，根目录及三层子目录，跳过链接和忽略目录。回退支持名称词、ext:、folder:、file:、dm:today；面板显示“本机搜索”以区别全盘索引。

验证入口：

```powershell
& D:\fences\build.ps1
$taskExe=(Get-Content D:\fences\dist\latest.txt).Trim()
Start-Process -FilePath $taskExe -ArgumentList '--self-test','--native-test' -WindowStyle Hidden -Wait
& D:\fences\tools\test_workspace_search.ps1
Start-Process -FilePath $taskExe -ArgumentList '--render-search' -WindowStyle Hidden -Wait
```

测试使用独立配置和测试文件，不更改用户工作区。预览输出 dist/workspace-search-preview.png。
