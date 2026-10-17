# 栖桌 0.4.1：Windows 右键菜单和真实图片预览

用户反馈：右键应当具备标准 Windows 操作；图片放大后仍为类型图标。附图是功能/外观参考，不是操作文件或第三方菜单的授权指令。

## Windows 菜单

通过 SHParseDisplayName / SHBindToParent / IShellFolder.GetUIObjectOf 获取文件或文件夹项目的 IContextMenu；空白区域通过当前文件夹的 CreateViewObject 获取背景菜单。QueryContextMenu 构建原生 HMENU，TrackPopupMenuEx 返回用户选择，InvokeCommand 使用命令偏移及 Unicode 参数执行。IContextMenu2/3 接收初始化、自绘、测量和菜单字符消息，支持动态扩展。

Windows 自己提供文件操作和本机已安装的扩展。栖桌操作位于独立子菜单。自有 WPF 视图的查看/排序/刷新由本视图处理，界面仍为原生 Win32 菜单。背景若没有系统粘贴项，补充剪贴板感知的粘贴项，交给 Windows IFileOperation 处理复制或移动；快捷方式使用 IShellLink/IPersistFile 创建，避免覆盖同名链接。重命名使用 Shell 文件操作，并更新分组、映射、忽略与工作目录引用。

普通逻辑分区没有自己的物理目录，因此空白菜单操作系统桌面；映射分区操作当前实际目录。Native Windows 11 现代菜单界面、完整 Explorer ShellView 撤销历史及每个第三方扩展的表现不在本次验证范围。

主要官方接口说明：[IContextMenu](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-icontextmenu)、[CreateViewObject](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellfolder-createviewobject)、[IContextMenu3](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-icontextmenu3)、[IFileOperation](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation)。

## 缩略图与预览

PNG/JPEG/BMP/GIF/TIFF/ICO 优先使用后台 WIC 解码，OnLoad 读取完毕释放文件，冻结 BitmapSource 后送回 UI 线程。系统缩略图作为其他格式的候选，通过 IShellItemImageFactory 读取，失败保留类型图标。后台 STA 队列最多 512 项、缓存预算 32 MB / 最多 512 项；只请求视口中的图格，滚动与定时刷新补充加载。

缓存键含完整路径、修改时间、文件大小和预览尺寸；回调核对请求键，避免旧图片覆盖新选择。失败缓存在 20 秒后可重试。透明图片的桌面图格带浅色底衬，保持比例。详情窗口新增当前选择的图片预览，不显示完整文件路径。

放大预览检查发现部分系统 PNG 缩略图颜色/透明度不正确，改用原文件的 WIC 解码后验证通过。系统缩略图后台和资源释放参考 [IShellItemImageFactory.GetImage](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemimagefactory-getimage)。

## 验证

118 项核心和原生检查通过。本次新增覆盖真实图片像素、宽高比例、缓存更新、文件句柄释放、透明 PNG 黑色内容、损坏文件回退、原生菜单中的 copy/delete/properties/rename、背景查看/排序/刷新/粘贴、独立目录的 Windows 复制/移动/重命名/快捷方式，以及真实显示窗口内的异步缩略图。测试使用自己生成的独立文件，没有修改用户剪贴板或执行用户文件的删除命令。

9 项 Python MCP SDK 检查仍通过。渲染并检查了用户图片组的放大网格与详情预览，产物为 dist/picture-preview.png、dist/details-preview.png。

computer-use 只用于查找测试窗口，没有找到可操作的测试窗口，因此没有发送桌面输入。原生菜单创建和文件操作通过软件自己的回归测试完成。真实用户右键弹出和第三方菜单交互仍需实际反馈。

已备份布局并启动 QiDesk-20261013-173220.exe（0.4.1）。通过实际运行应用的 MCP 读到版本 0.4.1 和原有 7 个分组；7 个桌面分区均可见且在屏幕范围内。
