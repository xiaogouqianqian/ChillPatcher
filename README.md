# ChillPatcher (xgqq 修改版)

本项目是基于 [ChillPatcher](https://github.com/BeyondtheApex/ChillPatcher) 的修改版本，主要修复了部分 UI 交互逻辑，并新增了 Bilibili 流媒体播放支持。

## ✨ 新增功能

### 📺 Bilibili 音乐模块 (`ChillPatcher.Module.Bilibili`)
新增了独立的 Bilibili 模块，支持将 B 站收藏夹作为游戏的音乐源。
* **流媒体播放**：通过 FFmpeg 实现音频流式播放，无需完全下载。
* **账号集成**：内置二维码扫码登录功能，支持同步个人收藏夹。
* **封面支持**：自动获取视频封面作为游戏内的专辑封面，并支持中心裁切适配。
* **依赖**：bilibili支持需要ffmpeg（release中已经附上）

## 🛠️ 问题修复与优化

### 1. 优化外部歌单加载逻辑（取消自动全选）
* **问题描述**：插件启动时会自动将所有外部来源的歌曲强制加入游戏的“当前播放列表”，导致玩家一进入游戏，播放列表就被成百上千首歌曲填满，且 UI 上的标签并未显示为选中状态，造成逻辑割裂。
* **修复方案**：修改了主程序 `Plugin.cs` 的同步逻辑。现在外部歌曲只会同步到游戏的**内部数据库**中，**不再自动修改存档的选中状态**。只有当玩家在 UI 中手动勾选对应的歌单时，歌曲才会加载到播放列表中。

### 2. ui改进
* 修正了部分时候歌单选择菜单ui无法正确显示歌单名称的问题

---

## 🔗 原作者信息
本项目基于 **BeyondtheApex** 开发的开源项目 ChillPatcher 进行修改。感谢原作者的杰出工作！

* **原仓库链接**: [https://github.com/BeyondtheApex/ChillPatcher](https://github.com/BeyondtheApex/ChillPatcher)