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
- 官方文档：https://github.com/dragonpan2/probably stolen modding/blob/main/ModdingGuide.md
- 官方变更记录：https://github.com/dragonpan2/probably stolen modding/blob/main/APIChanges.md
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
- 本机 Assembly CSharp.dll 静态 IL 检查：MachineBottlePrinter 用 BOTTLE_PRINTER_UPGRADE_COUNT_TAG 记录每件机器进度；3 次、9 次分别解锁档位，输出格子从 4x4 变成 5x4、6x4。
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
- 睡觉按钮正常路径为 EndNight  > BeginDay  > SaveGame；EndNight 内含 GoingSleep 和 HandlingNightlyServices 事件，故夜间出货可在正常保存前完成。异常退出与特殊结束分支仍需测试。
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



# 实现定稿（2026-11-16，DeepSeek Harness 接手后）

本节记录正式实现（`src\Core\*` + `src\Game\*`）相对旧原型的决策变化。旧原型已被重写，本节以本机 `Assembly CSharp.dll` 的 IL 取证为准；取证报告见 `docs\il\pricing.md`、`docs\il\crime protection.md`、`docs\il\mod api.md`、`docs\il\persistence.md`。

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

- 依据：`docs\il\quest protection.md`（Cecil 只读，逐条给方法签名与 IL 语义）。
- 剧情物只有 `GeneralHelper.importantStoryItemIds` 两条（`luxury_supply_crate`、`self_refilling_neuroactive_perfume`）；
- 全库没有 Quest/Objective 任务系统，任务是 `StoreRequest`，按 identifier 运行时匹配，
- `CanThisItemFillRequest(item)` 是原版自己的谓词。
- `IMPORTANT_TAG` 112 处工厂写入、仅 4 处读取，没有一处是"客户不买"；
- `PlayerStore.SellItem` 无类型检查；`MACHINE`/`DOCUMENT` 只出现在"以物抵债 Repo"，
- 而 `Repo()` 唯一调用者是高利贷催收 ⇒ 不能当禁售证据。
- 因此保护规则改为：剧情物数据表 ∪ 激活委托单命中 ∪ `NOT_FOR_RESALE` ∪ `MODULE_STUCK_TAG`
- ∪ 危险废物 ∪ 锁。**不按 IMPORTANT_TAG、不按 MACHINE/DOCUMENT 拒售**。
- 明确不宣称"能保证所有任务物不卖"：剧情脚本内部若有别的背包检查分支、或委托单在睡觉前过期，
- 静态证据覆盖不到，已列为需实机验证的边界。

## 2. 付款后的记账改为持久记账单（写入式 + 至多一次恢复）

- 旧行为的问题：付款成功后 `ApplyCrime`/`ApplyMislabel` 失败只写日志，同晚又写 day 标记 ⇒ 处罚永久丢失。
- 新结构：`src\Core\Ledger.cs`（纯规则：行状态机、编解码、按已付款子集生成、至多一次解析）
- + `src\Game\PendingLedger.cs`（modData 落盘、逐行提交、恢复）
- + `src\Core\Commit.cs` 的 `IObligationLedger` 契约。
- 顺序：先落盘账本 → 逐行 `Pending → InFlight → 落盘 → 提交 → Applied → 落盘`；
- 任一行失败即停，`ObligationsDone=false`，**不写 day 标记**；
- 下次读档（`OnGameLoadedLate`）或睡觉开头先 `Recover`，成功后再结算。
- 至多一次：崩溃/异常后遗留的 `InFlight` 一律视为已生效（`ResolveInFlight`），宁可少扣一次也不重复扣；
- 涉案幂等用 `secData.GetCrimeIndex(type) < 0` 判"首次出现"，提交后仍查不到该类型则抛异常留给重试。
- 只按已付款子集：`SettlementLedger.FromPaid` 的金额、假标签 1%、涉案都由**付款成功的条目**重算；
- 第二件付款失败时账本只含第一件（金额 80、假标签 1、涉案 200），未付款条目回箱且零记账义务。

## 3. 测试与验证

- `tests\CoreTests.cs` 增至 **315** 条（新增 `Ledger`、`CommitLedger` 两组）：账本编解码往返与非法文本拒绝、
- 子集金额/假标签/涉案重算、`InFlight` 至多一次、账本失败不写 day、账本抛异常、第二件付款失败只记第一件。
- 真实流水线：`build.ps1` → Built；`test rules.ps1` → 315 passed；`package.ps1` → `build\package dev`；
- `tools\scan.ps1` → `Refused=False`、`MethodsScanned=198`、Findings 全空。哈希见 `docs\acceptance.md` §7.4。

## 4. 边界（不做没证据的规则）

- 不把"装配设备/文件"整体禁售：原版销售通道没有这条规则，用户也未授权该取舍。
- 不宣称账本金额级幂等：`SecData` 没有按类型读金额的公开接口，只能做"类型是否已出现"的最小验证。
- 账本恢复、委托单过期时序、锁状态读档顺序都必须实机确认。

# 第四轮：容量四档、购价 2000、倍率文本框、贴图接入（2026-11-16）

用户定案（m01486 / m01489）：购价改 **2000**；容量四档依次为原版“储藏区”→ 25 点“储藏区（扩建）”→ 100 点“两倍扩建储藏区”→ 300 点“超科技储藏区”，删去原 50 阈值；旧存档不得因缩容丢货。

## 1. 容量：先核原版，再定档位

- 原版实际尺寸（Cecil 只读，`ShipSystemDirectory`）：`StorageBay()` = `CreateInventoryWindow(10,10,true)`（100 格，unitValue 300）；`StorageBayLarge()` = `(17,10)`（170 格，850）；`HyperTechBay()` = 一个机器窗口两页，每页 `new GameGridInventory(16,30)`（480 格/页，共 960，unitValue 10000）；`MakeshiftStorageBay()` 为非矩形 shape（宽 10）。全部名称取自 Unity 本地化 bundle，不是按英文名猜。
- 原版“扩建”机制 `StorageExpansionHelper.Expand` 只增加页签（`MAX_TABS=5`、kit tabs 2/1、`KIT_VALUE=1063`），并把套件 unitValue 加到容器价值。本模组**不采用**：升级只扩单网格，且不做材料增值（用户规格 7）。
- 结论映射：`CapacityThresholds={0,25,100,300}`，`CapacityWidths={10,17,17,16}`，`CapacityHeights={10,10,20,30}`，`MaxUpgradePoints=300`。
- 必须向用户明示的两点差异：**340 格（17×20）是 2×170 的推导值**，原版无此物品；~~480 格是原版超科技储藏区的单页~~（**第 5 轮已被推翻**：用户定案末档必须 960，改为单页 32×30 等容，见文末“第五轮”）。
- 证据与复现命令见 `docs\il\capacity and config.md`。

## 2. 不缩容：旧存档数据保护

- 旧版容量为 6×6→12×12（上限 30 点）；旧档可能已有 12×12 = 144 格且点数 < 25，而新 0–24 点目标是 10×10 = 100 格。
- 直接 `SetShape` 会让箱内物品失去合法位置，读档时 `UncheckedAccept` 序号错位只 `Debug.LogWarning`（静默丢货，见 `docs\il\persistence.md`）。
- 实现 `Rules.ShouldApplyShape(curW,curH,newW,newH)`：仅当目标格子数**严格更大**才应用；`BinItem.ApplySize` 先读当前尺寸再决定。
- 当前尺寸用 `grid.widthPixels / UISettings.current.gridSize` 反推（`gridSize=16`，IL 已证实）；不用 `grid.inventoryShape`，因为 `GridShape` 类型在本机 csc 下是 `CS0570`（成员类型不受语言支持），也不再用反射（会触发扫描器 `reflection external`）。
- 结果：旧 12×12 箱子保持 144 格，累计到 25 点（170 > 144）才长到 17×10。

## 3. 倍率设置：滑条改原生文本框

- 根因：`ModConfigWindow.BuildEntry` 只有 `IsNumber && HasRange` 才用 `AddSlider`，而滑条**没有数值回显**。
- 无范围的数值会走 `AddInput`：显示当前值、可直接输入；`Apply()` 时 `ReadWidget` 用 `float.TryParse(NumberStyles=167, InvariantCulture)` 解析后 `ApplyBoxed`。
- 因此三个倍率的 `Define` 传 `null, null`（不定范围），范围仍由 tooltip 文案 + `ModSettings.Snapshot()` 的 `Clamp` 保证；中英文 tooltip 均保留并写明“直接输入数字”与合法范围。
- 证据见 `docs\il\capacity and config.md` §5。

## 4. 贴图接入（美术由用户出图）

- 代码优先 `SetSpriteAndShapeFromMod(modId, "shipping_bin")`：在 `ModLoader.Mods` 找 `Manifest.ID == 本 mod`，`Sprites` 命中才用；否则回退原版 `Items/items_container::eleanor_box`。
- 打包：`package.ps1` 若发现 `packaging\Textures` 则递归复制到包内，否则打印“使用原版容器贴图”。
- 规范（每格 16px、宽高为 16 的倍数、建议 32×32、透明区不占格、格中心 alpha>0 即占格、只扫描 `Textures\Items` 直属目录、key 为文件名）写入 `docs\art spec.md`。
- 未生成任何美术资源，未改 art 目录。

## 5. 测试与验证（第 4 轮）

- `tests\CoreTests.cs` 增至 **336** 条：四档尺寸/等级、`ShouldApplyShape` 7 条（含旧 12×12 不缩容）、`ApplyUpgradePoints` 跨档与溢出（299+5 → 300 溢出 4；298+5 溢出 3；满级拒绝）、`PurchasePrice = 2000`。
- 真实流水线：`build.ps1` → Built；`test rules.ps1` → 336 passed；`package.ps1` → `build\package dev`；`tools\scan.ps1` → `Refused=False`、`MethodsScanned=203`、Findings 全空。哈希见 `docs\acceptance.md` §8.4。

## 6. 边界

- 16×30 单页的实际操作手感、17×20 推导档的平衡，需实机确认。
- 旧档容器保持 144 格（大于新档同点数容量）是有意的数据保护，不做迁移。
- 倍率非法输入原版不提交；越界值被 `Snapshot()` clamp，但配置文件保留玩家原值。
- `UISettings.gridSize` 若被其他 mod 改动，形状反推随之变化（读运行时值，未硬编码）。

# 第五轮：末档 960、形状迁移、正视贴图（2026-11-16）

## 1. 末档 960 而非 480
- 原版 `ShipSystemDirectory.HyperTechBay()` 两页各 `new GameGridInventory(16,30)` = 480，合计 **960**（IL 已证实）。
- 用户明确：末档必须完整 960，不允许只给半页；若单页 32×30 能可靠显示可等容，但必须说明 UI 限制。
- 选择单页 32×30 单网格，而不是改多页容器：多页需要 `MachineryTabs`/`MachineryContainer`，原版只用于机器窗口，没有“可移动多页容器”先例；把 contentWindow 改成多节点会让旧存档的“物品 + 非物品节点 BFS 序号”错位，解码只 `Debug.LogWarning` 并静默丢货。
- 代价：内容区宽 `32 × 16 = 512px`，低分辨率/UI 缩放下可能超出屏幕且无滚动条；原版每页宽 256px。需实机确认是否可接受。

## 2. 形状迁移必须重排而非只看面积
- 12×12(144) → 17×10(170) 面积变大但高度变矮，原本在 y=10/11 的物品会越界；`GameGridInventory.SetShape` 只重算像素与背景，不检查物品位置（IL 已证实）。
- 规则层新增 `src/Core/ShapeMigration.cs`：`Decide(curW, curH, tgtW, tgtH)` → `None` / `ApplyDirect`（真超集）/ `Relocate`（互有增减）/ `KeepCurrent`（缩容或目标非法）。
- 适配层 `BinItem.ApplySize(bin, allowRelocation)`：升级路径允许重排，执行“取出全部 → SetShape → 逐件 `GraphUtils.TryAcceptAll` 放回”，全部成功才写 `TagShapeApplied = 宽×1000+高`；失败则恢复原形状并原样放回，提示 `upgrade_pending_space`（材料已消耗、点数保留、下次腾出空间再拖材料）。
- 读档 `onLoaded` 传 `allowRelocation = false`：只做真超集扩容，绝不静默重排玩家摆放。

## 3. 倍率文本框（延续第 4 轮）
- 三个倍率保持“无 min/max 的 float 定义”，走原版 `AddInput` 原生文本框（显示当前值、可直接输入）；范围由 tooltip + `Snapshot()` 的 Clamp 保证。本轮未改该逻辑。

## 4. 正视贴图接入
- 轴测图弃用；使用 `art/wooden shipping bin front.png`（1024×1024 正视图，用户 API 生成并确认）。
- 处理：裁 alpha bbox（134..889 × 169..872）→ 等比缩放进 32×32（内容 32×30，偏移 (0,1)）→ 预乘 alpha 盒式降采样消除暗边 → 边缘 alpha 40/110 清理 → 四格中心 (8,8)/(24,8)/(8,24)/(24,24) 全部 255（2×2 占格）。
- 产物 `packaging/Textures/Items/shipping_bin.png`（32×32，2,553 B）；来源图保留在 `art/`。
- 运行时：`BinItem.TryApplyModSprite` 先探测 `ModLoader.Mods[].Sprites`，有图调 `SetSpriteAndShapeFromMod(modId, "shipping_bin")`，无图回退原版 `Items/items_container::eleanor_box`。

## 5. 验证（第 5 轮）
- `tests\CoreTests.cs` 增至 **349** 条：容量末档 960（32×30）、跨档/溢出期望更新、`ShapeMigration.Decide` 13 条（含 12×12→17×10 必须重排、17×20→17×10 缩容保持、旧 12×12→32×30 为超集）。
- 真实流水线：`build.ps1` → Built；`test rules.ps1` → 349 passed；`package.ps1` → `Included mod textures` + `build\package dev`；`tools\scan.ps1` → `Refused=False`、`MethodsScanned=209`、Findings 全空。包内 5 文件哈希见 `docs\acceptance.md` §9.4。

## 6. 边界
- 32×30 单页 UI 显示/操作需实机；若不可接受，需要多页容器改造（会牵动存档结构，另行设计）。
- 原版是两页 16×30，本实现是单页 32×30 等容，交互形态与原版不完全一致。
- 重排逻辑的规则层已测；实机 `Expel`/`TryAcceptAll` 的重排手感与失败回滚需实测（含塞满后升级）。
- 贴图 alpha 清理阈值（40/110）针对当前素材；换素材需重调并复查四格中心。
- 旧档 12×12 在点数 < 25 时保持 12×12（不缩容），属有意的数据保护。

# 第六轮：待扩容恢复、读档解码顺序、4×4 本体贴图（2026-11-16）

## 1. 待扩容（pending resize）取代“再拖材料”
- 触发场景：拖材料升级时点数已加、材料已消耗，但箱内布局放不下新形状（`RelocateIntoShape` 逐件放回失败）。
- 旧实现留下不可恢复状态：`CanUpgrade` 在 300 点直接拒绝，玩家无法再次触发迁移。
- 定案：失败时把目标尺寸写入 `lushanlan.shipping_bin.pending_shape`（`宽*1000+高`），并提示“已记下待扩容，不再消耗材料”；`CanUpgrade` 对“满级但有待扩容”放行；`DoUpgrade` 先零消耗重试一次；开箱（Harmony prefix）与睡觉（结算前）各重试一次。成功才清除标记并写夜报。
- 理由：迁移是否成功取决于箱内当前布局，只有玩家实际整理后才能确定；不消耗材料是避免玩家为失败付两次成本。

## 2. 读档先建最大画布，绝不先缩小
- 证据：`SaveItemNode` 不保存 inventory 形状；`SaveManager.DecodeNodesInternal` 先用工厂建物品、再按节点序号 `UncheckedAccept` 挂回子物品（不校验形状、不调 onLoaded）；`PlayerStore.LoadGame` 通过 `DecodeSaveItem` 触发 `onLoaded`，此时子物品已挂回。
- 定案：工厂一律 `CreateInventoryWindow(32,30)`；新建箱子由 `PrepareFresh` 收窄到 10×10；旧档（无 `TagShapeApplied`）保留 32×30 + 写 `pending_shape`，等开箱/睡觉迁移；新档按 `TagShapeApplied` 恢复上次形状。
- 理由：若工厂按当前档位建小画布，旧档物品在挂回瞬间就落在界外，之后扩容也不会把它们移回来（UI 里“消失”）。最大画布是唯一不需要几何判定的安全做法（`GridShape` 在本机 csc 中不可引用）。

## 3. 本体 4×4 与贴图版本化
- 用户要求箱子本体占格 4×4（64×64，16px/格），并验证 16 个格中心 alpha>0。
- 兼容问题：第 5 轮构建已把 sprite key `shipping_bin`（32×32，2×2）写进过玩家存档；若把 64×64 图直接放回同名 key，旧 2×2 物品会顶着 4×4 贴图渲染，并可能与背包邻格重叠。
- 定案：新 key `shipping_bin_v2`（64×64，4×4）；旧 key `shipping_bin` 保留为 32×32 legacy；`OnLoaded` 按存档 `spriteAtlasPath/spritePath` 恢复对应贴图（v2→4×4、legacy→2×2、无记录→原版容器图）。
- 另据 IL：存档不保存 `useSpriteFromMod`，读档必须重新 `SetSpriteAndShapeFromMod`，否则贴图解析走原版图集显示 unknown。旧 2×2 箱子保持 2×2 占格（不自动涨到 4×4），因为无法静态验证 4×4 是否会与邻格碰撞；列入实机验证。

## 4. 验证
- `tools\test-rules.ps1`：363 checks（新增 `TryDecodeShape` 7 条、`DecideOnLoad` 7 条）。
- `build.ps1` / `package.ps1` / `tools\scan.ps1`：Refused=False、MethodsScanned=220、Findings 全空。
- 包内新增/变更：`Textures\Items\shipping_bin_v2.png`（9,608 B）、`shipping_bin.png`（2,749 B，legacy 32×32）。

## 5. 边界
- 旧 2×2 箱子不自动变 4×4；需要时由玩家取出重放或另行迁移。
- 打开箱子的重试依赖 `ItemMouseDoubleClickHandler.OpenContentAction`；绕过它的路径会推迟到睡觉。
- 32×30 窗口在低分辨率的可读性、迁移后物品摆放位置的具体观感需实机确认。
