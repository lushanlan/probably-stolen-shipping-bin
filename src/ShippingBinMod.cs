using System;
using HarmonyLib;

namespace ShippingBin
{
    // 模组入口：只做装配，不承载规则。
    //
    // 分层：
    //   src\Core\*  纯规则（无游戏类型，可在游戏外完整测试）
    //   src\Game\*  游戏适配（物品工厂/结算事务/购买/赠送/本地化/配置）
    //
    // 生命周期（ModdingGuide 行 505-506）：Init -> OnEnable，读档时 hook 按
    // Init->Early->Normal->Late 顺序触发。
    public sealed class ShippingBinMod : IMod
    {
        internal static string ModId;
        internal static ModLog Log;
        internal static ModSettings SettingsRef;
        private static Harmony harmony;
        private static bool purchaseRegistered;

        public void Init(ModManifest manifest)
        {
            ModId = manifest.ID;
            Log = new ModLog(manifest);
            Loc.Init(ModId);

            SettingsRef = new ModSettings();
            SettingsRef.DefineAll(ModId);
        }

        public void OnEnable()
        {
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHook.OnNewGameStarted += OnNewGameStarted;
            ModHook.OnGameLoadedLate += OnGameLoadedLate;
            ModHook.OnWakingUpLate += TryDebugGrant;
            ModHook.OnGoingSleepEarly += OnGoingSleep;
            ModHook.OnModAssetsLoaded += OnAssetsLoaded;
            ModHook.OnCreateTooltipLate += BinTooltip.Append;

            harmony = new Harmony(ModId);
            Purchase.Patch(harmony);
            BinOpenHook.Patch(harmony);
            UpgradeDragPatch.Patch(harmony);
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHook.OnGameLoadedLate -= OnGameLoadedLate;
            ModHook.OnWakingUpLate -= TryDebugGrant;
            ModHook.OnGoingSleepEarly -= OnGoingSleep;
            ModHook.OnModAssetsLoaded -= OnAssetsLoaded;
            ModHook.OnCreateTooltipLate -= BinTooltip.Append;
            purchaseRegistered = false;

            ModHelper.RemoveNetworkUpgrades(ModId);

            if (harmony != null)
            {
                harmony.UnpatchAll(ModId);
                harmony = null;
            }
        }

        internal static void WriteLog(string message)
        {
            if (Log != null && !string.IsNullOrEmpty(message))
            {
                Log.Warning(message);
            }
        }

        // 本地化文件在 Init/OnEnable 之后才加载（ModdingGuide 行 443），
        // 所以购买项要等到资产全部就绪再注册，否则副标题会显示成键名。
        private static void OnAssetsLoaded(LoadedMod mod)
        {
            if (purchaseRegistered || mod == null || mod.Manifest == null || mod.Manifest.ID != ModId)
            {
                return;
            }
            purchaseRegistered = true;
            Purchase.Register(ModId);
        }

        private static void RegisterItems(ModItemDirectory directory)
        {
            if (directory == null)
            {
                return;
            }
            directory.Add(ModIds.ItemIdentifier, BinItem.Create);
        }

        private static void OnNewGameStarted(string scenarioId)
        {
            DebugGrant.OnNewGame(SettingsRef);
        }

        private static void TryDebugGrant()
        {
            DebugGrant.TryGrant(SettingsRef);
        }

        // 读档后补提交上次没落地完的记账义务（涉案/假标签）。
        private static void OnGameLoadedLate()
        {
            TryDebugGrant();
            try
            {
                PendingLedger.Recover(PlayerStore.Instance);
            }
            catch (Exception ex)
            {
                WriteLog("ledger recovery on load failed: " + ex);
            }
        }

        private static void OnGoingSleep()
        {
            // 睡前最后时机：先补发尚未成功的调试赠箱，再统一结算。
            // 结算挂在 OnGoingSleepEarly 是因为它在 EndNight 最前，且结果会被
            // 稍后的 BeginDay -> SaveGame 一并写入存档（IL 已证实）。
            DebugGrant.TryGrant(SettingsRef);
            NightSettlement.Run(SettingsRef);
        }
    }
}
