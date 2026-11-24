using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Content;

namespace DonQuixote;

/// <summary>
/// The "Don Quixote" playable character.
/// </summary>
[RegisterCharacter]
public sealed class DonQuixoteCharacter
    : ModCharacterTemplate<DonQuixoteCardPool, DonQuixoteRelicPool, DonQuixotePotionPool>
{
    // 人物.doc：初始血量 80/80，金币 110。
    public override int StartingHp => 80;

    public override int StartingGold => 110;

    public override int MaxEnergy => 3;

    public override CharacterGender Gender => CharacterGender.Feminine;

    public override Color NameColor => new Color("#d8c27a");

    public override float AttackAnimDelay => 0.2f;

    public override float CastAnimDelay => 0.25f;

    public override List<string> GetArchitectAttackVfx() => new List<string>();

    /// <summary>
    /// Borrow the Ironclad combat rig until Don Quixote gets its own spine rig.
    /// </summary>
    public override string PlaceholderCharacterId => "IRONCLAD";

    /// <summary>
    /// Don Quixote does not take part in vanilla epoch / timeline progression yet.
    /// </summary>
    public override bool RequiresEpochAndTimeline => false;

    public override CharacterAssetProfile AssetProfile => new()
    {        Ui = new CharacterUiAssetSet
        {
            IconTexturePath = "res://DonQuixote/images/char/icon.png",
            IconOutlineTexturePath = "res://DonQuixote/images/char/icon_outline.png",
            // The top bar portrait instantiates CharacterModel.Icon, which is a scene.
            IconPath = "res://DonQuixote/scenes/character_icon.tscn",
            CharacterSelectIconPath = "res://DonQuixote/images/charui/char_select_icon.png",
            // The game loads this path as a scene, not as a texture.
            CharacterSelectBgPath = "res://DonQuixote/scenes/char_select_bg.tscn",
            CharacterSelectLockedIconPath = "res://DonQuixote/images/charui/char_select_icon.png",
            MapMarkerPath = "res://DonQuixote/images/charui/map_marker.png",
        },
        Scenes = new CharacterSceneAssetSet
        {
            VisualsPath = "res://DonQuixote/scenes/creature_visuals/don_quixote.tscn",
            // RitsuLib derives these three scene paths from the entry id when they are left empty (鈥?don_quixote_character_don_quixote_character_*.tscn), and a failed load aborts the room transition ("black screen"), so borrow the Ironclad scenes until Don Quixote gets its own.
            EnergyCounterPath = "res://scenes/combat/energy_counters/ironclad_energy_counter.tscn",
            // 商店 / 火堆立绘：用模组自己的 spine 立绘（闲置循环 anim relaxed_loop）。
            MerchantAnimPath = "res://DonQuixote/scenes/merchant/don_quixote_merchant.tscn",
            RestSiteAnimPath = "res://DonQuixote/scenes/rest_site/don_quixote_rest_site.tscn",
        },
        VisualCues = DonQuixoteCombatCues.DonQuixote_Cues,
        // 人物独有内容注册（文档 04-14 第 "人物独有的内容注册" 节）：美味饼干会按 人物显示不同图标。
        VanillaRelicVisualOverrides = new[]
        {
            new CharacterVanillaRelicVisualOverride(
                CharacterOwnedVanillaRelicModelId.YummyCookie,
                new RelicAssetProfile
                {
                    IconPath = "res://DonQuixote/images/relics/cookie.png",
                    IconOutlinePath = "res://DonQuixote/images/relics/cookie_outline.png",
                }),
        },
    };

    /// <summary>
    /// Prefer the Spine rig, fall back to the frame-based rig when the spine resources cannot be resolved.
    /// </summary>
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        return DonQuixoteVisualsFactory.DonQuixote_Create();
    }
}

