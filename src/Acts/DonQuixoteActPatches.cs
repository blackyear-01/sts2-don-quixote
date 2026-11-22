using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace DonQuixote.Acts;

/// <summary>
/// 把「折射轨道线」接在本局章节列表的最后。
///
/// **不能再往 <c>ModelDb.Acts</c> 里塞了。** 原版那张表是全局硬编码的四层，
/// <c>ModelDb.ActsByIndex</c> 与 <c>ActModel.GetRandomList</c> 都从它派生；
/// 一旦把我们的层塞进去，**每一局**的 <c>RunState.Acts</c> 都会是 4 幕，
/// 其它「第四幕」mod 就会认为第四幕被人占了 —— 例如 goety（ApostleMod）的
/// <c>RevelationFlow.Enter</c> 直接抛
/// <c>InvalidOperationException: Cannot replace another mod's fourth act.</c>，
/// 第三层 Boss 打完就卡死在入口事件里，进不去第四层。
///
/// 所以分两步：
/// ・抽签阶段（本补丁）：只有结果是**干净的原版三层、第三层是荣耀**时，
///   我们才直接占第四位（没有别的 mod 抢）。
/// ・运行时（<see cref="DonQuixoteRailDeferPatch"/>）：如果第四位被别的 mod 占了，
///   等它把自己的第四幕 append 好、真正进入那一层时，我们**顺延到末尾**（第五层），
///   于是「原版三层 → 别人的第四层 → 我们的层」都能玩到。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetRandomList))]
internal static class DonQuixoteActListPatch
{
    private static bool DonQuixote_Warned;

    private static bool DonQuixote_Logged;

    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<ActModel> __result)
    {
        try
        {
            if (__result is not List<ActModel> DonQuixote_List)
            {
                return;
            }

            ActModel DonQuixote_Act = ModelDb.Act<DonQuixoteRefractionRail>();
            if (DonQuixote_Act == null)
            {
                return;
            }

            foreach (ActModel DonQuixote_Existing in DonQuixote_List)
            {
                if (DonQuixote_Existing != null
                    && (ReferenceEquals(DonQuixote_Existing, DonQuixote_Act)
                        || DonQuixote_Existing.Id == DonQuixote_Act.Id))
                {
                    return;
                }
            }

            // 已经不是原版三层（别的 mod 已经补了自己的第四幕）→ 先不插，
            // 交给运行时的顺延逻辑，等它把第四层占好我们再去末尾。
            if (DonQuixote_List.Count != 3)
            {
                return;
            }

            // 第三层不是原版的荣耀：这一层的归属权在别的 mod 手里，
            // 第四层也归它（goety 会把「启示录」接在这个位置），
            // 我们同样等运行时顺延。
            ActModel DonQuixote_Third = DonQuixote_List[DonQuixote_List.Count - 1];
            if (DonQuixote_Third is not Glory)
            {
                if (!DonQuixote_Logged)
                {
                    DonQuixote_Logged = true;
                    DonQuixoteModEntry.DonQuixote_Logger?.Info(
                        "[DonQuixote] act3 is " + (DonQuixote_Third?.GetType().Name ?? "null")
                        + " (not Glory) -> refraction rail will be deferred to the tail at runtime", 0);
                }

                return;
            }

            DonQuixote_List.Add(DonQuixote_Act);

            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] refraction rail appended as fourth act (acts="
                    + DonQuixote_List.Count + ")", 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            if (DonQuixote_Warned)
            {
                return;
            }

            DonQuixote_Warned = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] act list patch failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}

/// <summary>
/// 顺延：第四层已经被别的 mod 占了的话，把我们的层接在它**后面**。
///
/// 挂在 <see cref="RunManager.SetActInternal"/> 的 Prefix —— 这是
/// <c>EnterAct</c> 里「真正切到某一层」的第一步（设 <c>CurrentActIndex</c>、
/// 生成地图之前）。判断条件：
/// ・要进的层 <c>actIndex &gt;= 3</c>（第三层及以前一概不管）；
/// ・它刚好是当前列表的最后一层（说明列表里已经排好了别人的第四幕）；
/// ・它不是我们的层，而且整张表里都没有我们的层。
///
/// 满足就在末尾追加一层 mutable 的折射轨道线，并补一次
/// <c>GenerateRooms</c>（开局那次 <c>GenerateRooms</c> 只覆盖了当时的层，
/// 事后追加的层不补会没有 Boss / 先古房间）。
///
/// 时机很关键：goety 的 <c>RevelationFlow.Enter</c> 会先
/// <c>Acts.Append(RevelationAct)</c> 再 <c>EnterAct(3)</c>，并且严格校验
/// <c>Acts.Count == 4 &amp;&amp; Acts[3] is RevelationAct</c>。
/// 我们在 <c>EnterAct(3)</c> 内部才动手，它已经校验完了，不会再被冒犯；
/// 等它第四层打完 <c>EnterNextAct</c>，<c>CurrentActIndex=3 &lt; Count-1=4</c>，
/// 原版自己就会 <c>EnterAct(4)</c> 走到我们这层。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetActInternal))]
internal static class DonQuixoteRailDeferPatch
{
    private static bool DonQuixote_Warned;

    private static bool DonQuixote_Logged;

    /// <summary>
    /// 参数走 <c>__args</c> 而不是按名字绑 <c>int actIndex</c>：
    /// <c>SetActInternal</c> 是 async 方法，Harmony 打在编译器生成的状态机启动方法上，
    /// 参数名不一定保留；按位置取更稳（拿不到就当成 -1 直接跳过，不会误伤）。
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(RunManager __instance, object[] __args)
    {
        try
        {
            int actIndex = __args != null && __args.Length > 0 && __args[0] is int DonQuixote_Index
                ? DonQuixote_Index
                : -1;

            if (actIndex < 3)
            {
                return;
            }

            RunState? DonQuixote_State = __instance?.DebugOnlyGetState();
            if (DonQuixote_State == null)
            {
                return;
            }

            IReadOnlyList<ActModel> DonQuixote_Acts = DonQuixote_State.Acts;
            if (DonQuixote_Acts == null || DonQuixote_Acts.Count == 0)
            {
                return;
            }

            // 只处理「要进的就是当前最后一层」这种情况。
            if (actIndex != DonQuixote_Acts.Count - 1)
            {
                return;
            }

            if (DonQuixote_Acts[actIndex] is DonQuixoteRefractionRail)
            {
                return;
            }

            foreach (ActModel DonQuixote_Existing in DonQuixote_Acts)
            {
                if (DonQuixote_Existing is DonQuixoteRefractionRail)
                {
                    return;
                }
            }

            DonQuixoteRefractionRail? DonQuixote_Rail = ModelDb.Act<DonQuixoteRefractionRail>();
            if (DonQuixote_Rail == null)
            {
                return;
            }

            ActModel DonQuixote_Mutable = DonQuixote_Rail.ToMutable();

            List<ActModel> DonQuixote_Next = DonQuixote_Acts.ToList();
            DonQuixote_Next.Add(DonQuixote_Mutable);

            AccessTools.PropertySetter(typeof(RunState), "Acts")
                .Invoke(DonQuixote_State, new object[] { DonQuixote_Next });

            DonQuixote_Mutable.GenerateRooms(DonQuixote_State);

            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] refraction rail deferred to slot " + (DonQuixote_Next.Count - 1)
                    + " (act " + actIndex + " belongs to another mod: "
                    + DonQuixote_Acts[actIndex].GetType().Name + ", acts="
                    + DonQuixote_Next.Count + ")", 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            if (DonQuixote_Warned)
            {
                return;
            }

            DonQuixote_Warned = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] act defer patch failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}

/// <summary>
/// 让「折射轨道线」用自己的线性地图。
///
/// <c>ActModel.CreateMap</c> 不是 virtual，只能在这里拦截：
/// 只有我们的层换成 <see cref="DonQuixoteRefractionRailMap"/>，其他层照原样返回。
/// （原版标准地图会强制在第一行放怪物节点，而这一层不刷普通怪。）
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.CreateMap))]
internal static class DonQuixoteRailMapPatch
{
    /// <summary>
    /// Harmony 只认两类补丁方法：方法名**恰好**是 Prefix/Postfix，或者带
    /// [HarmonyPrefix]/[HarmonyPostfix] 特性。这里以前只把方法叫
    /// <c>DonQuixote_Prefix</c>、没加特性，结果这个方法被 Harmony 直接跳过，
    /// 我们的层一直走标准地图 → 第三层打完进第四层时
    /// <c>StandardActMap.AssignPointTypes</c> 里 <c>GetRowCount() - 7</c> 变负数，
    /// 抛 IndexOutOfRangeException，画面停在 FadeOut 的黑屏上。
    /// 特性必须留着。
    /// </summary>
    [HarmonyPrefix]
    private static bool DonQuixote_Prefix(ActModel __instance, RunState runState, ref ActMap __result)
    {
        if (__instance == null)
        {
            return true;
        }

        // 按类型判断，不依赖 Id 字符串（mutable 副本的 Id 也一致，但类型更保险）。
        if (__instance is not DonQuixoteRefractionRail)
        {
            return true;
        }

        __result = new DonQuixoteRefractionRailMap(runState);
        DonQuixoteModEntry.DonQuixote_Logger?.Info(
            "[DonQuixote] refraction rail map created: rows=" + __result.GetRowCount()
            + " cols=" + __result.GetColumnCount(), 0);
        return false;
    }
}

/// <summary>
/// 让「折射轨道线」直接复用荣耀的美术资源。
///
/// <c>ActModel</c> 里所有资源路径都由 <c>FilePathIdentifier</c>（= Id.Entry 小写）拼出来：
/// 休息室背景 <c>scenes/rest_site/&lt;id&gt;_rest_site</c>、地图背景
/// <c>images/packed/map/map_bgs/&lt;id&gt;/...</c>、战场背景
/// <c>scenes/backgrounds/&lt;id&gt;/&lt;id&gt;_background</c>。
/// 改这一个 getter 就等于「这一层的所有美术都指向荣耀」，不用另做一套图。
/// </summary>
[HarmonyPatch(typeof(ActModel), "get_FilePathIdentifier")]
internal static class DonQuixoteRailActPathPatch
{
    private const string DonQuixote_RailEntry = "DON_QUIXOTE_REFRACTION_RAIL";

    private const string DonQuixote_GloryId = "glory";

    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref string __result)
    {
        try
        {
            if (__instance == null)
            {
                return;
            }

            if (string.Equals(__instance.Id.Entry, DonQuixote_RailEntry, StringComparison.OrdinalIgnoreCase))
            {
                __result = DonQuixote_GloryId;
            }
        }
        catch (Exception)
        {
            // 拿不到 Id 时保持原值即可。
        }
    }
}

/// <summary>
/// 第四层只允许出参孙这一个先古。
///
/// 原版抽先古时会把「本局随机分到这一层的共享先古」（达尔夫之类）拼进候选池，
/// 所以只把 <see cref="DonQuixoteRefractionRail.AllAncients"/> 收成一个并不保险。
/// 这里不去碰抽签逻辑，直接在**进先古房间的唯一出口**上改成参孙：
/// <see cref="MegaCrit.Sts2.Core.Runs.RunManager"/> 造房间时调
/// <c>State.Act.PullAncient()</c>，返回值就是这一层要放哪个先古。
/// 同时补一次 <c>Ancient</c> 属性（地图节点/统计会读它），两边保持一致。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullAncient))]
internal static class DonQuixoteRailAncientPatch
{
    private static bool DonQuixote_Logged;

    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref EventModel __result)
    {
        if (__instance is not DonQuixoteRefractionRail)
        {
            return;
        }

        AncientEventModel? DonQuixote_samson = ModelDb.AncientEvent<Events.DonQuixoteAncientSamson>();
        if (DonQuixote_samson == null)
        {
            return;
        }

        if (!ReferenceEquals(__result, DonQuixote_samson))
        {
            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] refraction rail ancient forced to Samson (was "
                    + (__result?.Id.Entry ?? "?") + ")", 0);
            }

            __result = DonQuixote_samson;
        }
    }
}

/// <summary>同一件事的展示面：地图上的先古节点也读 <c>ActModel.Ancient</c>。</summary>
[HarmonyPatch(typeof(ActModel), "get_Ancient")]
internal static class DonQuixoteRailAncientGetterPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, ref AncientEventModel __result)
    {
        if (__instance is not DonQuixoteRefractionRail)
        {
            return;
        }

        AncientEventModel? DonQuixote_samson = ModelDb.AncientEvent<Events.DonQuixoteAncientSamson>();
        if (DonQuixote_samson != null)
        {
            __result = DonQuixote_samson;
        }
    }
}

/// <summary>
/// 读档时的空列表兜底：修「点继续游戏报错、进不去」。
///
/// 原版 <see cref="SerializableRoomSet"/> 的三个列表标了
/// <c>SaveIfNotCollectionEmptyOrNull</c> —— 列表为空时**整个字段不写进存档**，
/// 而反序列化后字段默认是 <c>null</c>（没有初始化器）。<see cref="RoomSet.FromSave"/>
/// 直接 <c>save.EventIds.Select(...)</c>，于是
/// <c>ArgumentNullException: Value cannot be null. (Parameter 'source')</c>，
/// 主菜单点「继续」直接失败。
///
/// 我们的「折射轨道线」是第四层，<c>AllEvents</c> 本来就是空的（这一层不刷事件），
/// 所以只要存档里带着这一层，读档必炸。原版四层每层都有事件，没暴露这个问题。
///
/// 这里在 <c>FromSave</c> 之前把 null 补成空表，其他逻辑一行不动：
/// 空事件表的层读回来还是空事件表，和存档前的运行时状态一致。
/// </summary>
[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.FromSave))]
internal static class DonQuixoteRoomSetSavePatch
{
    [HarmonyPrefix]
    private static void Prefix(SerializableRoomSet save)
    {
        if (save == null)
        {
            return;
        }

        if (save.EventIds == null)
        {
            save.EventIds = new List<ModelId>();
        }

        if (save.NormalEncounterIds == null)
        {
            save.NormalEncounterIds = new List<ModelId>();
        }

        if (save.EliteEncounterIds == null)
        {
            save.EliteEncounterIds = new List<ModelId>();
        }
    }
}

/// <summary>
/// 第四层（index 3）点宝箱节点会黑屏，根因在原版
/// <see cref="MegaCrit.Sts2.Core.Rooms.TreasureRoom"/> 的构造函数：
/// <code>
/// if (actIndex &lt; 0 || actIndex &gt; 2) throw new ArgumentOutOfRangeException("actIndex", "must be between 0 and 2");
/// </code>
/// 原版只有三层，作者把「幕序号」当成了硬上限；而这个参数在构造函数里**根本没有被使用**
/// （宝箱的 Spine 与皮肤都走 <c>act.ChestSpineResourcePath</c>，我们已经指向 act3），
/// 所以这里只要把越界的校验放过去就行，行为和原版完全一致。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Rooms.TreasureRoom), MethodType.Constructor, new[] { typeof(int) })]
internal static class DonQuixoteTreasureRoomPatch
{
    /// <summary>true = 走原构造函数；false = 跳过（只跳过那一句越界校验）。</summary>
    [HarmonyPrefix]
    private static bool Prefix(int actIndex)
    {
        return actIndex >= 0 && actIndex <= 2;
    }
}

/// <summary>
/// 进阶 10（双重 Boss）与恐惧模式下的「第三层双 Boss」补回。
///
/// 原版 <c>RunManager.GenerateRooms</c> 的写法是：
/// <code>
/// if (i == State.Acts.Count - 1 &amp;&amp; AscensionManager.HasLevel(AscensionLevel.DoubleBoss))
///     act.SetSecondBossEncounter(rng.NextItem(act.AllBossEncounters.Where(e => e.Id != act.BossEncounter.Id)));
/// </code>
/// 也就是说双 Boss 永远挂在**最后一层**。我们加进来一张「折射轨道线」（index 3），
/// 最后一层变成它，而它只有一个 Boss（白月骑士-巴里）——候选列表被排除后是空的，
/// <c>NextItem</c> 对空列表返回 null，于是第四层没第二 Boss，第三层也不再是最后一层。
/// 结果就是 A10 / 恐惧模式下的双 Boss 整体消失。
///
/// 这里在**造地图之前**补一次（<see cref="ActModel.CreateMap"/> 的 Prefix）：
/// 地图构造函数会读 <c>act.HasSecondBoss</c> 决定要不要多画一个 Boss 节点，
/// 所以必须赶在它前面把第二 Boss 设好。幂等：已有第二 Boss 时直接跳过；
/// 原版三幕（没有我们的层）时完全不介入。
///
/// 注：实装版本的 <c>ActModel.GenerateRooms</c> 签名与反编译出来的不一样
/// （Harmony 报 Ambiguous），所以没挂在那里 —— CreateMap 的签名是确定的，
/// 而且时机正好在 <c>StandardActMap</c> 读取 <c>HasSecondBoss</c> 之前。
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.CreateMap),
    new[] { typeof(RunState), typeof(bool) })]
internal static class DonQuixoteAct3DoubleBossPatch
{
    private static bool DonQuixote_Logged;

    [HarmonyPrefix]
    private static void Prefix(ActModel __instance, RunState runState)
    {
        try
        {
            if (__instance == null || __instance.Index != 2)
            {
                return;
            }

            if (__instance.SecondBossEncounter != null)
            {
                return;
            }

            RunManager? DonQuixote_run = RunManager.Instance;
            RunState? DonQuixote_state = runState ?? DonQuixote_run?.DebugOnlyGetState();
            if (DonQuixote_run == null || DonQuixote_state == null)
            {
                return;
            }

            IReadOnlyList<ActModel> DonQuixote_acts = DonQuixote_state.Acts;
            bool DonQuixote_railIsLast = DonQuixote_acts != null && DonQuixote_acts.Count > 0
                && DonQuixote_acts[DonQuixote_acts.Count - 1] is DonQuixoteRefractionRail;

            // 双重 Boss 的判据。原版只看 AscensionManager；
            // 恐惧模式（Dread）在本地化里写明「所有进阶调整项均启用」，
            // 这里把等级与模式名也一并兜住，避免它走的是另一条实现路径。
            bool DonQuixote_doubleBoss =
                DonQuixote_run.AscensionManager.HasLevel(AscensionLevel.DoubleBoss)
                || DonQuixote_state.AscensionLevel >= (int)AscensionLevel.DoubleBoss
                || DonQuixote_state.GameMode.ToString()
                    .IndexOf("dread", StringComparison.OrdinalIgnoreCase) >= 0;

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] act3 boss check: double=" + DonQuixote_doubleBoss
                + " railLast=" + DonQuixote_railIsLast
                + " second=" + (__instance.SecondBossEncounter?.Id.Entry ?? "none")
                + " asc=" + DonQuixote_state.AscensionLevel
                + " mode=" + DonQuixote_state.GameMode, 0);

            if (!DonQuixote_railIsLast)
            {
                // 最后一层不是我们的层：原版自己会处理，不插手。
                return;
            }

            if (!DonQuixote_doubleBoss)
            {
                return;
            }

            List<EncounterModel> DonQuixote_candidates = __instance.AllBossEncounters
                .Where(DonQuixote_encounter => DonQuixote_encounter != null
                    && DonQuixote_encounter.Id != __instance.BossEncounter.Id)
                .ToList();

            if (DonQuixote_candidates.Count == 0)
            {
                return;
            }

            EncounterModel? DonQuixote_second = DonQuixote_state.Rng.UpFront.NextItem(DonQuixote_candidates);
            if (DonQuixote_second == null)
            {
                return;
            }

            __instance.SetSecondBossEncounter(DonQuixote_second);

            if (!DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] act3 double boss restored: boss="
                    + __instance.BossEncounter.Id.Entry + " second=" + DonQuixote_second.Id.Entry
                    + " asc=" + DonQuixote_state.AscensionLevel
                    + " mode=" + DonQuixote_state.GameMode, 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] act3 double boss patch failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}

/// <summary>
/// 读档时的两种历史遗留修正（都在 <see cref="RunState.FromSerializable"/> 之后）。
///
/// **情况一：还没进第四层，但带着「第三层不是荣耀 + 第四层是我们的轨」。**
/// 那是「让位规则」上线前的存档（当时我们无条件把它塞进 <c>ModelDb.Acts</c>，
/// 每一局都是 4 幕）。这种存档读回来之后，goety 之类 mod 仍会在第三层打完时抛
/// <c>Cannot replace another mod's fourth act.</c> 卡死。所以把我们的层摘掉，
/// 让第三层的归属者自己去补第四幕，我们再顺延到它后面。
///
/// **情况二：已经站在别人的第四层里，但列表里没有我们的层。**
/// 那是顺延规则上线前、goety 第四幕的存档（Acts 只有 4 项）。补一层我们的到末尾，
/// 这样第四层打完能继续进我们的层。
///
/// 两种情况都只碰「还没进我们的层」的存档；已经在折射轨道线上的局绝不改写。
/// 新追加的层必须先 <c>GenerateRooms</c> —— <c>RunManager.InitializeSavedRun</c>
/// 随后会对 <c>State.Acts</c> 每一层调 <c>ValidateRoomsAfterLoad</c>，
/// 而 <c>_rooms</c> 是 null 会直接空引用。
/// </summary>
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class DonQuixoteRailLegacySavePatch
{
    private static bool DonQuixote_Warned;

    [HarmonyPostfix]
    private static void Postfix(RunState __result)
    {
        try
        {
            if (__result == null)
            {
                return;
            }

            IReadOnlyList<ActModel> DonQuixote_Acts = __result.Acts;
            if (DonQuixote_Acts == null || DonQuixote_Acts.Count == 0)
            {
                return;
            }

            // 情况一：让位规则之前的老存档，摘掉我们提前占住的第四层。
            if (__result.CurrentActIndex < 3
                && DonQuixote_Acts.Count == 4
                && DonQuixote_Acts[3] is DonQuixoteRefractionRail
                && DonQuixote_Acts[2] is not Glory)
            {
                AccessTools.PropertySetter(typeof(RunState), "Acts")
                    .Invoke(__result, new object[] { DonQuixote_Acts.Take(3).ToList() });

                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] legacy save: dropped refraction rail from acts (act3 is "
                    + (DonQuixote_Acts[2]?.GetType().Name ?? "null")
                    + ", it will be deferred after the other mod's fourth act)", 0);
                return;
            }

            // 情况二：已经在别人的第四层里，替它补上我们顺延的那一层。
            if (__result.CurrentActIndex < 3
                || __result.CurrentActIndex != DonQuixote_Acts.Count - 1)
            {
                return;
            }

            if (DonQuixote_Acts[__result.CurrentActIndex] is DonQuixoteRefractionRail)
            {
                return;
            }

            foreach (ActModel DonQuixote_Existing in DonQuixote_Acts)
            {
                if (DonQuixote_Existing is DonQuixoteRefractionRail)
                {
                    return;
                }
            }

            DonQuixoteRefractionRail? DonQuixote_Rail = ModelDb.Act<DonQuixoteRefractionRail>();
            if (DonQuixote_Rail == null)
            {
                return;
            }

            ActModel DonQuixote_Mutable = DonQuixote_Rail.ToMutable();
            List<ActModel> DonQuixote_Next = DonQuixote_Acts.ToList();
            DonQuixote_Next.Add(DonQuixote_Mutable);

            AccessTools.PropertySetter(typeof(RunState), "Acts")
                .Invoke(__result, new object[] { DonQuixote_Next });

            DonQuixote_Mutable.GenerateRooms(__result);

            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] legacy save: refraction rail restored to slot "
                + (DonQuixote_Next.Count - 1) + " after another mod's act "
                + __result.CurrentActIndex + " (" + DonQuixote_Acts[__result.CurrentActIndex].GetType().Name
                + ")", 0);
        }
        catch (Exception DonQuixote_Exception)
        {
            if (DonQuixote_Warned)
            {
                return;
            }

            DonQuixote_Warned = true;
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] legacy act fix failed: " + DonQuixote_Exception.Message, 0);
        }
    }
}

/// <summary>
/// 从「有第二 Boss 的一层」切到「只有单 Boss 的一层」时黑屏的修复。
///
/// 现象（goety 第四层打完 → 进我们的折射轨道线）：
/// <code>
/// Preloading 'Act=DON_QUIXOTE_REFRACTION_RAIL' ...
/// refraction rail map created: rows=4 cols=7
/// [ERROR] System.ObjectDisposedException: Cannot access a disposed object.
/// Object name: 'MegaCrit.Sts2.Core.Nodes.Screens.Map.NBossMapPoint'.
///   at Godot.Node.GetPath()
///   at NMapScreen.SetMap(...)
///   at RunManager.SetActInternal(...)
/// </code>
/// 异常把 <c>EnterAct</c> 打断在 <c>FadeOut</c> 之后，<c>EnterRoomInternal</c> 与
/// <c>FadeIn</c> 都没执行 → 停在黑屏。
///
/// 根因是 <see cref="NMapScreen"/> 上缓存了上一张地图的节点引用
/// （<c>_bossPointNode</c> / <c>_secondBossPointNode</c> / <c>_startingPointNode</c>）
/// 与 <c>_mapPointDictionary</c>。切换层面时这些节点已经被 <c>ClearScreens</c> /
/// <c>QueueFree</c> 释放，但字段还指着它们；<c>SetMap</c> 重建地图时一旦碰到
/// （<c>GetPath()</c> 之类）就抛 <c>ObjectDisposedException</c>。
/// 单 Boss ↔ 双 Boss 的层切换才会踩到，所以只有「别人的第四层之后」这一跳出问题。
///
/// 处理分两层：
/// ・Prefix 先把缓存引用清干净（有效节点顺手 <c>QueueFreeSafely</c>，无效的直接置 null），
///   并从 <c>_mapPointDictionary</c> 里摘掉已经释放的节点；
/// ・万一还有别的残留导致 <c>SetMap</c> 抛异常，Finalizer 里**重试一次**
///   （此时上一次的清理已经做完），成功就把异常吞掉，让 <c>EnterAct</c> 继续走完
///   <c>FadeIn</c>，不至于卡黑屏。
/// </summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
internal static class DonQuixoteMapScreenStaleNodePatch
{
    private static readonly FieldInfo? DonQuixote_BossField =
        AccessTools.Field(typeof(NMapScreen), "_bossPointNode");

    private static readonly FieldInfo? DonQuixote_SecondBossField =
        AccessTools.Field(typeof(NMapScreen), "_secondBossPointNode");

    private static readonly FieldInfo? DonQuixote_StartingField =
        AccessTools.Field(typeof(NMapScreen), "_startingPointNode");

    private static readonly FieldInfo? DonQuixote_DictField =
        AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary");

    private static bool DonQuixote_Logged;

    [HarmonyPrefix]
    private static void Prefix(NMapScreen __instance, ActMap map)
    {
        try
        {
            if (__instance == null || map == null)
            {
                return;
            }

            bool DonQuixote_HadStale = false;

            DonQuixote_HadStale |= DonQuixote_Release(__instance, DonQuixote_BossField, "_bossPointNode");
            DonQuixote_HadStale |= DonQuixote_Release(__instance, DonQuixote_SecondBossField, "_secondBossPointNode");
            DonQuixote_HadStale |= DonQuixote_Release(__instance, DonQuixote_StartingField, "_startingPointNode");
            DonQuixote_HadStale |= DonQuixote_PruneDictionary(__instance);

            if (DonQuixote_HadStale && !DonQuixote_Logged)
            {
                DonQuixote_Logged = true;
                DonQuixoteModEntry.DonQuixote_Logger?.Info(
                    "[DonQuixote] map screen: cleared stale point nodes before switching to "
                    + (map.GetType().Name) + " (" + map.GetRowCount() + "x" + map.GetColumnCount()
                    + ", secondBoss=" + (map.SecondBossMapPoint != null) + ")", 0);
            }
        }
        catch (Exception DonQuixote_Exception)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] map screen prefix failed: " + DonQuixote_Exception.Message, 0);
        }
    }

    /// <summary>
    /// 异常兜底：<c>SetMap</c> 真的抛了就在清理完的状态上重试一次。
    /// 成功返回 null（吞掉），失败把原异常抛回去。
    /// </summary>
    [HarmonyFinalizer]
    private static Exception? Finalizer(NMapScreen __instance, ActMap map, ulong seed, bool clearDrawings,
        Exception? __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        DonQuixoteModEntry.DonQuixote_Logger?.Warn(
            "[DonQuixote] map screen SetMap threw (" + __exception.GetType().Name + ": "
            + __exception.Message + "), retrying once", 0);

        try
        {
            __instance.SetMap(map, seed, clearDrawings);
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] map screen SetMap retry succeeded", 0);
            return null;
        }
        catch (Exception DonQuixote_RetryException)
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Warn(
                "[DonQuixote] map screen SetMap retry failed: "
                + DonQuixote_RetryException.GetType().Name + ": " + DonQuixote_RetryException.Message, 0);
            return __exception;
        }
    }

    private static bool DonQuixote_Release(NMapScreen screen, FieldInfo? field, string name)
    {
        if (field == null)
        {
            return false;
        }

        object? DonQuixote_Value = field.GetValue(screen);
        if (DonQuixote_Value == null)
        {
            return false;
        }

        if (DonQuixote_Value is not Node DonQuixote_Node)
        {
            field.SetValue(screen, null);
            return true;
        }

        bool DonQuixote_Valid = GodotObject.IsInstanceValid(DonQuixote_Node);
        if (DonQuixote_Valid)
        {
            DonQuixote_Node.QueueFreeSafely();
        }
        else
        {
            DonQuixoteModEntry.DonQuixote_Logger?.Info(
                "[DonQuixote] map screen: " + name + " was already disposed", 0);
        }

        field.SetValue(screen, null);
        return true;
    }

    private static bool DonQuixote_PruneDictionary(NMapScreen screen)
    {
        if (DonQuixote_DictField?.GetValue(screen) is not IDictionary DonQuixote_Dict
            || DonQuixote_Dict.Count == 0)
        {
            return false;
        }

        // 整张表都清掉：<c>SetMap</c> 自己开头也会 Clear，这里提前做只是为了让
        // 「上一次地图留下的、已经释放的 NMapPoint」不可能被后续逻辑碰到。
        // 新地图的节点会在 SetMap 里重新填。
        int DonQuixote_Count = DonQuixote_Dict.Count;
        DonQuixote_Dict.Clear();
        return DonQuixote_Count > 0;
    }
}
