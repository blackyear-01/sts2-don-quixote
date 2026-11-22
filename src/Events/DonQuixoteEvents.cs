using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DonQuixote.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Events;

/// <summary>
/// 事件公用工具：判断人物、增减堕落值计数器、发放/移除遗物、生成升级过的罕见卡。
/// </summary>
internal static class DonQuixoteEventHelper
{
    private static readonly PropertyInfo DonQuixote_RelicsProperty =
        typeof(Player).GetProperty("Relics", BindingFlags.Public | BindingFlags.Instance);

    public static bool DonQuixote_IsDonQuixote(Player DonQuixote_player)
    {
        return DonQuixote_player != null && DonQuixote_player.Character is DonQuixoteCharacter;
    }

    public static DonQuixoteKnightOfInstinct DonQuixote_KnightRelic(Player DonQuixote_player)
    {
        if (DonQuixote_player == null || DonQuixote_RelicsProperty == null)
        {
            return null;
        }

        if (DonQuixote_RelicsProperty.GetValue(DonQuixote_player) is not IEnumerable<RelicModel> DonQuixote_relics)
        {
            return null;
        }

        return DonQuixote_relics.OfType<DonQuixoteKnightOfInstinct>().FirstOrDefault();
    }

    /// <summary>
    /// 增减「向本能堕落的骑士」的堕落值计数器（事件里所有增减堕落值都走这里）。
    /// </summary>
    public static void DonQuixote_ChangeCorruption(Player DonQuixote_player, int DonQuixote_delta)
    {
        DonQuixoteKnightOfInstinct DonQuixote_relic = DonQuixote_KnightRelic(DonQuixote_player);
        if (DonQuixote_relic == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] corruption change skipped: knight relic not found", 0);
            return;
        }

        int DonQuixote_next = DonQuixote_relic.DonQuixote_DegenerateCounter + DonQuixote_delta;
        if (DonQuixote_next < 0)
        {
            DonQuixote_next = 0;
        }

        if (DonQuixote_next > 999)
        {
            DonQuixote_next = 999;
        }

        DonQuixote_relic.DonQuixote_DegenerateCounter = DonQuixote_next;
        DonQuixote_relic.DonQuixote_RefreshCounter();
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] event corruption " + (DonQuixote_delta >= 0 ? "+" : string.Empty)
            + DonQuixote_delta + " -> " + DonQuixote_next
            + " saved_props=" + DonQuixote_SavedPropCount(DonQuixote_relic), 0);

        try
        {
            DonQuixote.DonQuixoteDegenerateConsoleCmd.DonQuixote_SyncCombatCorruption(DonQuixote_player, 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event corruption sync failed: " + DonQuixote_Exception.Message, 0);
        }
    }

    /// <summary>
    /// 调试用：报告这个遗物当前会被序列化多少个自定义字段。
    /// 为 0 说明模型没进 ModelIdSerializationCache（[SavedProperty] 不生效，
    /// 联机开战同步会把计数清零）。
    /// </summary>
    private static string DonQuixote_SavedPropCount(RelicModel DonQuixote_relic)
    {
        try
        {
            MegaCrit.Sts2.Core.Saves.Runs.SavedProperties DonQuixote_props =
                MegaCrit.Sts2.Core.Saves.Runs.SavedProperties.From(DonQuixote_relic);
            int DonQuixote_ints = DonQuixote_props?.ints?.Count ?? 0;
            return DonQuixote_ints.ToString();
        }
        catch (Exception DonQuixote_Exception)
        {
            return "err:" + DonQuixote_Exception.GetType().Name;
        }
    }

    public static async Task DonQuixote_GrantRelic<T>(Player DonQuixote_player) where T : RelicModel
    {
        RelicModel DonQuixote_relic = ModelDb.AllRelics.FirstOrDefault(DonQuixote_model => DonQuixote_model is T);
        if (DonQuixote_relic == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn("[DonQuixote] relic template not found: " + typeof(T).Name, 0);
            return;
        }

        // ModelDb.AllRelics 里是 canonical 模板，RelicCmd.Obtain 要求可变的副本，
        // 直接传模板会抛 CanonicalModelException 并让事件回调中断（表现为事件卡住）。
        await RelicCmd.Obtain(DonQuixote_relic.ToMutable(), DonQuixote_player, 0);
        DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] event granted relic " + typeof(T).Name, 0);
    }

    public static async Task DonQuixote_RemoveRelic<T>(Player DonQuixote_player) where T : RelicModel
    {
        if (DonQuixote_RelicsProperty?.GetValue(DonQuixote_player) is not IEnumerable<RelicModel> DonQuixote_relics)
        {
            return;
        }

        RelicModel DonQuixote_relic = DonQuixote_relics.FirstOrDefault(DonQuixote_model => DonQuixote_model is T);
        if (DonQuixote_relic == null)
        {
            return;
        }

        await RelicCmd.Remove(DonQuixote_relic);
        DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] event removed relic " + typeof(T).Name, 0);
    }

    public static List<CardModel> DonQuixote_UpgradedUncommon(Player DonQuixote_player, int DonQuixote_count)
    {
        List<CardModel> DonQuixote_result = new List<CardModel>();
        if (DonQuixote_player?.Character?.CardPool == null)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event card skipped: player has no card pool", 0);
            return DonQuixote_result;
        }

        // 卡库里拿到的是 canonical 模板（没有 owner，RunState / CardScope 都是 null），
        // 直接用 CardScope.CreateCard 会空引用；必须用玩家所在 run 的作用域来建卡。
        if (DonQuixote_player.RunState is not ICardScope DonQuixote_scope)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event card skipped: run scope unavailable", 0);
            return DonQuixote_result;
        }

        List<CardModel> DonQuixote_possible;
        try
        {
            CardCreationOptions DonQuixote_options = CardCreationOptions.ForNonCombatWithDefaultOdds(
                new[] { DonQuixote_player.Character.CardPool },
                DonQuixote_card => DonQuixote_card.Rarity == CardRarity.Uncommon);

            DonQuixote_possible = DonQuixote_options
                .GetPossibleCards(DonQuixote_player)
                .Where(DonQuixote_card => DonQuixote_card != null && DonQuixote_card.Rarity == CardRarity.Uncommon)
                .ToList();
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event card pool failed: " + DonQuixote_Exception.Message, 0);
            return DonQuixote_result;
        }

        if (DonQuixote_possible.Count == 0)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event card skipped: no uncommon card in pool", 0);
            return DonQuixote_result;
        }

        for (int DonQuixote_i = 0; DonQuixote_i < DonQuixote_count; DonQuixote_i++)
        {
            // 联机时所有端都会重复执行这个选项，用 run 的共享 RNG 才能保证各端抽到同一张卡。
            MegaCrit.Sts2.Core.Random.Rng DonQuixote_rng = DonQuixote_EventRng(DonQuixote_player);
            int DonQuixote_index = DonQuixote_rng != null
                ? DonQuixote_rng.NextInt(DonQuixote_possible.Count)
                : Random.Shared.Next(DonQuixote_possible.Count);

            CardModel DonQuixote_pick = DonQuixote_possible[DonQuixote_index];
            CardModel DonQuixote_instance = DonQuixote_scope.CreateCard(DonQuixote_pick, DonQuixote_player);
            try
            {
                CardCmd.Upgrade(DonQuixote_instance, CardPreviewStyle.None);
            }
            catch (Exception DonQuixote_Exception)
            {
                DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                    "[DonQuixote] event upgrade failed: " + DonQuixote_Exception.Message, 0);
            }

            DonQuixote_result.Add(DonQuixote_instance);
        }

        return DonQuixote_result;
    }

    /// <summary>
    /// 事件发卡用的随机源：优先用 run 的共享 RNG（联机各端一致），拿不到时退回本地随机。
    /// </summary>
    private static MegaCrit.Sts2.Core.Random.Rng DonQuixote_EventRng(Player DonQuixote_player)
    {
        try
        {
            return DonQuixote_player?.RunState?.Rng?.Niche;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async Task DonQuixote_GiveRandomUpgradedUncommon(Player DonQuixote_player)
    {
        List<CardModel> DonQuixote_cards = DonQuixote_UpgradedUncommon(DonQuixote_player, 1);
        foreach (CardModel DonQuixote_card in DonQuixote_cards)
        {
            await CardPileCmd.Add(DonQuixote_card, PileType.Deck, CardPilePosition.Bottom, null, false);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] event gave upgraded card " + DonQuixote_card.Id.Entry
                + " to player " + DonQuixote_player.NetId, 0);
        }
    }

    public static async Task DonQuixote_ChooseUpgradedUncommon(
        Player DonQuixote_player, int DonQuixote_offer, LocString DonQuixote_prompt)
    {
        await DonQuixote_ChooseUpgradedUncommon(
            new BlockingPlayerChoiceContext(), DonQuixote_player, DonQuixote_offer, DonQuixote_prompt);
    }

    /// <summary>
    /// 联网版本：必须传入行动队列给的 PlayerChoiceContext，联机时选择界面才会路由到选项主人。
    /// </summary>
    public static async Task DonQuixote_ChooseUpgradedUncommon(
        PlayerChoiceContext DonQuixote_choiceContext,
        Player DonQuixote_player,
        int DonQuixote_offer,
        LocString DonQuixote_prompt)
    {
        List<CardModel> DonQuixote_cards = DonQuixote_UpgradedUncommon(DonQuixote_player, DonQuixote_offer);
        if (DonQuixote_cards.Count == 0)
        {
            return;
        }

        PlayerChoiceContext DonQuixote_context = DonQuixote_choiceContext ?? new BlockingPlayerChoiceContext();
        IEnumerable<CardModel> DonQuixote_chosen = await CardSelectCmd.FromSimpleGrid(
            DonQuixote_context, DonQuixote_cards, DonQuixote_player, new CardSelectorPrefs(DonQuixote_prompt, 1));

        CardModel DonQuixote_card = DonQuixote_chosen?.FirstOrDefault();
        if (DonQuixote_card == null)
        {
            DonQuixote_card = DonQuixote_cards[0];
        }

        await CardPileCmd.Add(DonQuixote_card, PileType.Deck, CardPilePosition.Bottom, null, false);
    }

    public static async Task DonQuixote_LoseMaxHp(Player DonQuixote_player, decimal DonQuixote_amount)
    {
        await DonQuixote_LoseMaxHp(new ThrowingPlayerChoiceContext(), DonQuixote_player, DonQuixote_amount);
    }

    public static async Task DonQuixote_LoseMaxHp(
        PlayerChoiceContext DonQuixote_choiceContext, Player DonQuixote_player, decimal DonQuixote_amount)
    {
        try
        {
            // v0.112 起 LoseMaxHp 的第 4 个参数从 bool 换成了 CardModel?（卡牌来源），
            // 事件里没有来源卡，传 null。
            await CreatureCmd.LoseMaxHp(
                DonQuixote_choiceContext ?? new ThrowingPlayerChoiceContext(),
                DonQuixote_player.Creature,
                DonQuixote_amount,
                null);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] event lost max hp " + DonQuixote_amount, 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] event lose max hp failed: " + DonQuixote_Exception.Message, 0);
        }
    }

    public static async Task DonQuixote_LoseHp(Player DonQuixote_player, decimal DonQuixote_amount)
    {
        await DonQuixote_LoseHp(new ThrowingPlayerChoiceContext(), DonQuixote_player, DonQuixote_amount);
    }

    public static async Task DonQuixote_LoseHp(
        PlayerChoiceContext DonQuixote_choiceContext, Player DonQuixote_player, decimal DonQuixote_amount)
    {
        await CreatureCmd.Damage(
            DonQuixote_choiceContext ?? new ThrowingPlayerChoiceContext(),
            DonQuixote_player.Creature,
            DonQuixote_amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null,
            null);
    }
}

/// <summary>
/// 第一层事件：灰烬中的某处。堂吉诃德获得二代眷属-桑丘（堕落值 +30），其他角色只能领取一张升级过的罕见卡。
/// </summary>
[RegisterSharedEvent]
public sealed class DonQuixoteEventAsh : ModEventTemplate
{
    public override EventAssetProfile AssetProfile => new(
        LayoutScenePath: null,
        InitialPortraitPath: "res://DonQuixote/images/events/event_sancho.png",
        BackgroundScenePath: null,
        VfxScenePath: null);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (DonQuixoteEventHelper.DonQuixote_IsDonQuixote(Owner))
        {
            return new[]
            {
                new EventOption(this, DonQuixote_Accept, InitialOptionKey("ACCEPT")),
            };
        }

        return new[]
        {
            new EventOption(this, DonQuixote_Leave, InitialOptionKey("LEAVE")),
        };
    }

    private async Task DonQuixote_Accept()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "ACCEPT");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.ACCEPT.description"));
    }

    private async Task DonQuixote_Leave()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "LEAVE");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.LEAVE.description"));
    }
}

/// <summary>
/// 第二层事件：梦幻的拉曼却领。堂吉诃德四选一（三条眷属线各给一件眷属遗物并 +30 堕落值，
/// 或走「冒险」拿 10 点生命上限 + 三选一罕见卡）；其他角色只有「冒险」这一个选项。
/// </summary>
[RegisterSharedEvent]
public sealed class DonQuixoteEventDream : ModEventTemplate
{
    public override EventAssetProfile AssetProfile => new(
        LayoutScenePath: null,
        InitialPortraitPath: "res://DonQuixote/images/events/event_dream.png",
        BackgroundScenePath: null,
        VfxScenePath: null);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (DonQuixoteEventHelper.DonQuixote_IsDonQuixote(Owner))
        {
            // 堂吉诃德四个选项全开：三条眷属线（血猎 / 告解 / 游行）+ 冒险（生命上限 + 三选一罕见卡）。
            return new[]
            {
                new EventOption(this, DonQuixote_Hunt, InitialOptionKey("HUNT")),
                new EventOption(this, DonQuixote_Confess, InitialOptionKey("CONFESS")),
                new EventOption(this, DonQuixote_Parade, InitialOptionKey("PARADE")),
                new EventOption(this, DonQuixote_Adventure, InitialOptionKey("ADVENTURE")),
            };
        }

        return new[]
        {
            new EventOption(this, DonQuixote_Adventure, InitialOptionKey("ADVENTURE")),
        };
    }

    private async Task DonQuixote_Hunt()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "HUNT");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.HUNT.description"));
    }

    private async Task DonQuixote_Confess()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "CONFESS");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.CONFESS.description"));
    }

    private async Task DonQuixote_Parade()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "PARADE");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.PARADE.description"));
    }

    private async Task DonQuixote_Adventure()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "ADVENTURE");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.ADVENTURE.description"));
    }
}

/// <summary>
/// 第三层事件：最终的对决。堂吉诃德二选一（留下桑丘 / 放任桑丘离开），其他角色只能围观。
/// </summary>
[RegisterSharedEvent]
public sealed class DonQuixoteEventDuel : ModEventTemplate
{
    public override EventAssetProfile AssetProfile => new(
        LayoutScenePath: null,
        InitialPortraitPath: "res://DonQuixote/images/events/event_duel.png",
        BackgroundScenePath: null,
        VfxScenePath: null);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (DonQuixoteEventHelper.DonQuixote_IsDonQuixote(Owner))
        {
            return new[]
            {
                new EventOption(this, DonQuixote_KeepSancho, InitialOptionKey("KEEP")),
                new EventOption(this, DonQuixote_LetGo, InitialOptionKey("LET_GO")),
            };
        }

        return new[]
        {
            new EventOption(this, DonQuixote_Watch, InitialOptionKey("WATCH")),
        };
    }

    private async Task DonQuixote_KeepSancho()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "KEEP");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.KEEP.description"));
    }

    private async Task DonQuixote_LetGo()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "LET_GO");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.LET_GO.description"));
    }

    private async Task DonQuixote_Watch()
    {
        // 事件选项的效果一律走联网动作，两端执行同一份逻辑，避免只有主机改状态导致不同步。
        DonQuixoteEventAction.DonQuixote_Request(Owner, "WATCH");

        SetEventFinished(L10NLookup(Id.Entry + ".pages.WATCH.description"));
    }
}
