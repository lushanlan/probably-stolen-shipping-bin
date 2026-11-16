# 出货箱贴图规范（交给美术出图用）

本文件描述**需求与已接入素材**。必须使用**正视图（front view）**；轴测/等距（isometric）图不符合原版风格，已明确弃用。把图放到约定路径后重新运行 `package.ps1` 即会打进包里并自动生效；找不到自定义贴图时退回原版容器贴图，不影响可玩性。来源图保留在 `art\` 目录不动，接入包的是处理后的 32×32 副本。

## 1. 文件放哪里

```
packaging\
└── Textures\
    └── Items\
        └── shipping_bin.png     ← 文件名必须正好是 shipping_bin
```

打包后进入 mod 目录：

```
<mod 目录>\
└── Textures\
    └── Items\
        └── shipping_bin.png
```

规则（来自原版 `ModLoader.LoadTextures` / `LoadTexturesFromFolder` 的 IL 与官方 ModdingGuide）：

- 只扫描 `Textures\Items\` **直属**目录，**不递归**；放到子目录会被静默忽略。
- 支持 `.png` 与 `.jpg`；**请用 PNG**（JPG 没有透明通道，整张图会被当作满格矩形）。
- 文件名（不含扩展名）就是 sprite key；本模组固定读取 `shipping_bin`，改文件名会导致读不到并回退原版贴图。
- 同一个 mod 里如有重名文件，后加载的会覆盖前者；不要放多个 `shipping_bin.*`。

## 2. 尺寸与像素

- 一个背包格 = **16 × 16 像素**（`UISettings.gridSize` 在 `UISettings..ctor` 中被赋值为 16，[IL 已证实]）。
- 图片宽、高都必须是 **16 的整数倍**：16×16（占 1×1 格）、32×32（占 2×2 格）、48×48（占 3×3 格）……
- 不是 16 的倍数时，原版按 `(int)(宽 / 16)` 截断取整，右侧/底部多余的像素会被丢弃。
- 推荐：**32×32**（占 2×2 格，作为可移动容器比较醒目且在背包/柜台里容易放下）。如果希望更小，用 16×16（1×1）。
- 图片按 1:1 像素绘制，不要做缩放降采样；原版强制 **点过滤（Point）、无 mipmap、Clamp 环绕**，任何抗锯齿/半透明渐变都会显得脏。

## 3. 透明与占格形状（重要）

物品占几格、是什么形状，**由图片的 alpha 通道自动推导**（`GameItem.SetSpriteAndShapeFromMod` 的 IL 已证实）：

- 把图片按 16×16 切成格子，对每个格子的**中心像素**取 alpha：
  - `alpha > 0` → 该格被物品占用；
  - `alpha == 0` → 该格为空（物品不占）。
- 因此：
  - 全不透明的 32×32 → 2×2 的方形占格；
  - 只画左上/右下两块（透明角）→ L 形等非矩形占格，原版支持；
  - **边缘不要半透明**：格中心只要 alpha 略大于 0 就算占用，半透明描边会让占格比视觉上大。
- 物品占格必须能放进背包/柜台，否则玩家拖不进去；占格越大越难放置。**建议最多 2×2（32×32）**。

## 4. 内容窗口（不是这张图）

出货箱打开后的格子界面由代码里的 `GameGridInventory`（10×10 起，随升级扩大）绘制，和这张 icon 图**没有关系**。这张图只决定：背包里的图标外观、以及物品自身占几格。

## 5. 生效与验证

```powershell
pwsh -File .\package.ps1     # 会打印 "Included mod textures: ...\packaging\Textures"
```

进游戏后在 F8 控制台确认：

```
[ModLoader] 出货箱: ... , 1 item sprite(s), ... loaded
[ModLoader] Item sprite loaded: shipping_bin (出货箱)
```

如果看到 `[GameItem] Mod sprite 'shipping_bin' from mod 'lushanlan.shipping_bin' not found.`，说明文件名或目录不对，代码会回退到原版 `Items/items_container::eleanor_box`。

## 6. 可选：Mods 菜单预览图

`packaging\preview.png` 会被当作 mod 列表里的预览图（官方 ModdingGuide 行 283）。它与物品贴图是两回事，尺寸随意（建议 512×512 以内）。当前 `package.ps1` 尚未复制它——如果需要，告诉我，我加一行复制逻辑。

## 7. 当前已接入的素材与处理方式（第 5 轮）

- 来源：`art/wooden-shipping-bin-front.png`（1024×1024，**正视图**，用户用 API 生成并目视确认；**不要**再使用轴测图 `art/wooden-shipping-bin.png`）。
- 处理步骤（System.Drawing，只做缩放/裁切，未重绘）：
  1. 读取 alpha bbox（本次 134..889 × 169..872 = 756×704）；
  2. 等比缩放进 32×32（本次内容 32×30，偏移 (0,1)）；
  3. 预乘 alpha 盒式降采样 `sum(rgb*a)/sum(a)`，避免直通 alpha 插值产生暗边；
  4. 边缘清理：`alpha < 40` → 全透明，`alpha >= 110` → 255，其余置 0；
  5. 校验四格中心 (8,8)/(24,8)/(8,24)/(24,24) 的 alpha = 255（即 2×2 占格）。
- 产物：`packaging/Textures/Items/shipping_bin.png`（32×32，2,553 B）。来源图保留在 `art/`，不修改、不删除。
- 换素材时重复上述步骤，并确认占格仍为 2×2（如需 1×1，把内容缩进单个 16×16 格并让另外三个格中心透明）。
## 8. 第 6 轮：最终贴图规格（4×4 本体）

### 8.1 当前交付的两张图
| 文件 | 尺寸 | 占格 | 用途 |
| --- | --- | --- | --- |
| `packaging\Textures\Items\shipping_bin_v2.png` | 64×64 | **4×4** | 新箱子本体（当前规格） |
| `packaging\Textures\Items\shipping_bin.png` | 32×32 | 2×2 | 旧存档兼容（第 5 轮构建写进存档的 key） |

### 8.2 4×4 的判定方式
- 每格 `UISettings.gridSize = 16` 像素；64×64 = 4×4 格。
- 形状由 `SetSpriteAndShapeFromMod` 按**每个格子的中心像素** alpha>0 推导（中心点：8/24/40/56 组合，共 16 个）。
- 验收命令（PowerShell + System.Drawing）会逐个打印 16 个中心 alpha；本轮结果全部 255，`opaque=3874 / transparent=222`。

### 8.3 从 1024×1024 正视素材导出的处理流程
1. 计算 alpha≥16 的包围盒（本轮 135..888 × 170..871 = 754×702）。
2. 非等比缩放到 64×64（小比例拉伸以填满整格，避免底边透明导致 4×4 不完整）。
3. 每个输出像素对源区域做**预乘 alpha 平均**：`sum(rgb*a)/sum(a)`、`a=255*sum(a)/n`。
4. 边缘清理：`a<40` 全透明、`a>=110` 置 255，其余置 0（消除半透明描边/暗边）。
5. 校验 16 个格中心 alpha>0，再写 PNG。

### 8.4 透明与形状要求（无变化）
- 必须 RGBA PNG；JPG 无透明会让整张图变成满格。
- 透明区域 = 空占格；本模组要求完整 4×4，因此 16 个格中心都必须不透明。
- 文件必须直属 `Textures/Items/`（扫描不递归），key 取文件名（不含扩展名）。
- 新增/替换贴图不需要改代码，只要保持文件名 `shipping_bin_v2.png`（64×64）。
