# 出货箱（Shipping Bin）

《深空当铺 / Probably Stolen》的一个容器类 Mod：买一个可移动、占背包格的出货箱，把想卖的东西放进去，**睡觉时**统一出售；多个箱子各自独立升级容量。

- Mod ID：`lushanlan.shipping_bin`
- 版本：`0.1.0-dev`（**候选构建，尚未实机验收；请不要用于正式存档**）
- 作者：lushanlan
- 依赖：游戏本体；无第三方库（Harmony 使用游戏自带 `0Harmony.dll`）

## 当前状态

已完成：规则层（纯 C#，游戏外可测）、游戏适配层（物品工厂/夜间结算事务/购买/升级/调试赠送/本地化/配置）、真实编译、打包、官方 ModScanner 静态验证。

未完成（需要实机验收）：游戏内放置、开箱 UI、拖入材料升级、购买界面按钮、睡觉结算的实际表现、存读档往返。详见 [docs/acceptance.md](docs/acceptance.md) 的"未验证风险"与"最小实机验收步骤"。

## 玩法

1. **获得**：买下店铺后，在王尔德恩惠（Wild Network）里购买；每天限购一次，无免费首箱。
2. **使用**：把物品拖进出货箱（赃物、违禁品也能放，是否出售由夜间的规则决定）。睡前可以随时取回。
3. **结算**：睡觉时（`OnGoingSleepEarly`）一次性结算所有箱子。关门、离开店铺都不会出售。
4. **升级**：把 `junk` / `scrap_metal` / `metal_ingot` 拖到出货箱上（与原版瓶子打印机同一种交互）。只扩容量，不提高售价，不把材料价值加进箱子价值。
5. **容量**：四档，名称与尺寸对照原版“储藏区”系列（IL 核实）：初始 10×10（原版储藏区）→ 25 点 17×10（原版储藏区（扩建））→ 100 点 17×20（两倍扩建，推导值）→ 300 点 32×30（超科技储藏区等容 **960** 格，单页实现）。满级 960 格。旧存档里更大的容器不会被缩容；若目标形状“变宽变矮”（如旧档 12×12 → 17×10），升级时会安全迁移（取出→重塑→放回，失败完整回滚）。迁移失败会记下“待扩容”（不再消耗材料），开箱或睡觉时自动重试并提示。箱子本体占 **4×4** 格（64×64 贴图）；旧存档里的 2×2 箱子保持原占格以免与背包邻居重叠。

## 出售规则（摘要）

- 售价 = 物品**当前状态显示估值** × 0.8（可配置；估值直接复用原版整树估值，含容器后代、锁后物品排除、`IGNORE_CHILD_VALUE`、商店事件、液体容器价）。
- 容器按"一件物品"整体出售；只要**任一后代**不可出售，整个待售根保留并给出中文原因。
- 保护（只用原生可证实的信号）：剧情关键物（运行时读取原版 `importantStoryItemIds`，两条数据）、当前激活委托单 `StoreRequest.CanThisItemFillRequest` 命中的物品、`NOT_FOR_RESALE`、`MODULE_STUCK`、危险废物、上锁物品、非玩家物品、出货箱自身与嵌套出货箱。**注意**：`IMPORTANT_TAG` 与 `MACHINE`/`DOCUMENT` 都**不**作为拒售条件——IL 证实原版销售通道没有任何类型检查，只有"以物抵债 Repo"才拒机器/文件，而 Repo 是高利贷催收专用。证据见 [docs/il/quest-protection.md](docs/il/quest-protection.md)。
- 赃物 / 违禁品各自有开关，默认开启。
- 涉案：以原版基础涉案金额（`GetNegociatedValue` × `STOLEN_VALUE_INT%`）为基准，默认 ×2；违禁品固定按原版 1:1 计，不套用赃物公式，也不提供额外倍率设置。
- 黑市声望快照：整晚所有箱子都用**睡觉开始时**的声望。≥100 减半、≥200 全免，两个开关默认开，全免优先；关闭全免后 200 仍减半。
- 假贴标：复用原版 `VendingMachineHelper.IsMislabelled`，只额外扣黑市声望（不引入原版 FRAUD/ENDANGERMENT 犯罪）。全晚按实际出售金额的 1% 计，档舍五入（`.5` 远离零），有假贴标销售时至少 1、最多 20（总夜上限）。

## 配置

配置写在 `<persistentDataPath>/ModConfigs/lushanlan.shipping_bin.json`，游戏内设置界面可改：

| 键 | 默认 | 范围 | 说明 |
|---|---|---|---|
| `sale_multiplier` | 0.8 | 0.0–5.0 | 出售倍率（按当前显示估值）；设置界面为**原生文本框**，直接输入数字、显示当前值 |
| `stolen_multiplier` | 2.0 | 0.0–10.0 | 赃物涉案倍率；同上，原生文本框直接输入 |
| `allow_stolen` | 开 | — | 关闭后箱内赃物整件保留 |
| `allow_contraband` | 开 | — | 关闭后箱内违禁品整件保留 |
| `half_at_100` | 开 | — | 黑市声望 ≥100 涉案减半 |
| `exempt_at_200` | 开 | — | 黑市声望 ≥200 涉案全免 |
| `early_unlock` | 关 | — | 买店前就显示购买项（只影响本模组入口） |
| `debug_grant` | 关 | — | 调试模式：每个新开局赠送一个箱子（`debug_grant_done` 记每局一次；空间不足保留 pending 补领；正常购买的箱子不影响补领） |

数值说明：0.8 出售倍率、2.0 赃物倍率、违禁品固定 1:1 是默认值；购价 2000、累计 25/100/300 点、四档容量 10×10 / 17×10 / 17×20 / 32×30 均由用户定案。倍率合法范围由 `Snapshot()` 强制 clamp，设置界面的 tooltip 也写明范围；数值集中定义在 `src/Core/Rules.cs`，测试覆盖边界。

结算失败语义（不夸大）：付款前失败会整单回滚并逐件核实是否回到箱内；`Pay` 是唯一不可逆点，付款中途失败时已付款的照常销毁、未付款的放回箱内。付款后的涉案/假贴标改为**持久记账单**：先落盘再逐行提交（`Pending → InFlight → Applied`），失败时**不写结算标记**，下次读档或睡觉时补提交；遗留 `InFlight` 按"至多一次"视为已生效，避免重复扣。记账只按**确实已付款**的条目重算金额与假标签（不按原整单）。

## 构建

环境：Windows + PowerShell 7；游戏目录在仓库父目录（`..\Probably Stolen_Data\Managed`）；`csc.exe` 来自 .NET Framework 4.x（不需要 dotnet SDK）。

```powershell
pwsh -File .\build.ps1      # 输出 build\ShippingBin.dll
pwsh -File .\package.ps1    # 构建 + 复制到 build\package-dev
pwsh -File .\tools\test-rules.ps1   # 纯规则测试（363 checks，游戏外）
pwsh -File .\tools\scan.ps1         # 官方 manifest parser + ModScanner 静态验证
```

## 安装

1. 关闭游戏。
2. 把 `build\package-dev` 整个目录复制到游戏目录下的 `Mods\shipping_bin\`（目录名随意，`manifest.xml` 与 `ShippingBin.dll` 必须在同一层）。
3. 启动游戏，在 Mods 列表中启用 `出货箱`。
4. **只用独立测试存档验收**，不要动现有正式存档。

## 卸载

1. 关闭游戏，删除 `Mods\shipping_bin\`。
2. 可选：删除 `ModConfigs\lushanlan.shipping_bin.json`（配置）。
3. 注意：已经存在于存档里的出货箱物品在卸载后会变成缺失物品；卸载前请先把箱内货物取出，并处理掉箱子本身。是否保留该存档由你决定。

## 目录结构

```
src\Core\      纯规则层（无游戏类型）：Rules / ItemFacts / Planner / Commit / Ledger / GrantPolicy
src\Game\      游戏适配层：ModIds / Loc / ModSettings / BinItem / BinWorld /
               FactsBuilder / NightSettlement / PendingLedger / Purchase / DebugGrant
src\ShippingBinMod.cs   IMod 入口（只做装配）
tests\CoreTests.cs      规则/事务/记账单/形状迁移/状态机测试（363 checks）
packaging\              manifest.xml + Localization\{zh,en}.csv
tools\                  build/扫描/规则测试/IL 调查辅助脚本
docs\                   决策记录、验收报告、IL 取证报告（docs\il\）
```

## 文档

- [docs/acceptance.md](docs/acceptance.md)：改动文件清单、真实验证输出、需求对照、未验证风险、最小实机步骤
- [docs/decisions-shipping-bin.md](docs/decisions-shipping-bin.md)：决策记录（含本次实现定稿）
- [docs/il/](docs/il/)：本机程序集 IL 取证报告（定价、犯罪/保护、Mod API、存档持久化、任务物与禁售证据、容量档位与配置 UI）
- [docs/art-spec.md](docs/art-spec.md)：出货箱贴图规范（新箱子 `packaging\Textures\Items\shipping_bin_v2.png` 64×64 = 4×4 格；legacy `shipping_bin.png` 32×32 = 2×2 格，供旧存档兼容；每格 16px，透明区不占格）
- `docs/settlement-handoff.md`、`docs/integration-review.md`、`docs/game-test-checklist.md`：历史原型记录，**不是**当前实现说明

## 安全声明

- 仓库不含游戏程序集、工具缓存与构建产物（见 `.gitignore`）。
- 未修改原版程序集，未触碰正式玩家存档；所有 IL 调查均为只读。
- 构建产物位于 `build\`（已被忽略），发布包需本人验收后自行打 tag。
