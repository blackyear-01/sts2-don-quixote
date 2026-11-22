using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Keywords;

namespace DonQuixote;

/// <summary>
/// 本模组的自定义关键词（血铸 / 技巧 / 增幅）。
/// 关键词通过 RitsuLib 的 ModKeywordRegistry 注册，注册后会得到一个动态生成的 CardKeyword 值，
/// 卡牌只要 AddKeyword 这个值，悬停时就会弹出关键词说明。
/// </summary>
internal static class DonQuixoteKeywords
{
    private const string DonQuixote_Table = "card_keywords";

    private const string DonQuixote_ForgeStem = "BLOOD_FORGE";
    private const string DonQuixote_TechniqueStem = "TECHNIQUE";
    private const string DonQuixote_AmplifyStem = "AMPLIFY";
    private const string DonQuixote_KnightStem = "KNIGHT";

    /// <summary>关键词标题 / 描述的本地化键，和 gen_loc.py 里写出的键一一对应。</summary>
    public const string DonQuixote_ForgeTitleKey = "DON_QUIXOTE_KEYWORD_BLOOD_FORGE.title";
    public const string DonQuixote_ForgeDescriptionKey = "DON_QUIXOTE_KEYWORD_BLOOD_FORGE.description";
    public const string DonQuixote_TechniqueTitleKey = "DON_QUIXOTE_KEYWORD_TECHNIQUE.title";
    public const string DonQuixote_TechniqueDescriptionKey = "DON_QUIXOTE_KEYWORD_TECHNIQUE.description";
    public const string DonQuixote_AmplifyTitleKey = "DON_QUIXOTE_KEYWORD_AMPLIFY.title";
    public const string DonQuixote_AmplifyDescriptionKey = "DON_QUIXOTE_KEYWORD_AMPLIFY.description";
    public const string DonQuixote_KnightTitleKey = "DON_QUIXOTE_KEYWORD_KNIGHT.title";
    public const string DonQuixote_KnightDescriptionKey = "DON_QUIXOTE_KEYWORD_KNIGHT.description";

    private const string DonQuixote_ForgeIcon = "res://DonQuixote/images/powers/hard_blood.png";
    private const string DonQuixote_TechniqueIcon = "res://DonQuixote/images/powers/bleed.png";
    private const string DonQuixote_AmplifyIcon = "res://DonQuixote/images/powers/wavering.png";
    private const string DonQuixote_KnightIcon = "res://DonQuixote/images/powers/duel_target.png";

    private static bool DonQuixote_registered;
    private static CardKeyword DonQuixote_forgeKeyword;
    private static CardKeyword DonQuixote_techniqueKeyword;
    private static CardKeyword DonQuixote_amplifyKeyword;
    private static CardKeyword DonQuixote_knightKeyword;

    /// <summary>
    /// 拥有「骑士」词条的卡：堕落值越低伤害越高（每低 10 点 +5% 最终伤害）。
    /// 用类型表而不是只查关键词，避免关键词还没挂上时（构造期 / 预览）判定失效。
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> DonQuixote_KnightCards =
        new System.Collections.Generic.HashSet<string>
        {
            "DonQuixoteDuelDeclaration",
            "DonQuixoteKnightDream",
            "DonQuixoteKnightCharge",
            "DonQuixoteSetOutOnAdventure",
            "DonQuixoteFairDuel",
            "DonQuixoteKnightLance",
            "DonQuixoteBloodFeastLance",
            "DonQuixoteKnightlyProtection",
            "DonQuixotePleaseHelp",
            "DonQuixoteThreeDayDuel",
            "DonQuixoteResoluteHeart",
            "DonQuixoteAdventureSight",
        };

    /// <summary>这张卡是否拥有「骑士」词条。</summary>
    public static bool DonQuixote_IsKnightCard(CardModel DonQuixote_card)
    {
        if (DonQuixote_card == null)
        {
            return false;
        }

        return DonQuixote_KnightCards.Contains(DonQuixote_card.GetType().Name);
    }

    public static CardKeyword DonQuixote_BloodForge
    {
        get
        {
            DonQuixote_Register();
            return DonQuixote_forgeKeyword;
        }
    }

    public static CardKeyword DonQuixote_Technique
    {
        get
        {
            DonQuixote_Register();
            return DonQuixote_techniqueKeyword;
        }
    }

    public static CardKeyword DonQuixote_Amplify
    {
        get
        {
            DonQuixote_Register();
            return DonQuixote_amplifyKeyword;
        }
    }

    /// <summary>
    /// 「骑士」词条：拥有该词条的卡对挂着「决斗对象」的敌人伤害 +50%
    /// （效果实现在 <see cref="DonQuixoteKnightDuelBonus"/> 里）。
    /// 用 <see cref="ModKeywordCardDescriptionPlacement.BeforeCardDescription"/> 让它在卡面上
    /// 自动排在描述前面，悬停时也能看到这条说明 —— 不用每张卡再手写一遍。
    /// </summary>
    public static CardKeyword DonQuixote_Knight
    {
        get
        {
            DonQuixote_Register();
            return DonQuixote_knightKeyword;
        }
    }

    /// <summary>注册三个关键词；重复调用是安全的（RitsuLib 对相同定义会直接复用）。</summary>
    public static void DonQuixote_Register()
    {
        if (DonQuixote_registered)
        {
            return;
        }

        DonQuixote_registered = true;

        try
        {
            ModKeywordRegistry DonQuixote_registry = ModKeywordRegistry.For(DonQuixoteModEntry.DonQuixote_ModId);

            DonQuixote_forgeKeyword = DonQuixote_registry.RegisterOwned(
                DonQuixote_ForgeStem,
                DonQuixote_Table,
                DonQuixote_ForgeTitleKey,
                DonQuixote_Table,
                DonQuixote_ForgeDescriptionKey,
                DonQuixote_ForgeIcon,
                ModKeywordCardDescriptionPlacement.None,
                true).CardKeywordValue;

            DonQuixote_techniqueKeyword = DonQuixote_registry.RegisterOwned(
                DonQuixote_TechniqueStem,
                DonQuixote_Table,
                DonQuixote_TechniqueTitleKey,
                DonQuixote_Table,
                DonQuixote_TechniqueDescriptionKey,
                DonQuixote_TechniqueIcon,
                ModKeywordCardDescriptionPlacement.None,
                true).CardKeywordValue;

            DonQuixote_amplifyKeyword = DonQuixote_registry.RegisterOwned(
                DonQuixote_AmplifyStem,
                DonQuixote_Table,
                DonQuixote_AmplifyTitleKey,
                DonQuixote_Table,
                DonQuixote_AmplifyDescriptionKey,
                DonQuixote_AmplifyIcon,
                ModKeywordCardDescriptionPlacement.None,
                true).CardKeywordValue;

            DonQuixote_knightKeyword = DonQuixote_registry.RegisterOwned(
                DonQuixote_KnightStem,
                DonQuixote_Table,
                DonQuixote_KnightTitleKey,
                DonQuixote_Table,
                DonQuixote_KnightDescriptionKey,
                DonQuixote_KnightIcon,
                ModKeywordCardDescriptionPlacement.BeforeCardDescription,
                true).CardKeywordValue;

            DonQuixote_Log("[DonQuixote] keywords registered: blood forge / technique / amplify / knight");
        }
        catch (System.Exception DonQuixote_error)
        {
            DonQuixote_registered = false;
            DonQuixote_Log("[DonQuixote] keyword registration failed: " + DonQuixote_error.Message);
        }
    }

    /// <summary>给一张卡挂上关键词（构造期调用即可，卡面与悬停都会生效）。</summary>
    public static void DonQuixote_Attach(CardModel DonQuixote_card, params CardKeyword[] DonQuixote_keywords)
    {
        if (DonQuixote_card == null || DonQuixote_keywords == null)
        {
            return;
        }

        for (int DonQuixote_index = 0; DonQuixote_index < DonQuixote_keywords.Length; DonQuixote_index++)
        {
            try
            {
                DonQuixote_card.AddKeyword(DonQuixote_keywords[DonQuixote_index]);
            }
            catch (System.Exception DonQuixote_error)
            {
                DonQuixote_Log("[DonQuixote] attach keyword failed: " + DonQuixote_error.Message);
            }
        }
    }
    private static void DonQuixote_Log(string DonQuixote_message)
    {
        try
        {
            MegaCrit.Sts2.Core.Logging.Logger DonQuixote_logger = DonQuixoteModEntry.DonQuixote_Logger;
            if (DonQuixote_logger == null)
            {
                return;
            }

            DonQuixote_logger.Info(DonQuixote_message);
        }
        catch (System.Exception)
        {
            // 日志失败不能影响游戏流程。
        }
    }
}