# 出货箱 Mod 交接文档（HANDOFF）

> 最后更新：2026-11-16（第 10 轮：第 8/9 轮集成复核修正：拖拽门补丁取参/放行条件、料斗递归补测、本地化键清理、数字校正）。
> 本文件是当前唯一权威的“做到哪一步、怎么构建、改了什么、别碰什么”说明。
> 早期原型交接见 `docs/settlement-handoff.md`（已过时，仅作历史记录）。

## 1. 项目一句话

《深空当铺 / Probably Stolen》的可移动出货箱 Mod：玩家购买/获赠一个占背包格的原生容器物品，把货物放进去，**睡觉时**统一按原版估值口径出售；多箱各自独立容量与升级点数。

## 2. 当前状态

- 代码：`src/Core`（纯规则层，不引用游戏类型，可在游戏外测试）+ `src/Game`（游戏适配层）+ `src/ShippingBinMod.cs`（入口）。
- 测试：`tools/test-rules.ps1` 共 **411 checks**（规则/保护/计划/事务/记账单/授予策略/形状迁移/tooltip 状态）。
- 包：`build/package-dev`（`pwsh -File .\package.ps1` 生成），含 `manifest.xml`、`ShippingBin.dll`、`Localization/{zh,en}.csv`、`Textures/Items/{shipping_bin_v2.png,shipping_bin.png}`。
- 扫描：`tools/scan.ps1` 真实调用本机 manifest parser 与 ModScanner，最近一次 `MethodsScanned=234`、Findings/HardFlags/Pending 全空、`Refused=False`。
- 实机：用户已确认“当前实机测试看来没问题”（**仅用户口头反馈，未覆盖全部规则矩阵**）。

## 3. 构建与验证（按顺序）

```powershell
pwsh -File .\build.ps1        # csc 编译到 build\ShippingBin.dll
pwsh -File .\tools\test-rules.ps1   # 纯规则层测试（当前 411 checks）
pwsh -File .\package.ps1      # 组装 build\package-dev
pwsh -File .\tools\scan.ps1   # manifest + ModScanner 扫描（Refused 必须为 False）
```

注意：
- 本机只有 .NET Runtime，编译器是 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，因此源码必须保持 **C# 5** 语法（无字符串插值、`?.`、`nameof`、表达式体成员、自动属性初始化器）。
- `build.ps1` 显式列出源文件（4 空格缩进），新增文件必须同时加进去，否则报 `CS0103`。
- csc 非确定性：DLL 每次重建哈希都会变；manifest 与 csv 稳定。

## 4. 模块地图

`src/Core`（纯逻辑，无游戏引用）：
- `Rules.cs`：全部数值与说明（售价 0.8、赃物 2.0、违禁固定 1:1、假标签 1% 且 1–20、购价 2000、容量四档 10x10 / 17x10 / 17x20 / 32x30 = 100/170/340/960、点数阈值 0/25/100/300、材料 junk 1 / scrap_metal 2 / metal_ingot 5）。
- `ItemFacts.cs`：一节点的保护判定（料斗、出货箱自身、锁、剧情物、机器/文档/重要物、赃物/违禁开关、所有权）。
- `Planner.cs`：全箱快照 → 计划（金额、赃物/违禁涉案、假标签、保留原因），顺序无关。
- `Commit.cs`：事务（复核 → 摘下 → 校验 → 付款 → 销毁 → 记账 → 落 day），失败回滚与如实报告。
- `Ledger.cs`：持久记账单（付款后涉案/假标签的至多一次重试）。
- `GrantPolicy.cs`：调试赠送策略（token + done，避免把正常购买的箱子误判为已赠送）。
- `ShapeMigration.cs`：形状迁移决策（只增不减、需重排、保守 pending）。
- `UpgradeStatus.cs`：tooltip 进度状态（Progress / MaxLevel / Pending，实际容量未知时 `ActualKnown=false`）。

`src/Game`（适配层）：
- `ModIds.cs` 全部 ID/tag/modData 键；`Loc.cs` 本地化薄封装。
- `ModSettings.cs`：8 项配置（2 个数值倍率 + 6 个开关）；两个倍率故意不带 min/max → 走原版 `AddInput` 文本框（可显示数值、可直接输入），范围由 `Snapshot()` clamp。
- `BinItem.cs`：物品工厂、容器谓词、升级（材料/点数/形状/反馈）、读档恢复、贴图恢复。
- `BinWorld.cs`：找箱、放回背包、前柜台救援与未保存位置警告。
- `FactsBuilder.cs`：GameItem → ItemFacts（含原版估值、涉案构成、假标签判定、料斗/剧情物/危险废物）。
- `NightSettlement.cs`：`OnGoingSleepEarly` 结算入口、夜间报告文案、事务世界实现。
- `PendingLedger.cs`：记账单落盘与恢复。
- `Purchase.cs`：王尔德恩惠购买项注册、购买前空间预检、失败绝对值回滚、买店前后可见性。
- `DebugGrant.cs`：新局赠送（只一次、失败保留 pending 重试）。
- `BinOpenHook.cs`：打开箱子时重试待扩容。
- `BinTooltip.cs`：物品说明追加“升级进度”行。
- `UpgradeDragPatch.cs`：修复金属锭等材料无法拖到出货箱升级（原版 `MaterialDirectory.CanIngotTarget` 是写死白名单，会在 `MayTarget/CanTarget` 处否决；对“本模组箱子 + `CanUpgrade` 为真”的情况补成 true，保留原版拖拽手势与 `GameItem.Target` 调用链）。

## 5. 最近几轮改动

- 第 6 轮：末档 960（单页 32x30，UI 限制已写入文档）；读档解码顺序修复（工厂先建最大画布，旧档保守 pending，开箱/睡觉再安全迁移）；4x4 本体贴图与 legacy 32x32 兼容。
- 第 7 轮：物品说明动态显示升级进度（`ModHook.OnCreateTooltipLate`），实际容量读取失败显示“暂不可读取”。
- 第 8 轮：`hopper_in`/`hopper_out`（进料斗/出料斗）不再被出售（整根保留并说明原因）；早间报告新增独立“销售额”行。
- 第 9 轮：金属/材料拖拽升级修复（`UpgradeDragPatch`）；销售额与售出件数改黑色；结算失败不再写入夜间报告（只写模组日志）。
- 第 10 轮（集成复核）：补丁改用 Harmony `__0` 取参；放行前显式校验“三种材料 + 本模组箱 + 所有权 + 未满级或 pending”；逐条审计 `GameItem.Target` 材料端回调（金属 `OnIngotTarget` 白名单外直接返回，材料只在箱子回调里消耗一次）；料斗递归保护补测（两层容器 + 普通容器不误拦）；删除 5 个失效 `log_commit_*` 文案键（zh/en 各 82 键）；数字校正（411 checks / 234 methods / 8 项配置）。

## 6. 已知边界 / 未实机验证

- 旧 2x2 箱子（第 5 轮存档）不会自动变成 4x4，占格按存档恢复，避免压到邻格物品。
- 料斗保护只覆盖原版两种（`hopper_in`/`hopper_out`），其它自动化设备若需要保护要另加证据。
- 16x30 / 32x30 内容的实际手感、低分辨率下的窗口宽度需实机确认。
- 记账单“至多一次”语义在真实崩溃/断电场景下未实机验证。
- 物品说明行序（原版 shortDescription 在 `OnCreateTooltipLate` 之后追加）意味着进度行出现在描述上方。

## 7. 约定（不要做）

- 不安装、不 push、不发布 Steam；仓库由用户维护。
- 不改 `art/` 目录、不重画素材；贴图由用户提供正视图后经脚本降采样接入。
- 不改原版程序集、不动正式玩家存档；实机只用独立测试档。
- 新增用户可见文本必须同时补 `packaging/Localization/zh.csv` 与 `en.csv`，且键集保持一致。
