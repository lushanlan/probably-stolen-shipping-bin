using System;

namespace ShippingBin.Core
{
    // 容器形状变更策略（纯规则，与游戏无关，可离线测试）。
    //
    // 背景：旧存档里的出货箱可能比当前档位尺寸更大或形状不同（例如第 3 轮版本的
    // 12 x 12 = 144 格）。GameGridInventory.SetShape 只重算像素与背景，不会检查
    // 已有物品是否仍然落在合法格内（IL 已证实），所以“面积变大”并不等于“安全”：
    //   12 x 12 (144) -> 17 x 10 (170) 面积变大，但高度从 12 缩到 10，
    //   原本在 y=10/11 两行的物品会越界，必须重新排布或保持原状。
    //
    // 因此按几何关系分三种处理：
    //   * 目标宽高都 >= 当前（真超集）：任何物品都不可能越界，直接设形状；
    //   * 非超集：需要把物品全部取出、设新形状、再逐件放回，失败则完整回滚；
    //   * 目标不大于当前：保持原状（绝不缩容，缩容必然丢货风险）。
    public enum ShapeAction
    {
        // 目标尺寸与当前一致，什么都不用做。
        None = 0,
        // 真超集：直接 SetShape + Validate 即可，不会让任何物品越界。
        ApplyDirect = 1,
        // 形状互有增减：必须取出全部物品后重排，失败回滚。
        Relocate = 2,
        // 目标比当前小，或目标非法：保持当前形状。
        KeepCurrent = 3
    }

    // 读档时对目标形状的处理结果（窗口形状 vs 点数目标）。
    public enum LoadShapeAction
    {
        // 目标与当前一致或目标非法：什么都不做。
        None = 0,
        // 可以把当前形状直接换成目标（目标对已有物品安全）。
        ApplyTarget = 1,
        // 目标形状会让已有物品越界：保留当前较大形状，并写入待扩容标记，
        // 等打开箱子/睡觉等安全时机再做重排。
        KeepAndMarkPending = 2
    }

    public static class ShapeMigration
    {
        // currentWidth/currentHeight <= 0 表示读不出当前尺寸（未知）：直接按目标应用，
        // 由后续逻辑自行验证；这类情况只可能出现在异常读档或没有形状的旧数据上。
        public static ShapeAction Decide(int currentWidth, int currentHeight, int targetWidth, int targetHeight)
        {
            if (targetWidth <= 0 || targetHeight <= 0)
            {
                return ShapeAction.KeepCurrent;
            }
            if (currentWidth <= 0 || currentHeight <= 0)
            {
                return ShapeAction.ApplyDirect;
            }
            if (currentWidth == targetWidth && currentHeight == targetHeight)
            {
                return ShapeAction.None;
            }
            if (targetWidth >= currentWidth && targetHeight >= currentHeight)
            {
                return ShapeAction.ApplyDirect;
            }
            long currentCells = (long)currentWidth * currentHeight;
            long targetCells = (long)targetWidth * targetHeight;
            if (targetCells < currentCells)
            {
                // 缩容一律拒绝：面积更大才能保证旧物品有地方放。
                return ShapeAction.KeepCurrent;
            }
            return ShapeAction.Relocate;
        }

        // 读档策略：存档不序列化 inventory 形状（SaveItemNode 只有 GameItem 的
        // itemShape/itemModifiedShape，IL 已证实），形状完全由工厂 + 点数 tag 重建。
        // 因此读档时若目标形状不是当前形状的真超集，且已有物品放不进目标形状，
        // 绝不能直接 SetShape（会越界、UI 里看不到物品），而要保留当前（可能更大的）
        // 形状并标记待扩容，等安全时机重排。
        public static LoadShapeAction DecideOnLoad(int currentWidth, int currentHeight, int targetWidth,
            int targetHeight, bool allItemsFitTarget)
        {
            if (targetWidth <= 0 || targetHeight <= 0)
            {
                return LoadShapeAction.None;
            }
            if (currentWidth <= 0 || currentHeight <= 0)
            {
                return LoadShapeAction.ApplyTarget;
            }
            if (currentWidth == targetWidth && currentHeight == targetHeight)
            {
                return LoadShapeAction.None;
            }
            if (allItemsFitTarget)
            {
                return LoadShapeAction.ApplyTarget;
            }
            if (targetWidth >= currentWidth && targetHeight >= currentHeight)
            {
                return LoadShapeAction.ApplyTarget;
            }
            return LoadShapeAction.KeepAndMarkPending;
        }

        // 尺寸编码用于写入物品 tag（实际生效的形状），格式：宽 * 1000 + 高。
        // 取值上限 32 x 30，远小于 1000，不会串位。
        public static int EncodeShape(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return 0;
            }
            return width * 1000 + height;
        }

        // 解码 EncodeShape 的取值；0 / 负数 / 超出当前容量上限的编码都视为“没有记录”。
        // 用于读取"上次实际应用的形状"与"待扩容目标形状"。
        public static bool TryDecodeShape(int encoded, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (encoded <= 0)
            {
                return false;
            }
            int w = encoded / 1000;
            int h = encoded % 1000;
            if (w <= 0 || h <= 0)
            {
                return false;
            }
            if (w > 32 || h > 32)
            {
                return false;
            }
            width = w;
            height = h;
            return true;
        }
    }
}

