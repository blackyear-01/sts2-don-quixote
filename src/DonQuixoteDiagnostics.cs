using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using DonQuixote_Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace DonQuixote;

/// <summary>
/// One-shot diagnostic that dumps what the game believes about ancients, so we
/// can tell whether the missing ancient relics are a content registration
/// problem or purely a UI/localization problem.
/// </summary>
internal static class DonQuixoteDiagnostics
{
    private static bool DonQuixote_LoggedAncients;

    public static void DonQuixote_LogAncientReport(DonQuixote_Logger DonQuixote_Logger)
    {
        if (DonQuixote_LoggedAncients)
        {
            return;
        }
        DonQuixote_LoggedAncients = true;
        try
        {
            var DonQuixote_Builder = new StringBuilder();
            List<AncientEventModel> DonQuixote_All = ModelDb.AllAncients.ToList();
            DonQuixote_Builder.Append($"[DonQuixote] ancients total={DonQuixote_All.Count}: ");
            DonQuixote_Builder.Append(string.Join(", ", DonQuixote_All.Select(DonQuixote_Describe)));
            DonQuixote_Logger.Info(DonQuixote_Builder.ToString(), 0);
            foreach (ActModel DonQuixote_Act in ModelDb.Acts)
            {
                List<AncientEventModel> DonQuixote_ActAncients;
                try
                {
                    DonQuixote_ActAncients = DonQuixote_Act.AllAncients.ToList();
                }
                catch (Exception DonQuixote_Ex)
                {
                    DonQuixote_Logger.Warn($"[DonQuixote] act {DonQuixote_Act.Id} ancients threw: {DonQuixote_Ex.Message}", 0);
                    continue;
                }
                DonQuixote_Logger.Info(
                    $"[DonQuixote] act {DonQuixote_Act.Id} ancients={DonQuixote_ActAncients.Count}: "
                    + string.Join(", ", DonQuixote_ActAncients.Select(DonQuixote_Describe)),
                    0);
            }
        }
        catch (Exception DonQuixote_Ex)
        {
            DonQuixote_Logger.Warn($"[DonQuixote] ancient report failed: {DonQuixote_Ex}", 0);
        }
    }

    private static string DonQuixote_Describe(AncientEventModel DonQuixote_Ancient)
    {
        try
        {
            return $"{DonQuixote_Ancient.Id}";
        }
        catch
        {
            return DonQuixote_Ancient.GetType().Name;
        }
    }

    private static bool DonQuixote_LoggedRelics;
    /// <summary>
    /// 打印本模组遗物池的内容与稀有度，用来确认「罕见 / 稀有遗物到底进没进
    /// 宝箱房和商店的随机池」。游戏只在 RelicRarity 为 Common / Uncommon / Rare / Shop
    /// 的遗物里抽（RelicGrabBag._rarities），其它稀有度永远不会进宝箱/商店。
    /// </summary>
    public static void DonQuixote_LogRelicPoolReport(DonQuixote_Logger DonQuixote_Logger)
    {
        if (DonQuixote_LoggedRelics)
        {
            return;
        }
        DonQuixote_LoggedRelics = true;
        try
        {
            List<RelicModel> DonQuixote_All = ModelDb.RelicPool<DonQuixoteRelicPool>().AllRelics.ToList();
            DonQuixote_Logger.Info($"[DonQuixote] relic pool total={DonQuixote_All.Count}", 0);
            foreach (RelicModel DonQuixote_Relic in DonQuixote_All)
            {
                RelicRarity DonQuixote_Rarity = DonQuixote_Relic.Rarity;
                bool DonQuixote_Rollable = DonQuixote_Rarity == RelicRarity.Common
                    || DonQuixote_Rarity == RelicRarity.Uncommon
                    || DonQuixote_Rarity == RelicRarity.Rare
                    || DonQuixote_Rarity == RelicRarity.Shop;
                DonQuixote_Logger.Info(
                    $"[DonQuixote] relic {DonQuixote_Relic.Id} rarity={DonQuixote_Rarity} rollable={DonQuixote_Rollable}",
                    0);
            }
        }
        catch (Exception DonQuixote_Ex)
        {
            DonQuixote_Logger.Warn($"[DonQuixote] relic pool report failed: {DonQuixote_Ex}", 0);
        }
    }
    /// <summary>
    /// 内容自检：确认这一轮改动的几个关键点真的落到了运行时模型上
    /// （新 power 有没有注册、卡牌接口/关键词对不对、选牌提示键在不在），
    /// 免得又出现「改了代码但游戏里没生效」这种只能靠实战才发现的问题。
    /// </summary>
    public static void DonQuixote_LogContentReport(DonQuixote_Logger DonQuixote_Logger)
    {
        try
        {
            bool DonQuixote_gritCap = ModelDb.Power<Powers.DonQuixotePowerGritCap>() != null;
            DonQuixote_Logger.Info($"[DonQuixote] selftest grit_cap_power={DonQuixote_gritCap}", 0);

            bool DonQuixote_noReflectionExhaust = ModelDb.Card<Cards.DonQuixoteNoReflection>()
                .CanonicalKeywords.Contains(CardKeyword.Exhaust);
            DonQuixote_Logger.Info(
                $"[DonQuixote] selftest no_reflection_exhaust={DonQuixote_noReflectionExhaust}", 0);

            Cards.DonQuixoteHardBloodTeaching DonQuixote_teaching = ModelDb.Card<Cards.DonQuixoteHardBloodTeaching>();
            int DonQuixote_teachCost = DonQuixote_teaching.DonQuixote_HardBloodCost;
            bool DonQuixote_promptKey = new LocString(
                "cards", "DON_QUIXOTE_CARD_DON_QUIXOTE_HARD_BLOOD_TEACHING.selectionScreenPrompt").Exists();
            DonQuixote_Logger.Info(
                $"[DonQuixote] selftest hard_blood_teaching cost={DonQuixote_teachCost} promptKey={DonQuixote_promptKey}",
                0);

            bool DonQuixote_corruptible = ModelDb.Card<Cards.DonQuixoteResoluteHeart>() is IDonQuixoteCorruptible DonQuixote_ci
                && DonQuixote_ci.DonQuixote_CorruptedCanonical is Cards.DonQuixoteHope;
            DonQuixote_Logger.Info(
                $"[DonQuixote] selftest resolute_heart_to_hope={DonQuixote_corruptible}", 0);

            // 卡面文本能否正常渲染（格式写错会在 GetFormattedText 里抛 LocException）。
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest grit_text="
                + ModelDb.Card<Cards.DonQuixoteExtraordinaryGrit>().GetDescriptionForPile(PileType.Hand)
                    .Replace("\n", " / "),
                0);
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest grit_upgrade_preview="
                + ModelDb.Card<Cards.DonQuixoteExtraordinaryGrit>().GetDescriptionForUpgradePreview()
                    .Replace("\n", " / "),
                0);
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest teaching_text="
                + ModelDb.Card<Cards.DonQuixoteHardBloodTeaching>().GetDescriptionForPile(PileType.Hand)
                    .Replace("\n", " / "),
                0);

            // La Sangre：升级前后都必须凑够 100 点血宴才能打出。
            Cards.DonQuixoteLaSangre DonQuixote_lasangre = ModelDb.Card<Cards.DonQuixoteLaSangre>();
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest la_sangre mustPay=" + DonQuixote_lasangre.DonQuixote_BloodFeastMustPay
                + " cost=" + DonQuixote_lasangre.DonQuixote_BloodFeastCost
                + " text=" + DonQuixote_lasangre.GetDescriptionForPile(PileType.Hand).Replace("\n", " / "),
                0);

            // 援护防御：打断依赖反射读 AttackCommand 的私有字段，字段名对不上就永远不触发。
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest guard_assist " + Powers.DonQuixotePowerGuardAssist.DonQuixote_Probe(),
                0);

            // 血魔之力：每回合开始给 2 层临时强壮。
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest bloodfiend_might " + Powers.DonQuixotePowerBloodfiendMight.DonQuixote_Probe(),
                0);

            // 望：必须只认自己打出的下一张攻击牌（多人下队友不能触发）。
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest hope " + Powers.DonQuixotePowerHope.DonQuixote_Probe(),
                0);

            // 重击演出：伤害 >20 时播，动画与投掷物贴图必须能加载。
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest heavy_hit_slash " + DonQuixoteHeavyHitSlash.DonQuixote_Probe(),
                0);

            // 拉曼却领舞台：两张背景贴图、两首自定义曲子、两张能力卡都必须能取到。
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest stage " + DonQuixoteStageDirector.DonQuixote_ContentProbe(),
                0);

            // 折射轨道线：章节是否真的进了 Acts 列表、Boss/先古/自定义地图能不能取到。
            DonQuixote_Logger.Info(
                "[DonQuixote] selftest rail " + Acts.DonQuixoteRefractionRailProbe.DonQuixote_Run(),
                0);
        }
        catch (Exception DonQuixote_Ex)
        {
            DonQuixote_Logger.Warn($"[DonQuixote] content selftest failed: {DonQuixote_Ex}", 0);
        }
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class DonQuixoteMainMenuReadyPatch
{
    private static void Postfix()
    {
        DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] main menu ready", 0);

        // BaseLib 的 CleanUp 空引用兜底：必须等到 BaseLib 程序集加载之后才能挂，
        // 主菜单就绪是第一个「所有 mod 都已加载、玩家还没点继续」的安全点。
        DonQuixoteBaseLibCleanupGuard.DonQuixote_Apply();

        if (DonQuixoteModEntry.DonQuixote_Logger != null)
        {
            DonQuixoteDiagnostics.DonQuixote_LogAncientReport(DonQuixoteModEntry.DonQuixote_Logger);
            DonQuixoteDiagnostics.DonQuixote_LogRelicPoolReport(DonQuixoteModEntry.DonQuixote_Logger);

            // 先古对话自检：mine=我们那组对话条数，valid=实际会被选中的条数，picked=是否选中我们。
            DonQuixoteAncientDialoguePatch.DonQuixote_SelfTest();

            DonQuixoteDiagnostics.DonQuixote_LogContentReport(DonQuixoteModEntry.DonQuixote_Logger);
        }
    }
}
