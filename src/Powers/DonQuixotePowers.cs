using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote.Powers;

/// <summary>
/// 血宴：触发流血时获得的资源，血宴不足 X 的卡无法打出。
///
/// 中立状态：既不是增益也不是减益（<see cref="PowerType.None"/>）。
/// 这样人工制品不会抵消它，「清增益 / 清负面 / 翻倍」也一律不碰它。
/// </summary>
public sealed class DonQuixotePowerBloodFeast : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override string CustomIconPath => "res://DonQuixote/images/powers/blood_feast.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/blood_feast_big.png";
}

/// <summary>
/// 堕落值：上限 100，每 10 点使消耗血宴的卡伤害 +5%。
/// 中立状态（<see cref="PowerType.None"/>）：不算增益也不算减益，人工制品无法抵消。
/// </summary>
public sealed class DonQuixotePowerCorruption : ModPowerTemplate
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override string CustomIconPath => "res://DonQuixote/images/powers/corruption.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/corruption_big.png";

    /// <summary>抽到可腐化的卡时，若堕落值已达阈值就替换成腐化版本。</summary>
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        return DonQuixoteCorruption.DonQuixote_TryCorruptOnDraw(choiceContext, this, card);
    }

    /// <summary>
    /// 堕落值的双向增伤（新钩子）：
    ///   非「骑士」词条的卡：每 10 点堕落值，最终伤害 +5%；
    ///   「骑士」词条的卡：每低于 100 点 10 点，最终伤害 +5%。
    /// 只作用于「自己打出的卡造成的伤害」，不吃流血 / 反伤这类无来源伤害。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature DonQuixote_target,
        decimal DonQuixote_amount,
        ValueProp DonQuixote_props,
        Creature DonQuixote_dealer,
        CardModel DonQuixote_cardSource,
        CardPlay DonQuixote_cardPlay)
    {
        if (Owner == null
            || DonQuixote_dealer == null
            || !ReferenceEquals(DonQuixote_dealer, Owner)
            || DonQuixote_cardSource == null)
        {
            return 1m;
        }

        int DonQuixote_amountNow = Amount > 0 ? (int)Amount : 0;
        if (DonQuixote_amountNow > DonQuixoteBloodMagic.DonQuixote_CorruptionMax)
        {
            DonQuixote_amountNow = DonQuixoteBloodMagic.DonQuixote_CorruptionMax;
        }

        int DonQuixote_knightSteps =
            (DonQuixoteBloodMagic.DonQuixote_CorruptionMax - DonQuixote_amountNow) / 10;
        int DonQuixote_plainSteps = DonQuixote_amountNow / 10;

        int DonQuixote_steps;
        if (DonQuixote_cardSource is Cards.DonQuixoteBloodFeastLance)
        {
            // 「饱食鲜血的长枪」是长枪的血魔形态：骑士侧与非骑士侧的增伤同时生效
            // （骑士牌按「离 100 的差额」算，非骑士牌按「当前堕落值」算，两边相加）。
            DonQuixote_steps = DonQuixote_knightSteps + DonQuixote_plainSteps;
        }
        else
        {
            DonQuixote_steps = DonQuixoteKeywords.DonQuixote_IsKnightCard(DonQuixote_cardSource)
                ? DonQuixote_knightSteps
                : DonQuixote_plainSteps;
        }

        if (DonQuixote_steps <= 0)
        {
            return 1m;
        }

        return 1m + (DonQuixoteBloodMagic.DonQuixote_CorruptionDamageStep * DonQuixote_steps);
    }

    /// <summary>堕落值一变高，手牌里已满足条件的牌立刻转化，不需要打出。</summary>
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature applier,
        CardModel cardSource)
    {
        if (!ReferenceEquals(power, this) || Owner == null || Owner.Player == null)
        {
            return;
        }

        await DonQuixoteCorruption.DonQuixote_TryCorruptHand(choiceContext, this, Owner.Player);
    }
}

/// <summary>
/// 复还的血之冲动：上限 10，每 5 层施加流血额外 +1 层，每 1 层额外 +1 强度与 +1% 伤害。
/// </summary>
public sealed class DonQuixotePowerBloodImpulse : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override string CustomIconPath => "res://DonQuixote/images/powers/blood_impulse.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/blood_impulse_big.png";
}

/// <summary>
/// 摇曳[血魔]：回合结束时获得等量护盾，下回合开始失去一半层数（向上取整）。
/// </summary>
public sealed class DonQuixotePowerWavering : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/wavering.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/wavering_big.png";

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || Owner == null || Owner.IsDead)
        {
            return;
        }

        int DonQuixote_block = Amount;
        if (DonQuixote_block <= 0)
        {
            return;
        }

        // 摇曳【血魔】的护盾按裸值结算：不经过敏捷、脆弱或任何其它修正。
        DonQuixoteBloodMagic.DonQuixote_RawBlockGain = true;
        try
        {
            await CreatureCmd.GainBlock(Owner, DonQuixote_block, ValueProp.Move, null, false);
        }
        finally
        {
            DonQuixoteBloodMagic.DonQuixote_RawBlockGain = false;
        }
    }

    // 联机安全：会改层数的回合开始效果必须走 AfterSideTurnStart。
    // 原来挂在 AfterPlayerTurnStart（每个玩家各自的回合准备），联机时一端的准备被暂停
    // 就可能让减半结算漏掉或多算 → 两端层数不一致 → 开战/回合开始校验和不同。
    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Player || Owner == null || Owner.IsDead)
        {
            return;
        }

        if (participants == null || !participants.Contains(Owner))
        {
            return;
        }

        int DonQuixote_before = Amount;
        if (DonQuixote_before <= 0)
        {
            return;
        }

        // 「加强防御」：摇曳持续多 1 回合后减半 —— 隔一个回合才减半一次。
        // 用回合号判定，两端算出来的结果一致。
        if (DonQuixoteBloodMagic.DonQuixote_AmountOf<DonQuixotePowerReinforcedDefense>(Owner) > 0)
        {
            int DonQuixote_round = combatState?.RoundNumber ?? 0;
            if (DonQuixote_round % 2 == 0)
            {
                return;
            }
        }

        // 「幸福笑容如鲜花般绽放之地 / 噩梦的拉曼却领」持续期间：摇曳不会在回合开始时消失。
        // 这两张卡自己在同一时点也会发摇曳，若不冻结，刚发的 20 层会立刻被减半。
        if (DonQuixoteStageRules.DonQuixote_KeepsWavering(Owner))
        {
            return;
        }

        // 向上取整的是保留的那一半：7 层保留 4 层（失去 3），5 层保留 3 层（失去 2）。
        int DonQuixote_kept = DonQuixoteBloodMagic.DonQuixote_CeilHalf(DonQuixote_before);
        if (DonQuixote_kept >= DonQuixote_before)
        {
            DonQuixote_kept = 0;
        }

        int DonQuixote_loss = DonQuixote_before - DonQuixote_kept;
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -DonQuixote_loss, Owner, null, false);

        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] wavering turn start layers " + DonQuixote_before
            + " -> kept " + DonQuixote_kept
            + " -> lost " + DonQuixote_loss,
            0);
    }
}

/// <summary>
/// 流血 X/Y：敌方每次行动（攻击的每段伤害、获得格挡、获得状态）时受到 X 点伤害，
/// 并给堂吉诃德等量血宴；层数 Y 每次 -1，Y 归零时强度归零。
/// </summary>
public sealed class DonQuixotePowerBleed : ModPowerTemplate, IPowerExtraIconAmountLabelsProvider
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/bleed.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/bleed_big.png";

    /// <summary>图标右侧的主数字显示剩余层数，左侧的角标显示流血强度。</summary>
    public override int DisplayAmount => DonQuixoteBloodMagic.DonQuixote_BleedLayers(Owner);

    /// <summary>
    /// 层数是存在另一个 power 上的，层数变化时主数字不会自动重绘，
    /// 这里暴露一个刷新入口供触发流血时调用。
    /// </summary>
    public void DonQuixote_RefreshAmountDisplay()
    {
        InvokeDisplayAmountChanged();
    }

    public IReadOnlyList<ExtraIconAmountLabelSlot> GetPowerExtraIconAmountLabelSlots()
    {
        if (Amount <= 0)
        {
            return Array.Empty<ExtraIconAmountLabelSlot>();
        }

        return new[]
        {
            new ExtraIconAmountLabelSlot(
                Amount.ToString(),
                ExtraIconAmountLabelCorner.TopLeft,
                default,
                new Godot.Color(0.93f, 0.55f, 0.58f),
                new Godot.Color(0.12f, 0.05f, 0.05f)),
        };
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Enemy && choiceContext != null)
        {
            DonQuixoteBloodMagic.DonQuixote_LastContext = choiceContext;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (Owner == null || Owner.IsDead || dealer == null || target == null)
        {
            return;
        }

        // 玩家身上的流血只由「打出 / 消耗卡牌」触发，见 AfterCardPlayed / AfterCardExhausted。
        if (Owner.Player != null)
        {
            return;
        }

        if (!ReferenceEquals(dealer, Owner) || ReferenceEquals(target, Owner))
        {
            return;
        }

        // 只认「怪物自己行动时的攻击段」：
        // ValueProp.Move 才是攻击伤害（尖刺 / 反伤 / 中毒这类是 Unpowered / Unblockable），
        // IsPerformingMove 为 true 才说明这只怪正在执行它自己的 MoveState；
        // 玩家回合里怪物靠反伤打出的那一下不算敌方行动，不触发流血。
        if (!props.HasFlag(ValueProp.Move))
        {
            return;
        }

        if (Owner.Monster == null || !Owner.Monster.IsPerformingMove)
        {
            return;
        }

        // 眩晕 / 睡眠只是「跳过回合」，不算行动。
        if (DonQuixote_CurrentMoveInert)
        {
            return;
        }

        // 多段攻击按「段」结算：每一段都触发一次流血，并且各扣 1 层（层数扣完就自然停下）。
        // 「段」用 CreatureCmd.Damage 的调用来划（见 DonQuixoteDamageBatchPatch）：
        // 一次调用 = 一段，AOE 打多个玩家也只算这一段，不会按人头翻倍。
        if (DonQuixote_LastBleedBatch == DonQuixote_DamageBatch)
        {
            return;
        }

        DonQuixote_LastBleedBatch = DonQuixote_DamageBatch;

        // 攻击段也算「这次行动结算过」，这样行动结束的兜底不会再补一次；
        // 但不再吃掉同一次行动里的其它类别（格挡 / 上状态 / 塞牌各自结算）。
        DonQuixote_MarkActionSettled(Owner);

        await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(choiceContext, Owner, true);
    }

    /// <summary>最近一张由流血玩家打出的牌，用于「打出并消耗」只结算一次。</summary>
    private static CardModel DonQuixote_LastPlayedCard;

    /// <summary>
    /// 敌方行动序号：每次 MonsterModel.PerformMove 开始时 +1（见 DonQuixoteEnemyMovePrefixPatch）。
    /// 它负责把「一次给 10 张状态牌」这类同一次行动里的多次钩子合并成一次结算；
    /// 攻击则按段算（多段攻击每段各触发一次），由 DonQuixote_MarkActionSettled 标记行动已结算。
    /// </summary>
    internal static long DonQuixote_MoveSerial;

    /// <summary>这次行动已经结算过的「类别:条目」。同一类别在同一行动里只算一次。</summary>
    private static readonly HashSet<string> DonQuixote_Claims = new HashSet<string>();

    /// <summary>认领表对应的（行动序号 + 生物）。</summary>
    private static long DonQuixote_ClaimSerial = -1;

    private static Creature DonQuixote_ClaimActor;

    /// <summary>当前这次敌方行动是不是「眩晕 / 睡眠」——这类行动不触发流血。</summary>
    private static bool DonQuixote_CurrentMoveInert;

    /// <summary>这次敌方行动到目前为止有没有结算过流血（有无都会阻止「行动结束兜底」再补一次）。</summary>
    private static bool DonQuixote_AnyClaimThisAction;

    /// <summary>流血结算类别：攻击不参与认领（按段各自结算）。</summary>
    internal const int DonQuixote_BleedCategoryBlock = 1;

    internal const int DonQuixote_BleedCategoryBuff = 2;

    internal const int DonQuixote_BleedCategoryDebuff = 3;

    internal const int DonQuixote_BleedCategoryCards = 4;

    internal const int DonQuixote_BleedCategoryOther = 5;

    /// <summary>
    /// 伤害「段」编号：每次 <c>CreatureCmd.Damage</c> 进入真正结算时 +1
    /// （见 DonQuixoteDamageBatchPatch）。多段攻击的每一段都是一次调用，
    /// AOE 打多个玩家则共用同一次调用，所以按它计数正好等于「攻击段数」。
    /// </summary>
    internal static long DonQuixote_DamageBatch;

    /// <summary>上一次因为攻击触发流血时所在的段编号，用来保证一段只结算一次。</summary>
    private static long DonQuixote_LastBleedBatch = -1;

    /// <summary>进入一段新的伤害结算。</summary>
    internal static void DonQuixote_NextDamageBatch()
    {
        DonQuixote_DamageBatch++;
    }

    /// <summary>
    /// 开始一次敌方行动：序号 +1，并记下这次行动是不是眩晕/睡眠。
    /// 眩晕（StunIntent / STUN_MOVE）和睡眠（SleepIntent / SLEEP_MOVE）只是「跳过回合」，不算行动。
    /// </summary>
    internal static void DonQuixote_BeginAction(MoveState DonQuixote_move)
    {
        DonQuixote_MoveSerial++;
        DonQuixote_CurrentMoveInert = DonQuixote_IsInertMove(DonQuixote_move);
        DonQuixote_Claims.Clear();
        DonQuixote_ClaimSerial = -1;
        DonQuixote_ClaimActor = null;
        DonQuixote_AnyClaimThisAction = false;
    }

    /// <summary>本次行动结束后复位，避免影响回合外的其它钩子。</summary>
    internal static void DonQuixote_EndAction()
    {
        DonQuixote_CurrentMoveInert = false;
        DonQuixote_AnyClaimThisAction = false;
        DonQuixote_Claims.Clear();
        DonQuixote_ClaimSerial = -1;
        DonQuixote_ClaimActor = null;
    }

    private static bool DonQuixote_IsInertMove(MoveState DonQuixote_move)
    {
        if (DonQuixote_move == null)
        {
            return true;
        }

        if (DonQuixote_move.Intents != null)
        {
            foreach (AbstractIntent DonQuixote_intent in DonQuixote_move.Intents)
            {
                if (DonQuixote_intent is StunIntent || DonQuixote_intent is SleepIntent)
                {
                    return true;
                }
            }
        }

        string DonQuixote_id = DonQuixote_move.Id ?? string.Empty;
        return DonQuixote_id.IndexOf("STUN", StringComparison.OrdinalIgnoreCase) >= 0
            || DonQuixote_id.IndexOf("SLEEP", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 认领「这次敌方行动某一类的流血结算」。
    /// 同一行动里同一类只算一次（连给 10 张状态牌、连续加几次格挡都只算一次）；
    /// 不同类各算一次 —— 所以「本回合施加 1 次负面 + 1 次增益」会触发两次流血。
    /// 眩晕 / 睡眠行动直接返回 false。
    /// </summary>
    internal static bool DonQuixote_TryClaimAction(
        Creature DonQuixote_actor,
        int DonQuixote_category,
        string DonQuixote_detail)
    {
        if (DonQuixote_actor == null || DonQuixote_actor.IsDead)
        {
            return false;
        }

        if (DonQuixote_CurrentMoveInert)
        {
            return false;
        }

        if (DonQuixote_ClaimSerial != DonQuixote_MoveSerial
            || !ReferenceEquals(DonQuixote_ClaimActor, DonQuixote_actor))
        {
            DonQuixote_ClaimSerial = DonQuixote_MoveSerial;
            DonQuixote_ClaimActor = DonQuixote_actor;
            DonQuixote_Claims.Clear();
        }

        string DonQuixote_key = DonQuixote_category.ToString()
            + ":"
            + (DonQuixote_detail ?? string.Empty);

        if (!DonQuixote_Claims.Add(DonQuixote_key))
        {
            return false;
        }

        DonQuixote_AnyClaimThisAction = true;
        return true;
    }

    /// <summary>这次敌方行动到目前为止有没有结算过（任意类别）。</summary>
    internal static bool DonQuixote_ActionAlreadySettled(Creature DonQuixote_actor)
    {
        return DonQuixote_AnyClaimThisAction
            && DonQuixote_ClaimSerial == DonQuixote_MoveSerial
            && ReferenceEquals(DonQuixote_ClaimActor, DonQuixote_actor);
    }

    /// <summary>
    /// 不检查「是否已经结算过」，直接把当前这次行动标记成已结算
    /// （攻击段走这条，避免行动结束的兜底再补一次）。
    /// </summary>
    internal static void DonQuixote_MarkActionSettled(Creature DonQuixote_actor)
    {
        if (DonQuixote_CurrentMoveInert)
        {
            return;
        }

        DonQuixote_ClaimSerial = DonQuixote_MoveSerial;
        DonQuixote_ClaimActor = DonQuixote_actor;
        DonQuixote_AnyClaimThisAction = true;
    }

    /// <summary>
    /// 兜底：有些行动（自愈、召唤、纯台词、抢钱…）不造成伤害也不给格挡/状态/塞牌，
    /// 上面的钩子一个都不会响。行动结束时补一次结算，保证「除眩晕睡眠外的所有行动都会触发流血」。
    ///
    /// 这里处在同步方法里（CombatHistory.MonsterPerformedMove 是普通方法），
    /// 只能 fire-and-forget；对这类「本来什么都不做」的行动，晚一两帧结算不影响结果。
    /// </summary>
    internal static void DonQuixote_SettleActionFallback(Creature DonQuixote_actor)
    {
        try
        {
            if (DonQuixote_actor == null || DonQuixote_actor.IsDead)
            {
                return;
            }

            if (!MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress)
            {
                return;
            }

            if (DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBleed>(DonQuixote_actor) == null)
            {
                return;
            }

            // 这次行动已经因为格挡 / 上状态 / 塞牌 / 攻击结算过了，兜底不再补。
            if (DonQuixote_ActionAlreadySettled(DonQuixote_actor))
            {
                return;
            }

            if (!DonQuixote_TryClaimAction(
                    DonQuixote_actor,
                    DonQuixote_BleedCategoryOther,
                    DonQuixote_actor.Monster?.NextMove?.Id))
            {
                return;
            }

            PlayerChoiceContext DonQuixote_ctx = DonQuixoteBloodMagic.DonQuixote_LastContext;
            if (DonQuixote_ctx == null)
            {
                return;
            }

            MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(
                DonQuixoteBloodMagic.DonQuixote_TriggerBleed(DonQuixote_ctx, DonQuixote_actor));
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] bleed action fallback failed: " + DonQuixote_ex.Message, 0);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null || Owner.IsDead || cardPlay == null || cardPlay.Player == null)
        {
            return;
        }

        if (Owner.Player == null || !ReferenceEquals(cardPlay.Player.Creature, Owner))
        {
            return;
        }

        // 记下这张牌：如果它紧接着被消耗，消耗钩子不再重复结算。
        DonQuixote_LastPlayedCard = cardPlay.Card;

        await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(choiceContext, Owner);
    }

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (Owner == null || Owner.IsDead || card == null || card.Owner == null)
        {
            return;
        }

        if (Owner.Player == null || !ReferenceEquals(card.Owner.Creature, Owner))
        {
            return;
        }

        // 这张牌刚刚被打出过（打出即消耗）→ 已经结算过一次，这里跳过。
        if (ReferenceEquals(card, DonQuixote_LastPlayedCard))
        {
            DonQuixote_LastPlayedCard = null;
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(choiceContext, Owner);
    }

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel cardSource)
    {
        if (Owner == null || Owner.IsDead || creature == null)
        {
            return;
        }

        if (!ReferenceEquals(creature, Owner))
        {
            return;
        }

        // 只有敌方获取防御才算「敌方行动」。
        if (Owner.Player != null)
        {
            return;
        }

        if (!DonQuixote_TryClaimAction(Owner, DonQuixote_BleedCategoryBlock, null))
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(
            DonQuixoteBloodMagic.DonQuixote_LastContext, Owner);
    }

    /// <summary>
    /// 敌方行动：给玩家塞牌（原版「策略」这类给状态牌的意图）也算一次敌方行动，要触发怪物的流血。
    ///
    /// 这条路径覆盖不到别的钩子：状态牌由 CardPileCmd.AddGeneratedCardsToCombat 生成，
    /// 它只抛 AfterCardGeneratedForCombat，既不造成伤害也不给格挡、不上状态，所以原来完全不流血。
    /// 该钩子不带「谁干的」，按原版注释 creator == null 表示敌方塞的牌（玩家自己生成的非空），
    /// 行动者则靠 MonsterModel.IsPerformingMove（只在该怪物执行 MoveState 期间为 true）认领。
    /// 一次行动里可能连给多张牌（SlimedBerserker 一次 10 张），用 DonQuixote_MoveSerial 合并成一次结算，
    /// 口径和其它钩子一致：一次敌方行动 = 一次流血。
    /// </summary>
    public override async Task AfterCardGeneratedForCombat(
        CardModel card,
        MegaCrit.Sts2.Core.Entities.Players.Player creator)
    {
        // creator 非空 = 玩家（或队友）自己生成的牌，不算敌方行动，而且打出时已经结算过流血。
        if (creator != null)
        {
            return;
        }

        if (Owner == null || Owner.IsDead || card == null || card.Owner == null)
        {
            return;
        }

        // 玩家身上的流血只由「打出 / 消耗卡牌」触发，见 AfterCardPlayed / AfterCardExhausted。
        if (Owner.Player != null)
        {
            return;
        }

        MegaCrit.Sts2.Core.Combat.ICombatState DonQuixote_state = Owner.CombatState;
        if (DonQuixote_state == null)
        {
            return;
        }

        // 认领这次行动的怪物。
        MonsterModel DonQuixote_actor = null;
        foreach (Creature DonQuixote_creature in DonQuixote_state.Creatures)
        {
            if (DonQuixote_creature != null
                && DonQuixote_creature.Monster != null
                && DonQuixote_creature.Monster.IsPerformingMove)
            {
                DonQuixote_actor = DonQuixote_creature.Monster;
                break;
            }
        }

        if (DonQuixote_actor == null || !ReferenceEquals(DonQuixote_actor.Creature, Owner))
        {
            return;
        }

        // 一次行动里连给多张状态牌只结算一次（否则「给 10 张」会瞬间打光 10 层）。
        if (!DonQuixote_TryClaimAction(Owner, DonQuixote_BleedCategoryCards, null))
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(
            DonQuixoteBloodMagic.DonQuixote_LastContext, Owner);
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature applier,
        CardModel cardSource)
    {
        if (Owner == null || Owner.IsDead || power == null || applier == null)
        {
            return;
        }

        if (power is DonQuixotePowerBleed || power is DonQuixotePowerBleedLayers)
        {
            return;
        }

        if (!ReferenceEquals(applier, Owner))
        {
            return;
        }

        // 自己给自己上状态不算「行动」；敌方给自己/队友上状态、或给玩家上状态才算敌方行动。
        if (Owner.Player != null)
        {
            return;
        }

        // 上增益和上负面是两类不同的行动：同一个意图里既给玩家上负面、又给自己上增益，
        // 要各触发一次流血（两次）。同一个状态在同一次行动里重复施加仍然只算一次。
        bool DonQuixote_onPlayer = power.Owner != null && power.Owner.Player != null;
        int DonQuixote_category = DonQuixote_onPlayer
            ? DonQuixote_BleedCategoryDebuff
            : DonQuixote_BleedCategoryBuff;

        if (!DonQuixote_TryClaimAction(Owner, DonQuixote_category, power.Id.Entry))
        {
            return;
        }

        await DonQuixoteBloodMagic.DonQuixote_TriggerBleed(choiceContext, Owner);
    }
}

/// <summary>
/// 给「一次敌方行动」编号，并记下这次行动是不是眩晕/睡眠。
/// 同一次行动里重复响的钩子（一次塞 10 张状态牌）靠这个序号合并成一次；
/// 攻击则按段结算，多段攻击每段各触发一次流血（见 DonQuixotePowerBleed.BeforeDamageReceived）。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class DonQuixoteEnemyMovePrefixPatch
{
    private static void Prefix(MonsterModel __instance)
    {
        DonQuixotePowerBleed.DonQuixote_BeginAction(__instance?.NextMove);
    }
}

/// <summary>
/// 行动结束兜底：自愈 / 召唤 / 抢钱 / 纯台词这类「什么钩子都不响」的行动，
/// 也要算一次敌方行动、触发一次流血（眩晕、睡眠除外）。
/// 跑在 MonsterModel.PerformMove 里 await 完成之后，是全同步方法，只能 fire-and-forget。
/// </summary>
[HarmonyLib.HarmonyPatch(
    typeof(MegaCrit.Sts2.Core.Combat.History.CombatHistory),
    nameof(MegaCrit.Sts2.Core.Combat.History.CombatHistory.MonsterPerformedMove))]
internal static class DonQuixoteEnemyMoveSettledPatch
{
    private static void Postfix(MonsterModel monster)
    {
        try
        {
            if (monster?.Creature != null)
            {
                DonQuixotePowerBleed.DonQuixote_SettleActionFallback(monster.Creature);
            }
        }
        finally
        {
            DonQuixotePowerBleed.DonQuixote_EndAction();
        }
    }
}

/// <summary>
/// 给每一段伤害编号：<c>CreatureCmd.Damage</c> 是真正结算的地方，多段攻击「打一下」调一次，
/// AOE 打多个玩家也只在同一次调用里循环，所以这个编号 = 攻击段数。
/// 流血的「多段攻击每段触发一次」就是靠它保证一段只结算一次（不会按人头翻倍）。
/// </summary>
[HarmonyLib.HarmonyPatch(
    typeof(CreatureCmd),
    nameof(CreatureCmd.Damage),
    new[]
    {
        // v0.113.0 起 Damage 多了 ModifyDamageHookType 参数，而且真正结算的那一支
        // 收的是 IEnumerable<DamageTargetData>：其余重载（含原本这里挂的
        // IEnumerable<Creature> 版本）都会转发到它，所以改挂最里面这一支，
        // 一次调用 = 一段伤害的语义保持不变。
        typeof(PlayerChoiceContext),
        typeof(IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.DamageTargetData>),
        typeof(decimal),
        typeof(ValueProp),
        typeof(Creature),
        typeof(CardModel),
        typeof(CardPlay),
        typeof(MegaCrit.Sts2.Core.Hooks.ModifyDamageHookType),
    })]
internal static class DonQuixoteDamageBatchPatch
{
    private static void Prefix()
    {
        DonQuixotePowerBleed.DonQuixote_NextDamageBatch();
    }
}

/// <summary>
/// 流血层数 Y。不可见，仅用于记录流血还能持续几层。
/// </summary>
public sealed class DonQuixotePowerBleedLayers : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>
/// 决斗对象：对堂吉诃德造成 / 受到伤害 +50%，对其他角色伤害为 0，回合结束减 1 层。
/// 中立状态（<see cref="PowerType.None"/>）：不按减益算，人工制品抵消不掉。
/// </summary>
public sealed class DonQuixotePowerDuelTarget : ModPowerTemplate
{
    /// <summary>受到多少点伤害才给「决斗的荣耀」。</summary>
    private const int DonQuixote_GloryDamageThreshold = 100;

    /// <summary>本回合是否被施加过效果（记回合号）。</summary>
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty(
        MegaCrit.Sts2.Core.Saves.Runs.SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_EffectRound { get; set; }

    /// <summary>累计受伤统计所属的回合号。</summary>
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty(
        MegaCrit.Sts2.Core.Saves.Runs.SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_DamageRound { get; set; }

    /// <summary>本回合累计受到的伤害。</summary>
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty(
        MegaCrit.Sts2.Core.Saves.Runs.SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_DamageTaken { get; set; }

    /// <summary>已经给过荣耀的回合号，避免同一回合重复给。</summary>
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty(
        MegaCrit.Sts2.Core.Saves.Runs.SerializationCondition.SaveIfNotTypeDefault)]
    public int DonQuixote_GloryRound { get; set; }

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 头顶（状态图标）直接用特写大图，小图标太糊看不清。
    public override string CustomIconPath => "res://DonQuixote/images/powers/duel_target_big.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/duel_target_big.png";

    public override async Task BeforeSideTurnEndVeryEarly(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        // 决斗对象挂在敌人身上，必须在「敌人的回合结束」才减层，
        // 否则玩家回合结束时状态就消失了，敌人根本没吃到 50% 增伤。
        if (side != CombatSide.Enemy || Owner == null || Owner.IsDead)
        {
            return;
        }

        Info("duel target turn end amount=" + Amount);

        if (Amount <= 1)
        {
            // 「持续了三天的决斗」：下两回合对同一目标继续施加决斗对象。
            // 这里不删除，改成消耗一层续期标记，层数保持 1。
            DonQuixotePowerDuelRenewal DonQuixote_renewal =
                DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerDuelRenewal>(Owner);
            if (DonQuixote_renewal != null && DonQuixote_renewal.Amount > 0)
            {
                await PowerCmd.Decrement(DonQuixote_renewal);
                Info("duel target renewed, remaining=" + DonQuixote_renewal.Amount);
                return;
            }

            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.Decrement(this);
    }

    private static void Info(string DonQuixote_message)
    {
        DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] " + DonQuixote_message, 0);
    }

    /// <summary>
    /// 决斗对象只和堂吉诃德互相造成 150% 伤害，与其他人之间造成与受到的伤害都为 0。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        if (Owner == null || target == null || dealer == null || Amount <= 0)
        {
            return 1m;
        }

        // 文案：对「施加者」造成与受到的最终伤害 +50%，对其他角色固定 0。
        // 施加者就是打出这张卡的生物；取不到时退回堂吉诃德（控制台施加的情况）。
        Creature DonQuixote_duelist = Applier ?? DonQuixoteBloodMagic.DonQuixote_PlayerCreature(Owner);
        if (DonQuixote_duelist == null)
        {
            return 1m;
        }

        if (ReferenceEquals(dealer, Owner))
        {
            return ReferenceEquals(target, DonQuixote_duelist) ? 1.5m : 0m;
        }

        if (ReferenceEquals(target, Owner))
        {
            return ReferenceEquals(dealer, DonQuixote_duelist) ? 1.5m : 0m;
        }

        return 1m;
    }

    /// <summary>
    /// 「本回合被施加效果」：任何以这只生物为目标的能力变化都算（自己身上叠层也算）。
    /// </summary>
    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature applier,
        CardModel cardSource)
    {
        if (Owner == null || power == null)
        {
            return Task.CompletedTask;
        }

        if (!ReferenceEquals(power.Owner, Owner))
        {
            return Task.CompletedTask;
        }

        // 只认「施加者是决斗者」的效果：流血层数自然衰减之类的变化不算。
        Creature DonQuixote_duelist = Applier ?? DonQuixoteBloodMagic.DonQuixote_PlayerCreature(Owner);
        if (DonQuixote_duelist == null)
        {
            return Task.CompletedTask;
        }

        if (applier != null && !ReferenceEquals(applier, DonQuixote_duelist))
        {
            return Task.CompletedTask;
        }

        DonQuixote_EffectRound = CombatState?.RoundNumber ?? 0;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 拥有该状态的对象，本回合被施加效果后若受到不低于 100 点伤害，
    /// 给予施加者 1 层决斗的荣耀（同一回合最多 1 层，荣耀层数上限 2）。
    /// </summary>
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (Owner == null || Owner.IsDead || target == null || result == null)
        {
            return;
        }

        if (!ReferenceEquals(target, Owner))
        {
            return;
        }

        int DonQuixote_round = CombatState?.RoundNumber ?? 0;
        if (DonQuixote_DamageRound != DonQuixote_round)
        {
            DonQuixote_DamageRound = DonQuixote_round;
            DonQuixote_DamageTaken = 0;
        }

        if (result.UnblockedDamage > 0)
        {
            DonQuixote_DamageTaken += result.UnblockedDamage;
        }

        if (DonQuixote_EffectRound != DonQuixote_round || DonQuixote_GloryRound == DonQuixote_round)
        {
            return;
        }

        if (DonQuixote_DamageTaken < DonQuixote_GloryDamageThreshold)
        {
            return;
        }

        Creature DonQuixote_duelist = Applier ?? DonQuixoteBloodMagic.DonQuixote_PlayerCreature(Owner);
        if (DonQuixote_duelist == null || DonQuixote_duelist.IsDead)
        {
            return;
        }

        // 决斗的荣耀层数上限 2，满了就不再给。
        DonQuixotePowerDuelGlory DonQuixote_glory =
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerDuelGlory>(DonQuixote_duelist);

        DonQuixote_GloryRound = DonQuixote_round;
        if (DonQuixote_glory != null && DonQuixote_glory.Amount >= 2)
        {
            return;
        }

        await PowerCmd.Apply<DonQuixotePowerDuelGlory>(
            choiceContext,
            DonQuixote_duelist,
            1m,
            DonQuixote_duelist,
            (CardModel)null,
            false);

        Info("duel glory +1 (damage " + DonQuixote_DamageTaken + ", round " + DonQuixote_round + ")");
    }
}

/// <summary>
/// 决斗的荣耀：回合结束减 1 层，获得额外 1 回合。
/// </summary>
public sealed class DonQuixotePowerDuelGlory : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 头顶（状态图标）直接用特写大图。
    public override string CustomIconPath => "res://DonQuixote/images/powers/duel_glory_big.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/duel_glory_big.png";

    public override bool ShouldTakeExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        return Owner != null && player != null && Owner.Player == player && Amount > 0;
    }

    /// <summary>
    /// 额外回合结束时才扣除层数，保证层数恰好兑换一个额外回合，
    /// 不会因为每回合都返回 true 而无限循环。
    /// 这与原版 AmbergrisPower（琥珀）的写法一致：只有持有者会拿到额外回合，
    /// 其他角色在 Hook.ShouldTakeExtraTurn 这一步就被跳过。
    /// </summary>
    public override async Task AfterTakingExtraTurn(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (Owner == null || player == null || Owner.Player != player)
        {
            return;
        }

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.Decrement(this);
    }
}

/// <summary>
/// 决斗续期：「持续了三天的决斗」用。挂在敌人身上，层数 = 还能续几回合；
/// 决斗对象本该在敌人回合结束时消失时，先扣这里的层数、把决斗对象保住。
/// </summary>
public sealed class DonQuixotePowerDuelRenewal : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

/// <summary>
/// 骑士的梦：每使用 1 张技能卡获得 1 点格挡。
/// </summary>
public sealed class DonQuixotePowerKnightDream : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => "res://DonQuixote/images/powers/knight_dream.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/knight_dream_big.png";

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null || cardPlay?.Card == null || cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }

        if (cardPlay.Card.Owner != Owner.Player)
        {
            return;
        }

        int DonQuixote_block = Amount;
        if (DonQuixote_block <= 0)
        {
            return;
        }

        await CreatureCmd.GainBlock(Owner, DonQuixote_block, ValueProp.Move, cardPlay, false);
    }
}

/// <summary>
/// 不负责任的梦：堕落牌有 50% 概率重放 1 次。
/// </summary>
public sealed class DonQuixotePowerIrresponsibleDream : ModPowerTemplate
{
    private static bool DonQuixote_Replaying;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override string CustomIconPath => "res://DonQuixote/images/powers/irresponsible_dream.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/irresponsible_dream_big.png";

    public override async Task AfterCardPlayed(PlayerChoiceContext DonQuixote_choiceContext, CardPlay DonQuixote_cardPlay)
    {
        if (DonQuixote_Replaying || DonQuixote_cardPlay?.Card == null)
        {
            return;
        }

        if (!(DonQuixote_cardPlay.Card is IDonQuixoteCorruptedCard))
        {
            return;
        }

        // 联机：只对自己打出的堕落牌生效。队友打出的堕落牌走的是队友自己的这份状态，
        // 不能由我这边的「不负责任的梦」替他重放（否则两边各重放一次，队友的卡凭空多打一遍）。
        if (DonQuixote_cardPlay.Player == null
            || Owner?.Player == null
            || !ReferenceEquals(DonQuixote_cardPlay.Player, Owner.Player))
        {
            return;
        }

        // 联机同步：绝对不能用 Random.Shared（各客户端各自随机，一端重放另一端不重放 → 直接不同步）。
        // 改用牌局共享的同步随机流，两端在同一位置取到同一个值。
        MegaCrit.Sts2.Core.Random.Rng DonQuixote_rng =
            DonQuixote_cardPlay.Player?.RunState?.Rng?.Niche;
        if (DonQuixote_rng == null)
        {
            return;
        }

        if (!DonQuixote_rng.NextBool())
        {
            return;
        }

        DonQuixote_Replaying = true;
        try
        {
            await CardCmd.AutoPlay(
                DonQuixote_choiceContext, DonQuixote_cardPlay.Card, DonQuixote_cardPlay.Target, AutoPlayType.Default, false, false);
        }
        catch (Exception DonQuixote_ex)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] irresponsible dream replay failed: " + DonQuixote_ex.Message, 0);
        }
        finally
        {
            DonQuixote_Replaying = false;
        }
    }
}

/// <summary>
/// 潜伏之血（展现洒落在战场上的潜伏之血）：血宴获取量 +Amount%（普通 50 = 1.5 倍，升级 100 = 2 倍）。
/// 加成统一在 <see cref="DonQuixoteBloodMagic.DonQuixote_GainBloodFeast"/> 里结算，
/// 所以流血触发、遗物、卡牌给的血宴都会被放大。
/// </summary>
public sealed class DonQuixotePowerLatentBlood : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override string CustomIconPath => "res://DonQuixote/images/powers/latent_blood.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/latent_blood_big.png";
}

/// <summary>
/// 好点子形态：每累计消耗 <see cref="DonQuixote_Threshold"/> 点血宴，获得 1 点能量。
/// 阈值存在能力自己身上（普通 20 / 升级 15），数量按累计消耗的整数倍结算。
/// </summary>
public sealed class DonQuixotePowerGoodIdeaForm : ModPowerTemplate
{
    /// <summary>每多少点血宴换 1 点能量。</summary>
    private int DonQuixote_threshold = 20;

    /// <summary>已经换算过能量的「消耗血宴总数」额度，避免同一段消耗重复回能。</summary>
    private int DonQuixote_converted;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    /// <summary>图标上显示「被打出的次数」（= 叠层），每多打出一张同样的血宴额度就多换 1 点能量。</summary>
    public override int DisplayAmount => Amount;

    /// <summary>
    /// 状态说明里的「每消耗 X 点血宴」用的是阈值，不是状态叠层，
    /// 原版只会注入 {Amount}（= 叠层，恒为 1），所以这里额外注入 {Threshold}。
    /// </summary>
    public override LocString Description
    {
        get
        {
            LocString DonQuixote_text = base.Description;
            DonQuixote_text.Add("Threshold", DonQuixote_Threshold);
            return DonQuixote_text;
        }
    }

    public override string CustomIconPath => "res://DonQuixote/images/powers/good_idea_form.png";

    public override string CustomBigIconPath => "res://DonQuixote/images/powers/good_idea_form_big.png";

    public int DonQuixote_Threshold => DonQuixote_threshold > 0 ? DonQuixote_threshold : 20;

    /// <summary>
    /// 打出能力时调用：登记阈值（多张取更低的），并把当前已消耗的血宴记为基准，
    /// 这样后打出的能力不会把之前消耗掉的血宴也换算成能量。
    /// </summary>
    public void DonQuixote_Initialize(int DonQuixote_newThreshold, int DonQuixote_feastTotal)
    {
        if (DonQuixote_newThreshold > 0
            && (DonQuixote_threshold <= 0 || DonQuixote_newThreshold < DonQuixote_threshold))
        {
            DonQuixote_threshold = DonQuixote_newThreshold;
        }

        int DonQuixote_baseline = DonQuixote_feastTotal / DonQuixote_Threshold;
        if (DonQuixote_baseline > DonQuixote_converted)
        {
            DonQuixote_converted = DonQuixote_baseline;
        }
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext DonQuixote_choiceContext,
        PowerModel DonQuixote_power,
        decimal DonQuixote_amount,
        Creature DonQuixote_applier,
        CardModel DonQuixote_cardSource)
    {
        if (DonQuixote_power is not DonQuixotePowerFeastTotal)
        {
            return;
        }

        Creature DonQuixote_owner = Owner;
        if (DonQuixote_owner == null || DonQuixote_owner.Player == null)
        {
            return;
        }

        int DonQuixote_total = DonQuixoteBloodMagic.DonQuixote_FeastTotal(DonQuixote_owner);
        int DonQuixote_should = DonQuixote_total / DonQuixote_Threshold;
        if (DonQuixote_should <= DonQuixote_converted)
        {
            return;
        }

        int DonQuixote_steps = DonQuixote_should - DonQuixote_converted;
        DonQuixote_converted = DonQuixote_should;

        // 每多打出一张「好点子形态」，同样的血宴额度就多换 1 点能量：
        // 1 张 = 每档 1 能量，2 张 = 每档 2 能量，以此类推。
        int DonQuixote_multiplier = Amount > 1 ? Amount : 1;
        int DonQuixote_gain = DonQuixote_steps * DonQuixote_multiplier;
        await PlayerCmd.GainEnergy(DonQuixote_gain, DonQuixote_owner.Player);
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] good idea form energy +" + DonQuixote_gain
            + " (copies " + DonQuixote_multiplier + ")"
            + " (feast total " + DonQuixote_total + ", threshold " + DonQuixote_Threshold + ")", 0);
    }
}
