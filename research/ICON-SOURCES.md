# 图标相关开源素材清单

给栖桌用的**素材库索引**：同类开源项目、图标资源库、系统接口文档。只做索引，不复制代码。

采集方式：GitHub Search API（2026-10-17），星数和许可证取自仓库元数据；**没有逐个克隆读源码**，所以下表的"可借鉴点"是待核实的线索，不是结论。核实过一行的，会在备注里写"已读 README/源码"。

---

## 一、同类桌面整理项目（代码参考）

改本地 Windows 图标这件事，最该看的就是这批 Fences 替代品怎么写。

| 仓库 | 语言 | 星 | 许可证 | 它做什么 | 可借鉴点（待核实） |
| --- | --- | --- | --- | --- | --- |
| [PinchToDebug/DeskFrame](https://github.com/PinchToDebug/DeskFrame) | C# | 530 | MIT | 把桌面整理成 frame，含 portals、widget | 星数最高、C# 且 MIT，优先读；看它怎么挂接桌面层 |
| [chrisdfennell/Pickets](https://github.com/chrisdfennell/Pickets) | C# | 38 | MIT | 原名 OpenFences，可移动可缩放的 fence | 名字和历史最接近 Fences，看分组与图标归属 |
| [kof2000git/desktop_box](https://github.com/kof2000git/desktop_box) | C# (.NET 8 + WPF) | 10 | MIT | 标签盒整理、原生右键菜单、**隐藏桌面图标** | 描述里明确写了 hide desktop icons，是"逐个隐藏"最好的对照样本 |
| [infitis-studio/My-Fancy-Fences](https://github.com/infitis-studio/My-Fancy-Fences) | C# (WPF) | 7 | MIT | 轻量 Fences 风格整理器 | 代码量小，适合看整体骨架 |
| [Walkoud/Palisades](https://github.com/Walkoud/Palisades) | C# | 6 | MIT | 桌面容器、gadgets、主题 | 组件/目录结构参考 |
| [Yuch3nE/luciddesk](https://github.com/Yuch3nE/luciddesk) | Rust | 15 | MIT | Fences 替代，图标分组 + Everything 搜索 + 预览 | 语言不同，但功能切片（分组/搜索/预览）划分值得抄思路 |
| [MrIvoe/Spaces](https://github.com/MrIvoe/Spaces) | C++ | 8 | **无许可证** | fence、主题、插件、Win32 API | 用到 Win32 API，但**没有 LICENSE = 不能拿代码**，只能看思路 |

**注意**：`Storagefloball/stardock-fences-newest` 这类仓库是商业软件的"安装包搬运"，不是源码，跳过。

---

## 二、图标资源库（SVG / 可直接转 ICO）

栖桌现在用的是 [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)（MIT，`code/assets/fluent` 已内置，许可随 `dist/Fluent-Icons-LICENSE.txt` 发布）。下面是可替换/可补充的候选，许可证都核过：

| 库 | 星 | 许可证 | 特点 | 能不能进 `code/assets` |
| --- | --- | --- | --- | --- |
| [tabler/tabler-icons](https://github.com/tabler/tabler-icons) | 22.3k | **MIT** | 6200+ 个，线性风格统一，有 `icons/` 纯 SVG 目录 | 可以，MIT 最省心 |
| [simple-icons/simple-icons](https://github.com/simple-icons/simple-icons) | 26.0k | **CC0-1.0** | 品牌 logo（各软件的官方标志） | 可以，CC0 无附加条件；但品牌标志本身有商标限制 |
| [lucide-icons/lucide](https://github.com/lucide-icons/lucide) | 25.1k | 自定义（ISC 系，需看 LICENSE） | Feather 图标的后继，风格极简 | 先确认 LICENSE 再拷 |
| [FortAwesome/Font-Awesome](https://github.com/FortAwesome/Font-Awesome) | 77.0k | 自定义（图标 CC BY 4.0 / 代码 MIT） | 图标量最大，但需署名 | 要附带署名，不适合直接内嵌 |
| [edent/SuperTinyIcons](https://github.com/edent/SuperTinyIcons) | 15.4k | 自定义（CC BY 4.0） | 每个 <1KB 的网站/应用 logo | 需署名，适合小图标 |
| [react-icons/react-icons](https://github.com/react-icons/react-icons) | 12.7k | 自定义（各图标集各自许可） | 只是把别的图标集打包成 React 组件 | 不适合本项目（要 React） |

**选型建议**：MIT 的 Tabler 当主力；需要品牌 logo 时用 CC0 的 simple-icons；两者都避免 Font-Awesome（署名约束）。

---

## 三、系统接口文档（改图标/控图标的正规做法）

| 主题 | 链接 | 用途 |
| --- | --- | --- |
| `IShellLinkW` | [learn.microsoft.com](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishelllinkw) | 读/写快捷方式，含 `SetIconLocation`/`GetIconLocation` |
| `IShellLinkW::SetIconLocation` | [sdk-api 源文件](https://raw.githubusercontent.com/MicrosoftDocs/sdk-api/ad992d90d4f35264b58406b908390601347cbc1a/sdk-api-src/content/shobjidl_core/nf-shobjidl_core-ishelllinkw-seticonlocation.md) | 参数语义：`pszIconPath` + `iIcon` |
| Fluent 图标官网 | [fluent2.microsoft.design/iconography](https://fluent2.microsoft.design/iconography) | 项目已在用的那套的检索页 |

本项目已经落地的实现见 [code/src/ShortcutIcons.cs](../code/src/ShortcutIcons.cs)（`IShellLink` + `IPersistFile`），验证见 [code/src/tests/ShortcutIconTests.cs](../code/src/tests/ShortcutIconTests.cs)。

---

## 四、SVG → ICO 转换（本项目无 NuGet，得用现成工具或系统 API）

| 方式 | 说明 |
| --- | --- |
| Inkscape / ImageMagick | 命令行批量：`magick icon.svg -define icon:auto-resize=256,64,32,16 icon.ico` |
| 在线转换 | 少量图标够用，不适合批量 |
| 项目内渲染 | WPF 的 `DrawingImage` → `RenderTargetBitmap` → 写 ICO，能复用现有 `FluentIcons` 解析，但要自己写 ICO 头 |

`.ico` 文件本身可以直接作为 `SetIconLocation` 的来源；`.exe`/`.dll` 则要指定序号。

---

## 五、核实清单（下一步）

- [ ] 读 `kof2000git/desktop_box` 的隐藏图标实现（SysListView32？隐藏属性？注册表？）
- [ ] 读 `PinchToDebug/DeskFrame` 的桌面挂接方式，对比本项目 `Shell.Attach`
- [ ] 确认 `lucide` 的实际 LICENSE 文件
- [ ] 决定是否把 Tabler 的 `icons/*.svg` 抽一部分进 `code/assets`

以上任一条要动手，先单独提一版任务，别顺手改。
