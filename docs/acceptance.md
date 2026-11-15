# 出货箱 Mod 验收报告（候选构建，第 3 轮：记账单 + 任务物证据）

- 日期：2026-11-16
- 构建：`build\package-dev`（候选开发包，**未做游戏内实机验收**）
- 结论：规则层与游戏适配层已实现并通过游戏外全部可执行验证（**315** 条规则/事务/记账单/状态机测试 + 真实编译 + 本机 ModScanner 扫描 198 个方法）；游戏内行为（放置、开箱、拖拽升级、购买按钮、睡觉结算、存读档）尚未实机运行，见"未验证风险"。**本报告不声称玩法已在游戏内验证通过。**

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
