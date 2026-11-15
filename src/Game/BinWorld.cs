using System;
using System.Collections.Generic;

namespace ShippingBin
{
    // 出货箱在世界里的定位、位置安全检查与放置辅助。
    internal static class BinWorld
    {
        internal static List<GameItem> FindAllBins()
        {
            List<GameItem> result = new List<GameItem>();
            PlayerStore store = PlayerStore.Instance;
            if (store == null)
            {
                return result;
            }

            List<GameItem> all;
            try
            {
                all = store.FindAllItem(false);
            }
            catch (Exception)
            {
                return result;
            }
            if (all == null)
            {
                return result;
            }

            for (int i = 0; i < all.Count; i++)
            {
                GameItem item = all[i];
                if (!BinItem.IsBin(item))
                {
                    continue;
                }
                if (Contains(result, item))
                {
                    continue;
                }
                result.Add(item);
            }
            return result;
        }

        internal static int CountOwnedBins()
        {
            List<GameItem> bins = FindAllBins();
            int count = 0;
            for (int i = 0; i < bins.Count; i++)
            {
                if (GeneralHelper.IsItemEffectivelyOwned(bins[i]))
                {
                    count++;
                }
            }
            return count;
        }

        // 只找“本模组调试赠送”出来的箱子（带专属 token），
        // 正常购买的箱子不算，避免把购买箱误判为已赠送。
        internal static List<GameItem> FindDebugGrantBins()
        {
            List<GameItem> result = new List<GameItem>();
            List<GameItem> bins = FindAllBins();
            for (int i = 0; i < bins.Count; i++)
            {
                GameItem bin = bins[i];
                if (BinItem.GetIntTag(bin, ModIds.TagDebugGrantToken) != 1)
                {
                    continue;
                }
                if (!GeneralHelper.IsItemEffectivelyOwned(bin))
                {
                    continue;
                }
                result.Add(bin);
            }
            return result;
        }

        internal static bool Contains(List<GameItem> list, GameItem item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (object.ReferenceEquals(list[i], item))
                {
                    return true;
                }
            }
            return false;
        }

        // 前柜台不进存档，而且 EndNight 内 EmporiumEntry.ClearFrontInv() 会把
        // 前柜台上的所有物品（含容器与内容）无条件 Destroy。结算最早发生在
        // OnGoingSleepEarly，早于 ClearFrontInv，因此在这里把箱子搬回背包栏。
        internal static void RescueFrontCounterBins()
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop == null || shop.frontInvinvElement == null)
            {
                return;
            }

            List<GameItem> bins = FindAllBins();
            for (int i = 0; i < bins.Count; i++)
            {
                GameItem bin = bins[i];
                if (!GeneralHelper.IsItemEffectivelyOwned(bin))
                {
                    continue;
                }
                if (!object.ReferenceEquals(bin.parentInventory, shop.frontInvinvElement))
                {
                    continue;
                }

                if (!shop.frontInvinvElement.Expel(bin))
                {
                    continue;
                }
                if (!TryPlaceIntoPlayerStorage(bin))
                {
                    // 放不回去就至少留在原处（会被原版清柜销毁），并在日志里说明。
                    BinItem.Feedback(Loc.Get("warn_front_counter_unrescued", BinItem.GetDisplayNameSafe(bin)));
                    shop.frontInvinvElement.UncheckedAccept(bin);
                    continue;
                }
                BinItem.Feedback(Loc.Get("warn_front_counter_rescued", BinItem.GetDisplayNameSafe(bin)));
            }
        }

        // 位置提示：这些原版容器不进存档，箱子放进去会在读档后消失。
        internal static void WarnUnsavedLocations()
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop == null)
            {
                return;
            }

            List<GameItem> bins = FindAllBins();
            for (int i = 0; i < bins.Count; i++)
            {
                GameItem bin = bins[i];
                if (!GeneralHelper.IsItemEffectivelyOwned(bin))
                {
                    continue;
                }
                if (IsInUnsavedStorage(bin, shop))
                {
                    BinItem.Feedback(Loc.Get("warn_unsaved_location", BinItem.GetDisplayNameSafe(bin)));
                }
            }
        }

        private static bool IsInUnsavedStorage(GameItem bin, EmporiumEntry shop)
        {
            GameInventory parent = bin.parentInventory;
            if (parent == null)
            {
                return false;
            }
            return object.ReferenceEquals(parent, shop.frontInvinvElement)
                || object.ReferenceEquals(parent, shop.afterhourInventory)
                || object.ReferenceEquals(parent, shop.bazarLeftinvElement)
                || object.ReferenceEquals(parent, shop.drainInvElement)
                || object.ReferenceEquals(parent, shop.faucetElement)
                || object.ReferenceEquals(parent, shop.swapBufferElement)
                || object.ReferenceEquals(parent, shop.responseInventory)
                || object.ReferenceEquals(parent, shop.responseInventoryClosable);
        }

        internal static bool TryPlaceIntoPlayerStorage(GameItem item)
        {
            if (item == null || item.parentInventory != null)
            {
                return false;
            }
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop == null)
            {
                return false;
            }
            if (TryAccept(shop.backInvinvElement, item))
            {
                return true;
            }
            if (TryAccept(shop.invElement, item))
            {
                return true;
            }
            return false;
        }

        internal static bool TryAccept(GameInventory inventory, GameItem item)
        {
            if (inventory == null || item == null || item.parentInventory != null)
            {
                return false;
            }
            try
            {
                return GraphUtils.TryAcceptAll(inventory, item, -1) > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool HasRoomFor(GameItem item)
        {
            EmporiumEntry shop = EmporiumEntry.Instance;
            if (shop == null || item == null)
            {
                return false;
            }
            try
            {
                if (shop.invElement != null && shop.invElement.TryFindOneValidInventorySlot(item) != null)
                {
                    return true;
                }
                if (shop.backInvinvElement != null && shop.backInvinvElement.TryFindOneValidInventorySlot(item) != null)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }
    }
}
