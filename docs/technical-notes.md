# 技术说明：本模组依赖的原版接口与结论

本文件是公开发布版的技术说明，**只保留方法引用与结论**，不包含原版反编译正文。
开发期逐条 IL 核证记录（含反编译片段）保存在维护者本地 `local-evidence/` 目录，不随仓库发布。

核证对象：本机 `Probably Stolen_Data/Managed/Assembly-CSharp.dll`（游戏版本 `0.46D`）。
核证方式：Cecil 静态读取（`tools/inspect.ps1`、`tools/callers.ps1` 及开发期脚本）、Unity 本地化资源解析、以及游戏内实机确认（范围见 `docs/testing.md`）。

> 原则：本模组尽量复用原版接口与语义，不自行重算原版已经算好的东西；对无法静态确定的行为，在文档中明确标注“未验证”，而不是猜测。

---

## 1. 容器与窗口

- `DirectoryUtils.CreateInventoryWindow(int width, int height, bool isDraggable)` 返回 `(PixelWindow window, GameInventory inv)`。
  出货箱用它在工厂里创建内容窗口，`inv` 实际类型为 `GameGridInventory`。
- `GameItem.SetContentWindow(PixelWindow)` 绑定内容窗口；`GameItem.contentWindow.child` 可取到内容 `GameGridInventory`。
- `GameGridInventory.SetShape(int width, int height)` + `Validate()` 调整网格形状。
- 打开容器不需要模组写点击逻辑：原版 `ItemMouseDoubleClickHandler.OpenContentAction(GameItem, Vector2)` 对任何带 `contentWindow` 的物品生效；双击/交互即可打开，`OverlayHandler.ShowInventory` 也走它。
- 容器加入谓词由 `GameInventory.mayInventoryAddItemFunc` 决定。**我们没有**使用 `ContainerHelper.InitContainerItemIncludeContainer`，因为它的谓词要求 `GeneralHelper.IsItemOwned(added)` 为真，会把赃物挡在箱外，与“赃物默认允许”的设定冲突。我们自行设置更宽松的谓词，并自行启用 `CONTAINER_TAG`。

## 2. 估值

- 原版“把整只容器作为一件物品估价”只有一个通道：`GameItem.GetCurrentValue(useRetailMarkup, withChild, includeEvents, forceMarkup, extraMarketMod)`。
  - 本模组取价参数为 `GetCurrentValue(false, true, true, false, 0)`，即“带全部后代、含营业事件、不加零售加价”。
  - 该调用内部由原版处理 `IGNORE_CHILD_VALUE`、`ITEM_BEHIND_LOCK_TAG`（锁后物品跳过）、客户市场修正继承、液体容器价值与嵌套容器后代，**不重复计价**。模组不手工逐子累加。
- 该调用（以及 `GetNegociatedValue`/`GetRefreshedValue`/`GetBaseValue`）**不乘 `unitCount`**。模组在待售根上显式乘 `max(1, unitCount)` 一次，以符合“按当前显示估值结算”。
- 原版“商店买走玩家物品”的统一口径是 `GetCurrentValue(false, true, true, false, 0)`；夜间强制零售加价的口径是 `GetCurrentValue(true, true, false, true, 0)`。
- 物品进入非后背包的自定义容器时，原版会移除“临时客户特征/违禁加价特征”（tag 不动）。因此结算按“入箱后的当前状态估值”计算，与 tooltip 显示一致，不重新添加违禁加价特征。

## 3. 销售、犯罪与声望

- 原版销售入口 `PlayerStore.SellItem(GameItem)` 的涉案金额基线是 `(int)item.GetNegociatedValue()`（上架成交价），不是 `GetCurrentValue`，也不是玩家到手金额。
- 犯罪提交通过 `SecData.CommitCrime(string crimeType, int amount)`；`RNG.MultiplyAndRound(int, double, bool)` 使用 `Math.Round` 默认（银行家舍入）。
  天赋 `IsIntimidating`（×0.67）与 `IsBlabbermouth`（×1.33）会各自再取整，属原版行为。
- 犯罪类型：赃物为 `FENCING`；违禁品按 `IsGameItemType` 映射为 `DRUG_TRAFFICKING`/`WEAPON_TRAFFICKING`/`CARD_TRAFFICKING`/`CONTRABAND_TRAFFICKING`。
- 本模组按“待售根”为犯罪提交单元：赃物只在其最上层赃物节点计一次，避免容器与子物品重复计罪；违禁品不使用赃物倍率公式（固定原版 1:1）。
- 黑市声望：`StoreReputation.GetBMReputation()`、`StoreReputation.ModReputation(StoreReputation.FACTION_BLACK_MARKET, -n, false)`。
- 金额/涉案的倍率与门槛只在本模组规则层内计算，不改变原版公式的取整方式。

## 4. 假贴标

- `VendingMachineHelper.IsMislabelled(GameItem)` 是 public static，判据来自 `ItemFeature`（伪装特征与真实特征不一致），与成交价无关、不改变成交价。
- 本模组只复用该判定，并按“整晚实际售出金额的 1%，最少 1、最多 20”扣黑市声望；**不调用** `TrackMislabelledSale`/`SettleMislabelledSales`，因此不会引入原版的 `FRAUD`/`ENDANGERMENT` 犯罪，也不会与原版夜账重复扣。
- 假贴标只对待售根判定一次（原版判定自身覆盖整棵子树），子物品不会重复累计。

## 5. 任务物与禁售保护

- 原版没有通用“任务物品”标记体系。剧情关键物只有两个 identifier：`luxury_supply_crate`、`self_refilling_neuroactive_perfume`（数据表位于 `GeneralHelper`）。
- 委托单（`StoreRequest`）在运行时按 identifier 匹配 `CanThisItemFillRequest`，不给物品打 tag。因此本模组在结算时遍历当前激活的委托单，命中则整根保留。
- `IMPORTANT_TAG` 广泛用于机器、家具、容器、文件、工具（数百处工厂写入），**不能**当作“任务物”或“禁止出售”的判据。
- 原版玩家销售通道（`CanSellThisItem`/`SellItem`）对机器/文档没有类型级禁售判断；`IsEligibleForRepo`（当铺/抵债通道）确实拒绝机器/文档/容器/IMPORTANT，但该通道只被高利贷催收使用，不能作为“玩家不能出售”的依据。
- 本模组真正拒售的依据只有：剧情关键物、当前激活委托单可填物品、`NOT_FOR_RESALE`、危险废物（`GeneralHelper.IsHazardousWaste`）、锁定/锁后、以及自动化料斗（见下）。未拥有物品仅在“赃物/违禁”开关关闭时拒收。

## 6. 自动化料斗

- 进料斗 identifier 为 `hopper_in`，带 `HOPPER_INTAKE_TAG`；出料斗 identifier 为 `hopper_out`，带 `HOPPER_OUTLET_TAG`。两者是玩家自动化物流的关键部件，**不应被出售**。
- 本模组按 tag 或 identifier 识别，且实施**整树递归**：只要待售根的后代中出现料斗，整根保留并给出原因。

## 7. 材料拖拽升级

- 原版金属类材料的拖拽升级门是双向委托合并：`GameItem.MayTarget`/`CanTarget` 会合并源与目标两侧的 `mayThisTargetItemFunc`/`mayItemTargetThisFunc` 等委托；而 `MaterialDirectory` 的 `CanIngotTarget` 是**写死白名单**（售货机核心、动物笼、水箱、RAS），对其它目标返回 false，因此原版不会把金属用到我们的箱子上。
- `ItemMouseDragHandler.EndDrag` 在进入 `GameItem.Target` 前会判 `MayTarget && CanTarget`。
- 本模组用 Harmony 后置补丁在这两个门上“仅当源是三材料之一、目标是本模组自己的出货箱、且 `BinItem.CanUpgrade` 通过”时放行；随后仍走原版 `GameItem.Target` 回调链，材料由箱子自身的 `onItemTargetThisFunc` 消耗一次并加点。
- 材料点数：`junk` 1、`scrap_metal` 2、`metal_ingot` 5；原版 `StartingPerkContent.ConsumeUpgradeMaterial` 在 Tinkerer 天赋下有概率免耗，但照样加点。

## 8. 恩惠购买

- `ModHelper.AddNetworkUpgrade(...)` 注册的升级项在不同界面有不同显示条件；`NetworkUpgrade.IsHiddenForRun` 对 Advanced 页的未买店玩家隐藏。
- 本模组把购买项注册在 Advanced 页（默认买店后才显示），并提供“提前解锁”配置只影响自己的入口。
- `WildNetworkUI.OnUseClicked` 的扣款顺序为“先恩惠、后现金”，原版没有失败回滚；本模组用 Harmony 前缀先做空间预检（放不下则不扣款），并在放置失败时按快照绝对值回滚现金、恩惠、统计与冷却。

## 9. 睡眠结算时机

- 玩家睡觉时 `PlayerStore.EndNight()` 先触发 `OnGoingSleepEarly`，随后处理服务/丢货/机器等，再 `FireOnGoingSleepLate`；之后 `BeginDay()` 进入新的一天并 `SaveGame()`。
- 本模组只在 `OnGoingSleepEarly` 结算，因此：关门不卖、睡前可取回、结算结果一定被当次存档保存。出售失败的细节只写入模组日志，不再污染次日早间报告。
- 早间报告由 `StartOfDayUIManager` 读取 `PlayerStore.nightLogs` 生成，显示后清空（只显示一次）。模组写入的报告行：件数与销售额（黑色）。

## 10. 存档持久化

- 存档只保存少量顶层窗口的 JSON；每个物品记录 identifier/形状/数值/标签/子物品索引等。容器窗口与其内部网格属于物品子树，随存档一起保存。
- 读档分两遍：先按 identifier 走物品工厂重建物品，再把子物品从记录中的“父 inventory 序号”挂回。**形状（shape）字段是物品自身的，容器网格形状不单独存**，所以本模组用自定义 tag 记录“实际应用的容量形状”，在 `onLoaded` 时恢复。
- 由于读档时子物品会先挂到工厂创建的网格上、之后才触发 `onLoaded`，本模组把工厂网格初始化为**最大画布 32×30**，避免旧档中位于较大坐标的物品在挂回瞬间越界；随后在 `onLoaded` 恢复已保存形状，或标记“待扩容”等到开箱/睡觉时安全迁移。
- 建造/修改容器形状时始终用 `Expel → SetShape → TryAcceptAll`，失败则回滚形状并把物品放回原箱。

## 11. 配置、本地化与事件

- 配置：`ModHelper.GetConfig(modId)` → `ModConfig.Define(key, default, section, labelFunc, [min,max | choices], tooltipFunc, requiresRestart)`。
  - **数值若不指定 min/max，原版设置界面会使用可输入文本的控件，并显示当前值**；指定了 min/max 才会变成无数字回显的滑条。本模组三个倍率因此采用“无范围 + 读取时 clamp”的方式。
- 本地化：`Localization/<lang>.csv`，每行 `key,text`，UTF-8，无表头，`#` 注释，首个逗号后全属文本，`\n` 表示换行；读取用 `ModHelper.GetLocalized(modId, key, args)`，按 `string.Format` 占位，缺键回退 `en`、再回退键名。当前提供 `zh`、`en`。
- 每存档数据：`ModHelper.SetModData*/GetModData*`（含 Bool/Int/Float/String），用于记录结算日、待扩容、调试赠送标记、待处理记账单。
- 关键事件：`OnModItemDirectoryInit`（注册物品）、`OnModAssetsLoaded`（本地化可用后再注册购买项）、`OnNewGameStarted`、`OnGameLoadedLate`、`OnGoingSleepEarly`（结算）、`OnCreateTooltipLate`（追加物品说明行）。

## 12. 物品说明（tooltip）

- 原版 tooltip 每次悬停重新构建；`ModHook.OnCreateTooltipLate(RichTextBuilder, GameItem)` 在 `shortDescription` 之前调用，是把动态进度行放进原生物品说明的官方位置。
- 本模组的动态行显示：累计点数、下一档阈值（25/100/300）、当前实际容量；满级显示已满级；存在“待扩容”时显示目标与**实际**容量；网格读取失败时显示“当前容量暂不可读取”，不用推算值冒充实际值。

## 13. 已知不确定项

- 结算失败后的“至多一次”补偿语义只在代码层与单元测试中覆盖，**未在真实崩溃/断电场景验证**。
- 32×30 的内容窗口宽度较大，低分辨率下可能超出屏幕（无滚动）。
- 旧存档中 2×2 占格的旧版箱子不会自动变成 4×4（避免与背包邻格物品重叠），新箱子为 4×4。
- 自动化料斗只覆盖原版 `hopper_in`/`hopper_out` 两种；其它模组新增的物流容器不会被识别。
- `manifest` 中 `GameVersions` 记录 `0.46D`：游戏更新后可能在模组列表显示“版本不匹配”，但不会阻止加载。
