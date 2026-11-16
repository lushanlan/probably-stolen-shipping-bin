# 公开发布前内容审计

审计对象：`E:\SteamLibrary\steamapps\common\Probably Stolen\shipping-bin-mod` 工作树 + `.git` 历史（5 个提交，134 个 loose object，无 pack 文件）。
审计时间：2026-11-16。本文件只记录**类别与处置**，不记录任何敏感值。

## 1. 内容分级与处置

| 内容 | 风险 | 处置 |
| --- | --- | --- |
| `docs/il/*.md`（7 份 IL 核证报告，含原版反编译片段） | 高：含原版代码片段，不适合随公开仓库分发 | 已移动到 gitignored 的 `local-evidence/il/`；仓库只保留 `docs/technical-notes.md`（方法引用与结论） |
| `docs/settlement-handoff.md`、`docs/integration-review.md`、`docs/release-plan.md` | 中：内部计划/历史交接，含内部流程与旧结论 | 已移动到 `local-evidence/` |
| `docs/reference/ModdingGuide.md`（官方文档缓存，169 KB） | 中：第三方文档全文 | 保持 gitignored（`docs/reference/`） |
| `docs/acceptance.md`、`docs/decisions-shipping-bin.md` | 低：主要是结论与验证记录，少量“IL”措辞 | 保留；深度证据改指向本地目录 |
| `art/*.png`、`art/*.jpg` | 低：AI 生成素材源图 | 保留（美术源，非发行包内容） |
| `obj/` 下 Cecil 脚本与转储、`.tools/gh/`（本机 GitHub CLI） | 低：本机工具 | `obj/`、`.tools/` 均已 gitignored |
| 游戏 `Managed/*.dll` | 高：原版程序集，禁止分发 | 从未在仓库内；发行白名单也会拒绝 |

## 2. 敏感信息扫描结论

扫描方式：工作树文件 + `.git` 全部 loose object 逐条解压后做模式匹配（密钥前缀、私钥块、URL userinfo、常见凭据文件、个人路径、手机号）。

结论：**未发现真实密钥、令牌、密码或私钥。**

命中项（均为误报或占位，仅记录文件与类型，不含取值）：

- `src/Game/ModIds.cs`：自定义 tag 名称中出现 `...debug_grant_token` 字样（这是游戏内 tag 标识符，不是密钥）。
- `docs/reference/ModdingGuide.md`、`local-evidence/release-plan.md`：文档中的 `+login <account>` 命令占位符（不是凭据）。
- `.git/config`：私有远端 URL（**不含**用户名/密码 userinfo）。
- 提交作者/提交者邮箱：仅 GitHub `noreply` 地址，无个人邮箱。
- 未发现 `.pem/.key/.pfx/.p12/.env/credentials` 等敏感文件名，未发现 `C:\Users\<用户名>` 形式的个人路径，未发现手机号。

## 3. 大型文件

| 文件 | 大小 | 状态 |
| --- | ---: | --- |
| `.tools/gh/bin/gh.exe` | 41,800,504 B | gitignored（本机工具，非项目内容） |
| `.tools/gh.zip` | 15,169,772 B | gitignored |
| `art/wooden-shipping-bin-front.png` | 3,049,181 B | 保留（美术源） |
| `art/wooden-shipping-bin.png` | 2,895,376 B | 保留（旧轴测参考，未用于发行） |

无游戏 DLL、无构建产物入库（`build/`、`obj/`、`*.dll`、`*.zip` 均 gitignored）。

## 4. 发行包内容白名单

`release.ps1` 对舞台执行白名单校验，当前发行包仅 9 个文件：

```
manifest.xml
ShippingBin.dll
LICENSE
README.md
CHANGELOG.md
Localization/en.csv
Localization/zh.csv
Textures/Items/shipping_bin.png
Textures/Items/shipping_bin_v2.png
```

不含：源代码、`.pdb`、IL 报告、docs、游戏程序集、Harmony/UnityEngine DLL、凭据。

## 5. 仍需维护者处理（本次未做）

1. **Git 历史**：历史提交中仍包含 `docs/il/*.md` 等含反编译片段的文件。按用户要求“不动 `.git` 历史、不 push、不改可见性”，本次未改写历史。公开仓库前需由维护者决定是否用 `git filter-repo`/BFG 清理，或直接以干净历史重新初始化。
2. 公开仓库的可见性切换与首次 push 由维护者执行。
3. Steam 创意工坊上传需人工登录 SteamCMD，仓库与脚本中不保存账号、密码或 Steam Guard 代码。
4. 首版发布后需在创意工坊页面接受 Workshop 法律协议，并记录 SteamCMD 回写的 `publishedfileid`。

## 6. 复核建议命令

```powershell
# 确认本地证据目录不会入库
git status --porcelain --ignored | Select-String 'local-evidence'

# 确认发行包内容
pwsh -File .\release.ps1
Get-Content .\build\release\SHA256SUMS.txt
```

（若本机无 `git`，可改用其它 Git 客户端执行等价检查。）
