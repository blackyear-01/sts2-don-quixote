using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace DonQuixote;

/// <summary>
/// Mod entry point: registers the assembly so RitsuLib can discover the character, card and relic registrations declared with attributes.
/// </summary>
[ModInitializer(nameof(DonQuixote_Initialize))]
public static class DonQuixoteModEntry
{
    public const string DonQuixote_ModId = "DonQuixote";

    public static Logger DonQuixote_Logger { get; private set; } = null!;

    public static void DonQuixote_Initialize()
    {
        Assembly DonQuixote_Assembly = Assembly.GetExecutingAssembly();
        DonQuixote_Logger = RitsuLibFramework.CreateLogger(DonQuixote_ModId);
        DonQuixote_Logger.Info("[DonQuixote] initializing character mod");

        // 自定义关键词必须在卡牌实例创建之前注册（卡牌构造期会 AddKeyword）。
        DonQuixoteKeywords.DonQuixote_Register();

        // 设置页要在补丁安装前注册，act 列表补丁会实时读取其中的开关。
        DonQuixoteSettingsPage.DonQuixote_Register();

        ModTypeDiscoveryHub.RegisterModAssembly(DonQuixote_ModId, DonQuixote_Assembly);

        RitsuLibFramework.EnsureGodotScriptsRegistered(DonQuixote_Assembly, DonQuixote_Logger);

        DonQuixoteSpineProbe.DonQuixote_Run(DonQuixote_Logger);

        DonQuixote_ApplyPatches(DonQuixote_Assembly);

        DonQuixoteGameOverArt.DonQuixote_Subscribe();

        DonQuixoteRightClick.DonQuixote_Register();

        Events.DonQuixoteEventAction.DonQuixote_Register();

        DonQuixote_RegisterAncientMappings();

        // 兼容层：让创意工坊 3764112372（Donquixote）的资产拦截补丁跳过我们的资源路径。
        DonQuixoteForeignAssetGuard.DonQuixote_Install(DonQuixote_Logger);
    }

    /// <summary>
    /// 人物独有内容注册：古老牙齿把「抑制的血之渴望」转化成先古卡「灼烧的饥渴」。
    /// </summary>
    private static void DonQuixote_RegisterAncientMappings()
    {
        try
        {
            RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<
                Cards.DonQuixoteBloodHunger,
                Cards.DonQuixoteBurningHunger>();

            DonQuixote_Logger.Info("[DonQuixote] archaic tooth mapping registered");
        }
        catch (System.Exception DonQuixote_Exception)
        {
            DonQuixote_Logger.Warn($"[DonQuixote] archaic tooth mapping failed: {DonQuixote_Exception}", 0);
        }
    }

    /// <summary>
    /// 判断一个类型是不是 Harmony 补丁类。
    /// </summary>
    private static bool DonQuixote_IsPatchClass(System.Type DonQuixote_type)
    {
        foreach (object DonQuixote_attribute in DonQuixote_type.GetCustomAttributes(true))
        {
            if (DonQuixote_attribute != null && DonQuixote_attribute.GetType().Name == "HarmonyPatch")
            {
                return true;
            }
        }

        // 兜底：只写了 [HarmonyPrefix] / [HarmonyPostfix] 方法的类。
        foreach (System.Reflection.MethodInfo DonQuixote_method in DonQuixote_type.GetMethods(
            System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.DeclaredOnly))
        {
            foreach (object DonQuixote_attribute in DonQuixote_method.GetCustomAttributes(true))
            {
                string DonQuixote_name = DonQuixote_attribute?.GetType().Name;
                if (DonQuixote_name == "HarmonyPrefix"
                    || DonQuixote_name == "HarmonyPostfix"
                    || DonQuixote_name == "HarmonyTranspiler"
                    || DonQuixote_name == "HarmonyFinalizer")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void DonQuixote_LogPatchState(string DonQuixote_label, System.Reflection.MethodInfo DonQuixote_method)
    {
        if (DonQuixote_method == null)
        {
            DonQuixote_Logger.Warn("[DonQuixote] patch target missing: " + DonQuixote_label, 0);
            return;
        }

        HarmonyLib.Patches DonQuixote_info = HarmonyLib.Harmony.GetPatchInfo(DonQuixote_method);
        int DonQuixote_prefixes = DonQuixote_info == null ? 0 : DonQuixote_info.Prefixes.Count;
        int DonQuixote_postfixes = DonQuixote_info == null ? 0 : DonQuixote_info.Postfixes.Count;
        DonQuixote_Logger.Info(
            "[DonQuixote] patch state " + DonQuixote_label
            + " prefix=" + DonQuixote_prefixes + " postfix=" + DonQuixote_postfixes, 0);
    }

    private static void DonQuixote_ApplyPatches(Assembly DonQuixote_Assembly)
    {
        try
        {
            HarmonyLib.Harmony DonQuixote_Harmony = new HarmonyLib.Harmony("sts2.donquixote");

            // 不用 PatchAll：PatchAll 遇到第一个目标不存在的补丁类就会整段抛异常。
            int DonQuixote_ok = 0;
            int DonQuixote_failed = 0;
            int DonQuixote_total = 0;
            foreach (System.Type DonQuixote_type in DonQuixote_Assembly.GetTypes())
            {
                // 注意：static class 的 IsAbstract 为 true.
                if (DonQuixote_type == null || !DonQuixote_type.IsClass)
                {
                    continue;
                }

                if (!DonQuixote_IsPatchClass(DonQuixote_type))
                {
                    continue;
                }

                DonQuixote_total++;
                try
                {
                    DonQuixote_Harmony.CreateClassProcessor(DonQuixote_type).Patch();
                    DonQuixote_ok++;
                }
                catch (System.Exception DonQuixote_patchError)
                {
                    DonQuixote_failed++;
                    DonQuixote_Logger.Warn(
                        "[DonQuixote] patch class failed: " + DonQuixote_type.Name
                        + " -> " + DonQuixote_patchError.GetBaseException().Message,
                        0);
                }
            }

            DonQuixote_Logger.Info(
                "[DonQuixote] combat pose patches applied (total=" + DonQuixote_total
                + " ok=" + DonQuixote_ok + " failed=" + DonQuixote_failed + ")", 0);

            DonQuixote_LogPatchState("CardCmd.Exhaust",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CardCmd), "Exhaust"));
            DonQuixote_LogPatchState("CardModel.MoveToResultPileWithoutPlaying",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Models.CardModel), "MoveToResultPileWithoutPlaying"));
            DonQuixote_LogPatchState("Hook.ModifyDamage",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Hooks.Hook), "ModifyDamage"));
            DonQuixote_LogPatchState("Hook.ModifyHpLost",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Hooks.Hook), "ModifyHpLost"));
            DonQuixote_LogPatchState("WeakPower.ModifyDamageMultiplicative",
                HarmonyLib.AccessTools.Method(
                    typeof(MegaCrit.Sts2.Core.Models.Powers.WeakPower), "ModifyDamageMultiplicative"));

            DonQuixote_LogPatchState("CardPileCmd.Add(CardPile)",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CardPileCmd), "Add",
                    new System.Type[]
                    {
                        typeof(MegaCrit.Sts2.Core.Models.CardModel),
                        typeof(MegaCrit.Sts2.Core.Entities.Cards.CardPile),
                        typeof(MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition),
                        typeof(MegaCrit.Sts2.Core.Models.AbstractModel),
                        typeof(bool)
                    }));
            DonQuixote_LogPatchState("CardPileCmd.Add(PileType)",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CardPileCmd), "Add",
                    new System.Type[]
                    {
                        typeof(MegaCrit.Sts2.Core.Models.CardModel),
                        typeof(MegaCrit.Sts2.Core.Entities.Cards.PileType),
                        typeof(MegaCrit.Sts2.Core.Entities.Cards.CardPilePosition),
                        typeof(MegaCrit.Sts2.Core.Models.AbstractModel),
                        typeof(bool)
                    }));

                    DonQuixote_LogPatchState("CardModel.HoverTips",
                HarmonyLib.AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Models.CardModel), nameof(MegaCrit.Sts2.Core.Models.CardModel.HoverTips)));
            DonQuixote_LogPatchState("CardModel.GetKeywordsWithSources",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Models.CardModel), "GetKeywordsWithSources"));
            DonQuixote_LogPatchState("AncientEventModel.Done",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Models.AncientEventModel), "Done"));
            DonQuixote_LogPatchState("NEventRoom.Proceed",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom), "Proceed"));
            // 第四层（折射轨道线）的地图拦截：这条必须是 prefix=1。
            DonQuixote_LogPatchState("ActModel.CreateMap",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Models.ActModel), "CreateMap"));
            // 第四层固定先古「参孙」：在 PullAncient（进先古房间的唯一出口）上覆盖。
            DonQuixote_LogPatchState("ActModel.PullAncient",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Models.ActModel), "PullAncient"));
            // 白月骑士死亡：StartDeathAnim 之前清掉姿势轨。
            DonQuixote_LogPatchState("NCreature.StartDeathAnim",
                HarmonyLib.AccessTools.Method(
                    typeof(MegaCrit.Sts2.Core.Nodes.Combat.NCreature), "StartDeathAnim"));
            // 读档兜底：有空列表的幕（第四层没有事件）存档里会把字段整段省掉。
            DonQuixote_LogPatchState("RoomSet.FromSave",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Rooms.RoomSet), "FromSave"));
            DonQuixote_LogPatchState("EventSynchronizer.ChooseOptionForEvent",
                HarmonyLib.AccessTools.Method(
                    typeof(MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer), "ChooseOptionForEvent"));
            DonQuixote_LogPatchState("AncientEventModel.get_DialogueSet",
                HarmonyLib.AccessTools.PropertyGetter(
                    typeof(MegaCrit.Sts2.Core.Models.AncientEventModel), "DialogueSet"));
            DonQuixote_LogPatchState("CreatureCmd.Damage(DamageTargetData)",
                HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CreatureCmd), "Damage",
                    new System.Type[]
                    {
                        typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext),
                        typeof(System.Collections.Generic.IEnumerable<
                            MegaCrit.Sts2.Core.Entities.Creatures.DamageTargetData>),
                        typeof(decimal),
                        typeof(MegaCrit.Sts2.Core.ValueProps.ValueProp),
                        typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature),
                        typeof(MegaCrit.Sts2.Core.Models.CardModel),
                        typeof(MegaCrit.Sts2.Core.Entities.Cards.CardPlay),
                        typeof(MegaCrit.Sts2.Core.Hooks.ModifyDamageHookType),
                    }));
            DonQuixoteBloodFeastGate.DonQuixote_Register(DonQuixote_Harmony);
        }
        catch (System.Exception DonQuixote_Exception)
        {
            DonQuixote_Logger.Warn($"[DonQuixote] combat pose patches failed: {DonQuixote_Exception}", 0);
        }
    }
}
