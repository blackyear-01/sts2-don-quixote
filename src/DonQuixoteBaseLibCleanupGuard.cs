using System;
using HarmonyLib;

namespace DonQuixote;

/// <summary>
/// 兜底：BaseLib 的 <c>RunManager.CleanUp</c> 后缀完全没有做 null 检查：
/// <code>
/// CustomMessageWrapper.Unregister(__instance.NetService);
/// CustomTargetedMessageWrapper.Unregister(__instance.RunLocationTargetedBuffer);
/// </code>
///
/// 主菜单点「继续」时，如果读档在 <c>RunState.FromSerializable</c> /
/// <c>SetUpSavedSingleplayer</c> 阶段就失败了，原版会走
/// <code>
/// catch (Exception) { RunManager.Instance.CleanUp(); DisplayLoadSaveError(); throw; }
/// </code>
/// 而此时 <c>NetService</c> 还没被赋值（null），BaseLib 这一句就抛
/// NullReferenceException，把**真正的读档异常整个顶掉**：
/// 日志里只剩一条看不出原因的 NRE，真正的错误（哪个字段/哪个模型读不出来）永远丢失。
///
/// 这里只补一个 finalizer 把这个 NullReferenceException 咽掉。CleanUp 本体已经跑完
/// （清理逻辑都在 finally 里，且 NetService 为 null 时本来也没有 handler 可注销），
/// 吞掉它不影响任何行为；原始异常会照常冒泡到 TaskHelper 的日志里，
/// 下次再出问题就能直接看到真凶。
///
/// 与快速模式（Fast Mode）无关，也不参与任何数值结算。
/// 注意：不能在本模组初始化时就挂 —— 那时 BaseLib 程序集还没被加载，
/// 反射找不到类型。改为「主菜单就绪」时再挂（见 DonQuixoteMainMenuReadyPatch），
/// 那之后玩家才有可能点「继续」。
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
