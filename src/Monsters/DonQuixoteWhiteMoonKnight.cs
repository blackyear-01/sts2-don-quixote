using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace DonQuixote.Monsters;

/// <summary>
/// 白月骑士-巴里（Boss）。
/// </summary>
public sealed class DonQuixoteWhiteMoonKnight : MonsterModel
{
    /// <summary>
    /// 意图1 的格挡。
    /// </summary>
    private const int DonQuixote_GuardBlock = 70;

    /// <summary>
    /// 意图2 回血。
    /// </summary>
    private const int DonQuixote_FeastHeal = 20;

    /// <summary>
    /// 意图3 的格挡。
    /// </summary>
    private const int DonQuixote_WallBlock = 50;

    /// <summary>
    /// 意图4 回血。
    /// </summary>
    private const int DonQuixote_DuelHeal = 30;

    /// <summary>
    /// 战斗开始时的人工制品层数。
    /// </summary>
    private const int DonQuixote_ArtifactStacks = 10;

    /// <summary>
    /// 「白月骑士-决斗对象」施加的层数。
    /// </summary>
    private const int DonQuixote_DuelLayers = 2;

    private static int DonQuixote_Ascended(int DonQuixote_eightPlus, int DonQuixote_normal)
    {
        return AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies, DonQuixote_eightPlus, DonQuixote_normal);
    }

    private static int DonQuixote_Hp(int DonQuixote_eightPlus, int DonQuixote_normal)
    {
        return DonQuixote_Ascended(DonQuixote_eightPlus, DonQuixote_normal);
    }

    private static int DonQuixote_WealAmount => DonQuixote_Ascended(3, 1);

    private static int DonQuixote_VulnerableAmount => DonQuixote_Ascended(3, 1);

    private static int DonQuixote_StrengthGain => DonQuixote_Ascended(5, 3);

    private static int DonQuixote_DexterityGain => DonQuixote_Ascended(8, 5);

    public override int MinInitialHp => DonQuixote_Hp(5000, 3250);

    public override int MaxInitialHp => DonQuixote_Hp(5000, 3250);

    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;

    public override float DeathAnimLengthOverride => 0.8f;

    /// <summary>
    /// 逐帧贴图做成的视觉场景（Sprite2D，没有 spine）。
    /// </summary>
    protected override string VisualsPath =>
        "res://DonQuixote/scenes/creature_visuals/white_moon_knight_spine.tscn";

    /// <summary>
    /// 骨架版视觉：所有动作动画都由 <see cref="DonQuixoteWhiteMoonPose"/> 在 track 1 上播。
    /// </summary>
    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        AnimState DonQuixote_idle = new AnimState("idle_loop", isLooping: true);
        AnimState DonQuixote_die = new AnimState("die");

        CreatureAnimator DonQuixote_animator = new CreatureAnimator(DonQuixote_idle, controller);
        DonQuixote_animator.AddAnyState("Idle", DonQuixote_idle);
        DonQuixote_animator.AddAnyState("Dead", DonQuixote_die);
        return DonQuixote_animator;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        // 专属战斗背景（资源\人物\白月骑士战斗场景.png），盖在原版背景之上、角色之下。
        DonQuixoteWhiteMoonBackdrop.DonQuixote_Show();

        PlayerChoiceContext DonQuixote_ctx = new ThrowingPlayerChoiceContext();

        // 文档：战斗开始时获得 10 层人工制品和 1 层白月骑士。
        await PowerCmd.Apply<ArtifactPower>(
            DonQuixote_ctx, Creature, DonQuixote_ArtifactStacks, Creature, null, false);
        await PowerCmd.Apply<Powers.DonQuixotePowerWhiteMoonKnight>(
            DonQuixote_ctx, Creature, 1m, Creature, null, false);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        List<MonsterState> DonQuixote_states = new List<MonsterState>();

        MoveState DonQuixote_lance = new MoveState(
            DonQuixote_MoveLance,
            DonQuixote_LanceMove,
            new DefendIntent(),
            new DebuffIntent(true),
            new MultiAttackIntent(DonQuixote_Ascended(13, 10), DonQuixote_Ascended(4, 3)));

        MoveState DonQuixote_curse = new MoveState(
            DonQuixote_MoveCurse,
            DonQuixote_CurseMove,
            new DebuffIntent(true),
            new HealIntent());

        MoveState DonQuixote_oath = new MoveState(
            DonQuixote_MoveOath,
            DonQuixote_OathMove,
            new BuffIntent(),
            new DefendIntent(),
            new SingleAttackIntent(DonQuixote_Ascended(60, 40)));

        MoveState DonQuixote_execute = new MoveState(
            DonQuixote_MoveExecute,
            DonQuixote_ExecuteMove,
            new DebuffIntent(),
            new SingleAttackIntent(DonQuixote_Ascended(28, 21)),
            new HealIntent());

        // 文档：在 1-4 个意图间循环 → 固定 1→2→3→4→1。
        DonQuixote_lance.FollowUpState = DonQuixote_curse;
        DonQuixote_curse.FollowUpState = DonQuixote_oath;
        DonQuixote_oath.FollowUpState = DonQuixote_execute;
        DonQuixote_execute.FollowUpState = DonQuixote_lance;

        DonQuixote_states.Add(DonQuixote_lance);
        DonQuixote_states.Add(DonQuixote_curse);
        DonQuixote_states.Add(DonQuixote_oath);
        DonQuixote_states.Add(DonQuixote_execute);

        return new MonsterMoveStateMachine(DonQuixote_states, DonQuixote_lance);
    }

    private const string DonQuixote_MoveLance = "WHITE_MOON_LANCE";

    private const string DonQuixote_MoveCurse = "WHITE_MOON_CURSE";

    private const string DonQuixote_MoveOath = "WHITE_MOON_OATH";

    private const string DonQuixote_MoveExecute = "WHITE_MOON_EXECUTE";

    private async Task DonQuixote_LanceMove(IReadOnlyList<Creature> DonQuixote_targets)
    {
        PlayerChoiceContext DonQuixote_ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainBlock(Creature, DonQuixote_GuardBlock, ValueProp.Move, null);

        await DonQuixote_MarkRandomTargets(DonQuixote_ctx, DonQuixote_targets);

        // 意图 1 的对应动作：4 帧挥砍（与伤害并行播，不额外拖时间）。
        DonQuixoteWhiteMoonPose.DonQuixote_PlayAttack(Creature);

        int DonQuixote_hits = DonQuixote_Ascended(4, 3);
        await DamageCmd.Attack(DonQuixote_Ascended(13, 10))
            .FromMonster(this)
            .WithHitCount(DonQuixote_hits)
            .OnlyPlayAnimOnce()
            .Execute(null);
    }

    private async Task DonQuixote_CurseMove(IReadOnlyList<Creature> DonQuixote_targets)
    {
        PlayerChoiceContext DonQuixote_ctx = new ThrowingPlayerChoiceContext();

        IEnumerable<Creature> DonQuixote_victims = DonQuixote_targets.Count > 0
            ? DonQuixote_targets
            : CombatState.GetOpponentsOf(Creature);

        await PowerCmd.Apply<WeakPower>(
            DonQuixote_ctx, DonQuixote_victims, DonQuixote_WealAmount, Creature, null, false);
        await PowerCmd.Apply<VulnerablePower>(
            DonQuixote_ctx, DonQuixote_victims, DonQuixote_VulnerableAmount, Creature, null, false);

        await CreatureCmd.Heal(Creature, DonQuixote_FeastHeal, true);
    }

    private async Task DonQuixote_OathMove(IReadOnlyList<Creature> DonQuixote_targets)
    {
        PlayerChoiceContext DonQuixote_ctx = new ThrowingPlayerChoiceContext();

        await PowerCmd.Apply<StrengthPower>(
            DonQuixote_ctx, Creature, DonQuixote_StrengthGain, Creature, null, false);
        await PowerCmd.Apply<DexterityPower>(
            DonQuixote_ctx, Creature, DonQuixote_DexterityGain, Creature, null, false);
        await CreatureCmd.GainBlock(Creature, DonQuixote_WallBlock, ValueProp.Move, null);

        // 意图 3：拉弓姿态在「意图出现」时就已经摆好了（见 DonQuixoteWhiteMoonIntentPatch）。
        if (!DonQuixoteWhiteMoonPose.DonQuixote_IsAiming(Creature))
        {
            DonQuixoteWhiteMoonPose.DonQuixote_SnapBowAim(Creature);
        }

        // 意图 3 也是「先点名再射」：场上挂着「白月骑士-决斗对象」时。
        await DonQuixote_MarkWeakest(DonQuixote_ctx, DonQuixote_targets);

        // 结算伤害时仍保持瞄准，打完再播松弦与收势。
        await DamageCmd.Attack(DonQuixote_Ascended(60, 40))
            .FromMonster(this)
            .Execute(null);

        await DonQuixoteWhiteMoonPose.DonQuixote_PlayBowRelease(Creature);
    }

    private async Task DonQuixote_ExecuteMove(IReadOnlyList<Creature> DonQuixote_targets)
    {
        PlayerChoiceContext DonQuixote_ctx = new ThrowingPlayerChoiceContext();

        // 血量最低的一名单位；必须在攻击之前施加。
        await DonQuixote_MarkWeakest(DonQuixote_ctx, DonQuixote_targets);

        // 意图 4 的对应动作：同样走 4 帧挥砍。
        DonQuixoteWhiteMoonPose.DonQuixote_PlayAttack(Creature);

        await DamageCmd.Attack(DonQuixote_Ascended(28, 21))
            .FromMonster(this)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);

        await CreatureCmd.Heal(Creature, DonQuixote_DuelHeal, true);
    }

    /// <summary>
    /// 场上所有「敌对方」里活着的那些。
    /// </summary>
    private List<Creature> DonQuixote_Opponents(IReadOnlyList<Creature> DonQuixote_targets)
    {
        List<Creature> DonQuixote_pool = CombatState.GetOpponentsOf(Creature)
            .Where(DonQuixote_c => DonQuixote_c != null && DonQuixote_c.IsAlive)
            .ToList();

        if (DonQuixote_pool.Count == 0)
        {
            DonQuixote_pool = DonQuixote_targets
                .Where(DonQuixote_c => DonQuixote_c != null && DonQuixote_c.IsAlive)
                .ToList();
        }

        return DonQuixote_pool;
    }

    /// <summary>
    /// 对生命值最低的一名单位施加「白月骑士-决斗对象」。
    /// </summary>
    private async Task DonQuixote_MarkWeakest(
        PlayerChoiceContext DonQuixote_ctx,
        IReadOnlyList<Creature> DonQuixote_targets)
    {
        // 取「场上所有敌对方」，不是这一击的目标列表。
        List<Creature> DonQuixote_pool = DonQuixote_Opponents(DonQuixote_targets);

        Creature? DonQuixote_weakest = null;
        decimal DonQuixote_lowest = decimal.MaxValue;
        foreach (Creature DonQuixote_candidate in DonQuixote_pool)
        {
            if (DonQuixote_candidate.CurrentHp >= DonQuixote_lowest)
            {
                continue;
            }

            DonQuixote_lowest = DonQuixote_candidate.CurrentHp;
            DonQuixote_weakest = DonQuixote_candidate;
        }

        if (DonQuixote_weakest == null)
        {
            return;
        }

        await PowerCmd.Apply<Powers.DonQuixotePowerWhiteMoonDuel>(
            DonQuixote_ctx, DonQuixote_weakest, DonQuixote_DuelLayers, Creature, null, false);
    }

    /// <summary>
    /// 对随机 1-2 名目标施加「白月骑士-决斗对象」（单人时就是唯一那名玩家）。
    /// </summary>
    private async Task DonQuixote_MarkRandomTargets(
        PlayerChoiceContext DonQuixote_ctx,
        IReadOnlyList<Creature> DonQuixote_targets)
    {
        List<Creature> DonQuixote_pool = DonQuixote_Opponents(DonQuixote_targets);

        if (DonQuixote_pool.Count == 0)
        {
            return;
        }

        int DonQuixote_count = DonQuixote_pool.Count >= 2 && Rng.NextBool()
            ? 2
            : 1;

        for (int DonQuixote_index = 0; DonQuixote_index < DonQuixote_count && DonQuixote_pool.Count > 0; DonQuixote_index++)
        {
            Creature DonQuixote_pick = Rng.NextItem(DonQuixote_pool);
            if (DonQuixote_pick == null)
            {
                break;
            }

            DonQuixote_pool.Remove(DonQuixote_pick);
            await PowerCmd.Apply<Powers.DonQuixotePowerWhiteMoonDuel>(
                DonQuixote_ctx, DonQuixote_pick, DonQuixote_DuelLayers, Creature, null, false);
        }
    }

}
