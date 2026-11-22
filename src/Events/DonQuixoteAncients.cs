using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace DonQuixote.Events;

/// <summary>两个先古共用的配色：文档要求对话按钮与对话框都是 RGB(115, 251, 253)。</summary>
internal static class DonQuixoteAncientColors
{
    public static Color DonQuixote_Aqua => new Color(115f / 255f, 251f / 255f, 253f / 255f);

    public static Color DonQuixote_AquaButton => new Color(115f / 255f, 251f / 255f, 253f / 255f, 0.75f);
}

/// <summary>
/// 先古：巴里（绰号「白月骑士」）。
///
/// 遗物.docx：
/// ・只会在荣耀这一章生成 → 由 <see cref="DonQuixoteBarryAncientPatch"/> 追加进 Glory 的先古列表；
/// ・固定池1（仅堂吉诃德可选）：光荣的决斗 / 胜利之证 / 血液结晶；
/// ・固定池2（其他角色可选）：芒果 / 圆顶礼帽 / 化学物X。
/// </summary>
public sealed class DonQuixoteAncientBarry : AncientEventModel
{
    public override Color ButtonColor => DonQuixoteAncientColors.DonQuixote_AquaButton;

    public override Color DialogueColor => DonQuixoteAncientColors.DonQuixote_Aqua;

    private IEnumerable<EventOption> DonQuixoteDonQuixotePool => new List<EventOption>
    {
        RelicOption<Relics.DonQuixoteGloriousDuel>(),
        RelicOption<Relics.DonQuixoteProofOfVictory>(),
        RelicOption<Relics.DonQuixoteBloodCrystal>(),
    };

    private IEnumerable<EventOption> DonQuixoteOtherPool => new List<EventOption>
    {
        RelicOption<Mango>(),
        RelicOption<BowlerHat>(),
        RelicOption<ChemicalX>(),
    };

    public override IEnumerable<EventOption> AllPossibleOptions =>
        DonQuixoteDonQuixotePool.Concat(DonQuixoteOtherPool).ToList();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool DonQuixote_isDonQuixote = Owner?.Character is DonQuixoteCharacter;
        return (DonQuixote_isDonQuixote ? DonQuixoteDonQuixotePool : DonQuixoteOtherPool).ToList();
    }

    protected override AncientDialogueSet DefineDialogues()
    {
        return new AncientDialogueSet
        {
            // 文档只给了「堂吉诃德专属」和「其他角色」两套，没有单独的首次访问台词。
            FirstVisitEverDialogue = null,
            CharacterDialogues = new Dictionary<string, IReadOnlyList<AncientDialogue>>
            {
                // 堂吉诃德专属：好久不见啊，我有些东西或许能帮上你的忙，冒险顺利
                [AncientEventModel.CharKey<DonQuixoteCharacter>()] = new List<AncientDialogue>
                {
                    new AncientDialogue(string.Empty) { IsRepeating = true },
                },
            },
            AgnosticDialogues = new List<AncientDialogue>
            {
                // 其他角色：我这有些补给但愿能帮上你的忙，冒险者
                new AncientDialogue(string.Empty) { IsRepeating = true },
            },
        };
    }
}

/// <summary>
/// 先古：参孙（绰号「？？？」）。
///
/// 遗物.docx：
/// ・只会在折射轨道出现 → 挂在 <see cref="Acts.DonQuixoteRefractionRail.AllAncients"/> 上；
/// ・固定池1（仅堂吉诃德可选）：硬血长柄刀 / 蜜拉卡 / 撕裂的血袋；
/// ・固定池2（其他角色可选）：赌博筹码 / 蜥蜴尾巴 / 不休陀螺。
/// </summary>
public sealed class DonQuixoteAncientSamson : AncientEventModel
{
    public override Color ButtonColor => DonQuixoteAncientColors.DonQuixote_AquaButton;

    public override Color DialogueColor => DonQuixoteAncientColors.DonQuixote_Aqua;

    private IEnumerable<EventOption> DonQuixoteDonQuixotePool => new List<EventOption>
    {
        RelicOption<Relics.DonQuixoteHardBloodHalberd>(),
        RelicOption<Relics.DonQuixoteMiraka>(),
        RelicOption<Relics.DonQuixoteTornBloodBag>(),
    };

    private IEnumerable<EventOption> DonQuixoteOtherPool => new List<EventOption>
    {
        RelicOption<GamblingChip>(),
        RelicOption<LizardTail>(),
        RelicOption<UnceasingTop>(),
    };

    public override IEnumerable<EventOption> AllPossibleOptions =>
        DonQuixoteDonQuixotePool.Concat(DonQuixoteOtherPool).ToList();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool DonQuixote_isDonQuixote = Owner?.Character is DonQuixoteCharacter;
        return (DonQuixote_isDonQuixote ? DonQuixoteDonQuixotePool : DonQuixoteOtherPool).ToList();
    }

    protected override AncientDialogueSet DefineDialogues()
    {
        return new AncientDialogueSet
        {
            FirstVisitEverDialogue = null,
            CharacterDialogues = new Dictionary<string, IReadOnlyList<AncientDialogue>>
            {
                // 堂吉诃德专属两行：
                //   「堕落的拉·曼却血魔们的亲族大人，一代眷属堂吉诃德」
                //   「若其他长老们看到你的模样，不知会多么痛心疾首啊……」
                [AncientEventModel.CharKey<DonQuixoteCharacter>()] = new List<AncientDialogue>
                {
                    new AncientDialogue(string.Empty, string.Empty) { IsRepeating = true },
                },
            },
            AgnosticDialogues = new List<AncientDialogue>
            {
                // 其他角色：「……」
                new AncientDialogue(string.Empty) { IsRepeating = true },
            },
        };
    }
}
