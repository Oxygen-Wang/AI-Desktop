# 小猫形象素材来源

这里的 PNG 来自用户提供的参考项目 `references/PathCat-main`（径猫）：

- `references/PathCat-main/quietpet/assets/cat/` → 本目录，逐字复制，未做任何修改。

结构：

- `idle/cat_idle_0.png`、`sleeping/cat_sleeping_0.png` 等 46 个状态目录，每个目录一张 `<状态>` 单帧图，构成「默认猫」形象。
- `calico/cat_<状态>_0.png` 共 46 张，构成「毛绒三花」形象。
- `manifest.json` 是参考项目自己的清单，本应用不读取它。

`code/build.ps1` 会在编译后把这些文件原样复制到 `dist/cat/`，程序按 `AppDomain.CurrentDomain.BaseDirectory\cat` 读取。

PathCat 源码没有声明项目许可证（见其 README「源码尚未指定项目许可证」），这些图片由用户提供并授权在本项目中使用。若将来要对外分发，需要先确认素材授权。用户自行处理三花矢量形象，本轮保留现有素材及形象选择。
