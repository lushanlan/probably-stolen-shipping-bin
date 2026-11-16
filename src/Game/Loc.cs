using System;

namespace ShippingBin
{
    // 本地化只做一层薄封装：所有面向玩家的文本都从 Localization/*.csv 读取。
    // 缺键时回退顺序由原版 ModHelper.GetLocalized 处理（当前语言 -> en -> 键名）。
    internal static class Loc
    {
        private static string modId = ModIds.Namespace;

        internal static void Init(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                modId = id;
            }
        }

        internal static string Get(string key)
        {
            return ModHelper.GetLocalized(modId, key, null);
        }

        internal static string Get(string key, object arg0)
        {
            object[] args = new object[] { arg0 };
            return ModHelper.GetLocalized(modId, key, args);
        }

        internal static string Get(string key, object arg0, object arg1)
        {
            object[] args = new object[] { arg0, arg1 };
            return ModHelper.GetLocalized(modId, key, args);
        }

        internal static string Get(string key, object arg0, object arg1, object arg2)
        {
            object[] args = new object[] { arg0, arg1, arg2 };
            return ModHelper.GetLocalized(modId, key, args);
        }

        internal static string Get(string key, object arg0, object arg1, object arg2,
                                   object arg3)
        {
            object[] args = new object[] { arg0, arg1, arg2, arg3 };
            return ModHelper.GetLocalized(modId, key, args);
        }

        internal static string Get(string key, object arg0, object arg1, object arg2,
                                   object arg3, object arg4, object arg5)
        {
            object[] args = new object[] { arg0, arg1, arg2, arg3, arg4, arg5 };
            return ModHelper.GetLocalized(modId, key, args);
        }
    }
}
