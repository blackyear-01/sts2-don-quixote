using System;
using HarmonyLib;

namespace DonQuixote;

/// <summary>
/// 兜底：BaseLib 的 RunManager.CleanUp 后缀完全没有做 null 检查：主菜单点「继续」时。
/// </summary>
internal static class DonQuixoteBaseLibCleanupGuard
{
    private const string DonQuixote_TargetType = "BaseLib.Patches.Networking.RunManagerPatches";

    private const string DonQuixote_TargetMethod = "DisposeCustomMessageHandlers";

    private static bool DonQuixote_Applied;

    private static bool DonQuixote_Logged;

    public static void DonQuixote_Apply()
    {
        if (DonQuixote_Applied)
        {
            return;
        }

        try
        {
            Type DonQuixote_type = AccessTools.TypeByName(DonQuixote_TargetType);
            if (DonQuixote_type == null)
            {
                // 没装 BaseLib（或还没加载）：什么都不做，下次主菜单就绪时再试。
                return;
            }

            System.Reflection.MethodBase DonQuixote_target =
                AccessTools.Method(DonQuixote_type, DonQuixote_TargetMethod);
            if (DonQuixote_target == null)
            {
                DonQuixote_Applied = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] baselib cleanup null-guard: target method not found", 0);
                return;
            }

            System.Reflection.MethodInfo DonQuixote_finalizer =
                AccessTools.Method(typeof(DonQuixoteBaseLibCleanupGuard), nameof(DonQuixote_Finalizer));

            new Harmony("sts2.donquixote.baselibcleanup").Patch(
                DonQuixote_target,
                finalizer: new HarmonyMethod(DonQuixote_finalizer));

            DonQuixote_Applied = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] baselib cleanup null-guard applied", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixote_Applied = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] baselib cleanup null-guard failed: " + DonQuixote_Exception.Message, 0);
        }
    }

    private static Exception? DonQuixote_Finalizer(Exception __exception)
    {
        if (__exception is NullReferenceException)
        {
            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] baselib cleanup guard: swallowed NullReferenceException "
                    + "(RunManager.NetService was null — usually the save failed to load)",
                    0);
            }

            return null;
        }

        return __exception;
    }
}
