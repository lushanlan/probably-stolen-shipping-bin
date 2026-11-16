# 出货箱（Shipping Bin）

《深空当铺 / Probably Stolen》的容器类 Mod：买一个可移动、占背包格的出货箱，把想卖的东西放进去，**睡觉时**统一出售；多个箱子各自独立升级容量。

- Mod ID：`lushanlan.shipping_bin`
- 版本：`0.1.0`
- 作者：lushanlan
- 依赖：游戏本体（含游戏自带的 `0Harmony.dll`），不需要其它 Mod 或框架。
- 游戏版本：本机实测为 `0.46D`（见 manifest 的 `<GameVersions>`；版本不符只会显示提示，不会阻止加载）。

## 安装

1. 关闭游戏。
2. 把本目录（含 `manifest.xml`、`ShippingBin.dll`、`Localization\`、`Textures\`）整个放进游戏的 `Mods\` 目录，例如：

   ```text
   <游戏安装目录>\Mods\shipping-bin\
       manifest.xml
       ShippingBin.dll
       Localization\zh.csv
       Localization\en.csv
       Textures\Items\shipping_bin_v2.png
       Textures\Items\shipping_bin.png
   ```

3. 启动游戏。若 Mods 菜单里能看到「出货箱」，说明加载成功。
4. **不要**改名 `ShippingBin.dll`，也不要把它从 `manifest.xml` 的 `<Assemblies>` 里删掉。

## 使用

- **获得**：买下店铺后，在王尔德恩惠（Wild Network）里购买；每天限购一次，没有免费首箱。
- **使用**：把物品拖进出货箱。赃物、违禁品也能放，是否出售由夜间规则决定；睡前可以随时取回。
- **结算**：睡觉时统一结算所有箱子（关门、离开店铺都不会出售）。次日早间报告会出现两行黑色文字：「出货箱：卖出 N 件。」与「出货箱销售额：M。」。结算失败时，失败详情只写入模组日志，不会写进早间报告。
- **升级**：把 `junk` / `scrap_metal` / `metal_ingot` 拖到出货箱上（与原版拖拽升级同一种手势）。升级只扩容量，不提高售价；每箱点数独立。悬停箱子可看到升级进度、下一档阈值与当前实际容量。
- **容量**：初始 10x10 → 25 点 17x10 → 100 点 17x20 → 300 点 32x30（共 960 格，单页显示）。旧存档中更大的箱子不会被缩容；形状迁移失败会记下「待扩容」，开箱或睡觉时自动重试，不额外消耗材料。
- **自动化**：原版自动化料斗（进料斗 `hopper_in`、出料斗 `hopper_out`）不会被出售，放在箱子里也会整根保留并在报告里说明原因。

## 配置

游戏内设置界面可改（8 项）：出售倍率（默认 0.8，原生文本框可直接输入数字）、赃物涉案倍率（默认 2.0）、允许出售赃物、允许出售违禁品、黑市声望 ≥100 时涉案减半、≥200 时全免、提前显示购买项（默认关）、新局赠送（默认关）。

配置文件：`<persistentDataPath>/ModConfigs/lushanlan.shipping_bin.json`。删除该文件即可恢复默认值。

## 卸载

1. 关闭游戏。
2. 从 `Mods\` 删除本模组目录。
3. **注意**：如果存档里还有本模组的出货箱（以及箱内物品），卸载后这些物品会变成无法识别的物品。卸载前请先：把箱内物品取出 → 把空箱丢进垃圾桶/卖掉 → 存一次档。或者先备份存档。
4. 如需清理残留配置，删除 `ModConfigs\lushanlan.shipping_bin.json`。

## 已知限制

- 结算失败（极少数情况下）依赖持久记账单在后续开箱/读档/睡觉时重试；「至多一次」语义尚未在真实崩溃/断电场景下实机验证。
- 32x30 内容窗口在低分辨率下可能偏宽（原生窗口无滚轮），属原版 UI 限制。
- 旧存档里 2x2 占格的箱子不会自动变成 4x4，以免压到相邻物品。
- 料斗保护只覆盖原版两种料斗；其它 Mod 的自动化设备未做保护。

## 许可

本模组原创代码与文档使用 MIT 许可证（同包 `LICENSE`）。游戏本体、Unity、Harmony 及其它第三方材料的权利不由本包重新授权。箱体贴图为 AI 生成素材（方舟 Seedream），来源记录见仓库 `art/README.md`。

---

# Shipping Bin (English)

A movable container for *Probably Stolen*: put goods inside, and they are sold **when you sleep**; each bin has its own capacity upgrades.

- Mod ID: `lushanlan.shipping_bin`
- Version: `0.1.0`
- Author: lushanlan
- Requires: the base game (bundled `0Harmony.dll`). No other framework.

## Install

1. Close the game.
2. Copy this whole folder (with `manifest.xml`, `ShippingBin.dll`, `Localization\`, `Textures\`) into `<game>\Mods\shipping-bin\`.
3. Launch the game; the Mods menu should list "出货箱".
4. Do not rename `ShippingBin.dll` or remove it from `<Assemblies>` in `manifest.xml`.

## Usage

- Buy it from the Wild Network after owning the store (one per day, no free first bin).
- Drag items in; stolen/contraband can be stored, and the nightly rules decide whether they sell. Take items back before sleeping if you change your mind.
- Settlement happens only when you sleep. Next morning's report shows two black lines: items sold and total sales. Failures are written to the mod log only.
- Upgrade by dragging `junk` / `scrap_metal` / `metal_ingot` onto the bin. Capacity only (10x10 → 17x10 at 25 pts → 17x20 at 100 pts → 32x30 = 960 cells at 300 pts). Old larger bins are never shrunk.
- Vanilla automation hoppers (`hopper_in` / `hopper_out`) are never sold and keep the whole container instead.

## Uninstall

Close the game, delete the mod folder. **Before uninstalling**, empty and dispose of any shipping bins in your save (otherwise they become unknown items), or back up the save. Delete `ModConfigs/lushanlan.shipping_bin.json` to reset settings.

## License

Original code and docs of this mod are MIT-licensed (see `LICENSE`). Game, Unity, Harmony and other third-party materials are not relicensed here. The bin sprite is AI-generated (Volcengine Seedream); provenance is recorded in `art/README.md` of the source repository.
