# README 展示素材

这些文件用于 GitHub 首页，不复制进应用的运行目录。

| 素材 | 用途与来源 |
| --- | --- |
| `hero-concept.png` | imagegen 生成的品牌概念主图，非软件截图；[完整提示词](PROMPTS.md) |
| `overview.gif` | 桌面分区、搜索组件、矢量桌宠的循环演示 |
| `feature-tour.mp4` | 同一组演示帧编码的 12 秒视频，供播放与分享 |
| `partition-documents.png`、`partition-pictures.png` | 程序原生分区组件，以虚构文件展示标签切换 |
| `search-all.png` | 程序原生搜索组件，结果为明确标记的演示数据 |
| `pet-loop.gif` | 程序内 `VectorCatVisual` 的真实动画帧 |

功能素材由 `media-render.cs` 调用已编译程序中的 WPF 组件生成，使用独立示例目录，不读取个人桌面、真实搜索历史或用户配置。这些素材是组件演示，未录制用户的桌面操作。概念主图展示视觉方向，不能据此推断已实现的界面细节。

## 重建功能素材

在仓库根目录构建测试程序，然后运行 `build-media.ps1`。可用 `-TestBinary` 指定已验证的测试 EXE；`-Python` 指定带 Pillow 的 Python。Python 和 Pillow 仅用于文档 GIF 编码，不是应用构建或运行依赖。临时编译器输出、示例文件和逐帧 PNG 放在忽略的 `experimental/` 中。

```powershell
& .\code\assets\readme\build-media.ps1
```

动图使用有限帧数和共享调色板控制体积，静态截图保留原始清晰度。更新文案在 `media-copy.json` 中完成。主图单独生成，重建脚本不会替换它。

传入 `-Ffmpeg ffmpeg` 可同时编码 MP4；也可对已有帧单独运行 `encode-video.ps1`。FFmpeg 仅是可选的文档制作工具，不随栖桌分发，不参与应用构建或运行。视频使用与动图相同的演示帧，不冒充桌面操作录屏。
