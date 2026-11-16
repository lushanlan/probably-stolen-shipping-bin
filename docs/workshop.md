# 创意工坊发布清单（Steam Workshop）

本文件是公开发布的步骤与模板说明。**不包含任何凭据**；上传需维护者人工登录。

## 1. 产物位置

| 产物 | 路径 |
| --- | --- |
| 发行目录（上传内容根） | `build\release\ShippingBin\` |
| 发行 zip（给玩家手动安装） | `build\release\ProbablyStolen-ShippingBin-0.1.0.zip` |
| 哈希清单 | `build\release\SHA256SUMS.txt` |
| 封面（512×512 透明底） | `build\release\workshop\preview.png` |
| SteamCMD 上传描述文件 | `build\release\workshop\workshop_item.vdf` |
| 中英文案源文件 | `packaging\workshop\description.zh.txt`、`description.en.txt` |
| VDF 模板 | `packaging\workshop\workshop.vdf.template` |

重新生成：

```powershell
pwsh -File .\release.ps1
pwsh -File .\tools\make-workshop-vdf.ps1
```

## 2. VDF 字段

生成后的 `workshop_item.vdf` 关键字段：

```
"workshopitem"
{
    "appid"           "4348910"
    "publishedfileid" "0"            // 首次上传填 0；更新时填 SteamCMD 回写的 id
    "contentfolder"   "<发行目录绝对路径>"
    "previewfile"     "<preview.png 绝对路径>"
    "visibility"      "2"            // 2 = 隐藏（首次）；正式公开改 0
    "title"           "出货箱 · Shipping Bin"
    "description"     "<中英合并文案，换行已转义为 \\n>"
    "changenote"      "v0.1.0 ..."
}
```

生成脚本会打印实际路径；`preview.png` 必须小于 1 MB（当前约 0.5 MB）。

## 3. 上传步骤（人工执行）

1. 确认已经 `pwsh -File .\release.ps1` 并且 `build\release\ShippingBin\manifest.xml` 存在。
2. 确认 `workshop_item.vdf` 中 `contentfolder` / `previewfile` 是本机**绝对路径**。
3. 打开终端，人工登录 SteamCMD（账号、密码、Steam Guard 由维护者交互输入，**不要写进脚本或仓库**）：

   ```
   steamcmd +login <account> +workshop_build_item "<本机绝对路径>\build\release\workshop\workshop_item.vdf" +quit
   ```

4. 首次上传使用 `publishedfileid=0`、`visibility=2`（隐藏）。上传成功后 SteamCMD/网页会给出新的 item id。
5. 在 Steam 创意工坊页面确认条目、接受 Workshop 法律协议、检查封面/描述/变更记录。
6. 测试无误后把 `publishedfileid` 写回 VDF（或重新运行脚本带 `-PublishedFileId <id>`），将 `visibility` 改为 `0` 公开；或直接在网页端切换可见性。
7. 后续更新：同一 `publishedfileid` 重新上传，`changenote` 写本次变更。

## 4. 发布前检查

- [ ] `pwsh -File .\tools\test-rules.ps1` → `411 checks passed, 0 failed.`
- [ ] `pwsh -File .\tools\scan.ps1` → `Refused: False`、`Findings: {}`、`MethodsScanned: 234`
- [ ] `pwsh -File .\release.ps1` → 生成 9 文件发行目录 + zip + `SHA256SUMS.txt`
- [ ] zip 内不含 `*.cs`、`*.pdb`、docs、游戏 DLL
- [ ] `preview.png` 512×512、透明底、< 1 MB
- [ ] 描述文案中不出现本机路径、账号或其它个人信息
- [ ] `docs/testing.md` 的“未验证/未覆盖”一栏已随实机进展更新
- [ ] 仓库中无 `local-evidence/`、`build/`、`obj/`、`.tools/` 内容

## 5. 素材与授权

- 箱体贴图由维护者用 AI 生成，源图在 `art/`。
- 模组原创代码为 MIT 许可（见 `LICENSE`）；游戏本体与第三方素材不在本模组授权范围内。
- 工坊描述中已声明 AI 素材与 MIT 授权。
