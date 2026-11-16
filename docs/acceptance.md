# 出货箱 Mod 验收报告（候选构建，第 5 轮：末档 960、形状迁移安全、正视贴图接入）

- 日期：2026-11-16
- 构建：`build\package-dev`（候选开发包，**未做游戏内实机验收**）
- 结论：规则层与游戏适配层已实现并通过游戏外全部可执行验证（**349** 条规则/事务/记账单/形状迁移/状态机测试 + 真实编译 + 本机 ModScanner 扫描 209 个方法）；游戏内行为（放置、开箱、拖拽升级、购买按钮、睡觉结算、存读档）尚未实机运行，见"未验证风险"。**本报告不声称玩法已在游戏内验证通过。**

## 0. 针对上一轮验收驳回的修复（5 条）

| 驳回项 | 修复内容 | 验证 |
|---|---|---|
| ① `FactsBuilder` 仍用 `IMPORTANT_TAG && !isContainer` 一刀切，会误拦非容器重要机器，与报告矛盾 | 已**删除** IMPORTANT_TAG 作为保护条件（第 2 轮）。**第 3 轮进一步删除 `MACHINE`/`DOCUMENT` 类型拒售**：IL 证实原版 `PlayerStore.SellItem` 没有任何类型检查，`MACHINE`/`DOCUMENT` 只出现在"以物抵债 Repo"里，而 Repo 的唯一调用者是高利贷催收。现有保护只用原生可证实的信号：剧情物数据表 + 当前激活委托单 `CanThisItemFillRequest` + `NOT_FOR_RESALE` + `MODULE_STUCK_TAG` + 危险废物 + 锁。详见 §7 与 `docs\il\quest-protection.md`。 | 代码审查 + `docs\il\quest-protection.md` 的 IL 引用 |
| ② `DebugGrant` 用 `CountOwnedBins()>0` 清 pending，把正常购买的箱子当成已赠送 | 新增两层持久标记：`debug_grant_done`（每局 done，只赠一次）与专属物品 tag `lushanlan.shipping_bin.debug_grant_token`（只标本次赠送的箱）。新增纯规则 `GrantPolicy.Decide(enabled, done, pending, hasGrantTokenBin)` 决定 `None/MarkDone/Grant`。正常购买的箱子没有 token，因此**不会**阻塞 pending 补领；补领成功后写 done。空间失败保留 pending 并提示，重试时机为 `OnGameLoadedLate`/`OnWakingUpLate`/`OnGoingSleepEarly`。开关关闭时不改任何标记（重新打开后可补领）。 | `TestGrantPolicy` 6 组状态 + "pending 失败→玩家正常购买（无 token）→仍补领"场景 |
| ③ `Detach` 失败时回滚列表不含当前部分 detach 项；回滚吞异常却报 `RolledBack=true` | `Committer.Run` 在调用 `Detach` **之前**就把该条目加入 `attempted`（异常/失败项也在内）；回滚逆序调用 `ReturnToBin` 后必须再 `VerifyReturned`（适配层直接检查 `parentInventory==grid && grid.childItems.Contains(root)`）。`RolledBack` 仅在"付款尚未开始且所有放回都核实通过"时为 true；任何一条抛异常或核实失败都记入 `RollbackFailures`，`RolledBack=false`，并写 `Failures`。 | `TestCommit`：Detach 硬失败、部分 detach、detach 抛异常、回滚抛异常、`VerifyReturned` 失败 |
| ④ 付款后各步异常无补偿，不能宣称"任何一步失败都付款前回滚" | 事务语义改为**精确分段**（报告不再夸大）：付款前失败 = 全额回滚（逐条核实）；`Pay` 为不可逆点（契约要求原子加法），付款中途失败 = 已付款条目照常销毁（避免"钱+货"双得）+ 未付款条目逆序放回；销毁失败重试一次并如实上报，但条目仍记为已售；涉案/假贴标/写 day 失败都各自捕获并记录 `FailurePhase`，写 day 放最后。`CommitResult` 新增 `PaymentStarted/PaidAmount/FailurePhase/Failures/RollbackFailures/Settled`，夜报按真实状态输出（部分记账/回滚成功/回滚未确认/未写结算标记）。 | `TestCommit` 15 个故障场景（见 4.9 表） |
| ⑤ 复核标签显示估值、原版涉案、违禁副作用；不要新增用户未要求的设置或推定犯罪类型 | 已删除用户未要求的"违禁品涉案倍率"设置（`Rules.ContrabandCrimeFactor=1.0` 固定原版 1:1），配置与本地化同步删除。标签显示估值取入箱后**当前状态** `GetCurrentValue(false,true,true,false,0)`：原版物品进入非 backInv 容器时会剥离 contraband/客户临时特征（IL 已证实），因此不额外加成，与 tooltip 自洽。涉案基础沿用原版 `GetNegociatedValue() × STOLEN_VALUE_INT%`，犯罪类型只使用原版 `CheckSoldContraband` 的五种，不添加推定类型。 | 代码 + IL 报告交叉核对；`TestPlanner` 违禁品仍 500（`StolenMultiplier=5` 不借用） |

## 1. 第 2 轮验证输出（历史记录；最新第 3 轮输出见 §7）

```text
> pwsh -File .\build.ps1
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll

> pwsh -File .\tools\test-rules.ps1
[ok]   Rules
[ok]   Protection
[ok]   Planner
[ok]   Commit
[ok]   GrantPolicy

242 checks passed, 0 failed.
All core rule tests passed (game integration NOT tested).
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)

> pwsh -File .\package.ps1
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll
Development package only: E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\package-dev

> pwsh -File .\tools\scan.ps1
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=

Refused        : False
Error          :
MethodsScanned : 165
Capabilities   : {}
Findings       : {}
HardFlags      : {}
Pending        : {}
```

`tools\test-rules.ps1` 用 `csc.exe` 编译 `src\Core\*.cs` + `tests\CoreTests.cs` 并执行，非零退出即失败；`tools\scan.ps1` 使用本机 `Assembly-CSharp.dll` 的 `ModLoader.ParseManifestForTools` 与 `ModScanner.Scan`（非自造规则），扫描对象是最终包。

## 2. 第 2 轮包哈希（历史记录；最新哈希见 §7）

| 文件 | 字节 | SHA256 |
|---|---|---|
| `ShippingBin.dll` | 49,152 | `1377DCB4971F96E32D193F0F851D4604CE3ED05A2F517F327B79C35CE2664384` |
| `manifest.xml` | 498 | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| `Localization\zh.csv` | 4,576 | `6EB74136A35F84C9E063F98DF246B36975318A2AAE7FE3EBD70300193BBA875B` |
| `Localization\en.csv` | 4,943 | `A250B0E88C4A84C0DC5A85217C9C853AF094E4027943A6079A6B2F54A04DFA8C` |

本机 `csc.exe`（.NET Framework 4.0，无 Roslyn 确定性编译）每次重编译会生成新 MVID，`ShippingBin.dll` 哈希逐次不同属正常；`manifest.xml` 与两个 csv 哈希稳定，可用来确认交付内容。复核命令：`Get-FileHash build\package-dev\ShippingBin.dll -Algorithm SHA256`。

## 3. 第 2 轮改动文件清单（历史记录；第 3 轮见 §7.5）

新增：

- `README.md`：中文使用/构建/安装/卸载/配置说明
- `docs/acceptance.md`：本报告
- `src\Core\Rules.cs`：数值与公式集中定义（售价、涉案、门槛、容量、材料、购买冷却；违禁品固定原版 1:1）
- `src\Core\ItemFacts.cs`：物品事实树、拒绝码、保护判定
- `src\Core\Planner.cs`：全箱快照 → 计划（去重、整根保留、金额、涉案、假贴标）
- `src\Core\Commit.cs`：提交流程（复核 → 取下 → 核实脱离 → 付款 → 销毁 → 涉案 → 假贴标 → 落 day），含部分付款/回滚核实
- `src\Core\GrantPolicy.cs`：调试赠送状态机（纯规则，可测）
- `src\Game\ModIds.cs`：独立命名空间 ID/tag/配置键/本地化键/犯罪类型/贴图常量（含 `debug_grant_token`、`debug_grant_done`）
- `src\Game\Loc.cs`、`ModSettings.cs`、`BinItem.cs`、`BinWorld.cs`、`FactsBuilder.cs`、`NightSettlement.cs`、`Purchase.cs`、`DebugGrant.cs`
- `tests\CoreTests.cs`：242 条规则/事务/状态机测试（含故障注入的 FakeWorld；第 3 轮已扩到 315 条）
- `docs\il\pricing.md`、`docs\il\crime-protection.md`、`docs\il\mod-api.md`、`docs\il\persistence.md`：IL 取证报告

重写/修改：

- `src\ShippingBinMod.cs`：只做装配的入口（IMod）
- `build.ps1`：编译 `src\Core\*` + `src\Game\*` + 入口（响应文件传参；已加入 `GrantPolicy.cs`）
- `tools\test-rules.ps1`：csc 编译纯规则层并运行
- `packaging\manifest.xml`、`packaging\Localization\zh.csv`、`en.csv`：描述与全部运行时键（本轮删除违禁倍率键，新增结算失败明细键）
- `docs\decisions-shipping-bin.md`：追加"实现定稿"与"验收修复"

删除：

- `src\Rules.cs`（原型期算术骨架，已被 `src\Core\Rules.cs` 取代）
- 临时文件 `tests\_tail.txt`（已拼入 `tests\CoreTests.cs`）

未改动：`package.ps1`、`tools\scan.ps1`、`tools\inspect.ps1`、`tools\callers.ps1`、原版游戏程序集、玩家存档。

## 4. 需求逐项对照

### 4.1 容器与结算

| 需求 | 实现 | 验证 |
|---|---|---|
| 原生可移动、占库存的容器物品 | `BinItem.Create`：`CreateInventoryWindow` + `SetContentWindow` + `CONTAINER_TAG` | 编译；实机未验 |
| 多箱、各自容量与升级 tag | 点数写每箱自己的 `upgrade_points` TagState，随物品存档 | 规则测试：点数/余点/满级 |
| 满级有限且明显大于原大型容器 | 12×12=144 格 > 原版 72/77 格 | 规则测试：容量档位 |
| 只有睡觉出售、关门不卖 | 仅订阅 `OnGoingSleepEarly`，无 shutter 钩子 | 代码审查 |
| 睡前可取回 | 出售只发生在睡眠钩子 | 实机未验 |
| 全箱先快照再提交 | `Planner.Build` 先快照全部箱，再 `Committer.Run` | 测试：顺序、回滚 |
| 物品/金额/保存一致，防重复付款吞货 | 见 4.9 事务语义与故障注入表 | 测试：15 个故障场景 |
| 清晰中文夜间日志 | `WriteNightReport` / `WriteFailureReport`，覆盖成功与失败状态 | 本地化键 zh/en 各 73 条，键集一致 |

### 4.2 整树出售与保护

| 需求 | 实现 | 验证 |
|---|---|---|
| 容器连后代整体出售 | 待售根估值取 `GetCurrentValue(false,true,true,false,0)`，原版一次性递归后代 | IL 已证实 + 测试 `VanillaTreeValue` 优先 |
| 任一后代禁售则整根保留并说明理由 | `Protection.Evaluate` 递归整树，非空即 `RetainedEntry` | 测试：任务后代/嵌套箱/保护项 |
| 任务/非玩家物品保护 | 剧情物 = 运行时读取原版 `importantStoryItemIds` 两条；非拥有物品拒绝；**非剧情 IMPORTANT 物不额外保护（未支持范围，已明示）** | 测试 `reason_quest`/`reason_not_owned` |
| 出货箱本身及嵌套箱保护 | 每个节点 `IsShippingBin` 都判保留 | 测试：嵌套箱 |
| 去重 | 箱与待售根按引用去重 | 测试：跨箱重复、同箱重复列出 |
| 装配设备/容器估值复用原生 | 整树估值；`IGNORE_CHILD_VALUE`/锁后由原版处理 | 测试：`IGNORE_CHILD_VALUE`、锁后 |
| 避免重复算子价值 | 不挂 `SetBasicContainerValueChangeRule`，不手工累加子物品 | 代码审查 + IL 证据 |

### 4.3 售价、假标签、开关

| 需求 | 实现 | 验证 |
|---|---|---|
| 售价 = 当前状态显示估值 × 0.8（可设置） | `Rules.SaleAmount`，范围 0–5 | 测试：取整与边界 |
| 假标签按标签显示价、允许获利 | 估值取当前显示估值，假贴标不改变售价 | 测试：假贴标只影响声望 |
| 倍率范围集中说明 | 默认值与范围集中在 `src\Core\Rules.cs` 与 README | README 配置表 |
| 赃物/违禁品开关默认开 | 默认 true，关闭后整根保留 | 测试 `reason_stolen_off`/`reason_contraband_off` |
| 任务保护依据运行时标记，不按名字猜 | 原版 `importantStoryItemIds` 运行时读取 + 原版收购白名单同源类型判定 | IL 已证实 + 测试 |

### 4.4 涉案金额与声望

| 需求 | 实现 | 验证 |
|---|---|---|
| 涉案以原版基础而非实际收入，默认 ×2 | 基础 = `GetNegociatedValue` × `STOLEN_VALUE_INT%`，再乘 `StolenMultiplier` | 测试：6 档声望 × 两开关矩阵 |
| 原 `STOLEN_VALUE_INT` 是百分比 | `Rules.StolenBasis(value, percent)` | 测试：500@50% = 250 |
| BM ≥100 减半、≥200 全免，两开关默认开，全免优先，关全免后 200 仍减半 | `Rules.StolenFactor` 先判 200 再判 100 | 测试：全矩阵 |
| 全晚统一用起始声望 | 快照一次 `GetBMReputation()` 传入 `Planner.Build` | 测试：多箱正反序结果相同 |
| 避免原销售回调重复犯罪 | 不调 `PerformOnSoldCheck`/`MarkStolenOrContrabandSale`，逐条 `CommitCrime` | IL 已证实（原回调逐子树记罪） |
| 违禁品不能套赃物公式、不新增用户设置 | 违禁品固定原版 1:1（`ContrabandCrimeFactor`），类型只用原版五种 | 测试：`StolenMultiplier=5` 时违禁品仍 500 |

### 4.5 假贴标

| 需求 | 实现 | 验证 |
|---|---|---|
| 复用售货机 `IsMislabelled` | `FactsBuilder.SafeMislabelled(root)`，只在待售根判一次 | IL 已证实 |
| 只额外扣 BM 声望，不引入 FRAUD/ENDANGERMENT | 只调 `StoreReputation.ModReputation(FACTION_BLACK_MARKET,-n,false)` | IL 已证实 |
| 全晚实际出售金额 1%，四舍五入（.5 远离零） | `Rules.MislabelPenalty` 基于 `plan.MislabelledAmount` | 测试：半数、上限、下限 |
| 有销售至少 1、最多 20（总夜） | 全晚合计一次 | 测试：多箱合计与上限 |
| 在赃物快照减免后最后扣 | `Committer.Run`：涉案 → 假贴标 → 落 day | 测试：犯罪先于假贴标、犯罪异常时假贴标仍生效 |
| 归属清楚不重复 | 只在根判一次；子物品不单独累计 | 测试：子物品不重复计 |

### 4.6 购买

| 需求 | 实现 | 验证 |
|---|---|---|
| 只在王尔德恩惠正常购买，无免费首箱 | 只通过 `AddNetworkUpgrade` 注册 | 代码审查 |
| 重复买按日冷却，次日重置 | `cooldownDays=1`，原版 `OnDayStart` 递减 | `CanPurchaseToday` |
| 默认买店才显示 | 注册在 `Advanced` tab，沿用原版 `IsHiddenForRun` | IL 已证实 |
| 提前解锁默认关、仅影响自有入口 | `early_unlock` 只改本 id 的 `IsHiddenForRun` 结果 | `ShouldShowPurchase` |
| 空间失败不丢钱/恩惠/箱子，回滚统计冷却 | prefix 先探测空间，不足直接阻止扣款；onUnlock 放置失败按快照**绝对值**恢复现金/恩惠/两统计并复位升级状态 | 代码审查（实机未验） |

### 4.7 升级

| 需求 | 实现 | 验证 |
|---|---|---|
| 原生打印机式"物品当目标"交互 | `mayItemTargetThisFunc`/`canItemTargetThisFunc`/`onItemTargetThisFunc`，与瓶子打印机同形 | IL 已证实 |
| junk 1 / scrap_metal 2 / metal_ingot 5 可替代累计 | `Rules.MaterialPoints`（材料真实 ID 已核定：junk/scrap_metal/metal_ingot） | 测试：材料点数 |
| 各箱独立余点跨档保留 | 点数存每箱 tag；`ApplyUpgradePoints` 保留余点 | 测试：余点与跨档 |
| 满级拒绝、最后溢出中文提示 | 满级不消耗；溢出返回 `Overflow` 并提示 | 测试：29+5→溢出 4 |
| Tinkerer 20% 免耗仍加点 | `StartingPerkContent.ConsumeUpgradeMaterial` | IL 已证实 |
| 升级只扩容量、不提高售价、不加材料价值 | `ApplySize` 只改形状；不改 `unitValue` | 代码审查 |

### 4.8 调试赠送

| 需求 | 实现 | 验证 |
|---|---|---|
| 默认关 | `debug_grant` 默认 false | 配置声明 |
| 每局赠送一次、普通读档不重复 | `OnNewGameStarted` + `debug_grant_done` 持久标记 | `TestGrantPolicy` |
| 只识别本次赠送箱 | 赠送箱带专属 `debug_grant_token`；正常购买的箱子不影响补领 | `TestGrantPolicy` "正常购买不阻塞补领" |
| 不受买店限制、不改购买限制、不依赖 dev.txt | 直接创建+放置，不读 dev.txt，不改购买项 | 代码审查 |
| 空间失败持久 pending 并提示、不丢失不重复 | 失败保留 pending + 中文提示，多个时机重试；开关关闭时不改标记 | 代码审查 + 状态机测试 |

### 4.9 事务语义与故障注入测试（本轮新增，直接回应驳回 ③④）

事务顺序：`Revalidate → Detach(全部) → VerifyDetached(全部) → Pay(逐条) → Destroy → ApplyCrime → ApplyMislabel → MarkSettled`。

- **付款前**（复核/取下/核实失败）：回滚"本次尝试过的全部条目"（含失败的那一件），逆序 `ReturnToBin` 并逐条 `VerifyReturned`；只有全部核实通过才 `RolledBack=true`。
- **付款中**：`Pay` 是唯一不可逆点（契约要求原子加法）。某条付款抛异常时，已付款条目**照常销毁**（避免钱货双得），未付款条目逆序放回；`PaidAmount` 只累计真正成功的条目。
- **付款后**：销毁失败重试一次并如实记录（钱已付，条目仍计为已售）；涉案、假贴标各自 try/catch 记录 `FailurePhase`；`MarkSettled` 放最后，失败时报告"未写结算标记"，此时箱内已无可卖条目，同一晚再次睡觉只会重试写标记，不会重复付款。

| 故障注入场景（`tests\CoreTests.cs` 的 FakeWorld） | 断言 |
|---|---|
| 正常路径 | 付款 240、售 2、写 day 最后一步、付款前所有取下已核实 |
| 取下直接失败 | `RolledBack=true`、零付款、未写 day、两件都在箱内 |
| 部分取下（已脱离但报失败） | 失败项本身也被放回并核实 |
| 取下抛异常 | 异常项与前序项全部放回 |
| 回滚抛异常 / 放回未核实 | `RolledBack=false`、`RollbackFailures` 含物品名 |
| 核实脱离失败 | 全部回滚、零付款 |
| 复核失败 | 零副作用、不取下、不写 day |
| 第二件付款抛异常 | 付款 80、售 1、已付款销毁、未付款回箱、未写 day、`FailurePhase=pay` |
| 销毁瞬时失败 | 重试成功、钱货一致 |
| 销毁持续失败 | 钱已付仍如实记失败、仍标记结算 |
| 涉案提交异常 | 钱货照常、假贴标仍扣 8、已结算、`FailurePhase=crime` |
| 假贴标异常 | 涉案 2000 仍记录、已结算 |
| 写 day 失败 | `Settled=false`、已付款、如实上报 |
| 空夜成功 / 空夜写 day 失败 | 仍写 day；失败上报且未结算 |

## 5. 未验证风险（必须实机确认，报告不声称已通过）

1. **物品工厂与贴图**：`CreateInventoryWindow` + `GameItemElement` 的运行时组合能否正常显示/放置（贴图有 DLL 字符串证据，未在游戏内看过）。
2. **开箱 UI**：依赖原版 `ItemMouseDoubleClickHandler` 对带 `contentWindow` 物品的点击行为（IL 已证实），未实机点击。
3. **拖拽升级**：`mayItemTargetThisFunc`/`canItemTargetThisFunc` 的实际调用方向与触发条件；`ConsumeUpgradeMaterial` 的实际表现。
4. **购买链路**：两个 Harmony prefix 与 `IsHiddenForRun` postfix 是否按预期命中；`OnModAssetsLoaded` 注册后 UI 是否立即可见。
5. **空间探测**：`SpawnItem` 探针 + `TryFindOneValidInventorySlot` 的判定与实际放入是否一致。
6. **夜间结算**：`Expel`/`UncheckedAccept` 在真实库存/窗口中的行为；`OverlayHandler` 刷新；失败回滚的真实表现（游戏外仅用 FakeWorld 验证了编排）。
7. **存读档**：容器内容随子树保存/挂回（IL 已证实机制）；升级后 12×12 读档；`debug_grant_done/pending`、升级点数跨档读档。
8. **前柜台风险**：`EndNight` 的 `ClearFrontInv` 会销毁前柜台一切；本模组结算前尝试搬回并提示，但"搬不回去"的玩家路径未验证。
9. **涉案显示**：`SecData.OnNewDay` 在 BeginDay 对当夜金额先减 20（evidenceLevel<80），与售货机同源；界面显示可能比提交值低 20。
10. **假贴标判定**：`VendingMachineHelper.IsMislabelled` 对容器根的实际返回未实机验证。
11. **任务物保护不是"绝对保证"**（第 3 轮修正表述）：原生可证实的信号只有剧情物数据表（2 件）、当前激活委托单 `CanThisItemFillRequest`、`NOT_FOR_RESALE`、危险废物与锁；`IMPORTANT_TAG` 与 `MACHINE`/`DOCUMENT` 都**没有**"客户不买"的原生证据，因此不再据此拒售。残余风险：剧情脚本内部若另有"检查背包某物"的分支、或委托单在睡眠前已过期，我们无法静态保证不卖；详见 `docs\il\quest-protection.md` §7。
12. **设置界面外观**：区段为字面中文、标签为延迟委托，未实机查看。
13. **`tools\scan.ps1`**：静态扫描不等于游戏加载或运行验证。

## 6. 最小实机验收步骤（独立测试档）

1. 关闭游戏，复制 `build\package-dev` 到 `Mods\shipping_bin\`；启动游戏，确认 Mods 列表出现"出货箱"且无加载报错。
2. **新游戏**（不要用正式存档），确认调试模式默认关闭；打开 `debug_grant` 后开新局，确认背包出现一个出货箱（只出现一次）；再开一局确认不会重复赠送。
3. 打开箱子（点击/双击），把普通物品、一个装满物品的容器、一个上锁物品、一个机器分别放进去。
4. 拖 `junk` 到箱子：确认 6×6→8×8、点数 +1；累计到 10×10、12×12；满级后确认拒绝并出现中文提示；再拖 `metal_ingot` 检查溢出提示。
5. 睡觉，确认夜报：卖出件数/收入/保留件数及原因/涉案明细/假贴标扣声望；确认上锁物品、`NOT_FOR_RESALE` 物品、任务委托物、嵌套出货箱被保留且说明原因（普通机器按原版规则**可以**卖出，不再一刀切保留）。
6. 同一天再次睡觉（读档回睡前）：确认不重复付款；读档确认保留物与升级尺寸都在。
7. 关门不睡觉：确认没有出售。把箱子放前柜台睡觉：确认被搬回或给出中文警告。
8. 购买：买店前默认看不到购买项；打开 `early_unlock` 后可见；每天限购一次；背包塞满后购买，确认钱和恩惠不损失且提示空间不足。
9. 存读档往返后重复 3–6，确认容器内容与升级点数不丢。

任一步失败，请提供 `Player.log` 与 `ModConfigs\lushanlan.shipping_bin.json`。本轮未触碰正式存档，测试档可随意删除。

## 7. 第 3 轮：主复核两项收束（2026-11-16）

### 7.1 任务物保护与"禁售"的原生证据

完整 IL 引用、分类实例与复现命令见 `docs\il\quest-protection.md`。要点：

- 剧情物唯一数据表 = `GeneralHelper.importantStoryItemIds`，运行时**只有两条**：
  `luxury_supply_crate`、`self_refilling_neuroactive_perfume`；
  其读取方法 `LoadImportantStoryItemTag` 全库唯一调用者是 `PlayerStore.DecodeSaveItem`（存档袋解码）。
- 全库**没有** Quest/Objective/Mission/Contract 任务系统；任务是 `StoreRequest`，按 identifier 在运行时匹配：
  `StoreRequest.CanThisItemFillRequest(GameItem)` 是原版自己的"这件能不能交任务"谓词。
- `IMPORTANT_TAG` 约 112 处工厂写入（机器/家具/容器/文件/工具），只有 4 处读取，且没有一处是"客户不买"。
- `PlayerStore.SellItem` IL **无任何类型检查**；`MACHINE`/`DOCUMENT` 只出现在 `IsEligibleForRepo`
  （以物抵债），而 `Repo()` 的唯一调用者是高利贷催收 `CreateLoanSharkCollect` ⇒ 不是销售通道。
- 原版真正的拒收判据是 `StoreClient.IsClientRefusingItem`：`NOT_FOR_RESALE`、黑名单、违禁偏好、
  variety、化学纯度、`IsHazardousWaste`、危险水标签、奢侈偏好、以及"容器按后代整体拒收"。
- 原版**没有**"IMPORTANT 物不可销毁"保护（`EmporiumEntry.DestroyChoiceItems` 无条件 Destroy）。

据此 `src\Game\FactsBuilder.cs` 的保护规则改为：

```text
IsQuestRelated = identifier ∈ importantStoryItemIds
                 或 任一激活 storeRequest.CanThisItemFillRequest(item)
IsProtected    = NOT_FOR_RESALE | MODULE_STUCK_TAG | GeneralHelper.IsHazardousWaste(item)
BehindLock     = ITEM_BEHIND_LOCK_TAG | LOCKED_TAG | LockHelper.IsCurrentlyLocked(item)
```

`IMPORTANT_TAG`、`MACHINE`、`DOCUMENT` 不再参与拒售。粒度仍为"整棵待售根里任一后代被拒 → 整根保留"。

### 7.2 付款后的记账：持久记账单（write-ahead + 逐行提交 + 至多一次恢复）

问题：付款成功后 `ApplyCrime`/`ApplyMislabel` 失败只写日志，而当晚若仍写 day 标记，处罚就永久丢失。

实现（`src\Core\Ledger.cs` + `src\Game\PendingLedger.cs` + `src\Core\Commit.cs`）：

1. `Committer` 只把**确实付款成功**的条目交给 `IObligationLedger.PersistAndApply`；
   金额、假标签 1%、涉案都按这个**已付款子集重算**，绝不按原整单。
2. `SettlementLedger.FromPaid` 生成待办行；`PendingLedger.Sink` 先 `SetModData` 落盘，再逐行提交：
   `Pending → InFlight → 落盘 → 提交 → Applied → 落盘`。任一步失败即停，账本保持最新已落盘状态。
3. 涉案金额幂等：提交前用 `secData.GetCrimeIndex(type) < 0` 判断"该类型是否首次出现"，
   提交后仍查不到该类型则抛异常留给下次重试（`SecData` 没有按类型读金额的公开方法，这是可静态做到的最小验证）。
4. 恢复：`PlayerStore` 读档 `OnGameLoadedLate` 与每次睡觉开头调用 `PendingLedger.Recover`；
   遗留 `InFlight` 一律视为 `Applied`（至多一次，避免重复扣），随后继续提交其余行。
5. 记账未全部落地时 `ObligationsDone=false`，`Committer` **不写 day 标记**；
   下一次睡觉先补提交，若仍失败则本晚不出售并给出中文提示（不覆盖账本）。
6. 付款中途失败（例如第二件抛异常）：已付款条目销毁并只按它记账，未付款条目回箱、零记账义务；
   `PaidAmount`/`PaidEntries` 只含真实付款成功者。

### 7.3 本轮真实验证输出

```text
> pwsh -File .\build.ps1
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll

> pwsh -File .\tools\test-rules.ps1
[ok]   Rules
[ok]   Protection
[ok]   Planner
[ok]   Commit
[ok]   Ledger
[ok]   CommitLedger
[ok]   GrantPolicy

315 checks passed, 0 failed.
All core rule tests passed (game integration NOT tested).
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)

> pwsh -File .\package.ps1
Development package only: ...\build\package-dev

> pwsh -File .\tools\scan.ps1
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=
Refused        : False
Error          :
MethodsScanned : 198
Capabilities   : {}
Findings       : {}
HardFlags      : {}
Pending        : {}
```

新增测试覆盖（`TestLedger` / `TestCommitLedger`）：只按已付款子集生成账本（80 而非 240）、
假标签按子集重算（1 而非 2）、涉案按子集重算（200 而非 700）、编解码往返与 5 类非法文本拒绝、
`InFlight` 视为已生效（含假标签）、账本失败不写 day 且 `FailurePhase=ledger`、
账本抛异常不写 day、第二件付款失败时账本只收到 1 条并回箱未付款项。

### 7.4 本轮包哈希（`build\package-dev`）

| 文件 | 大小 | SHA-256 |
|---|---|---|
| manifest.xml | 498 B | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| ShippingBin.dll | 57,856 B | `AA91C964AB2A2F7CAB55221781B21B59026E7308A938A7B2A20401B4A6880DCF` |
| Localization\en.csv | 5,292 B | `AF2F6C704C99BC4034E6B4CBB1FF1E9D8D701A328C262E42642AAAC5EA9F31C2` |
| Localization\zh.csv | 4,872 B | `D5FF669218FE360AF207DB6F37A81E91EB9E7928946B67FC83759E0D2FB48E15` |

DLL 由 csc 编译，**哈希逐次不同**（非确定性编译，本表记录本轮最后一次构建值）；manifest 与 csv 哈希稳定。

### 7.5 本轮改动文件

- 新增：`src\Core\Ledger.cs`、`src\Game\PendingLedger.cs`、`docs\il\quest-protection.md`。
- 修改：`src\Core\Commit.cs`（`IObligationLedger`/`PaidEntries`/`ObligationsDone`/付款中途失败按已付款子集记账/`DestroyPaidAndReturnUnpaid` 传入正确 day）、
  `src\Game\NightSettlement.cs`（开局补提交、账本 sink、失败报告）、`src\Game\FactsBuilder.cs`（任务物/禁售证据化）、
  `src\Game\ModIds.cs`（`pending_obligations`）、`src\ShippingBinMod.cs`（读档补提交）、
  `src\Game\ModSettings.cs`/`src\Core\Rules.cs`/`src\Core\Planner.cs`（删除违禁倍率设置，固定 1:1）、
  `tests\CoreTests.cs`（+73 项断言）；`src\Game\PendingLedger.cs` 在记账单全部落地后立即清除，避免下次读档把已完成账本当成遗留义务重复提示、`build.ps1`（源文件清单）、两份本地化 csv（+3 键、改保护文案）。
- 删除：`src\Rules.cs`（原型残留，早已不在构建内）、违禁倍率配置项与本地化键。

### 7.6 现在可以交给用户实机测试的范围

1. 放置/开箱：新局或测试档拿到箱子，确认能拖进背包、点击打开、6×6 网格正常。
2. 升级：拖 `junk`/`scrap_metal`/`metal_ingot` 到箱子上，确认扩容 8×8/10×10/12×12、点数与溢出提示、满级拒绝。
3. 睡觉结算：箱内放普通物品、容器（含后代）、上锁物品、`NOT_FOR_RESALE` 物品、当前委托单要求物品，
   确认夜报的卖出/保留/原因/涉案/假贴标；确认关门不卖、睡前可取回。
4. 事务与恢复：正常睡觉后立刻读档回睡前，确认不重复付款；把背包塞满后购买，确认钱/恩惠不丢。
5. 记账恢复：若能在实机制造 `CommitCrime` 抛异常（不易构造），预期是夜报提示"本次没有写入结算标记"，
   下次读档/睡觉补提交；常规路径只需确认夜报涉案金额与 `SecData` 界面一致（注意 `OnNewDay` 会先减 20）。

### 7.7 仍未解决 / 需实机确认的边界

1. 剧情脚本内部是否另有"检查背包某物"的分支（本次未逐一反编译 `SerpicoStoryline` 等）——
   因此**不宣称"所有任务物绝不卖出"**，只保证上述四类原生信号。
2. 委托单在 `EndNight` 前过期/完成的时序：若单子已被移除，箱内对应物品会正常出售。
3. `CanThisItemFillRequest` 只比 identifier，任务要求 `junk` 时箱内 junk 会被整体保留（偏保守）。
4. `LockHelper.IsCurrentlyLocked` 的读档恢复顺序、危险水标签是否该整体拒收，均需实机确认。
5. 账本恢复只做了"至多一次"与类型首次出现验证；`SecData` 无按类型读金额接口，无法做金额级幂等校验。
6. 其余第 2 轮列出的实机项（贴图、开箱、拖拽升级、购买补丁、空间探测、前柜台、存读档）仍全部未验证。

## 8. 第 4 轮：四档容量、购价 2000、倍率文本框、贴图接入（历史记录：该轮末档为 16×30/480，已被第 5 轮 32×30/960 覆盖）（2026-11-16）

用户指令：m01486（购价 2000、按原版"储藏区"核实容量、倍率要能看到数字并可输入、调查贴图规范）与 m01489（四档定案：0 / 25 / 100 / 300 点，删 50 阈值，旧档不得缩容丢货）。本轮**只改源码/测试/文档**，未安装、未发布、未改 art 目录。

### 8.1 四档容量与原版证据

| 累计点数 | 名称 | 尺寸 | 格数 | 证据 |
|---|---|---|---|---|
| 0 | 储藏区 | 10×10 | 100 | 原版 `ShipSystemDirectory.StorageBay()` 完全一致 [IL 已证实] |
| 25 | 储藏区（扩建） | 17×10 | 170 | 原版 `StorageBayLarge()` 完全一致 [IL 已证实] |
| 100 | 两倍扩建储藏区 | 17×20 | 340 | 2×170 推导 [推导]：原版无此物品 |
| 300 | 超科技储藏区 | 16×30 | 480 | 原版 `HyperTechBay()` 的单页尺寸 [IL 已证实]，原版共两页 960 |

原版 `HyperTechBay()` 是"一个机器窗口 + 两页各 16×30"；本模组容器是单 `contentWindow` 单网格（存档按节点序号挂回子物品，运行期不能换结构），故取单页。原版 `StorageExpansionHelper.Expand` 只加页签不加格，并且会把扩建套件价值加到容器价值——本模组两件都不采用。

详细证据、复现命令、以及原版其它储藏区（`MakeshiftStorageBay` 非矩形、枪械师/化学师 17×10、`MachineBay` 11×14、走私舱 5×5/8×8/3×3）见 [docs/il/capacity-and-config.md](il/capacity-and-config.md)。

### 8.2 不缩容：旧存档数据保护

旧版容量为 6×6→12×12（上限 30 点）。旧档可能已有 12×12 = 144 格且点数 < 25，而新档 0–24 点目标为 10×10 = 100 格。直接 `SetShape` 会让格内物品失去合法位置，读档时 `UncheckedAccept` 序号错位只 `Debug.LogWarning`（静默丢货）。

实现：`Rules.ShouldApplyShape(curW, curH, newW, newH)` 只在目标格子数**严格更大**时返回 true；`BinItem.ApplySize` 先读当前尺寸再决定是否 `SetShape`。当前尺寸用 `grid.widthPixels / UISettings.current.gridSize` 反推（`gridSize = 16`，IL 已证实）；不用 `grid.inventoryShape`（`GridShape` 在本机 csc 下 `CS0570`）、也不用反射（会触发扫描器 `reflection-external`）。

结果：旧 12×12 箱子保持 144 格，直到累计 25 点（170 > 144）才长到 17×10。跨档逻辑由 `tests/CoreTests.cs` 的 `ShouldApplyShape` 7 条用例覆盖。

### 8.3 倍率设置：滑条改为原生数字输入

根因（`ModConfigWindow.BuildEntry` IL）：只有 `IsNumber && HasRange` 才用 `AddSlider`，而滑条没有数值回显。没有范围的数值会走 `AddInput`，显示当前值、可直接输入，`Apply()` 时 `ReadWidget` 用 `float.TryParse(NumberStyles = 167, InvariantCulture)` 解析后 `ApplyBoxed`。

改动：三个倍率（出售 0.8、赃物 2.0、违禁固定 1:1 不再可调）的 `config.Define` 传 `null, null`，UI 变为原生文本框；范围仍由 tooltip 文案 + `ModSettings.Snapshot()` 的 `Clamp` 保证；中英文 tooltip 均保留并写明"直接输入数字"与合法范围。

### 8.4 本轮真实验证输出与哈希

```text
> pwsh -File .\build.ps1
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll

> pwsh -File .\tools\test-rules.ps1
[ok] Rules / Protection / Planner / Commit / Ledger / CommitLedger / GrantPolicy
336 checks passed, 0 failed.
All core rule tests passed (game integration NOT tested).
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)

> pwsh -File .\package.ps1
No packaging\Textures yet; bin uses the vanilla container sprite.
Development package only: ...\build\package-dev

> pwsh -File .\tools\scan.ps1
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=
Refused        : False
MethodsScanned : 203
Capabilities   : {}
Findings       : {}
HardFlags      : {}
Pending        : {}
```

包内 4 文件哈希（`build\package-dev`）：

| 文件 | 大小 | SHA256 |
|---|---|---|
| `manifest.xml` | 498 B | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| `ShippingBin.dll` | 59,392 B | `02BEADA2A3DF0882EB3DCC14169BFE762F54935849EF5F0BE495F7FFDDBC115C` |
| `Localization\en.csv` | 5,526 B | `0816F62C33EC1620CF814A6032018007955FD444DC9E6AF528421E8EB46CCA23` |
| `Localization\zh.csv` | 5,100 B | `D03A907E14FA6A45526291FAF02F41ED0F9F1CCE62E0E082A3D4217079E5B77A` |

`csc.exe` 无确定性编译：重新构建会使 DLL 哈希变化，manifest 与 csv 哈希稳定。

### 8.5 本轮改动文件

- 修改：`src/Core/Rules.cs`（购价 2000、四档常量、`CapacityWidthFor/HeightFor/CellsFor`、`ShouldApplyShape`、`UpgradeOutcome` 增宽高字段）、`src/Game/BinItem.cs`（尺寸应用、像素反推、模组贴图优先）、`src/Game/ModSettings.cs`（倍率去范围）、`src/Game/ModIds.cs`（`BinSpriteKey`）、`tests/CoreTests.cs`（336 条）、`packaging/Localization/zh.csv`、`packaging/Localization/en.csv`、`package.ps1`（复制 `packaging\Textures`）、`README.md`、`docs/decisions-shipping-bin.md`、`docs/acceptance.md`、`docs/game-test-checklist.md`。
- 新增：`docs/il/capacity-and-config.md`、`docs/art-spec.md`。
- 未改：`build.ps1` 文件清单（本次无新源文件）、`packaging/manifest.xml`、`art` 目录（不存在）。

### 8.6 本轮可交给用户实机测试的范围

1. 升级四档：新箱初始 10×10；累计 25 点变 17×10；100 点变 17×20；300 点变 16×30 且再拖材料提示已满级。
2. 旧档不缩容：载入第 3 轮存档里有 12×12 箱子的局，确认箱子仍是 12×12、箱内物品完整；累计到 25 点后变 17×10。
3. 购价：王尔德恩惠界面显示的基础价应为 2000（若装了影响 `upgradePriceMultiplier` 的 Perk 会按原版折扣变化），购买后冷却一天。
4. 倍率设置：设置界面里三个倍率应是**可输入数字的文本框**，打开即显示当前值（0.8 / 2.0），输入新值后生效；输入非法字符不会提交。
5. 贴图回退：当前包没有 `Textures`，进游戏后箱子用原版容器图；F8 日志不应出现 `Mod sprite ... not found`。
6. 出图后接入：把图放到 `packaging\Textures\Items\shipping_bin.png`（32×32 推荐），重跑 `package.ps1`，日志出现 `Included mod textures`，进游戏后箱子换成新图。

### 8.7 本轮未解决 / 需实机确认

1. 16×30 单页（480 格）在出货箱窗口里的实际显示与操作手感。
2. 17×20（340 格）是推导档位，平衡性需实机判断。
3. 旧档 12×12 容器保持 144 格、与新档同点数不同尺寸，是**有意的数据保护**，不做迁移。
4. 倍率文本框：原版非法输入不提交；越界值被 `Snapshot()` clamp，但配置文件里保留玩家原值。
5. `UISettings.gridSize` 若被其他 mod 改动，尺寸反推随之变化（读运行时值，未硬编码 16）。
6. 此前各轮的实机项（放置/开箱/拖拽升级/购买补丁/空间探测/前柜台救援/存读档往返/记账恢复）仍全部未验证。

## 9. 第 5 轮：末档 960、形状迁移安全、正视贴图接入（2026-11-16）

### 9.1 末档 960（用户定案，不再给 480）

- 原版 `ShipSystemDirectory.HyperTechBay()`：一个机器窗口内两页，每页 `new GameGridInventory(16,30)` = 480，合计 **960**（IL 已证实，见 `docs/il/capacity-and-config.md` §2/§9）。
- 本模组容器只有一个 `contentWindow` + 单个 `GameGridInventory`：原版存档按“物品 + 非物品节点 BFS 序号”挂回子物品，运行期改结构会让序号错位、解码只 `Debug.LogWarning` 并静默丢货（`docs/il/persistence.md`）。多页还需 `MachineryTabs`/`MachineryContainer`，原版只用于机器窗口，没有可移动多页容器先例，页签选择持久化也未实机验证。
- 因此末档实现为 **单页 32×30 = 960 格**，与 960 等容但结构仍是单网格。
- **UI 限制（必须实机确认）**：内容区宽 `32 × 16px = 512px`，低分辨率或 UI 缩放下可能超出屏幕，且没有滚动条；原版两页每页宽 256px。

### 9.2 形状迁移安全（旧档不丢货）

- 只比较面积不够：12×12(144) → 17×10(170) 面积更大，但高度变矮，原本在 y=10/11 的物品会越界；`GameGridInventory.SetShape` 只重算像素与背景，不检查物品位置（IL 已证实）。
- 新增纯规则 `src/Core/ShapeMigration.cs`：`Decide(curW, curH, tgtW, tgtH)`
  - 目标非法 → `KeepCurrent`；当前尺寸未知 → `ApplyDirect`；尺寸相同 → `None`；
  - 目标宽高都不小于当前（真超集）→ `ApplyDirect`（任何物品都不可能越界）；
  - 目标面积更小 → `KeepCurrent`（绝不缩容）；
  - 其余（变宽变矮）→ `Relocate`。
- `BinItem.ApplySize(bin, allowRelocation)`：升级路径传 `true`，执行 取出全部 → `SetShape` → 逐件 `GraphUtils.TryAcceptAll(grid, item, -1)` 放回 → 全部成功才写形状 tag；任一件放不回则撤回、恢复原形状、原样放回，并提示 `upgrade_pending_space`（点数已保留，腾出空间后再拖材料）。读档路径 `onLoaded` 传 `false`，绝不静默重排玩家摆放。
- 实际生效形状写入 `TagShapeApplied = 宽 × 1000 + 高`，便于跨版本审计。

### 9.3 正视贴图接入

- 素材：`art/wooden-shipping-bin-front.png`（1024×1024，正视图，用户 API 生成并目视确认；旧轴测图 `art/wooden-shipping-bin.png` 未使用，`art/` 目录未被修改）。
- 处理（System.Drawing，非重绘）：裁 alpha bbox（134..889 × 169..872 = 756×704）→ 等比缩放进 32×32（内容 32×30，偏移 (0,1)）→ 预乘 alpha 盒式降采样（`sum(rgb*a)/sum(a)`，消除暗边）→ 边缘清理（`alpha < 40` 全透明、`alpha >= 110` 置 255）。
- 校验：四格中心 (8,8)/(24,8)/(8,24)/(24,24) alpha 全 255 → 2×2 占格；产物 `packaging/Textures/Items/shipping_bin.png`（32×32，2,553 B）。
- 运行时：`BinItem.TryApplyModSprite` 在 `ModLoader.Mods` 里找到本 mod 且 `Sprites` 含 `shipping_bin` 时调用 `SetSpriteAndShapeFromMod`（按非透明像素推导占格），否则回退原版 `Items/items_container::eleanor_box`；`package.ps1` 复制 `packaging/Textures` 并打印 `Included mod textures`。

### 9.4 本轮真实验证输出与哈希

```text
> pwsh -File .\build.ps1
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll

> pwsh -File .\tools\test-rules.ps1
[ok]   Rules
[ok]   Protection
[ok]   Planner
[ok]   Commit
[ok]   Ledger
[ok]   CommitLedger
[ok]   GrantPolicy
[ok]   ShapeMigration

349 checks passed, 0 failed.
All core rule tests passed (game integration NOT tested).
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)

> pwsh -File .\package.ps1
Included mod textures: ...\shipping-bin-mod\packaging\Textures
Development package only: ...\build\package-dev

> pwsh -File .\tools\scan.ps1
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=
Refused        : False
MethodsScanned : 209
Capabilities   : {}
Findings       : {}
HardFlags      : {}
Pending        : {}
```

包内 5 文件哈希（`build\package-dev`）：

| 文件 | 大小 | SHA256 |
|---|---|---|
| `manifest.xml` | 498 B | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| `ShippingBin.dll` | 61,952 B | `BB0EE0626D1EAF9CEE6F9D1CD0A286F28599A3DBE01F68D39ABD6F19BA3F2157` |
| `Localization\en.csv` | 5,686 B | `8092DFCBEE6441C7C613BFF61C9A8C69E7D4AD70C8955745F2ECFC37AA0DF740` |
| `Localization\zh.csv` | 5,261 B | `A8D5FBCAD2FC2645EE860A2FB31AC65F249AB4FE16B65EA99A24BE61303902AF` |
| `Textures\Items\shipping_bin.png` | 2,553 B | `C028624A3FB0BB8C0925A18A7FD7579A3A28FD12145EAA75B9ED1061183FB817` |

`csc.exe` 无确定性编译：重新构建会使 DLL 哈希变化，manifest 与 csv/png 哈希稳定。

### 9.5 本轮改动文件

- 新增：`src/Core/ShapeMigration.cs`（形状迁移纯规则）、`packaging/Textures/Items/shipping_bin.png`（正视 32×32 贴图）。
- 修改：`src/Core/Rules.cs`（`MaxCapacityWidth = 32`、末档 `32×30`、注释改为 960/UI 限制）、`src/Game/BinItem.cs`（`ApplySize(bin, allowRelocation)`、重排与回滚、`TagShapeApplied`、`upgrade_pending_space`、`onLoaded` 不重排）、`src/Game/ModIds.cs`（`TagShapeApplied`）、`build.ps1`（加入 `ShapeMigration.cs`，共 18 个源文件）、`tests/CoreTests.cs`（349 条）、`packaging/Localization/{zh,en}.csv`（`item_description` 改为 32×30/960、新增 `upgrade_pending_space`）、`README.md`、`docs/decisions-shipping-bin.md`、`docs/acceptance.md`、`docs/game-test-checklist.md`、`docs/il/capacity-and-config.md`、`docs/art-spec.md`。
- 未改：`packaging/manifest.xml`、`package.ps1`（第 4 轮已支持复制 `packaging\Textures`）、`art/` 目录。

### 9.6 本轮可交给用户实机测试的范围

1. 末档 960：累计 300 点后箱子应为 **32×30**；确认内容窗口在常用分辨率下是否完整可见、能否操作（预期无滚动条）。
2. 形状迁移：用第 3 轮 12×12 旧档（点数 < 25），累计到 25 点时升到 17×10，确认物品数量不变；若先塞满再升级，放不下时应提示“腾出空间”、箱子保持原状、材料消耗但点数保留。
3. 贴图：本包已含 `Textures\Items\shipping_bin.png`，进游戏确认箱子为木箱**正视图**、占 2×2 格、无黑边/暗边。
4. 四档 + 购价 2000 + 倍率文本框：同第 4 轮清单。
5. 读档：升到 32×30 后存读档，确认尺寸与箱内物品保持、读档不重排。

### 9.7 本轮未解决 / 需实机确认

1. **32×30 单页的 UI 显示与操作（内容区宽 512px）——必须实机确认**；若不可接受，需要改造为多页容器（会牵动存档结构，需另行设计）。
2. 原版是“两页 16×30”，本实现是“单页 32×30 等容”，交互形态与原版不完全一致。
3. 重排逻辑规则层已测；游戏中 `Expel`/`TryAcceptAll` 的真实重排结果与失败回滚需实机（含“塞满后升级”的极端用例）。
4. 贴图 alpha 清理阈值（40/110）是针对当前素材调的；换素材需重新检查边缘与四格中心。
5. 旧档 12×12 在点数 < 25 时保持 12×12（不缩容），与新档同点数不同尺寸，属**有意的数据保护**。
6. 此前各轮的实机项（放置/开箱/拖拽升级/购买补丁/空间探测/前柜台救援/存读档往返/记账恢复）仍全部未验证。
## 10. 第 6 轮：待扩容恢复、读档解码顺序、4×4 本体贴图（2026-11-16）

### 10.1 修复“满级后永远无法重试”
- 问题：旧实现在 `DoUpgrade` 中先 `ConsumeUpgradeMaterial` + `SetPoints`，再 `ApplySize`；形状迁移失败只提示“再拖材料”，而 `CanUpgrade` 在点数达到 300 时直接拒绝 ⇒ 材料已消耗、点数已满、容量却没落地，且无法再次触发。
- 现实现（`src\Game\BinItem.cs`）：
  - 迁移失败时把目标尺寸写入物品 tag `lushanlan.shipping_bin.pending_shape`（编码 `宽*1000+高`），**不再需要额外材料**；
  - `CanUpgrade` 改为 `点数满 但有待扩容` 时仍允许拖材料触发；
  - `DoUpgrade` 一进来先做一次“零消耗重试” `TryApplyPendingSize`，成功则清除标记；若此时已满级，只提示满级；
  - 重试时机：打开箱子（Harmony prefix `ItemMouseDoubleClickHandler.OpenContentAction`）、睡觉（`NightSettlement.Run` 结算前）。
- 重试逻辑：`RelocateIntoShape` 把箱内物品全部 `Expel` → `SetShape(目标)` → 逐件 `GraphUtils.TryAcceptAll` 校验 → 全部成功才写新形状；任一件放不回则恢复原形状、按原顺序放回、保留待扩容标记。

### 10.2 读档解码顺序与旧档丢物风险（IL 已证实）
- `SaveItemNode` **不保存** inventory 形状（字段只有物品自身的 `itemShape/itemModifiedShape` 等），容器形状完全由工厂重建。
- `SaveManager.DecodeNodesInternal`：第一遍 `DirectoryMaster.Item(identifier, true)` 走工厂建物品；第二遍按非物品节点 BFS 序号 `nodeDict[invIdx].UncheckedAccept(child)` 挂回子物品（失败只 `Debug.LogWarning`）。整个过程**不调用 `onLoaded`、不校验形状**。
- `PlayerStore.LoadGame` 对每个顶层根调 `DecodeSaveItem`，`onLoaded` 在子物品挂回之后才触发。
- 因此工厂改为一律建 **32×30 最大画布**：旧存档里任何 ≤32×30 的保存坐标都不会在挂回瞬间越界；新箱子再由 `PrepareFresh` 收窄到 10×10。
- 旧档（没有 `TagShapeApplied` 记录）在 `OnLoaded` 中**绝不直接缩小**：保留 32×30，写入 `pending_shape` 与 `TagShapeApplied`，提示“开箱或睡觉时自动整理”，等安全时机再迁移。新档读档时恢复上次实际应用的形状。

### 10.3 本体占格 4×4 与旧存档兼容
- 新箱子本体：`packaging\Textures\Items\shipping_bin_v2.png`，64×64（16px/格），16 个格中心 alpha=255 ⇒ `SetSpriteAndShapeFromMod` 推导出完整 **4×4** 占格。
- 旧存档兼容：第 5 轮构建曾把 sprite key `shipping_bin` 写进存档（32×32 = 2×2）。因此保留 `shipping_bin.png`（32×32）作为 legacy key，并让 `OnLoaded` 依据存档里的 `spriteAtlasPath/spritePath` 恢复对应贴图：
  - `shipping_bin_v2` → 4×4；
  - `shipping_bin` → 2×2（旧物品保持原占格，避免把 4×4 贴图套到 2×2 物品上与背包邻居重叠）；
  - 无 mod 贴图信息 → 回退原版 `Items/items_container::eleanor_box`。
- 存档只保存 `spriteAtlasPath/spritePath`，**不保存 `useSpriteFromMod`**，所以读档必须重新调用 `SetSpriteAndShapeFromMod`，否则贴图解析会走原版图集并显示 unknown（`ResolveSpriteByName` IL 已证实）。

### 10.4 真实验证（本机执行）
```text
> pwsh -File .\build.ps1
Built ...\build\ShippingBin.dll

> pwsh -File .\tools\test-rules.ps1
[ok] Rules / Protection / Planner / Commit / Ledger / CommitLedger / GrantPolicy / ShapeMigration
363 checks passed, 0 failed.
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)

> pwsh -File .\package.ps1
Included mod textures: ...\packaging\Textures
Development package only: ...\build\package-dev

> pwsh -File .\tools\scan.ps1
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=
Refused        : False
MethodsScanned : 220
Capabilities / Findings / HardFlags / Pending : {}
```

### 10.5 本轮包哈希（`build\package-dev`）
| 文件 | 字节 | SHA-256 |
| --- | --- | --- |
| `manifest.xml` | 498 | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| `ShippingBin.dll` | 65,536 | `02F40C4FE1822EFD1005BE7CA33A03831A9A680D00FF4E0E166F106C3C6A2B0B` |
| `Localization\zh.csv` | 5,512 | `EBB22479F713966818F3BBCE04E3E9D78CD4CD25290C43C1C29FA1C52E66C276` |
| `Localization\en.csv` | 6,008 | `265298FE11305BFADC5D0C509A912C680D269FCA3D42A714D26DB64076D133A3` |
| `Textures\Items\shipping_bin_v2.png` | 9,608 | `9749E6C3F3434395D960F51A6B718697F6C39E844BD04FF809ADE7E5FBDB03C5` |
| `Textures\Items\shipping_bin.png` | 2,749 | `B3FF2B16C582429F1C256C649015AE0986F3226348D748538745BAB543AC6B33` |

DLL 由 csc 非确定性编译，每次重建哈希都会变；manifest 与本地化/贴图哈希稳定。

### 10.6 本轮改动文件
- 新增：`src\Game\BinOpenHook.cs`（开箱时重试待扩容）、`packaging\Textures\Items\shipping_bin_v2.png`（64×64）。
- 修改：`src\Core\ShapeMigration.cs`（新增 `TryDecodeShape`）、`src\Game\BinItem.cs`（最大画布工厂、`PrepareFresh`、`OnLoaded` 恢复形状/贴图、pending 读写与重试、`CanUpgrade`/`DoUpgrade`）、`src\Game\ModIds.cs`（`TagPendingShape`、`BinSpriteKey=shipping_bin_v2`、`LegacyBinSpriteKey`）、`src\Game\NightSettlement.cs`（结算前重试）、`src\ShippingBinMod.cs`（安装开箱补丁）、`build.ps1`（新增源文件）、`packaging\Localization\{zh,en}.csv`（+2 键、修订 1 键）、`tests\CoreTests.cs`（+14 断言）。`packaging\Textures\Items\shipping_bin.png` 改为 legacy 32×32。

### 10.7 需要实机验证（第 6 轮新增）
1. 新开一局（调试赠送或购买）得到箱子：确认背包里实物占 **4×4** 格、正视木箱外观、可放置 / 可打开。
2. 旧存档（第 5 轮构建的箱子）：确认读档后箱子仍显示原 2×2 外观、**周围物品没有被挤掉或吞掉**；打开一次箱子确认内部物品位置不变。
3. 满级场景：把点数升到 300 且箱内布局放不下 → 拖材料 → 应出现“已记下待扩容、不再消耗材料”；腾出空间后开箱/睡觉应出现“已自动整理并应用”的夜报。
4. 旧档 12×12 → 17×10 迁移：读档后先保留 32×30 并提示；开箱或睡觉后应整理为 17×10，物品数量与种类不变。
5. 存读档往返：升级到 960 后存读档，容量保持 32×30、贴图不变成 unknown。

### 10.8 未解边界
- 旧 2×2 箱子不会自动升级为 4×4：无法在不与背包邻居碰撞的前提下静态验证 4×4 是否可放，故保留原占格。若需要旧箱变 4×4，需实机确认后另做迁移（例如取出到空背包再重放）。
- 16×30/32×30 的窗口在低分辨率下的可读性仍未实机验证；`PixelWindow` 无滚动。
- `GridShape` 在本机 csc 中是 unsupported type，无法做逐格碰撞判定；迁移完全依赖“取出—重塑—逐件放回”的真实 API 行为。
- 打开箱子的 Harmony prefix 只在 `ItemMouseDoubleClickHandler.OpenContentAction` 上；若某条路径绕过它（例如脚本直接 Show），待扩容会推迟到睡觉。

## 11. 第 7 轮：物品说明动态显示升级进度

用户要求（m02220）：实机反馈“目前测试看起来没问题”，新增一项小改动——物品说明/tooltip 动态显示**每箱**累计升级点数、下一档累计阈值（25/100/300）与**当前实际容量**；满级显示已满级与累计点数；有待扩容时说明“待扩容 + 实际容量”，不能把目标尺寸当成已应用；保留原有说明；中英文官方本地化；必须核对本机 `GameItem` 动态描述/tooltip 原生机制。不改计价、容量与任何规则。

### 11.1 原生机制证据（Cecil IL，`Assembly-CSharp.dll` 只读）

- `GameItemElement.GetTooltipBasic() : RichTextBuilder` 的构建顺序：`GeneralHelper.CreateGeneralItemTooltip` → 各 `*Helper.CreateTooltip` → `ModInternal.CreateTooltipEarly(builder,item)`（IL_03e3，内部 `ModHook.FireOnCreateTooltipEarly`）→ `PlayerStore.devMode` 时的 `CreateDevTooltipEarly` → 其余 helper → `ModInternal.CreateTooltipLate(builder,item)`（IL_062d，内部 `ModHook.FireOnCreateTooltipLate`）→ `CreateDevTooltipLate` → **之后**（IL_0646 / IL_0672）才追加 `item.shortDescription` 与 `flavorText`。
- `GetTooltipBasic` 的唯一调用者是 `GameItemElement.GetTooltip()` 与 `GetTooltipAdvanced()`，即**每次悬停都会重新构建**：不需要缓存，升级点数与容量变化会在下一次悬停时自然刷新。
- `GameItem.SetExamineWindow` 全库**无调用者**；`ItemMouseDoubleClickHandler.OpenExamineAction` 只显示已经存在的 examineWindow。⇒ 物品“说明文本”的实际呈现位置就是 tooltip，因此本模组没有、也不需要新建检查窗口。
- `RichTextBuilder.AddLine(string text, bool ignoreEmpty = true, RenderHandler.ColorPalette color = -1, ...)` 后的参数都有默认值，模组可直接 `builder.AddLine(text)`。

### 11.2 实现

- 纯逻辑层 `src\Core\UpgradeStatus.cs`：`UpgradeStatusKind{Progress, MaxLevel, Pending}` 与 `UpgradeStatusLogic.Compute(points, actualW, actualH, pendingW, pendingH)`。规则：点数 < 0 归 0；实际宽高 <= 0 原样记为 0（不猜测）；pending 只有宽高都 > 0 才有效；**pending 优先于满级**；满级时下一档阈值为 0。
- `src\Core\Rules.cs` 新增 `NextThresholdFor(points)`：从下标 1 开始（下标 0 的 `0` 是初始档，不是“下一档”），返回 25 / 100 / 300，满级返回 0；只用于文案，不参与计算。
- `src\Game\BinTooltip.cs`：`BinTooltip.Append(RichTextBuilder, GameItem)`，只对出货箱生效；读取该箱自己的 points tag、实际容量（`BinItem.TryGetBinShapeSize`，由 `grid.widthPixels / UISettings.gridSize` 反推）与 `pending_shape`；键由 `UpgradeStatusLogic.TooltipKey` 选择（已知容量用 `tooltip_upgrade_{progress,max,pending}`，读取失败用对应 `*_unknown`），`builder.AddLine(...)`；异常只写日志，不影响原版 tooltip。
- `src\ShippingBinMod.cs`：`OnEnable` 订阅 `ModHook.OnCreateTooltipLate += BinTooltip.Append`，`OnDisable` 反向退订。
- 本地化：`zh.csv` / `en.csv` 各新增 6 键（85 键，键集一致）——三种状态各一条“已知容量”文案，外加对应的 `*_unknown` 变体（读取失败时显示“当前容量暂不可读取”）。三种文案都同时包含“点数、阈值/上限、容量”，pending 文案额外包含“待扩容目标 + 当前实际容量”，不会把目标尺寸说成已生效。

### 11.3 状态矩阵

| 状态 | 显示内容 |
| --- | --- |
| 未满级 | 累计 N 点；下一档 M 点；当前容量 W×H |
| 已满级 | 已满级（N/300 点）；当前容量 W×H |
| 有待扩容 | 累计 N 点（上限 300）；待扩容至 W2×H2，当前实际容量 W×H |
| 实际容量读取失败 | 保留点数/下一档（或 pending 目标），容量位置显示“当前容量暂不可读取”，不显示任何推算尺寸 |

### 11.4 真实验证（本轮）

```text
> pwsh -File .\build.ps1
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll
> pwsh -File .\tools\test-rules.ps1
[ok]   Rules / Protection / Planner / Commit / Ledger / CommitLedger
[ok]   GrantPolicy / ShapeMigration / UpgradeStatus

403 checks passed, 0 failed.
All core rule tests passed (game integration NOT tested).
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)
> pwsh -File .\package.ps1
Included mod textures: ...\packaging\Textures
Development package only: ...\build\package-dev
> pwsh -File .\tools\scan.ps1
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=
Refused        : False
Error          :
MethodsScanned : 229
Capabilities   : {}
Findings       : {}
HardFlags      : {}
Pending        : {}
```

本地化键集校验：`zh` 85 键、`en` 85 键，键集完全一致；`tooltip_upgrade_progress/max/pending` 及对应 `*_unknown` 变体两边都存在。

### 11.5 本轮包哈希（`build\package-dev`）

| 文件 | 字节 | SHA-256 |
| --- | --- | --- |
| `manifest.xml` | 498 | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| `ShippingBin.dll` | 68,096 | `E00EE5101D9325CA16F92F48A68C4A0B557C955E11A39D7CFED47058670AED4D` |
| `Localization\zh.csv` | 6,207 | `71BC97148AF5D050EE06213EF1D45E96CD77329C694815408FCF61B87EC8C6B5` |
| `Localization\en.csv` | 6,732 | `1F82FCB608E0D7F275561C1D8761DC90B2C70EB20255F0DC310309552D8CFA90` |
| `Textures\Items\shipping_bin_v2.png` | 9,608 | `9749E6C3F3434395D960F51A6B718697F6C39E844BD04FF809ADE7E5FBDB03C5` |
| `Textures\Items\shipping_bin.png` | 2,749 | `B3FF2B16C582429F1C256C649015AE0986F3226348D748538745BAB543AC6B33` |

DLL 由 csc 非确定性编译，重建会变；manifest 与本地化/贴图哈希稳定。

### 11.6 本轮改动文件

- 新增：`src\Core\UpgradeStatus.cs`（状态判定纯逻辑）、`src\Game\BinTooltip.cs`（tooltip 追加）、`docs\il\tooltip.md`（机制证据）。
- 修改：`src\Core\Rules.cs`（`NextThresholdFor`）、`src\Game\BinItem.cs`（`GetPendingSize`、`TryGetBinShapeSize`）、`src\Game\Loc.cs`（新增 4 参 / 6 参 `Get` 重载）、`src\ShippingBinMod.cs`（订阅/退订 `OnCreateTooltipLate`）、`build.ps1`（新增 2 个源文件）、`packaging\Localization\{zh,en}.csv`（+3 键）、`tests\CoreTests.cs`（+31 断言，`Run("UpgradeStatus", ...)`）、`README.md`、`docs\acceptance.md`、`docs\decisions-shipping-bin.md`、`docs\game-test-checklist.md`。

### 11.7 需要实机验证（第 7 轮新增）

1. 鼠标悬停出货箱：说明里出现“升级进度”行，点数、下一档、当前容量与实际情况一致。
2. 拖 1 个废料（1 点）进箱子后重新悬停：点数与下一档立刻更新（无需重开游戏）。
3. 两个箱子的点数不同时，悬停各自显示各自进度（每箱独立）。
4. 满级箱子：显示“已满级（N/300）”，不显示下一档。
5. 存在待扩容的箱子：显示“待扩容至 W2×H2，当前实际容量 W×H”，且实际容量不是目标尺寸。
6. 存读档后再次悬停：点数与容量与存档前一致；贴图不变成 unknown。

### 11.8 未解边界

- 进度行通过 `OnCreateTooltipLate` 追加，按原版顺序位于 `shortDescription` **之前**；原说明仍然完整保留。若要严格显示在说明之后，需要改动原版 tooltip 构建，不在本模组范围内。
- 实际容量通过 `grid.widthPixels / UISettings.gridSize` 反推；若因异常读不到网格，tooltip 显示“当前容量暂不可读取”，**不**用按点数推算的档位尺寸冒充实际值（pending 场景仍显示待扩容目标）。此分支已有规则层测试覆盖（`ActualKnown=false` 与 `*_unknown` 键选择）。
- 本轮只做了规则层与构建/扫描验证；游戏内 tooltip 的实际排版（换行、颜色、与其它 mod 追加行的顺序）需实机确认。
- 用户在本轮反馈“目前实机测试看起来没问题”（m02220），这只说明用户已验证过的部分功能可用，**不等于**全部需求矩阵或全部存档场景都已实机验收；本文件第 6/7 节的实机清单仍需逐项确认。

---

## 12. 第 8 轮：自动化料斗保护 + 次日报告销售额

用户需求（m02433）：“游戏内我们需要用到出入货口来进行自动化。所以出货口不应该被卖掉。确认这一点。以及第二天的夜间报告上需要显示销售额。”

### 12.1 自动化料斗不参与出售（IL 已证实）

| 工厂方法 | identifier | 本地化名字 | tag |
| --- | --- | --- | --- |
| `HopperHelper.CreateIntake()` | `hopper_in` | 进料斗（`item_intake_hopper_name`） | `HOPPER_INTAKE_TAG` |
| `HopperHelper.CreateOutlet()` | `hopper_out` | 出料斗（`item_outlet_hopper_name`） | `HOPPER_OUTLET_TAG` |

- `HopperHelper.IsIntake` / `IsOutlet` 只按上述两个 tag 判断；描述证实进料斗会把所在库存送进出料斗所在库存，二者构成原版自动化链的两端。
- 实现：`ModIds.TagHopperIntake/TagHopperOutlet/AutomationHopperIds`；`FactsBuilder.IsAutomationHopper`（tag 或 identifier 命中，tag 被剥离时仍可兜住）；`ItemFacts.IsAutomationHopper`；`RejectCode.AutomationHopper` + `reason_automation_hopper`；`Protection.SingleNodeCode` 顺序：空 → 自动化料斗 → 出货箱自身 → 锁 → 剧情物 → 机器/文档/普通重要物 → 赃物/违禁开关 → 非玩家物品。
- 后代命中同样整根保留（复用既有“任一代被拒则整根保留”规则），夜间报告会在保留原因里显示“自动化传送料斗（进料斗/出料斗）不会被出售”。
- 证据与复现命令见 [docs/il/quest-protection.md](il/quest-protection.md) §8。

### 12.2 次日早间报告显示销售额（IL 已证实路径）

- `PlayerStore.nightLogs` 是 `List<string>`；`AddNightLog(string)` 原样追加，`AddNightLog(string, hexColor)` 会包装成 `<color=#hex>…</color>`。
- 夜间结算在 `OnGoingSleepEarly` 写入的日志不会被 `PlayerStore.BeginDay()` 清理；次日 `StartOfDayUIManager.OpenUI()` → `ShowMorningReport()` → `InitPanel()` 遍历**全部** `nightLogs` 生成行、随后 `nightLogs.Clear()`（所以报告只显示一次）。
- 因此本轮把原来的单行“卖出 N 件，收入 M”拆成两行，明确显示销售额：
  - `log_sold`（zh「出货箱：卖出 {0} 件。」/ en "Shipping bin: sold {0} items."）
  - 新增 `log_sales_total`（zh「出货箱销售额：{0}。」/ en "Shipping bin sales: {0}."）

### 12.3 真实验证

```text
> pwsh -File .\build.ps1
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll
> pwsh -File .\tools\test-rules.ps1
[ok] Rules / Protection / Planner / Commit / Ledger / CommitLedger / GrantPolicy / ShapeMigration / UpgradeStatus
407 checks passed, 0 failed.
All core rule tests passed (game integration NOT tested).
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)
> pwsh -File .\package.ps1
Included mod textures: ...\packaging\Textures
Development package only: ...\build\package-dev
> pwsh -File .\tools\scan.ps1
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=
Refused        : False
MethodsScanned : 230
Capabilities / Findings / HardFlags / Pending : {}
```

本地化：`zh` 与 `en` 各 87 键，键集完全一致（脚本校验 diff 为空）。

### 12.4 包哈希（`build\package-dev`）

| 文件 | 字节 | SHA-256 |
| --- | --- | --- |
| manifest.xml | 498 | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| ShippingBin.dll | 68,608 | `D87682F0D8D746F463FC94B88290388FA66788DA93B75E354AF665DFB1FF0E55` |
| Localization\en.csv | 6,840 | `8CFCB44F5BC4E12725EA127E8C442292158A2F9E73773F9EC057329EA60D71F2` |
| Localization\zh.csv | 6,327 | `FF54A7CD71185D449FCE07016A2EB2ADD5C08B1C8FB6DFE01825F97FE87521C9` |
| Textures\Items\shipping_bin_v2.png | 9,608 | `9749E6C3F3434395D960F51A6B718697F6C39E844BD04FF809ADE7E5FBDB03C5` |
| Textures\Items\shipping_bin.png | 2,749 | `B3FF2B16C582429F1C256C649015AE0986F3226348D748538745BAB543AC6B33` |

DLL 由 `csc.exe` 构建且无确定性选项，**重新构建会得到不同哈希**；manifest 与两张贴图稳定不变。

### 12.5 本轮改动文件

- `src\Core\ItemFacts.cs`（`RejectCode.AutomationHopper`、`ItemFacts.IsAutomationHopper`、`KeyFor`、判定顺序）
- `src\Core\Rules.cs`（无改动，本轮未动数值）
- `src\Game\ModIds.cs`（料斗 tag / identifier 表）
- `src\Game\FactsBuilder.cs`（`IsAutomationHopper` 读取运行时 tag 与 identifier）
- `src\Game\NightSettlement.cs`（`log_sold` 只报件数，新增 `log_sales_total` 行）
- `packaging\Localization\zh.csv`、`en.csv`（`log_sales_total`、`reason_automation_hopper`，`log_sold` 文案调整）
- `tests\CoreTests.cs`（根料斗、进料斗、后代料斗整根保留、reason key，共 +4 断言）
- 文档：本文件 §12、`docs\il\quest-protection.md` §8、`docs\decisions-shipping-bin.md` 第八轮、`docs\game-test-checklist.md`、`README.md`

### 12.6 实机清单

- [ ] 把进料斗/出料斗放进出货箱，睡觉后两者仍在箱内，夜间报告保留原因显示“自动化传送料斗（进料斗/出料斗）不会被出售”。
- [ ] 含有料斗的容器整体不被出售（容器与其后代一起保留）。
- [ ] 正常卖出物品后，次日早间报告出现“出货箱：卖出 N 件。”与“出货箱销售额：M。”两行，金额与现金增加一致。
- [ ] 报告只显示一次：关掉早间报告后再次查看不会重复出现同一批日志。

### 12.7 未解边界

- 只保护原版 `hopper_in` / `hopper_out`；其它模组自定义的“传送/自动化”物品若没有这两个 tag 或 identifier，不在保护范围。
- 报告里的“销售额”仅指**本模组出货箱**的成交额，不含店内正常销售；店内销售由原版自己的日志展示。
- 早间报告读取后即 `Clear()`，若同时安装了会在报告后追加日志的其它 mod，行顺序由各自的写入时机决定；本轮仅静态验证。
- 料斗在游戏里的自动化运行（连接、每晚传送）未由本模组改动，需实机确认保护不影响其功能。

## 13. 第 9 轮：金属拖拽升级修复、报告颜色、失败日志（2026-11-16，用户 m02627）

### 13.1 问题与根因（IL 已证实）

- 现象：把金属锭（或 junk / scrap_metal）拖到出货箱上不会升级；对出货箱"使用"金属同样无效。
- 根因：原版拖拽在 `ItemMouseDragHandler.EndDrag` 里要求 `currentItem.MayTarget(lastItem)` **且** `currentItem.CanTarget(lastItem)` 才继续，然后才调用 `ItemBehaviourManager.Target` 与 `GameItem.Target(target)`。而金属锭在 `MaterialDirectory.MetalIngot()` 中给自己设置了 `mayThisTargetItemFunc` / `canThisTargetItemFunc = MaterialDirectory.CanIngotTarget`，后者是**写死白名单**：只允许 `vending_machine_core`、动物笼、水箱、RAS，其它目标一律返回显式 `false`。这个 `false` 在 `MayTarget/CanTarget` 的合成结果里否决了我们在**箱子一侧**设置的 `mayItemTargetThisFunc/canItemTargetThisFunc`，于是 `GameItem.Target` 根本不会被调用，挂在箱子上的 `onItemTargetThisFunc` 也就永远不触发。
- 结论：不是容器谓词或点数/材料判断的问题，而是原版交互门的白名单先否决。

### 13.2 修复

- 新增 `src/Game/UpgradeDragPatch.cs`：Harmony postfix 挂在 `GameItem.MayTarget` 与 `GameItem.CanTarget` 上，只在"结果为 `false`、源物品是升级材料、目标确认是本模组出货箱、`BinItem.CanUpgrade` 为真"时把结果补成 `true`；其它情况完全不干预。首次补门时写一条模组日志 `upgrade drag gate overridden (...)`，便于排查。
- 保留了原版完整调用链：仍是原版拖拽手势 → `GameItem.Target` → 箱子自己的 `onItemTargetThisFunc` → `DoUpgrade`（点数、材料消耗、Tinkerer 豁免、形状迁移、pending 重试全部沿用原逻辑）。
- 接线：`build.ps1` 源文件清单加入该文件；`ShippingBinMod.OnEnable` 中 `UpgradeDragPatch.Patch(harmony)`（`OnDisable` 的 `UnpatchAll` 覆盖）。

### 13.3 报告颜色与失败日志

- `WriteNightReport`：`log_sold`（卖出件数）与 `log_sales_total`（销售额）颜色改为 `#000000`。
- `WriteFailureReport`：不再调用 `PlayerStore.AddNightLog`，结算失败只写模组日志（`WriteLog`，包含阶段、原因、物品、已售件数、已付金额、是否开始付款、是否回滚、是否落 day、回滚未完成项），次日早间报告不再出现内部错误码。

### 13.4 真实验证

```text
> pwsh -File .\build.ps1
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll
> pwsh -File .\tools\test-rules.ps1
[ok]   Rules / Protection / Planner / Commit / Ledger / CommitLedger / GrantPolicy / ShapeMigration / UpgradeStatus
407 checks passed, 0 failed.
All core rule tests passed (game integration NOT tested).
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)
> pwsh -File .\package.ps1
Included mod textures: ...\packaging\Textures
Development package only: ...\build\package-dev
> pwsh -File .\tools\scan.ps1
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=
Refused        : False
MethodsScanned : 234
Capabilities   : {}
Findings       : {}
HardFlags      : {}
Pending        : {}
```

### 13.5 包哈希（`build/package-dev`）

| 文件 | 大小 | SHA256 |
| --- | --- | --- |
| manifest.xml | 498 | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| ShippingBin.dll | 69,632 | `F478896CFD18A9AC112310F09B41017D04BE938E7F0530918BCFA57AAFD0F703` |
| Localization/en.csv | 6,840 | `8CFCB44F5BC4E12725EA127E8C442292158A2F9E73773F9EC057329EA60D71F2` |
| Localization/zh.csv | 6,327 | `FF54A7CD71185D449FCE07016A2EB2ADD5C08B1C8FB6DFE01825F97FE87521C9` |
| Textures/Items/shipping_bin_v2.png | 9,608 | `9749E6C3F3434395D960F51A6B718697F6C39E844BD04FF809ADE7E5FBDB03C5` |
| Textures/Items/shipping_bin.png | 2,749 | `B3FF2B16C582429F1C256C649015AE0986F3226348D748538745BAB543AC6B33` |

说明：csc 非确定性，DLL 每次重建哈希会变；manifest 与 csv 稳定。

### 13.6 改动文件

- 新增：`src/Game/UpgradeDragPatch.cs`、`docs/HANDOFF.md`。
- 修改：`build.ps1`（源文件清单 +1）、`src/ShippingBinMod.cs`（注册补丁）、`src/Game/NightSettlement.cs`（报告颜色、失败只写日志）、`docs/settlement-handoff.md`（标注为历史文件）、`docs/acceptance.md`、`docs/decisions-shipping-bin.md`、`docs/game-test-checklist.md`、`README.md`。

### 13.7 需要实机确认

- [ ] 金属锭拖到出货箱：出现"容量提升"提示，点数 +5，材料被消耗（Tinkerer 20% 时可保留但点数仍加）。
- [ ] junk / scrap_metal 同样可拖拽升级（+1 / +2）。
- [ ] 把金属拖到普通物品或售货机上：行为与原版一致（仍只对白名单目标生效，不被本补丁影响）。
- [ ] 次日早间报告里"卖出 N 件"与"销售额 M"为黑色字，且没有结算失败提示行。
- [ ] 故意制造失败（例如把箱子放进前柜台场景）后查看模组日志，确认失败细节落在日志而不是报告中。

### 13.8 未解边界

- 拖拽门补丁只覆盖"源是三种升级材料、目标是本模组出货箱"这一种组合；其它模组物品若也复用金属白名单逻辑，不受影响也不会被顺带修好。
- 报告颜色只改了本模组写入的两行，原版/其它模组的行保持原色。
- 结算失败现在"只写日志"，若玩家不查看日志，可能不知道当晚没有结算成功（这是按用户要求做的取舍）。

## 14. 第 10 轮：第 8/9 轮集成复核（2026-11-16，Codex 验收）

本轮只做有边界的复核与修正，不新增玩法、不动 `art/`、不安装、不发布、不 push。

### 14.1 UpgradeDragPatch 复核

- 本机签名核对：`GameItem.MayTarget(GameItem targetItem)`、`GameItem.CanTarget(GameItem targetItem)`、`GameItem.Target(GameItem targetItem)` 的参数名都是 `targetItem`；补丁已改用 Harmony 的 `__0` 取参（`Postfix(GameItem __instance, GameItem __0, ref bool __result)`），不再依赖参数名。
- 放行条件收窄并显式化：`Rules.MaterialPoints(source.identifier) > 0`（只可能是 junk / scrap_metal / metal_ingot）→ `BinItem.IsBin(target)` → `BinItem.CanUpgrade(source,target)`（双方 `IsItemEffectivelyOwned`、未满级或仍有 pending）。其它组合保持原版结果。
- `GameItem.Target` 材料端副作用逐条审计（IL 已证实，补丁注释同步）：
  - `source.onItemTargetItemFunc`：金属锭未设置；
  - `target.onItemTargetItemFunc`：本模组箱子未设置；
  - `source.onThisTargetItemFunc`：金属锭 = `MaterialDirectory.OnIngotTarget`，四个白名单分支（售货机核心/笼子/水箱/RAS）之外直接 `ret`，**不消耗材料**；
  - `target.onItemTargetThisFunc`：本模组箱子 = `BinItem.DoUpgrade`，只在这里 `StartingPerkContent.ConsumeUpgradeMaterial` 一次；
  - 四个 slot 版本回调：金属锭只设置 `may/canSelectSlotItemFunc`，未设置 `on*`；
  - 结尾播放 `source.soundUse` 并刷新库存 UI。
- `ItemMouseDragHandler.EndDrag` 会两次调用 `MayTarget/CanTarget`（IL_02fe/0311 与 IL_033a/034d），本补丁只改布尔结果、幂等，`GameItem.Target` 仍只调用一次 ⇒ 不会重复升级或多耗材料。
- `ItemBehaviourManager.Target` 只遍历目标身上的 `ItemTargetBehaviour` 组件，本模组箱子没有该组件 ⇒ 无额外副作用。

### 14.2 料斗保护复核

- `FactsBuilder.BuildNode` 对每个节点判定料斗（tag `HOPPER_INTAKE_TAG`/`HOPPER_OUTLET_TAG` 或 identifier `hopper_in`/`hopper_out`）；`Protection.Evaluate` 递归整棵后代树，任一层命中即整根保留并报告命中的那个料斗。
- 新增测试：两层容器深处的料斗仍整根保留；普通容器（有子物品/空容器）不被误拦。

### 14.3 颜色与本地化复核

- 保留用户最新要求：`log_sold`/`log_sales_total` 使用 `#000000`；结算失败只写模组日志，不写 `nightLogs`。
- 本地化键覆盖：重新用脚本比对代码中的字面键与 `zh.csv`/`en.csv` 键集，删除 5 个因“失败不再进报告”而失效的键（`log_commit_item`、`log_commit_partial`、`log_commit_rolled_back`、`log_commit_not_settled`、`log_commit_rollback_incomplete`），zh/en 各 **82** 键、键集完全一致。
- 动态构造的键逐一核对存在：`log_commit_<phase>_failed` 的 11 个阶段（plan/revalidate/detach/verify/pay/destroy/crime/mislabel/settle/ledger/rollback）与 `log_commit_invalid_plan`、`crime_<type>` 的 5 个类型、`reason_*`、`tooltip_upgrade_*` 全部存在。
- `WriteFailureReport` 仍用 `Loc.Get(FailureReasonKey)` 生成日志文本，键存在，只是输出到日志而不是报告。

### 14.4 HANDOFF 修正

- `MethodsScanned` 230 → **234**；测试数 407 → **411**；配置数“9 项” → **8 项（2 个数值倍率 + 6 个开关）**（违禁倍率已在第 2 轮删除）。
- `src/Game` 模块清单补 `BinOpenHook` / `BinTooltip` / `UpgradeDragPatch`。

### 14.5 真实验证

```text
> pwsh -File .\build.ps1
Built ...\build\ShippingBin.dll
> pwsh -File .\tools\test-rules.ps1
[ok] Rules / Protection / Planner / Commit / Ledger / CommitLedger / GrantPolicy / ShapeMigration / UpgradeStatus
411 checks passed, 0 failed.
> pwsh -File .\package.ps1
Included mod textures: ...\packaging\Textures
Development package only: ...\build\package-dev
> pwsh -File .\tools\scan.ps1
Refused        : False
MethodsScanned : 234
Capabilities   : {}
Findings       : {}
HardFlags      : {}
Pending        : {}
```

### 14.6 包哈希（`build/package-dev`，本轮重建）

DLL 因 csc 非确定性每次重建会变；manifest 与 csv 稳定。

| 文件 | 大小 | SHA256 |
| --- | --- | --- |
| Localization\en.csv | 6371 | `882793367D4A343A7FA4401047CDF4CC164C028C87533221A7945F2CF75BB8E3` |
| Localization\zh.csv | 5914 | `396FD08A5812C07938DC9C94FD9D6FD799B8FCF795EC6499D0C71B3B607A1570` |
| manifest.xml | 498 | `08656A2A07004D0635F997EC447EB8E9DC7AD618992F35F5F5F3FE7005D077EB` |
| ShippingBin.dll | 69632 | `4032715FF2522288267DE9064E6C4212A85E1043C4378D9EF582132BA8DC3C7B` |
| Textures\Items\shipping_bin_v2.png | 9608 | `9749E6C3F3434395D960F51A6B718697F6C39E844BD04FF809ADE7E5FBDB03C5` |
| Textures\Items\shipping_bin.png | 2749 | `B3FF2B16C582429F1C256C649015AE0986F3226348D748538745BAB543AC6B33` |
