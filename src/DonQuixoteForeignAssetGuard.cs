using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Modding;

namespace DonQuixote;

/// <summary>
/// 兼容层（不改对方文件、不改本模组任何效果）：
///
/// 创意工坊模组 3764112372（Donquixote，作者「帽子」）里的
/// <c>DonquixoteMod.DonquixoteAssetCachePatch</c> 会拦截 <c>AssetCache.LoadAsset</c>，
/// 并用 <c>path.Contains("/donquixote", OrdinalIgnoreCase)</c> 判断「这是不是我的资源」。
/// 这个判断不区分大小写，于是我们 <c>res://DonQuixote/...</c> 的资源也被它当成自家资源：
/// 页面/场景（.tscn）会被它拿去当贴图 <c>ResourceLoader.Load</c>，必然失败，
/// 刷一串 ERROR，最后才回退到原版加载器。
///
/// 这里给它的 Prefix 再套一层前缀：遇到我们自己的路径就直接放行
/// （<c>__result = true</c> 等价于「我没处理，请走原版逻辑」），其他路径原样交给它。
/// 由于对方模组的 DLL 可能比我们先加载，也可能比我们后加载，这里用两个触发点：
/// 1) 本模组初始化时先试一次；2) 给 <c>ModManager.TryLoadMod</c> 挂 postfix，
///    每加载完一个模组再试一次，直到它的类型出现并被补丁住。
/// </summary>
internal static class DonQuixoteForeignAssetGuard
{
    /// <summary>对方补丁类的类型名（不含命名空间，避免写死 DonquixoteMod 前缀）。</summary>
    private const string DonQuixote_ForeignTypeName = "DonquixoteAssetCachePatch";

    /// <summary>本模组 PCK 的资源根。大小写敏感：对方的小写 Donquixote/ 目录不在此列。</summary>
    private const string DonQuixote_OwnResourceRoot = "res://DonQuixote/";

    private const string DonQuixote_HarmonyId = "sts2.donquixote.compat";

    private static HarmonyLib.Harmony DonQuixote_Harmony;
    private static MegaCrit.Sts2.Core.Logging.Logger DonQuixote_Logger;
    private static bool DonQuixote_installed;
    private static bool DonQuixote_retryHookInstalled;
    private static bool DonQuixote_warnedOnce;
    private static bool DonQuixote_skipLogged;

    /// <summary>已扫描过的程序集，避免每次重试都重新反射一遍。</summary>
    private static readonly HashSet<Assembly> DonQuixote_scannedAssemblies = new HashSet<Assembly>();

    /// <summary>
    /// 尝试安装防护。可以在任意时刻反复调用：对方还没加载就静默返回，等下一次触发。
    /// </summary>
    internal static void DonQuixote_Install(MegaCrit.Sts2.Core.Logging.Logger logger)
    {
        if (DonQuixote_installed)
        {
            return;
        }
        DonQuixote_Logger = logger ?? DonQuixote_Logger;
        try
        {
            DonQuixote_Harmony ??= new HarmonyLib.Harmony(DonQuixote_HarmonyId);
            DonQuixote_InstallRetryHook();

            MethodInfo DonQuixote_foreignPrefix = DonQuixote_FindForeignPrefix();
            if (DonQuixote_foreignPrefix == null)
            {
                // 对方模组还没加载完，等 ModManager.TryLoadMod 的 postfix 再来一次。
                return;
            }

            HarmonyMethod DonQuixote_guardPrefix = new HarmonyMethod(
                typeof(DonQuixoteForeignAssetGuard).GetMethod(
                    nameof(DonQuixote_SkipOwnPaths),
                    BindingFlags.Static | BindingFlags.NonPublic));
            DonQuixote_Harmony.Patch(DonQuixote_foreignPrefix, prefix: DonQuixote_guardPrefix);

            DonQuixote_installed = true;
            DonQuixote_Logger?.Info(
                "[DonQuixote] compat guard installed on " + DonQuixote_foreignPrefix.DeclaringType?.FullName
                + ".Prefix (our res://DonQuixote/ assets skip it)", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            if (!DonQuixote_warnedOnce)
            {
                DonQuixote_warnedOnce = true;
                DonQuixote_Logger?.Warn(
                    "[DonQuixote] compat guard install failed (safe to ignore, only log noise): "
                    + DonQuixote_Exception, 0);
            }
        }
    }

    /// <summary>每加载完一个模组就再试一次安装，用于对方模组比我们后加载的情况。</summary>
    private static void DonQuixote_InstallRetryHook()
    {
        if (DonQuixote_retryHookInstalled)
        {
            return;
        }
        DonQuixote_retryHookInstalled = true;

        MethodInfo DonQuixote_tryLoadMod = AccessTools.Method(
            typeof(ModManager), "TryLoadMod", new[] { typeof(Mod) });
        if (DonQuixote_tryLoadMod == null)
        {
            DonQuixote_Logger?.Warn(
                "[DonQuixote] compat guard: ModManager.TryLoadMod not found, defer will not work", 0);
            return;
        }

        HarmonyMethod DonQuixote_postfix = new HarmonyMethod(
            typeof(DonQuixoteForeignAssetGuard).GetMethod(
                nameof(DonQuixote_OnModLoaded),
                BindingFlags.Static | BindingFlags.NonPublic));
        DonQuixote_Harmony.Patch(DonQuixote_tryLoadMod, postfix: DonQuixote_postfix);
        DonQuixote_Logger?.Info(
            "[DonQuixote] compat guard: retry hook armed on ModManager.TryLoadMod", 0);
    }

    private static void DonQuixote_OnModLoaded(Mod mod)
    {
        if (DonQuixote_installed)
        {
            return;
        }
        DonQuixote_Install(DonQuixote_Logger);
    }

    /// <summary>
    /// 找到对方那个 <c>Prefix(AssetCache, string, ref Resource)</c>。
    /// 用签名字面匹配，对方换命名空间也不影响；签名对不上就静默放弃。
    /// </summary>
    private static MethodInfo DonQuixote_FindForeignPrefix()
    {
        Assembly DonQuixote_own = typeof(DonQuixoteForeignAssetGuard).Assembly;
        Type[] DonQuixote_signature =
        {
            typeof(AssetCache),
            typeof(string),
            typeof(Resource).MakeByRefType(),
        };

        foreach (Assembly DonQuixote_assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (DonQuixote_assembly == DonQuixote_own || DonQuixote_assembly.IsDynamic)
            {
                continue;
            }
            if (DonQuixote_scannedAssemblies.Contains(DonQuixote_assembly))
            {
                continue;
            }
            DonQuixote_scannedAssemblies.Add(DonQuixote_assembly);

            IEnumerable<Type> DonQuixote_types;
            try
            {
                DonQuixote_types = DonQuixote_assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException DonQuixote_Exception)
            {
                DonQuixote_types = DonQuixote_Exception.Types;
            }
            catch (Exception)
            {
                continue;
            }

            foreach (Type DonQuixote_type in DonQuixote_types)
            {
                if (DonQuixote_type == null || DonQuixote_type.Name != DonQuixote_ForeignTypeName)
                {
                    continue;
                }

                MethodInfo DonQuixote_method = DonQuixote_type.GetMethod(
                    "Prefix",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    DonQuixote_signature,
                    null);
                if (DonQuixote_method != null && DonQuixote_method.ReturnType == typeof(bool))
                {
                    return DonQuixote_method;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 对方 Prefix 的前缀：我们的资源直接跳过它，等价于「它没命中」。
    /// 目标方法返回 bool（true = 继续走 AssetCache.LoadAsset 原版逻辑）。
    /// </summary>
    private static bool DonQuixote_SkipOwnPaths(string path, ref bool __result)
    {
        if (DonQuixote_IsOwnResourcePath(path))
        {
            __result = true;
            if (!DonQuixote_skipLogged)
            {
                DonQuixote_skipLogged = true;
                DonQuixote_Logger?.Info(
                    "[DonQuixote] compat guard: foreign asset patch skipped for " + path, 0);
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// 判断是不是本模组 PCK 内的资源。大小写敏感比较：
    /// 对方自己用的是 <c>res://Donquixote/...</c>（小写 q），不会被我们误放行。
    /// </summary>
    private static bool DonQuixote_IsOwnResourcePath(string path)
    {
        return !string.IsNullOrEmpty(path)
            && path.StartsWith(DonQuixote_OwnResourceRoot, StringComparison.Ordinal);
    }
}
