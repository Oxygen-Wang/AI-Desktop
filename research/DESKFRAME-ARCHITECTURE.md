# DeskFrame 架构与风格拆解

对象：[references/DeskFrame](../references/DeskFrame)（PinchToDebug/DeskFrame，C#，MIT，530★，v1.44）
读法：25 个 cs 文件，重点看 `Core/Instance.cs`、`Core/InstanceController.cs`、`DeskFrameWindow.xaml.cs`、`Util/Interop.cs`、`MainWindow.xaml.cs` 和 `DeskFrame.csproj`。

---

## 一、技术栈

`.NET 8 + WPF`（`net8.0-windows10.0.26100.0`，`SupportedOSPlatformVersion 10.0.19041`），AnyCPU/x64。

| 依赖 | 用途 |
| --- | --- |
| `WPF-UI.Tray` | Fluent 风格控件 + 托盘 |
| `H.Hooks` | 全局键鼠钩子（桌面双击、快捷键） |
| `Slions.VirtualDesktop`（+ 私有 `VirtualDesktop.dll`） | 虚拟桌面感知，`ShowOnVirtualDesktops` 属性 |
| `Microsoft-WindowsAPICodePack-Shell` | Shell 图标提取 |
| `Svg` | SVG 图标渲染 |
| `Microsoft.CodeAnalysis` | 正则/表达式相关的编译期校验（用于 `FileFilterRegex`） |
| `Microsoft.Toolkit.Uwp.Notifications` | 通知 |
| `CsWinRT` + COM 引用 `Shell32`、`IWshRuntimeLibrary` | 系统集成、快捷方式 |

---

## 二、分层（实际只有五层，很浅）

```
MainWindow               托盘、设置入口、全局双击检测、静态 _controller 单例
  └ InstanceController   实例增删、注册表读写、Explorer 重启后的重建
      └ Instance         单个 frame 的"视图模型"，~60 个属性，INotifyPropertyChanged
          └ DeskFrameWindow   单 frame 窗口（193 KB，全部交互逻辑都在这）
Util/Interop             Win32 P/Invoke 集中营
Util/ShellContextMenu    51 KB，自己实现的 IContextMenu 右键菜单
Util/RegistryHelper      注册表读写
FrameSettingsDialog      单 frame 设置（44 KB + 54 KB XAML）
```

没有接口、没有依赖注入、没有仓储层。`MainWindow._controller` 是静态全局单例，`Instance` 的属性 setter 直接调它写注册表。

---

## 三、四个关键设计

### 1. 持久化用注册表，而且是"属性即写入"

`SOFTWARE\DeskFrame\Instances\<frame 名字>`，每个实例一个子键；全局默认值在 `SOFTWARE\DeskFrame` 根上。

`Instance` 的每个 setter 长这样：

```csharp
set {
    if (_iconSize != value) {
        _iconSize = Math.Clamp(value, 16, 256);
        OnPropertyChanged(nameof(IconSize), value.ToString());
    }
}
// OnPropertyChanged 内部：不是全局属性就 WriteToRegistry(propertyName, value, this)
```

好处是零序列化代码、改完立刻生效；代价是**副作用藏在属性里**，改名要走 `WriteOverInstanceToKey` 这种特殊分支，`isWindowClosing` 还得单独挡一下。

### 2. 桌面挂接：SetParent 到 SHELLDLL_DefView（真子窗口）

`DeskFrameWindow.SetAsDesktopChild()`：

```csharp
// EnumWindows 里找任意带 SHELLDLL_DefView 的顶层窗口
IntPtr shellView = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);
SetParent(hwnd, shellView);
style &= ~WS_POPUP;            // 去掉弹出窗口样式
// 之后 SetWindowPos(SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE|SWP_SHOWWINDOW)
```

和栖桌的区别：栖桌用 `SetWindowLongPtr(GWLP_HWNDPARENT)` 把窗口**owner**设成 WorkerW（保留顶层窗口），DeskFrame 直接把 frame **变成桌面视图的子窗口**。后者天然和图标同层、不会被盖上，但跨进程 SetParent 后 WPF 的 DPI、输入、坐标都要自己扛（它也确实自己算 DPI：`GetDpiForWindow` + `Graphics.FromHwnd`）。

### 3. "点桌面空白处"用 UI Automation 判定，而不是自己算坐标

`MainWindow` 里常驻全局双击检测，命中条件：

```csharp
IntPtr hwndUnderCursor = WindowFromPoint(pt) 的结果；
if (hwndUnderCursor == desktopListView && !IsDesktopIconHit(pt))  → 触发隐藏/显示

static bool IsDesktopIconHit(POINT screenPt) {
    var element = AutomationElement.FromPoint(new Point(screenPt.X, screenPt.Y));
    return element.Current.ControlType == ControlType.ListItem;   // 点的是图标就不算空白
}
```

先 `FindWindow("Progman")` → `SHELLDLL_DefView` → `SysListView32/FolderView` 拿桌面列表句柄，再和光标下的窗口比对。这套做法比截图比色稳，但依赖 UIAutomation，在部分系统上慢。

### 4. 容错靠"定时检查 + 全量重建"

`InstanceController.CheckFrameWindowsLive()`：遍历所有 frame 窗口，句柄为 0 的计数；一旦**全部**失效（Explorer 重启的典型症状），直接关掉全部、清空集合、`InitInstances()` 重来。部分失效则只调 `AdjustPosition()` 重新定位。

对比栖桌：栖桌是**守护进程 + 持久化恢复映射**保证桌面图标能还原；DeskFrame 管的是自己的窗口，所以"重建"就够了，不管原生图标状态。

---

## 四、代码风格（特点鲜明，优缺点都明显）

**优点**

- 功能密度高：一个 frame 的窗口能搜、能过滤（正则）、能自定义排序、能内嵌打开文件夹、能挂虚拟桌面、能灰度化非活动窗口。
- 动画完整：透明度、展开高度、灰度（`Shaders/GrayscaleEffect.ps` 像素着色器）、标题栏图标淡入，都有独立的 `Animate*` 方法。
- 多语言：`Lang.resx` + cs-CZ 等，`Properties/Lang.Designer.cs` 45 KB。

**缺点（不建议学）**

- **上帝类**：`DeskFrameWindow.xaml.cs` 193 KB / 一个类；文件枚举、排序、拖放、动画、右键菜单、桌面挂接全在里面。
- **命名随意**：`_customOderFiles`（Order 拼错）、`LastAccesedToFirstRow`（Accessed 拼错）、`Forms = System.Windows`（别名指向 WPF 却叫 Forms）、`isWindowClosing` 公开字段。
- **副作用藏在 getter/setter 里**，属性一改就落盘，没有事务、没有撤销。
- **`async void` + UI 线程 `Thread.Sleep` 轮询**，`SetAsDesktopChild` 里直接 `while(true) { ... Thread.Sleep(1000); }`。
- 依赖重：8 个 NuGet + 2 个 COM 引用 + 一个仓库内二进制 `VirtualDesktop.dll`。

---

## 五、和栖桌的对照

| 维度 | DeskFrame | 栖桌 |
| --- | --- | --- |
| 桌面挂接 | `SetParent` 到 `SHELLDLL_DefView`（子窗口） | `GWLP_HWNDPARENT = WorkerW`（owner，保留顶层） |
| 持久化 | 注册表，逐属性写 | `state.json` + 快照 + 撤销 |
| 代码组织 | 一个 193 KB 窗口类 | 按模块拆（Controller / FenceWindow / FileFeatures…） |
| 第三方依赖 | 8 NuGet + 2 COM | 0（只用 .NET Framework 自带） |
| Explorer 重启 | 定时检查，全灭重建 | 守护进程 + 恢复映射 |
| 原生图标 | 不接管，只把自己塞进桌面层 | 有整层隐藏开关，尚未逐个接管 |
| 图标来源 | 真实文件夹 + FileSystemWatcher | 分组归属 + 文件夹映射，两套语义分开 |

**可借鉴的两点**：① 桌面子窗口挂接方式（如果栖桌遇到分区被图标层盖住的问题）；② UIAutomation 判"点的是不是图标"，比纯坐标判定稳。

**不要借鉴的**：注册表逐属性持久化、上帝类、`Thread.Sleep` 轮询。
