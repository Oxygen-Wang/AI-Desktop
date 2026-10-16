# SnapNote 截图批注

独立 Windows 工具，使用系统 .NET Framework 4.x 编译，无需安装 SDK 或第三方包。

```powershell
& D:\fences\code\snapnote\build.ps1
& D:\fences\output\SnapNote\SnapNote.exe
```

把 PNG、JPG、BMP 或 GIF 图片拖进窗口，也可以点击“导入图片”、按 Ctrl+O 选择文件，或按 Ctrl+V 粘贴剪贴板图片。默认是“编号 1 2 3”工具：单击图片连续放置编号，双击编号填写说明。也支持画笔、矩形、箭头、文字、颜色与撤销（Ctrl+Z）。

“保存文本”导出编号、说明和坐标；PNG 和 PPTX 导出已合成批注的图片，PPTX 为单页演示文稿。JSON 保存原始图片（Base64 PNG）与结构化批注，之后可以用“打开 JSON”继续编辑，也可供 AI/MCP 程序读写。

JSON 格式版本为 `snapnote-1`，顶层字段为 `format`、`image`、`marks`。每个批注包含 `kind`、`x1`、`y1`、`x2`、`y2`、`text`、`note`、`color`。当前 EXE 尚未内置模型调用。
