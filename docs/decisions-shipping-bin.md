# 出货箱 Mod 决策记录

## 讨论方式
- 结论：使用 grill 常规强度（2），保留决策记录。
- 理由：用户明确选择；逐轮补齐会影响方案的重要缺口。

## 已确定的需求
- 结论：制作类似星露谷出货箱的容器，每晚出售其中货物。
- 理由：用户的核心玩法目标；具体结算时点及特殊货物规则待定。
- 结论：模组设置提供出售倍率。
- 理由：用户希望自行调节收益；计价基准、默认值和范围待定。
- 结论：默认买下店铺后解锁，另设开关允许买店前解锁。
- 理由：兼顾正常进度与提前使用需求。
- 结论：支持使用垃圾、废金属、金属锭升级。
- 理由：用户希望引入材料成长；升级效果、材料配方和等级数待定。
- 结论：先做可行性调查，再讨论实现。
- 理由：用户明确要求；目前仅完成文档级初查，未完成本机 API 与游戏内验证。

## 调查依据与边界
- 官方文档：https://github.com/dragonpan2/probably-stolen-modding/blob/main/ModdingGuide.md
- 官方变更记录：https://github.com/dragonpan2/probably-stolen-modding/blob/main/APIChanges.md
- 文档提供模组设置、夜间事件和按存档保存的模组数据接口。
- 本机版本、容器序列化、物品计价、买店状态、材料 ID 及结算保存顺序仍需代码核实。
- 设计建议尚未由用户确认，不作为已定需求。

## 已确定的玩法选择
- 结论：出货箱采用可移动、占空间、可打开并放入货物的容器物品形式（Q3 A）。
- 理由：用户选择该形式；优先调查复用原生容器交互的实现。
- 结论：以物品当前状态对应的价值作为出售倍率的基准（Q4 A）。
- 理由：用户希望按当前状态估值；耐久、品质等具体估值因素及接口仍需核实。
- 结论：升级只增加容量，不增加出售倍率（Q5 A）。
- 理由：用户选择容量成长，出售收益由模组倍率设置控制。

## 第二轮选择与新增要求
- 结论：玩家睡觉结束当天时结算（Q6 A）。
- 理由：用户明确选择。
- 结论：放入的容器连同内容物一并出售（Q8 B），需要逐层正确计价并防止重复计算。
- 理由：用户明确选择整体出售。
- 结论：赃物出售导致的涉案金额默认为基础的 2 倍，倍率可在模组设置中调整；黑市声望达到 100 后减半。
- 理由：用户新增要求；基础金额的定义与声望判断时点待确认，相关原版机制待核实。
- 待澄清：用户选择 Q7 B（拒收赃物和违禁品），同时要求赃物出售后增加涉案金额。尚不能确定最终收货范围，不能擅自覆盖其中任一要求。

## 后续已确认规则（覆盖前述冲突和待定项）
- 结论：赃物、违禁品默认都允许出售，分别提供模组设置开关（Q9 B 及补充）。任务物品和非玩家所有物品仍保护。
- 结论：涉案金额以原版基础涉案金额为基准，不随出售收益倍率变化（Q10 A）；原版计算入口待验证。
- 结论：容器内含任何不允许出售的物品时，整箱保留并提示（Q11 A）。
- 结论：黑市声望达到 100 时涉案金额减半，达到 200 时完全减免；两个效果分别提供开关，默认开启。门槛固定，不开放数值设置（Q12 A）。
- 结论：出售价格倍率默认 0.8（Q14 B）。设置范围尚未确认。
- 结论：出货箱在王尔德的恩惠处直接购买，冷却一天。未启用买店前解锁时，必须买下店铺后才显示购买入口。
- 理由：用户补充了具体获取渠道；优先按该补充理解，Q13 A 的免费赠送是否仍保留待澄清。
- 待定：购买价格、是否同时免费赠送首箱、冷却的具体重置时点、多个箱子与升级等级的关系。

## 获取与升级归属确认
- 结论：所有出货箱均从王尔德的恩惠购买，不免费赠送首箱（Q15 A），覆盖此前 Q13 A 的免费赠送选择。
- 结论：每个箱子独立升级，分别消耗材料（Q16 A）。
- 结论：购买冷却按游戏日计算，睡觉进入次日后重置购买次数（Q17 A），不是滚动 24 小时。
- 理由：用户逐项选择 AAA。

## 材料与原版升级路径
- 结论：使用可替代的升级点数制（Q18 C）：垃圾 1 点、废金属 2 点、金属锭 5 点。
- 理由：用户明确指定权重；各等级所需点数、余点处理待定。
- 结论：Q19 不直接选择自拟扣料方式，先调查原版水瓶打印机的升级路径，优先复用。
- 理由：用户指出原版已有升级实现，并要求检查。
- Q20 尚未回答；用户消息末尾为未完成的“Q”，不推断容量选择。

## 本机升级实现调查与容量目标
- 结论：满级容量明显大于原版大型容器（Q20 B）。具体尺寸待核实原生容器后提案。
- 本机 Assembly-CSharp.dll 静态 IL 检查：MachineBottlePrinter 用 BOTTLE_PRINTER_UPGRADE_COUNT_TAG 记录每件机器进度；3 次、9 次分别解锁档位，输出格子从 4x4 变成 5x4、6x4。
- 材料入口：MaterialDirectory.Electronic 的目标使用回调调用 UpgradePrinter；ModuleHelper.CanElectronicPartTarget 检查材料和目标所有权及可升级条件。
- UpgradePrinter 增加进度、把材料 unitValue 加到机器价值、调用 StartingPerkContent.ConsumeUpgradeMaterial 并播放音效；不是通用点数升级 API，出货箱需要自有进度与材料适配。
- ConsumeUpgradeMaterial 会在 StartingPerk.IsTinkerer 且 RNG.Roll(20) 成功时保留材料，否则 Destroy 材料。是否让出货箱继承此效果待确认。
- 扩容调用 GameGridInventory.SetShape 和 Validate；onLoaded 会重新应用升级状态。以上为本机静态代码证据，尚未游戏内测试。

## 天赋、余点与假贴标
- 结论：沿用原版升级节省材料天赋（Q21 A），对应天赋触发时不消耗材料但仍增加升级点数。
- 结论：跨档升级余点累计保留，满级后拒绝继续投入（Q22 A）。最终满级溢出时的提示方式尚未确认。
- 需求：适配原版假贴标功能，处罚方向暂定为扣黑市声望。
- 待调查：假贴标的原版标记、真实价值与标签价值计算、处罚触发与原版检测机制。
- 待决定：假贴标售出按真实价值还是标签价值计价、扣声望规则，以及扣声望与涉案金额减免的结算顺序。

## 自动售货机假贴标参考调查
- 用户指定参考原版自动售货机。
- 本机静态 IL：VendingMachineHelper.IsMislabelled 检查物品及全部子物品的特征，考虑禁用、已揭露、真假匹配及价值关系，不是仅检查是否使用过贴标器。
- TrackMislabelledSale 按传入金额 / 100 累计声望损失，并区分 FRAUD 与 ENDANGERMENT；该金额的售货调用端来源仍待核实。
- SettleVendingMislabels 将声望损失分配到上下层派系，取整后合计至少 1；SettleMislabelRep 对每派系损失限制为最多 20，写入夜间日志。
- 出货箱若改扣黑市声望，不能直接调用完整售货机结算，否则会引入原版派系扣分和犯罪后果。处罚比例、上限、犯罪后果是否保留，以及同晚声望门槛顺序待用户确认。

## 假贴标处罚定案
- 结论：沿用原版假贴标判定，仅额外扣黑市声望，不引入售货机假贴标的欺诈或危害处罚；赃物涉案规则独立生效（Q23 A）。
- 结论：所有出货箱当晚假贴标货物的实际出售总额按 1% 合计扣声望，统一取整；有此类销售至少扣 1，每晚最多扣 20（Q24 A）。具体半数取整方式实现时明确。
- 结论：以当晚结算开始时的黑市声望快照计算全部箱子的赃物减免，再统一扣假贴标声望，影响下一晚（Q25 A）。
- 理由：用户逐项选择 AAA，统一结算避免箱子遍历顺序影响结果。
- 待澄清：假贴标商品按真实估值还是标签显示估值出售；此前当前状态估值选择未明确真假标签口径。

## 假贴标计价定案
- 结论：假贴标货物按标签显示估值乘出售倍率结算（Q26 A），允许标签带来收益变化，同时扣黑市声望。
- 理由：用户选择收益与信誉之间的取舍，覆盖此前估值口径的歧义。

## 后续本机接口核实
- ModHelper.AddNetworkUpgrade 存在 cost、onUnlock、tab、prerequisites、cooldownDays 等参数；NetworkUpgrade.OnDayStart 逐日递减冷却。可支持每日重复购买的接入方向，购买失败处理仍需验证。
- 买店状态为 PlayerStore.IsPropertyPaid，BuyOutStore 会设置 true。NetworkUpgrade.IsHiddenForRun 对 Advanced 页已有买店隐藏逻辑；自定义入口的提前解锁例外需要单独适配。
- 黑市声望有 StoreReputation.GetBMReputation / ModBMReputation。
- GetVendingPrice 调用 GetCurrentValue(true,true,false,true,0)，参数分别为 useRetailMarkup、withChild、includeEvents、forceMarkup、extraMarketMod；不能把这些布尔值误解为真假估值开关。真假特征累计尚需继续追踪。
- SecData.CheckDeliveredStolen 使用 STOLEN_TAG 和 STOLEN_VALUE_INT，以传入价值乘标签百分比，再 ApplyCrimeModifier，最后 CommitCrime(FENCING)。不能把 STOLEN_VALUE_INT 直接视为涉案金额。
- 睡觉按钮正常路径为 EndNight -> BeginDay -> SaveGame；EndNight 内含 GoingSleep 和 HandlingNightlyServices 事件，故夜间出货可在正常保存前完成。异常退出与特殊结束分支仍需测试。
- 自动售货机销售还调用 MarkStolenOrContrabandSale 和 PerformOnSoldCheck，出货箱必须核对副作用后选择性接入，避免重复处罚。
- 尚未制作或运行原型；大容量界面上限、完整标签计价和存档往返仍未验证。

## 开发交接与调试模式
- 用户结束 grill，要求提供新对话开发 prompt。
- 新增需求：保留调试模式，作为模组设置开关；开启后在开局时给予一个出货箱。
- 开发建议（非用户明确数值要求）：调试默认关闭；按新建一局触发赠送，避免每次读档重复发放。正常购买渠道和买店限制不因调试赠送而修改。

## 待定问题
4. 结算时点、特殊物品与嵌套容器如何处理？
5. 初始容量、等级数、材料消耗、默认倍率及范围。
6. 旧存档补领、重复生成防护、关闭提前解锁和卸载时的货物处理。

## 开发原型状态（尚未交付）
- 编译通过的原型包含物品目录注册、设置、升级回调、购买前空间检查和调试待领逻辑；这些均未经游戏内测试。
- 初始 6×6，累计 5/15/30 点时暂设 8×8、10×10、12×12；尚未验证界面上限或最终默认。
- 购买原型暂设 500，每游戏日冷却一次。失败回滚尚须核查统计、异常与部分放置副作用。
- 本机材料 ID 为 junk、scrap_metal、metal_ingot；使用原版 ConsumeUpgradeMaterial。材料选择和目标委托仍须运行验证。
- InitContainerItemIncludeContainer 用于允许容器进入；嵌套出货箱保护尚未完成。
- 尚未实现夜间销售，不能安装此原型用于正常游戏。任务物品识别、计价、犯罪与结算事务仍待完成。

---

# 实现定稿（2026-11-16，DeepSeek Harness 接手后）

本节记录正式实现（`src\Core\*` + `src\Game\*`）相对旧原型的决策变化。旧原型已被重写，本节以本机 `Assembly-CSharp.dll` 的 IL 取证为准；取证报告见 `docs\il\pricing.md`、`docs\il\crime-protection.md`、`docs\il\mod-api.md`、`docs\il\persistence.md`。

## 分层架构

- 结论：拆成"纯规则层 + 游戏适配层"。
  - `src\Core\`：`Rules`（全部数值与公式）、`ItemFacts`（物品事实树与保护判定）、`Planner`（全箱快照与出售计划）、`Commit`（两阶段提交事务）。不引用任何游戏类型，可在游戏外用 csc 编译并跑 184 条断言。
  - `src\Game\`：`ModIds`（ID/tag/配置键/本地化键）、`Loc`、`ModSettings`、`BinItem`、`BinWorld`、`FactsBuilder`、`NightSettlement`、`Purchase`、`DebugGrant`。
  - `src\ShippingBinMod.cs`：只做注册与装配。
- 理由：结算规则（去重、整根保留、顺序独立、回滚）是本模组风险最高的部分，必须在游戏外可反复验证；游戏侧只负责把 `GameItem` 翻译成 `ItemFacts`、把计划落到真实库存。

## IL 驱动的六处关键修正（取代旧原型假设）

1. **估值口径**：旧原型逐子累加 `part.GetCurrentValue(...) * part.unitCount`。IL 证实原版整件取价只需一次 `root.GetCurrentValue(false, useRetailMarkup:false, withChild:true, includeEvents:true, forceMarkup:false, extraMarketMod:0)`：`GetItemMiscValue` 内部处理 `IGNORE_CHILD_VALUE`，`GetCurrentChildValue` 内部跳过 `ITEM_BEHIND_LOCK_TAG` 并递归全部后代，`ComposeStagedValue` 只对根算 innate/market、misc 加在乘完之后。实现改为整树一次调用，`unitCount` 只乘一次（原版容器口径本身不乘数量）。
2. **IMPORTANT_TAG 判定**：旧原型 `IMPORTANT_TAG && !CONTAINER_TAG` 对容器恒为 false（`ContainerHelper` 四个 Init* 都会同时给容器打上两个 tag，全库 112 处工厂写入 IMPORTANT_TAG），导致容器完全绕过保护。实现改为：剧情物用原版 `importantStoryItemIds` 数据表（只有 `luxury_supply_crate`、`self_refilling_neuroactive_perfume` 两条）；`IMPORTANT_TAG` 只对非容器生效；机器/文件/`NOT_FOR_RESALE`/`MODULE_STUCK_TAG` 单独判保护；出货箱自身由 ID 判定。
3. **犯罪记账**：旧原型猜 `ApplyCrimeModifier(FACTION_BLACK_MARKET, basis)`。IL 证实原版 FENCING 基础是 `GetNegociatedValue()`（上架成交价）乘 `STOLEN_VALUE_INT%`，再由 `SecData.CheckSoldContraband` 提交；`ApplyCrimeModifier` 的字符串参数是"卖给谁"，不是"哪个阵营"。实现改为 `FactsBuilder` 预计算 `CrimeLine`（Type/Basis/Kind），Planner 按设置倍率算金额，`Committer` 逐条 `secData.CommitCrime(type, amount)`；不调用 `PerformOnSoldCheck`/`MarkStolenOrContrabandSale`（它们会展开整棵子树逐件记罪，与整件出售冲突）。
4. **事务顺序**：旧原型先写 `last_settlement_day` 再卖。实现改为 `Committer.Run`：全部 Revalidate → 全部 `Expel` → 全部验证 `parentInventory == null` → 逐条付款（`ModCash`）→ 逐条 `Destroy` → 犯罪 → 假贴标 → **最后**写 day 标记；任一步失败按逆序 `UncheckedAccept` 放回且绝不付款。
5. **购买回滚**：旧原型只恢复 `playerCash`/`wildFavor` 且依赖冷却复位。IL 证实原版购买先扣款、再 `NetworkUpgrade.Unlock()`，无 try/catch、无回滚。实现改为 Harmony 前缀在扣款前快照现金/恩惠/两统计并先探测背包空间（不足直接阻止扣款），onUnlock 放置失败时按快照**绝对值**恢复全部四项并复位 `state=1`、`cooldownCurrent=0`。
6. **注册时机**：本地化在 `Init`/`OnEnable` 之后才加载，故购买项注册移到 `OnModAssetsLoaded`；`NetworkUpgrade.RegisterModUpgrade` 会 `InvalidateDefinitions()` 并在运行时字典缺项时 `CopyForRuntime`，运行时注册安全。

## 数值默认（均为开发提案，非用户定值）

`src\Core\Rules.cs` 集中定义并附注释：售价倍率 0.8（0–5）、赃物倍率 2.0（0–10）、违禁品固定原版 1:1（不提供倍率设置）、BM 减半 100 / 全免 200、假贴标 1%（1–20）、购买价 500、冷却 1 天、容量 6→8→10→12（累计 5/15/30 点）、材料点数 junk 1 / scrap_metal 2 / metal_ingot 5。其中 500 与 5/15/30 沿用旧原型提议值，可由实机与经济平衡再调。

## 与原型的其它差异

- 不再用 `static Dictionary<GameItem, GameGridInventory>` 缓存网格（读档不重建的缺陷），改为每次从 `bin.contentWindow.child as GameGridInventory` 取。
- 不调用 `ContainerHelper.InitContainerItemIncludeContainer`：其谓词要求 `IsItemOwned`，会拒收赃物，与"赃物默认允许"冲突。改为自设 `mayInventoryAddItemFunc` + `EnableTag(CONTAINER_TAG)`。
- 不挂 `ContainerHelper.SetBasicContainerValueChangeRule`：它把子物品 `unitValue` 加减到箱上，会与 withChild 估值重复计算。
- 不做"材料价值加到机器/箱价值"的原版行为（用户明确禁止机械增值）。
- 新增前柜台救援：IL 证实 `PlayerStore.EndNight()` 会调用 `EmporiumEntry.ClearFrontInv()` 无条件 `Destroy` 前柜台一切（含容器与内容），因此结算开始时把放在前柜台的出货箱搬回背包，搬不动则中文警告。
- 假贴标只扣 `FACTION_BLACK_MARKET` 声望，不调用 `TrackMislabelledSale`（原版它会引入 FRAUD/ENDANGERMENT 犯罪并由 `SettleMislabelledSales` 扣上下层派系声望）。
- 全部 ID/tag/配置键集中在 `src\Game\ModIds.cs`，命名空间前缀 `lushanlan.shipping_bin`。
- 本地化 `zh.csv`/`en.csv` 覆盖全部运行时键；`Loc.Get` 延迟取值避免 Init 期未加载。

## 未在实机验证的部分

放置与贴图、开箱 UI、拖拽升级触发、购买按钮与 Harmony 命中、睡眠结算真实表现、中途失败回滚、存读档往返、前柜台救援路径。清单与最小验收步骤见 `docs\acceptance.md` 第 5、6 节。

## 验收修复（2026-11-16，针对上一轮驳回的 5 条）

### 1. IMPORTANT_TAG 不再作为保护条件

原实现 `IsProtected = ... || (IMPORTANT_TAG && !IsContainer)` 与"不按 tag 一刀切"矛盾，会误拦带 IMPORTANT 的非容器重要机器。现改为：

- 剧情物只来自原版运行时数据 `GeneralHelper.importantStoryItemIds`（`AccessTools.Field` 读取，IL 已证实只有 `luxury_supply_crate`、`self_refilling_neuroactive_perfume` 两条），读取失败才回退到 `ModIds.VanillaStoryItems` 同内容副本。
- 其它保护与原版收购白名单同源：`MACHINE`/`DOCUMENT` 类型、`NOT_FOR_RESALE`、`MODULE_STUCK_TAG`、上锁、非玩家物品、出货箱自身与嵌套箱。**（第 3 轮已删除 `MACHINE`/`DOCUMENT` 拒售：IL 证实原版销售通道没有类型检查，见文末"第三轮收束"。）**
- **明确取舍**：非剧情类 IMPORTANT 物品不做额外保护（属未支持范围，已写入 README 与验收报告风险清单），因为该 tag 在原生容器、机器、家具上大量存在，不能作为"任务物"判据。

### 2. 调试赠送改为专属标记，不再与正常购买的箱子混淆

- 每局 done：`ModHelper` modData `debug_grant_done`；失败待补：`debug_grant_pending`。
- 只识别"本次赠送"的箱子：赠送时写入专属物品 tag `lushanlan.shipping_bin.debug_grant_token`。
- 纯规则 `GrantPolicy.Decide(enabled, done, pending, hasGrantTokenBin) → None/MarkDone/Grant`，状态机可测：关闭开关不动标记；存在 token 箱 → MarkDone；done → 不再赠；正常购买的箱子没有 token，因此不会阻塞 pending 补领。
- 空间不足时保留 pending 并中文提示，在 `OnGameLoadedLate`/`OnWakingUpLate`/`OnGoingSleepEarly` 重试；成功即 MarkDone，失败销毁临时实例再保留 pending。

### 3. 提交事务：部分生效纳入回滚、回滚必须核实

- `Detach` 在调用前就把条目加入 `attempted`，异常或返回 false 的那一件也在回滚列表内（它可能已部分脱离）。
- 回滚逆序 `ReturnToBin` 后必须 `VerifyReturned`（适配层检查 `parentInventory == grid && grid.childItems.Contains(root)`）。
- `RolledBack` 仅在"付款尚未开始且每条放回都核实通过"时为 true；否则 `RolledBack=false` 且 `RollbackFailures` 记录物品名，不再静默吞异常。

### 4. 付款后的语义精确化（不再声称"任何一步失败都付款前回滚"）

顺序：`Revalidate → Detach(全部) → VerifyDetached(全部) → Pay(逐条) → Destroy → ApplyCrime → ApplyMislabel → MarkSettled`。

- 付款前失败：整单逆序放回并核实，零付款。
- `Pay` 是唯一不可逆点（契约：原子加法）。付款中途抛异常：已付款条目照常销毁（避免钱货双得），未付款条目逆序放回；`PaidAmount` 只累计成功条目。
- Destroy 失败重试一次，仍失败则如实记录但条目按已售记账（钱已付）。
- ApplyCrime/ApplyMislabel/MarkSettled 各自捕获并记录 `FailurePhase`；写 day 放最后，失败时报告"未写结算标记"，此时箱内已无可卖条目，同晚重睡只会重试写标记。

### 5. 复核估值/涉案/违禁副作用；删除多余设置

- 删除用户未要求的"违禁品涉案倍率"配置（`cfg_contraband_multiplier` 两键一并删），违禁品固定按原版 1:1（`Rules.ContrabandCrimeFactor=1.0`），`Settings.ContrabandMultiplier` 字段移除。
- 售价基础 = 入箱后当前状态 `GetCurrentValue(false, true, true, false, 0)`；原版物品进入非 backInv 容器会剥离 contraband 加成与客户临时特征（IL 已证实），故不额外加成，与 tooltip 显示自洽。
- 涉案基础沿用原版 `GetNegociatedValue() × STOLEN_VALUE_INT%`；犯罪类型只使用原版 `CheckSoldContraband` 的五种（FENCING/DRUG_TRAFFICKING/WEAPON_TRAFFICKING/CARD_TRAFFICKING/CONTRABAND_TRAFFICKING），不新增推定类型。
- 测试也随之更新：违禁品在 `StolenMultiplier=5.0` 时仍为 500（不借用赃物倍率）。

### 测试与验证

`tests\CoreTests.cs` 扩到 242 条，新增 `FakeWorld` 故障注入（部分 detach、回滚抛异常、放回未核实、第二件付款失败、销毁瞬时/持续失败、犯罪异常、假贴标异常、写 day 失败、空夜）与 `GrantPolicy` 状态机用例。真实执行结果与最终包哈希见 `docs\acceptance.md`。
# 第三轮收束（2026-11-16，主复核两项要求）

## 1. 任务物保护与"禁售"改为证据驱动

- 依据：`docs\il\quest-protection.md`（Cecil 只读，逐条给方法签名与 IL 语义）。
- 剧情物只有 `GeneralHelper.importantStoryItemIds` 两条（`luxury_supply_crate`、`self_refilling_neuroactive_perfume`）；
  全库没有 Quest/Objective 任务系统，任务是 `StoreRequest`，按 identifier 运行时匹配，
  `CanThisItemFillRequest(item)` 是原版自己的谓词。
- `IMPORTANT_TAG` 112 处工厂写入、仅 4 处读取，没有一处是"客户不买"；
  `PlayerStore.SellItem` 无类型检查；`MACHINE`/`DOCUMENT` 只出现在"以物抵债 Repo"，
  而 `Repo()` 唯一调用者是高利贷催收 ⇒ 不能当禁售证据。
- 因此保护规则改为：剧情物数据表 ∪ 激活委托单命中 ∪ `NOT_FOR_RESALE` ∪ `MODULE_STUCK_TAG`
  ∪ 危险废物 ∪ 锁。**不按 IMPORTANT_TAG、不按 MACHINE/DOCUMENT 拒售**。
- 明确不宣称"能保证所有任务物不卖"：剧情脚本内部若有别的背包检查分支、或委托单在睡觉前过期，
  静态证据覆盖不到，已列为需实机验证的边界。

## 2. 付款后的记账改为持久记账单（写入式 + 至多一次恢复）

- 旧行为的问题：付款成功后 `ApplyCrime`/`ApplyMislabel` 失败只写日志，同晚又写 day 标记 ⇒ 处罚永久丢失。
- 新结构：`src\Core\Ledger.cs`（纯规则：行状态机、编解码、按已付款子集生成、至多一次解析）
  + `src\Game\PendingLedger.cs`（modData 落盘、逐行提交、恢复）
  + `src\Core\Commit.cs` 的 `IObligationLedger` 契约。
- 顺序：先落盘账本 → 逐行 `Pending → InFlight → 落盘 → 提交 → Applied → 落盘`；
  任一行失败即停，`ObligationsDone=false`，**不写 day 标记**；
  下次读档（`OnGameLoadedLate`）或睡觉开头先 `Recover`，成功后再结算。
- 至多一次：崩溃/异常后遗留的 `InFlight` 一律视为已生效（`ResolveInFlight`），宁可少扣一次也不重复扣；
  涉案幂等用 `secData.GetCrimeIndex(type) < 0` 判"首次出现"，提交后仍查不到该类型则抛异常留给重试。
- 只按已付款子集：`SettlementLedger.FromPaid` 的金额、假标签 1%、涉案都由**付款成功的条目**重算；
  第二件付款失败时账本只含第一件（金额 80、假标签 1、涉案 200），未付款条目回箱且零记账义务。

## 3. 测试与验证

- `tests\CoreTests.cs` 增至 **315** 条（新增 `Ledger`、`CommitLedger` 两组）：账本编解码往返与非法文本拒绝、
  子集金额/假标签/涉案重算、`InFlight` 至多一次、账本失败不写 day、账本抛异常、第二件付款失败只记第一件。
- 真实流水线：`build.ps1` → Built；`test-rules.ps1` → 315 passed；`package.ps1` → `build\package-dev`；
  `tools\scan.ps1` → `Refused=False`、`MethodsScanned=198`、Findings 全空。哈希见 `docs\acceptance.md` §7.4。

## 4. 边界（不做没证据的规则）

- 不把"装配设备/文件"整体禁售：原版销售通道没有这条规则，用户也未授权该取舍。
- 不宣称账本金额级幂等：`SecData` 没有按类型读金额的公开接口，只能做"类型是否已出现"的最小验证。
- 账本恢复、委托单过期时序、锁状态读档顺序都必须实机确认。
