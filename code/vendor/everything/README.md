# Everything 搜索依赖

这里保留栖桌全盘搜索需要的官方 x64 运行文件，下载源码后无需再从本机的参考项目或工具目录补文件。构建时按固定清单复制到 `dist/Everything/`。

| 文件 | 用途 |
| --- | --- |
| `Everything.exe` | Everything 1.4.1.1032 索引程序 |
| `Everything.lng` | 官方多语言资源 |
| `Everything64.dll` | SDK 接口，供栖桌异步查询索引 |
| `es.exe` | ES 1.1.0.38，SDK 不可用时的命令行查询备用入口 |
| `LICENSE-*.txt` | Everything、ES 和 SDK 的许可与版权声明 |
| `SOURCE.json` | 官方下载地址、归档及文件 SHA256 |

这些是第三方运行依赖，不是栖桌生成物。未收录个人配置、索引数据库、搜索历史或完整参考项目。官方 SDK 的其他架构、开发示例和无关工具不参与本应用构建。

应用优先使用设置中指定的 Everything 程序及同目录依赖，然后查找随构建附带的 `Everything/`，最后兼容程序目录和系统安装位置。旧配置中的自定义路径仍有效，失效时回退到附带版本。

在控制面板“文件搜索”中点击“启动 / 检查 Everything 连接”。首次使用需完成 Everything 的索引设置；NTFS 全盘索引通常需要启用 Everything 服务或使用管理员权限，栖桌不自动安装服务或修改权限。未连接时会显示本机有限范围搜索状态。

依赖来源：[voidtools 下载页](https://www.voidtools.com/downloads/)、[SDK 文档](https://www.voidtools.com/support/everything/sdk/)、[ES 项目](https://github.com/voidtools/ES)。许可原文与这些文件一同保留；更新版本时应重新下载官方文件、核对来源并更新 `SOURCE.json`，不要从正在使用的目录复制配置或数据库。
