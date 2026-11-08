# Flotsam Tutor · 浮游世界物品指南

一个随《浮游世界》启动的 Windows x64 模组。提供独立配方小窗、顶部库存批量丢弃、小地图和快捷航行。

## 安装与更新

1. 退出游戏，从 [Releases](https://github.com/2683445453/FlotsamTutor/releases) 下载并完整解压 `FlotsamTutor.zip`。
2. 双击 `Install.cmd`。如果出现目录选择框，选择含有 `Flotsam.exe` 的文件夹。
3. 像平常一样从 Epic 或 Steam 启动游戏，模组会自动加载。

更新时运行新版 `Install.cmd`。卸载时退出游戏并运行 `Uninstall.cmd`。卸载保留可供其他模组使用的 BepInEx。

适用于已验证的 Flotsam `1.0.1f9` Windows x64 构建。安装包内置 [BepInEx 5.4.23.6](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6)，安装器会检查游戏版本和已有加载器冲突。

## 游戏内操作

| 功能 | 操作 |
| --- | --- |
| 配方查询 | 在物品图标上按 **鼠标中键**。点击材料继续查，点击返回箭头回到上一页。 |
| 工作站定位 | 点击配方中央的工作站按钮，镜头会定位到最近已建成的同类工作站并打开原生面板。 |
| 修改查询键 | 在 **设置 → 游戏设置 → 模组 · 配方查询键** 打开录入开关，按新键或组合键。Esc 取消。可用字母、数字、F1–F12、鼠标中键和侧键，也可组合 Ctrl、Shift、Alt。 |
| 丢弃物品 | 在顶部库存栏普通右键，拖动滑块或输入数量后确认。弹窗打开时暂停，关闭后恢复原速。 |
| 小地图 | 在 **设置 → 游戏设置** 分别打开小地图和快捷航行。滚轮缩放，右键拖动，开启航行时按住左键移动。 |

普通右键保留给顶部库存丢弃。想沿用旧版查询方式，可录入 **Ctrl＋右键**。

## 从源码构建

需要 PowerShell 7.2+、.NET SDK 和本地已安装的受支持游戏版本。

```powershell
./build.ps1 -GameDirectory 'D:\Games\Flotsam'
```

构建会下载并校验固定版本的 BepInEx，输出 `dist/FlotsamTutor.zip`。游戏文件仅用作本地编译引用。

## 许可

项目许可见 [LICENSE](LICENSE)。随发行包附带的 BepInEx 许可见 [LICENSE.BepInEx.txt](LICENSE.BepInEx.txt)。
