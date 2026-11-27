using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DonQuixote.Powers;
using DonQuixote.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace DonQuixote;

/// <summary>
/// 控制台命令：dq_corruption &lt;value&gt; — 把当前角色的「堕落值」直接设为指定数字。
/// </summary>
public sealed class DonQuixoteCorruptionConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "dq_corruption";

    public override string Args => "<value:int>";

    public override string Description => "Set Don Quixote corruption (堕落值 0-100) to the given value.";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player issuingPlayer, string[] args)
    {
        if (args == null || args.Length == 0 || !int.TryParse(args[0], out int DonQuixote_value))
        {
            return new CmdResult(false, "usage: dq_corruption <value:int>");
        }

        if (DonQuixote_value < 0)
        {
            DonQuixote_value = 0;
        }

        if (DonQuixote_value > DonQuixoteBloodMagic.DonQuixote_CorruptionMax)
        {
            DonQuixote_value = DonQuixoteBloodMagic.DonQuixote_CorruptionMax;
        }

        Creature DonQuixote_creature = issuingPlayer?.Creature;
        if (DonQuixote_creature == null)
        {
            return new CmdResult(false, "no player creature in the current room");
        }

        DonQuixotePowerCorruption DonQuixote_power = DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerCorruption>(DonQuixote_creature);
        if (DonQuixote_power == null)
        {
            return new CmdResult(false,
                "corruption power is not on this creature yet - run 'power DON_QUIXOTE_POWER_CORRUPTION 1 0' first");
        }

        int DonQuixote_delta = DonQuixote_value - DonQuixote_power.Amount;
        if (DonQuixote_delta == 0)
        {
            return new CmdResult(true, $"corruption already {DonQuixote_value}");
        }

        DonQuixoteConsoleReflection.DonQuixote_ApplyPowerDelta(DonQuixote_power, DonQuixote_creature, DonQuixote_delta);
        return new CmdResult(true, $"corruption -> {DonQuixote_value}");
    }
}

/// <summary>
/// 控制台命令：dq_corruption_rate &lt;value&gt; — 设置「向本能堕落的骑士」 每回合施加的堕落值计数。
/// </summary>
public sealed class DonQuixoteCorruptionRateConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "dq_corruption_rate";

    public override string Args => "<value:int>";

    public override string Description => "Set how much corruption 向本能堕落的骑士 adds every turn.";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player issuingPlayer, string[] args)
    {
        if (args == null || args.Length == 0 || !int.TryParse(args[0], out int DonQuixote_value))
        {
            return new CmdResult(false, "usage: dq_corruption_rate <value:int>");
        }

        DonQuixoteKnightOfInstinct DonQuixote_relic = DonQuixoteConsoleReflection.DonQuixote_FindRelic<DonQuixoteKnightOfInstinct>(issuingPlayer);
        if (DonQuixote_relic == null)
        {
            return new CmdResult(false, "向本能堕落的骑士 is not owned by this player");
        }

        DonQuixote_relic.DonQuixote_CorruptionPerTurn = DonQuixote_value < 0 ? 0 : DonQuixote_value;
        return new CmdResult(true, $"corruption per turn -> {DonQuixote_relic.DonQuixote_CorruptionPerTurn}");
    }
}

/// <summary>
/// 控制台命令：dq_bleed &lt;strength&gt; &lt;layers&gt; [target-index] — 给敌人叠加流血强度与层数（不指定目标时默认第一个敌人）。
/// </summary>
public sealed class DonQuixoteBleedConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "dq_bleed";

    public override string Args => "<strength:int> <layers:int> [target-index:int]";

    public override string Description =>
        "Add Bleed strength/layers to an enemy (default: first enemy). Index follows the vanilla 'power' command order.";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player issuingPlayer, string[] args)
    {
        if (args == null || args.Length < 2)
        {
            return new CmdResult(false, "usage: dq_bleed <strength:int> <layers:int> [target-index:int]");
        }

        if (!int.TryParse(args[0], out int DonQuixote_strength))
        {
            return new CmdResult(false, "arg 1 must be the Bleed strength to add");
        }

        if (!int.TryParse(args[1], out int DonQuixote_layers))
        {
            return new CmdResult(false, "arg 2 must be the Bleed layers to add");
        }

        int DonQuixote_targetIndex = 0;
        bool DonQuixote_hasIndex = args.Length > 2;
        if (DonQuixote_hasIndex && !int.TryParse(args[2], out DonQuixote_targetIndex))
        {
            return new CmdResult(false, "arg 3 must be the target index");
        }

        Creature DonQuixote_player = issuingPlayer?.Creature;
        if (DonQuixote_player == null)
        {
            return new CmdResult(false, "no player creature in the current room");
        }

        List<Creature> DonQuixote_enemies =
            DonQuixoteBloodMagic.DonQuixote_EnemiesOf(DonQuixote_player)?.ToList();

        if (DonQuixote_enemies == null || DonQuixote_enemies.Count == 0)
        {
            return new CmdResult(false, "no enemies in the current combat");
        }

        if (!DonQuixote_hasIndex)
        {
            DonQuixote_targetIndex = 0;
        }

        if (DonQuixote_targetIndex < 0 || DonQuixote_targetIndex >= DonQuixote_enemies.Count)
        {
            return new CmdResult(false,
                $"invalid enemy index {DonQuixote_targetIndex}, valid range 0-{DonQuixote_enemies.Count - 1}");
        }

        Creature DonQuixote_target = DonQuixote_enemies[DonQuixote_targetIndex];

        Task DonQuixote_task = DonQuixote_ApplyAsync(
            DonQuixote_target, DonQuixote_player, DonQuixote_strength, DonQuixote_layers);

        return new CmdResult(
            DonQuixote_task,
            true,
            $"Bleed +{DonQuixote_strength} strength / +{DonQuixote_layers} layers -> enemy #{DonQuixote_targetIndex}");
    }

    private static async Task DonQuixote_ApplyAsync(
        Creature DonQuixote_target,
        Creature DonQuixote_applier,
        int DonQuixote_strength,
        int DonQuixote_layers)
    {
        PlayerChoiceContext DonQuixote_ctx = new BlockingPlayerChoiceContext();

        if (DonQuixote_strength != 0)
        {
            await PowerCmd.Apply<DonQuixotePowerBleed>(
                DonQuixote_ctx, DonQuixote_target, DonQuixote_strength, DonQuixote_applier, null, false);
        }

        if (DonQuixote_layers != 0)
        {
            await PowerCmd.Apply<DonQuixotePowerBleedLayers>(
                DonQuixote_ctx, DonQuixote_target, DonQuixote_layers, DonQuixote_applier, null, false);
        }
    }
}

/// <summary>
/// 控制台命令：dq_bloodfeast &lt;amount:int&gt; — 增减堂吉诃德自身的血宴层数（负数为减少）。
/// </summary>
public sealed class DonQuixoteBloodFeastConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "dq_bloodfeast";

    public override string Args => "<amount:int> (negative to reduce)";

    public override string Description => "Add or remove Blood Feast for Don Quixote.";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player issuingPlayer, string[] args)
    {
        if (args == null || args.Length < 1)
        {
            return new CmdResult(false, "usage: dq_bloodfeast <amount:int> (negative to reduce)");
        }

        if (!int.TryParse(args[0], out int DonQuixote_amount) || DonQuixote_amount == 0)
        {
            return new CmdResult(false, "arg 1 must be a non-zero int, negative to reduce");
        }

        Creature DonQuixote_player = issuingPlayer?.Creature;
        if (DonQuixote_player == null)
        {
            return new CmdResult(false, "no player creature in the current room");
        }

        int DonQuixote_before = DonQuixoteBloodMagic.DonQuixote_BloodFeast(DonQuixote_player);
        int DonQuixote_after = DonQuixote_before + DonQuixote_amount;
        if (DonQuixote_after < 0)
        {
            DonQuixote_after = 0;
        }

        Task DonQuixote_task = DonQuixote_ApplyAsync(DonQuixote_player, DonQuixote_amount);
        return new CmdResult(
            DonQuixote_task,
            true,
            $"Blood Feast {DonQuixote_before} -> {DonQuixote_after} ({DonQuixote_amount:+0;-0})");
    }

    private static async Task DonQuixote_ApplyAsync(Creature DonQuixote_player, int DonQuixote_amount)
    {
        PlayerChoiceContext DonQuixote_ctx = new BlockingPlayerChoiceContext();
        DonQuixotePowerBloodFeast DonQuixote_feast =
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerBloodFeast>(DonQuixote_player);

        if (DonQuixote_feast == null)
        {
            if (DonQuixote_amount <= 0)
            {
                return;
            }

            await PowerCmd.Apply<DonQuixotePowerBloodFeast>(
                DonQuixote_ctx, DonQuixote_player, DonQuixote_amount, DonQuixote_player, null, false);
            return;
        }

        await PowerCmd.ModifyAmount(
            DonQuixote_ctx, DonQuixote_feast, DonQuixote_amount, DonQuixote_player, null, false);
    }
}

/// <summary>
/// 控制台命令：dq_feast_total &lt;value:int&gt; — 把「消耗血宴总数」设为指定值，并立刻结算一次血宴奖励（每 325 点一张舞台卡）。
/// </summary>
public sealed class DonQuixoteFeastTotalConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "dq_feast_total";

    public override string Args => "<value:int>";

    public override string Description =>
        "Set 消耗血宴总数 and immediately run the 325-point stage card reward check.";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player issuingPlayer, string[] args)
    {
        if (args == null || args.Length < 1 || !int.TryParse(args[0], out int DonQuixote_value) || DonQuixote_value < 0)
        {
            return new CmdResult(false, "usage: dq_feast_total <value:int>");
        }

        Creature DonQuixote_creature = issuingPlayer?.Creature;
        if (DonQuixote_creature == null)
        {
            return new CmdResult(false, "no player creature in the current room");
        }

        DonQuixotePowerFeastTotal DonQuixote_power =
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerFeastTotal>(DonQuixote_creature);
        if (DonQuixote_power == null)
        {
            return new CmdResult(false, "feast total power is not on this creature yet - consume some 血宴 first");
        }

        int DonQuixote_before = DonQuixote_power.Amount;
        int DonQuixote_delta = DonQuixote_value - DonQuixote_before;
        Task DonQuixote_task = DonQuixote_ApplyAsync(DonQuixote_creature, DonQuixote_power, DonQuixote_delta);
        return new CmdResult(
            DonQuixote_task,
            true,
            $"消耗血宴总数 {DonQuixote_before} -> {DonQuixote_value}, reward check queued");
    }

    private static async Task DonQuixote_ApplyAsync(
        Creature DonQuixote_creature,
        DonQuixotePowerFeastTotal DonQuixote_power,
        int DonQuixote_delta)
    {
        PlayerChoiceContext DonQuixote_ctx = new BlockingPlayerChoiceContext();
        if (DonQuixote_delta != 0)
        {
            await PowerCmd.ModifyAmount(
                DonQuixote_ctx, DonQuixote_power, DonQuixote_delta, DonQuixote_creature, null, false);
        }

        await DonQuixoteFeastReward.DonQuixote_Check(DonQuixote_creature);
    }
}

/// <summary>
/// 控制台命令：dq_degenerate &lt;increase|decrease&gt; &lt;amount&gt; — 增减「向本能堕落的骑士」的堕落计数（遗物下方显示的数字）。
/// </summary>
public sealed class DonQuixoteDegenerateConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "dq_degenerate";

    public override string Args => "<increase|decrease> <amount:int>";

    public override string Description =>
        "Increase or decrease the 堕落 counter of 向本能堕落的骑士 (shown under the relic).";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player issuingPlayer, string[] args)
    {
        if (args == null || args.Length < 2)
        {
            return new CmdResult(false, "usage: dq_degenerate <increase|decrease> <amount:int>");
        }

        if (!int.TryParse(args[1], out int DonQuixote_amount) || DonQuixote_amount < 0)
        {
            return new CmdResult(false, "amount must be a non-negative integer");
        }

        string DonQuixote_direction = (args[0] ?? string.Empty).Trim().ToLowerInvariant();
        int DonQuixote_sign;
        if (DonQuixote_direction == "increase" || DonQuixote_direction == "+")
        {
            DonQuixote_sign = 1;
        }
        else if (DonQuixote_direction == "decrease" || DonQuixote_direction == "-")
        {
            DonQuixote_sign = -1;
        }
        else
        {
            return new CmdResult(false, "direction must be 'increase' or 'decrease'");
        }

        DonQuixoteKnightOfInstinct DonQuixote_relic =
            DonQuixoteConsoleReflection.DonQuixote_FindRelic<DonQuixoteKnightOfInstinct>(issuingPlayer);
        if (DonQuixote_relic == null)
        {
            return new CmdResult(false, "向本能堕落的骑士 is not owned by this player");
        }

        int DonQuixote_next = DonQuixote_relic.DonQuixote_DegenerateCounter + DonQuixote_sign * DonQuixote_amount;
        if (DonQuixote_next < 0)
        {
            DonQuixote_next = 0;
        }

        if (DonQuixote_next > DonQuixoteBloodMagic.DonQuixote_CorruptionMax)
        {
            DonQuixote_next = DonQuixoteBloodMagic.DonQuixote_CorruptionMax;
        }

        DonQuixote_relic.DonQuixote_DegenerateCounter = DonQuixote_next;
        DonQuixote_relic.DonQuixote_RefreshCounter();

        int DonQuixote_delta = DonQuixote_sign * DonQuixote_amount;
        Task DonQuixote_sync = DonQuixote_SyncCombatCorruption(issuingPlayer, DonQuixote_delta);
        return new CmdResult(DonQuixote_sync, true, $"degenerate counter -> {DonQuixote_next}");
    }

    /// <summary>
    /// 把计数变化同步到战斗中的堕落值，方便在战斗里直接观察堕落转化。
    /// </summary>
    public static async Task DonQuixote_SyncCombatCorruption(Player DonQuixote_player, int DonQuixote_delta)
    {
        if (DonQuixote_player?.Creature == null || DonQuixote_delta == 0)
        {
            return;
        }

        BlockingPlayerChoiceContext DonQuixote_context = new BlockingPlayerChoiceContext();
        DonQuixotePowerCorruption DonQuixote_power =
            DonQuixoteBloodMagic.DonQuixote_Find<DonQuixotePowerCorruption>(DonQuixote_player.Creature);

        if (DonQuixote_power == null)
        {
            if (DonQuixote_delta <= 0)
            {
                return;
            }

            await PowerCmd.Apply<DonQuixotePowerCorruption>(
                DonQuixote_context, DonQuixote_player.Creature, DonQuixote_delta, DonQuixote_player.Creature, null, true);
        }
        else
        {
            await PowerCmd.ModifyAmount(
                DonQuixote_context, DonQuixote_power, DonQuixote_delta, DonQuixote_player.Creature, null, true);
        }

        await DonQuixoteCorruption.DonQuixote_TryCorruptHandOfPlayer(DonQuixote_context, DonQuixote_player);
    }
}
internal static class DonQuixoteConsoleReflection
{
    private static MethodInfo DonQuixote_ApplyInternalMethod;

    public static void DonQuixote_ApplyPowerDelta(PowerModel DonQuixote_power, Creature DonQuixote_owner, int DonQuixote_delta)
    {
        DonQuixote_ApplyInternalMethod ??= typeof(PowerModel).GetMethod(
            "ApplyInternal",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        DonQuixote_ApplyInternalMethod?.Invoke(DonQuixote_power, new object[]
        {
            DonQuixote_owner,
            (decimal)DonQuixote_delta,
            true,
        });
    }

    public static T DonQuixote_FindRelic<T>(Player DonQuixote_player) where T : RelicModel
    {
        if (DonQuixote_player == null)
        {
            return null;
        }

        PropertyInfo DonQuixote_relicsProperty = typeof(Player).GetProperty("Relics");
        if (DonQuixote_relicsProperty?.GetValue(DonQuixote_player) is IEnumerable<RelicModel> DonQuixote_relics)
        {
            return DonQuixote_relics.OfType<T>().FirstOrDefault();
        }

        return null;
    }
}

/// <summary>
/// 把自定义控制台命令注册进游戏的命令表（命令表来自源生成器，只能靠补丁追加）。
/// </summary>
[HarmonyPatch(typeof(AbstractConsoleCmdSubtypes), nameof(AbstractConsoleCmdSubtypes.All), MethodType.Getter)]
internal static class DonQuixoteConsoleCommandPatch
{
    private static bool DonQuixote_Injected;

    [HarmonyPostfix]
    private static void DonQuixote_Postfix(ref IReadOnlyList<Type> __result)
    {
        if (DonQuixote_Injected || __result == null)
        {
            return;
        }

        List<Type> DonQuixote_types = new(__result)
        {
            typeof(DonQuixoteCorruptionConsoleCmd),
            typeof(DonQuixoteCorruptionRateConsoleCmd),
            typeof(DonQuixoteBleedConsoleCmd),
            typeof(DonQuixoteDegenerateConsoleCmd),
            typeof(DonQuixoteBloodFeastConsoleCmd),
            typeof(DonQuixoteEnchantConsoleCmd),
        };

        __result = DonQuixote_types;
        DonQuixote_Injected = true;
        DonQuixoteModEntry.DonQuixote_Logger?.Info("[DonQuixote] console commands registered (dq_corruption, dq_corruption_rate, dq_bleed, dq_degenerate, dq_bloodfeast, dq_enchant)");
    }
}

/// <summary>
/// 控制台命令：dq_enchant [index]。
/// </summary>
public sealed class DonQuixoteEnchantConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "dq_enchant";

    public override string Args => "[hand-index:int]";

    public override string Description => "Spend 30 Blood Feast to enchant a card in hand with Indestructible.";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player issuingPlayer, string[] args)
    {
        if (issuingPlayer?.Creature == null)
        {
            return new CmdResult(false, "no player in the current room");
        }

        int DonQuixote_index = 0;
        if (args != null && args.Length > 0 && !int.TryParse(args[0], out DonQuixote_index))
        {
            return new CmdResult(false, "arg 1 must be an int hand index");
        }

        List<CardModel> DonQuixote_hand =
            MegaCrit.Sts2.Core.Entities.Cards.CardPile.Get(
                MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand, issuingPlayer).Cards.ToList();

        if (DonQuixote_hand.Count == 0)
        {
            return new CmdResult(false, "hand is empty");
        }

        if (DonQuixote_index < 0 || DonQuixote_index >= DonQuixote_hand.Count)
        {
            return new CmdResult(false, $"hand index out of range (0..{DonQuixote_hand.Count - 1})");
        }

        int DonQuixote_have = DonQuixoteBloodMagic.DonQuixote_BloodFeast(issuingPlayer.Creature);
        if (DonQuixote_have < 30)
        {
            return new CmdResult(false, $"not enough Blood Feast ({DonQuixote_have}/30)");
        }

        CardModel DonQuixote_card = DonQuixote_hand[DonQuixote_index];
        Task DonQuixote_task = DonQuixote_EnchantAsync(issuingPlayer, DonQuixote_card);
        return new CmdResult(
            DonQuixote_task,
            true,
            $"spent 30 Blood Feast, enchanting {DonQuixote_card.Id.Entry} with Indestructible");
    }

    private static async Task DonQuixote_EnchantAsync(Player DonQuixote_player, CardModel DonQuixote_card)
    {
        PlayerChoiceContext DonQuixote_ctx = new BlockingPlayerChoiceContext();
        await DonQuixoteBloodMagic.DonQuixote_ConsumeBloodFeast(
            DonQuixote_ctx, DonQuixote_player.Creature, 30, DonQuixote_card);
        MegaCrit.Sts2.Core.Commands.CardCmd.Enchant<Enchantments.DonQuixoteIndestructibleCard>(
            DonQuixote_card, 1m);
        // 走 Accumulate：遗物的「满 325 发卡」检查挂在那里。
        await DonQuixoteBloodMagic.DonQuixote_AccumulateFeastTotal(
            DonQuixote_ctx, DonQuixote_player.Creature, 30, DonQuixote_card);
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] indestructible enchant applied to " + DonQuixote_card.Id.Entry, 0);
    }
}
