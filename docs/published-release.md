# v0.1.0 发布记录

2026-11-16：SteamCMD 使用作者账号成功上传，输出 `Committing update...Success.`。

- 工坊：[可能是星露谷：出货箱 | Shipping Bin](https://steamcommunity.com/sharedfiles/filedetails/?id=3840748573)
- Workshop PublishedFileId：`3840748573`，AppID：`4348910`。
- Steam 公开接口核验：result=1、visibility=0（公开）、banned=0，标题匹配。
- [GitHub v0.1.0 Release](https://github.com/lushanlan/probably-stolen-shipping-bin/releases/tag/v0.1.0)
- GitHub 仓库公开，模组采用 MIT；原版 IL 调查文件已从公开 main 历史移除，原始证据及完整历史备份仅留本地。
- 安装包 SHA256：`DC4E3863BA348CF1CAC0740B958771EE74714BC88685FA535EFF7A027BF221E5`。

后续运行 tools/make-workshop-vdf.ps1 默认更新同一工坊条目（visibility=0），不要将 publishedfileid 改回0，否则会新建条目。

上传成功及公开API核验不等于订阅安装实测。验证订阅版时，本地 Mods 中相同 Mod ID 的副本会优先，须暂时移开该副本，不能删除玩家存档。
