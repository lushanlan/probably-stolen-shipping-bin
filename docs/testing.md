# 验证记录

本文件记录**真实执行过**的构建、测试、扫描与游戏内确认范围。
原则：只写做过的；游戏内没做的，列在“未验证/未覆盖”。

## 环境

- 游戏：`Probably Stolen`，本机版本 `0.46D`（读取自 `Probably Stolen_Data/globalgamemanagers` 的 PlayerSettings）。
- 编译器：`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5 语法；本机 `dotnet` 只有 runtime）。
- Shell：PowerShell 7.6.9。
- 原版程序集：`Probably Stolen_Data/Managed/Assembly-CSharp.dll`（只读调查，未修改）。

## 命令与真实结果

### 1. 构建

```
pwsh -File .\build.ps1
```

```
Built E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod\build\ShippingBin.dll
```

### 2. 规则与事务测试（游戏外）

```
pwsh -File .\tools\test-rules.ps1
```

```
[ok] Rules
[ok] Protection
[ok] Planner
[ok] Commit
[ok] Ledger
[ok] CommitLedger
[ok] GrantPolicy
[ok] ShapeMigration
[ok] UpgradeStatus
411 checks passed, 0 failed.
All core rule tests passed (game integration NOT tested).
PASS tools/test-rules.ps1 (game integration is NOT tested by this script)
```

覆盖概要：

- 规则：售价/赃物/违禁/假贴标的倍率与门槛、取整（半数远离零）、容量四档与跨档、升级余点与溢出、满级拒绝、购买冷却、调试赠送决策、形状迁移决策、tooltip 状态格式。
- 保护：所有权、锁、任务物（剧情表 + 激活委托单）、`NOT_FOR_RESALE`、危险废物、自动化料斗（含嵌套两层容器）、出货箱自身、嵌套出货箱、环引用。
- 规划：多箱顺序独立、跨箱/同箱去重、整树估值与 `IGNORE_CHILD_VALUE`、赃物顶层只计一次、违禁品不套赃物公式、假贴标全晚合计 1%（1–20）、溢出保留。
- 事务：成功路径顺序、Detach 部分失败（含失败项自身）回滚、Detach/Verify 异常、回滚自身失败（不假报成功）、第二件付款异常（只对已付款子集记账）、销毁瞬时/持久失败、犯罪/假贴标异常、写结算日失败、空夜。
- 记账单：编码/解码往返与非法文本、`InFlight` 视为已生效、付款子集重算、记账失败不写结算日。

注意：以上是**纯规则层**测试，不加载游戏程序集，不代表玩法已在游戏中全部验证。

### 3. 开发包

```
pwsh -File .\package.ps1
```

```
Development package only: ...\build\package-dev
```

### 4. 官方 manifest parser + ModScanner

```
pwsh -File .\tools\scan.ps1
```

```
Manifest ID=lushanlan.shipping_bin; Assemblies=ShippingBin.dll; Capabilities=
Refused        : False
MethodsScanned : 234
Capabilities   : {}
Findings       : {}
HardFlags      : {}
Pending        : {}
```

### 5. 发行候选（独立目录 + zip + 哈希清单）

```
pwsh -File .\release.ps1
```

生成 `build\release\ShippingBin\`（9 个文件）与 `build\release\ProbablyStolen-ShippingBin-0.1.0.zip`，并对发行目录再跑一次 `tools\scan.ps1`（结果同上），最后写 `build\release\SHA256SUMS.txt`。

### 6. 工坊 VDF 与封面

```
pwsh -File .\tools\make-workshop-vdf.ps1
```

生成 `build\release\workshop\preview.png`（512×512，透明底）与 `build\release\workshop\workshop_item.vdf`（`appid=4348910`、`publishedfileid=0`、`visibility=2`）。**不登录、不上传。**

## 发行候选哈希（SHA-256）

`build\release\ShippingBin\`：

| 文件 | 字节 | SHA-256 |
| --- | ---: | --- |
| manifest.xml | 658 | `3B892B5E4C0EA94E9812719DAA7D873AA87C22E41F82ACF5DC1589DED594145C` |
| ShippingBin.dll | 69,632 | `3DBFBE4CBC635C83AD5D291FA224A6F8CAF37128A0FDB1D1BE5CFA2E2DD6F04E` |
| LICENSE | 1,066 | `0A03C453DD0D79867A020F33E1F775986E31EB1C5A13B77CCF0800FEEAEF6708` |
| README.md | 5,911 | `4E9774C76CD9B48FBC4736BD68C46410749035C37B7FBE68A38DC3C2E6A50CDF` |
| CHANGELOG.md | 2,356 | `779E7EB29F308359DC7A17F0E41072B2CEE96CE2B194D9757D06A3421AAA76A1` |
| Localization/en.csv | 6,371 | `882793367D4A343A7FA4401047CDF4CC164C028C87533221A7945F2CF75BB8E3` |
| Localization/zh.csv | 5,914 | `396FD08A5812C07938DC9C94FD9D6FD799B8FCF795EC6499D0C71B3B607A1570` |
| Textures/Items/shipping_bin_v2.png | 9,608 | `9749E6C3F3434395D960F51A6B718697F6C39E844BD04FF809ADE7E5FBDB03C5` |
| Textures/Items/shipping_bin.png | 2,749 | `B3FF2B16C582429F1C256C649015AE0986F3226348D748538745BAB543AC6B33` |

压缩包与工坊附带文件：

| 文件 | 字节 | SHA-256 |
| --- | ---: | --- |
| ProbablyStolen-ShippingBin-0.1.0.zip | 54,089 | `DC4E3863BA348CF1CAC0740B958771EE74714BC88685FA535EFF7A027BF221E5` |
| workshop/preview.png | 513,677 | `06BA3235D550AFA380A4BD11A6A57CC3256CAA38E491713CFA08B64310F5420C` |
| workshop/workshop_item.vdf | — | `5467667838DA126C028F81ADE6379F8E9F1467CFC660704C4A5E6EA3F8F37E6D` |

说明：`csc` 的编译不是确定性的，重建后 DLL 哈希会变化；manifest、本地化、贴图哈希稳定。

## 游戏内已确认（维护者实机回报）

以下三项为**确切确认通过**，不扩写为全矩阵：

1. `junk` / `scrap_metal` / `metal_ingot` 各拖拽升级一次，分别得到 1 / 2 / 5 点，且 tooltip 立即刷新。
2. 嵌套在其它容器里的自动化料斗（进料斗/出料斗）整体保留、不被出售；普通容器仍按原规则正常出售。
3. 次日早间报告显示出货箱的件数与销售额，且为黑色文本。

## 未验证 / 未覆盖

- 结算失败后的持久记账“至多一次”恢复：仅在单元测试与代码路径上覆盖，**未**在真实崩溃、断电、强制退出场景验证。
- 32×30 内容窗口在低分辨率/小窗口下的实际显示（原版窗口无滚动）。
- 旧存档中 2×2 占格的旧版箱子与新 4×4 箱子的混用表现（已刻意不迁移旧箱占格，避免与邻格物品重叠）。
- 除 `hopper_in`/`hopper_out` 之外的其它模组物流容器。
- 完整的犯罪/假贴标组合矩阵（仅单元测试覆盖规则层）。
- 购买时背包空间不足、放置失败回滚的真实游戏内表现（代码与单测覆盖，未逐项实机）。
- 调试模式赠送的跨读档/跨新局行为（未在真实存档生命周期里逐项验证）。
