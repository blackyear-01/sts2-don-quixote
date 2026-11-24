using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DonQuixote;

/// <summary>
/// 美味饼干（YUMMY_COOKIE）是原版先古遗物。
/// </summary>
internal static class DonQuixoteCookieRelicIcon
{
    internal const string DonQuixote_CookieEntry = "YUMMY_COOKIE";

    internal const string DonQuixote_CookieIconPath = "res://DonQuixote/images/relics/cookie.png";

    internal const string DonQuixote_CookieOutlinePath = "res://DonQuixote/images/relics/cookie_outline.png";

    private static Texture2D DonQuixote_CookieIcon;

    private static Texture2D DonQuixote_CookieOutline;

    private static bool DonQuixote_LoggedIcon;

    private static readonly HashSet<string> DonQuixote_SeenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 这枚遗物的持有者是不是堂吉诃德。
    /// </summary>
    internal static bool DonQuixote_IsDonQuixoteRelic(RelicModel DonQuixote_Relic)
    {
        if (DonQuixote_Relic == null)
        {
            return false;
        }

        try
        {
            MegaCrit.Sts2.Core.Entities.Players.Player DonQuixote_Owner = DonQuixote_Relic.Owner;
            if (DonQuixote_Owner != null)
            {
                return DonQuixote_Owner.Character is DonQuixoteCharacter;
            }

            // 还没拿到手（先古遗物奖励界面）时 Owner 为空，改用当前这局的主角色判断。
            RunState DonQuixote_State = RunManager.Instance == null ? null : RunManager.Instance.DebugOnlyGetState();
            if (DonQuixote_State == null)
            {
                return false;
            }

            IReadOnlyList<MegaCrit.Sts2.Core.Entities.Players.Player> DonQuixote_Players = DonQuixote_State.Players;
            if (DonQuixote_Players == null || DonQuixote_Players.Count == 0)
            {
                return false;
            }

            return DonQuixote_Players[0].Character is DonQuixoteCharacter;
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn("[DonQuixote] relic owner lookup failed: " + DonQuixote_Exception.Message, 0);
            return false;
        }
    }

    internal static bool DonQuixote_IsCookie(RelicModel DonQuixote_Relic)
    {
        if (DonQuixote_Relic == null)
        {
            return false;
        }

        try
        {
            ModelId DonQuixote_Id = DonQuixote_Relic.Id;
            string DonQuixote_Entry = DonQuixote_Id == null ? null : DonQuixote_Id.Entry;
            if (DonQuixote_Entry == null)
            {
                return false;
            }

            if (DonQuixote_SeenEntries.Add(DonQuixote_Entry) && DonQuixote_Entry.IndexOf("COOKIE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] cookie relic icon requested for entry " + DonQuixote_Entry, 0);
            }

            return string.Equals(DonQuixote_Entry, DonQuixote_CookieEntry, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn("[DonQuixote] relic id lookup failed: " + DonQuixote_Exception.Message, 0);
            return false;
        }
    }

    internal static Texture2D DonQuixote_GetIcon()
    {
        if (DonQuixote_CookieIcon == null)
        {
            DonQuixote_CookieIcon = Godot.ResourceLoader.Load<Texture2D>(DonQuixote_CookieIconPath, null, Godot.ResourceLoader.CacheMode.Reuse);
            if (DonQuixote_CookieIcon != null && !DonQuixote_LoggedIcon)
            {
                DonQuixote_LoggedIcon = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] cookie relic icon override applied", 0);
            }
            else if (DonQuixote_CookieIcon == null)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn("[DonQuixote] cannot load " + DonQuixote_CookieIconPath, 0);
            }
        }

        return DonQuixote_CookieIcon;
    }

    internal static Texture2D DonQuixote_GetOutline()
    {
        if (DonQuixote_CookieOutline == null)
        {
            DonQuixote_CookieOutline = Godot.ResourceLoader.Load<Texture2D>(DonQuixote_CookieOutlinePath, null, Godot.ResourceLoader.CacheMode.Reuse);
        }

        return DonQuixote_CookieOutline;
    }
}

/// <summary>
/// 只对美味饼干生效的贴图替换，其它遗物原样返回。
/// </summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.Icon), MethodType.Getter)]
internal static class DonQuixoteCookieIconPatch
{
    private static void Postfix(RelicModel __instance, ref Texture2D __result)
    {
        if (!DonQuixoteCookieRelicIcon.DonQuixote_IsCookie(__instance))
        {
            return;
        }

        if (!DonQuixoteCookieRelicIcon.DonQuixote_IsDonQuixoteRelic(__instance))
        {
            return;
        }

        Texture2D DonQuixote_Texture = DonQuixoteCookieRelicIcon.DonQuixote_GetIcon();
        if (DonQuixote_Texture != null)
        {
            __result = DonQuixote_Texture;
        }
    }
}

/// <summary>
/// 描边版本，背包/提示里会给遗物图标加描边，同样换成我们的。
/// </summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IconOutline), MethodType.Getter)]
internal static class DonQuixoteCookieOutlinePatch
{
    private static void Postfix(RelicModel __instance, ref Texture2D __result)
    {
        if (!DonQuixoteCookieRelicIcon.DonQuixote_IsCookie(__instance))
        {
            return;
        }

        if (!DonQuixoteCookieRelicIcon.DonQuixote_IsDonQuixoteRelic(__instance))
        {
            return;
        }

        Texture2D DonQuixote_Texture = DonQuixoteCookieRelicIcon.DonQuixote_GetOutline();
        if (DonQuixote_Texture != null)
        {
            __result = DonQuixote_Texture;
        }
    }
}
