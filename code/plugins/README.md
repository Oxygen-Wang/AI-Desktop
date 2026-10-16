# 栖桌组件插件与组合包

在“插件与组件”点击“安装组件 / 组合包”，选择 JSON 文件，查看安装预览，再安装到组件库。点击单个卡片的“＋ 添加”或勾选多个组件组合添加。

也可以点击桌面组件区“＋ 添加组件”，或左键长按桌面空白处约 0.35 秒、拖动框选，松开后选择“在此添加组件”。

内置类型：clock、calendar、note、countdown、tasks、focus、choice。插件字段为 id、name、kind、description、text、accent。待办和随机选择器的 text 每行一项。插件为声明式内容与样式，不执行外部代码。

单插件文件为 `.qidesk-plugin.json`；桌面组合包为 `.qidesk-pack.json`。组合可从组件库勾选保存，或从已有桌面组件导出。朋友导入后可以再次添加、调整和导出。组合包保留内容、外观和相对位置，计时器进度与任务勾选重新开始。

可以安装 examples/what-to-eat.qidesk-plugin.json、examples/creative-prompt.qidesk-plugin.json，或整套 examples/focus-desktop.qidesk-pack.json。

字段与验证细节见 ../research/COMPONENT-LIBRARY.md。包大小最多 1 MB，最多 30 个组件，组合库最多 20 套。