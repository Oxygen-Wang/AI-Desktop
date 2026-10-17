# AI 功能独立进程（sidecar exe）设计与实现

日期：2026-10-17。对应场景：开发 Skill —— "将 AI 功能开发成 exe，主程序内部调用 exe，不要让功能耦合太深"。

## 决策

AI 功能从主程序进程里剥出来，做成独立构建产物 `Ai-QiDesk-<时间戳>.exe`（与 `Mcp-QiDesk-*.exe`
同一套构建方式），主程序以子进程方式调用它。两边之间只有一条进程边界：

- 主程序负责：发现 exe、拉起进程、发请求、收响应、退出时收尾。不知道提示词、HTTP、解析任何细节。
- AI exe 负责：stdin/stdout 上的 NDJSON 协议和全部 AI 逻辑。不引用 `Store`、`Controller`
  和任何窗口类型，只认纯数据（文件名、扩展名、是否文件夹、分组 id 和名称）。

这样 AI 逻辑升级、替换甚至换实现（本地模型、其他 Skill 命令）都不需要动主程序；
AI exe 崩溃也不会带走主程序（参考 Go 微内核 + sidecar exe 的架构文章）。

## 开源参考（只借鉴模式，不拷代码）

- `references/matemium-sidecar`（Matemium，source-available 许可，仅阅读参考）：
  Tauri 桌面壳 + Python sidecar。`matemium/ipc/PROTOCOL.md` 定义 NDJSON 信封
  （request/response/event）、错误码、`shutdown` 命令；`matemium/ipc/server.py` 是
  sidecar 主循环；`desktop/src-tauri/src/sidecar.rs` 是宿主侧：按请求 id 关联响应、
  stdout 跨 chunk 分行、进程终止时挂起请求全部失败、shutdown 后 Kill 兜底。
- Tauri 本地 AI sidecar 设计契约（legioncodeinc/vibe-coding-tools 示例文档）：有界读取、
  启动期限、协议版本、readiness 帧、每个终止分支只消费一次子进程、诊断信息脱敏。
- Go 微内核 AI 桌面助手重构文章（mhpq.cn）：单体 exe 的三个死结（panic 全死、改一处发全套、
  AI 自改功能没有安全边界），拆成微内核 + sidecar exe 扩展，协议"只加不改不删"。

三者共同点：宿主只做进程管理和协议帧，能力全在 sidecar；协议带版本和请求 id；
生命周期（启动、就绪、超时、取消、退出）由宿主统一负责。

## 协议 v1（NDJSON，UTF-8，一行一帧，`\n` 结尾）

请求（主程序 → exe）：

```json
{"type":"request","id":"1","command":"recommend","params":{...}}
```

响应（exe → 主程序）：

```json
{"type":"response","id":"1","ok":true,"result":{"suggestions":[{"index":0,"groupId":"g1","reason":"文档 · 文本文件"}]}}
{"type":"response","id":"1","ok":false,"error":{"code":"MODEL_HTTP","message":"模型返回 HTTP 500。请检查服务地址、模型和密钥。"}}
```

事件（exe → 主程序，请求与响应之间可能出现）：

```json
{"type":"event","event":"ready","data":{"version":"0.7.1","protocol":"1"}}
{"type":"event","event":"progress","data":{"message":"正在请求模型…"}}
```

命令：

| 命令 | 参数 | 结果 |
| --- | --- | --- |
| `ping` | 无 | `{ok, version, protocol}` |
| `recommend` | `endpoint`、`model`、`files`、`groups` | `{suggestions:[{index,groupId,reason}]}` |
| `shutdown` | 无 | `{shutdown:true}` 后退出 |

错误码：`BAD_REQUEST`（信封或参数）、`ENDPOINT_INVALID`、`MODEL_HTTP`、`MODEL_RESPONSE`、
`INTERNAL_ERROR`。所有面向用户的信息保持中文，与现有界面一致。

`recommend` 参数：

- `endpoint`：必填，`http://` 或 `https://` 绝对地址； Ollama 根地址或兼容服务的 `/v1` 地址。
- `model`：模型名称（允许为空，由服务端报错）。
- `files`：`[{index,name,extension,isFolder}]`，最多 300 项，由主程序枚举桌面并按忽略规则过滤后给出。
- `groups`：`[{id,name}]`，普通分组（无文件夹映射）；为空时返回空建议，保持现有行为。

exe 侧校验：index 在范围内且不重复、groupId 必须存在于 groups、reason 截断到 120 字、
展示理由加分组名前缀（`分组名 · 理由`）。Ollama 与 Chat Completions 的地址拼接、请求体、
`QIDESK_AI_KEY` 读取、90 秒超时、响应数组提取，全部原样搬进 exe，行为不变。

隐私不变式：只发送文件名、扩展名、是否文件夹与分组名（README 既有承诺），不发送完整路径。

## 生命周期

- 主程序首次请求时懒拉起 exe（隐藏窗口，`--ai-serve`），之后复用；等 `ready` 帧最多 10 秒。
- 单请求超时 100 秒（exe 内部 HTTP 90 秒）；超时或进程异常退出时杀进程复用例，下次重新拉起。
- exe 空闲 10 分钟自行退出；stdin 关闭（EOF）即退出。
- 主程序"退出栖桌"时先发 `shutdown`，2 秒内没响应再 `Kill` 兜底。
- 协议行上限 2 MB，与 `LayoutBridge` / `McpSession` 一致。

## 构建与文件

- `code/src/AiCore.cs`：AI 逻辑（纯数据进出的 `Recommend`），不依赖 Store/UI。
- `code/src/AiSidecar.cs`：exe 侧 NDJSON 服务循环（ready、分发、错误信封、空闲退出）。
- `code/src/AiClient.cs`：主程序侧客户端（发现 exe、进程管理、协议收发、Store → 请求 → 建议映射）。
- `code/src/Ai.cs` 删除，逻辑迁入 `AiCore.cs`。
- `code/build.ps1` 产出 `Ai-QiDesk-<stamp>.exe` 并写 `dist/latest-ai.txt`，加入保留列表。
- `Program.cs` 增加 `--ai-serve` 分发（与 `--mcp` 同一处风格）。

发现顺序：`dist/latest-ai.txt` 指向的文件 → 不存在则取程序目录下最新的 `Ai-QiDesk-*.exe` →
都没有就报"找不到 AI 组件，请重新构建"。

## 测试

- `code/src/tests/AiSidecarTests.cs`：
  - AiCore 单测：TcpListener 假模型服务，覆盖 Ollama 与 Chat Completions 两种响应、
    HTTP 错误、非数组响应、index 越界/重复、未知分组、地址非法、超过 300 项、无分组。
  - 端到端：spawn dist 中的 Ai exe，`ping`、`recommend`（走本地假服务）、`shutdown`；
    exe 不存在时记录跳过（构建后必然存在）。
- 既有 506 项自检全部保持通过；代码守卫无新增违规。
