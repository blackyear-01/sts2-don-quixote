"""Build a Godot 4 PCK (v3) for the Don Quixote mod without a Godot editor.

Usage:
    python build_pck.py            # rebuild pck into build/DonQuixote.pck

Layout inside the pck:
    DonQuixote.json                       (mod manifest copy)
    DonQuixote/localization/<lang>/*.json
    DonQuixote/scenes/creature_visuals/don_quixote.tscn
    DonQuixote/images/.../<name>.png      (raw png, kept for runtime loaders)
    DonQuixote/images/.../<name>.png.import
    .godot/imported/<name>.png-<md5>.ctex

Textures are converted to Godot's compressed texture format (GST2 + lossless
WebP payload) because the game loads `res://*.png` through the import remap.

IMPORTANT: localization files must ONLY be written below `DonQuixote/...`.
A pck-root `localization/<lang>/<table>.json` mounts at
`res://localization/...`, which shadows the base game's own localization
tables and turns every vanilla string into a raw key.
"""
import hashlib
import json
import os
import struct

from PIL import Image

MOD_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(MOD_ROOT, "assets", "art")
SCENES = os.path.join(MOD_ROOT, "assets", "scenes")
GAME = r"D:\Steam\steamapps\common\Slay the Spire 2"
TEMPLATE_PCK = os.path.join(GAME, "character", "Matthias", "Matthias.pck")
OUT_PCK = os.path.join(MOD_ROOT, "build", "DonQuixote.pck")

# ctex header constants sampled from every shipped .ctex file.
CTEX_MAGIC = 0x32545347  # "GST2"
CTEX_FIELDS = {
    1: 1,
    4: 0x0D000000,
    5: 0xFFFFFFFF,
    6: 0,
    7: 0,
    8: 0,
    9: 2,
    11: 0,
    12: 5,
}

UID_CHARSET = "abcdefghijklmnopqrstuvwxyz0123456789"


def uid_for(key: str) -> str:
    """Deterministic Godot style uid so .import files and .tscn agree."""
    digest = hashlib.md5(("DonQuixote/" + key).encode("utf-8")).digest()
    value = int.from_bytes(digest[:8], "big")
    out = []
    for _ in range(13):
        out.append(UID_CHARSET[value % 36])
        value //= 36
    return "uid://" + "".join(out)


CTEX_CACHE = os.path.join(MOD_ROOT, "build", "ctex_cache")


def make_ctex(png_path: str) -> bytes:
    """Pack an image as Godot CompressedTexture2D (uncompressed-in-header + WebP payload).

    Results are cached per source md5: the 2048x7084 spine atlas needs a lot of
    memory to lossless-encode, so converting it once and reusing the bytes keeps
    the whole build well below the sandbox memory limit.
    """
    with open(png_path, "rb") as f:
        source_digest = hashlib.md5(f.read()).hexdigest()

    cached_path = os.path.join(CTEX_CACHE, source_digest + ".ctex")
    if os.path.exists(cached_path):
        with open(cached_path, "rb") as f:
            return f.read()

    img = Image.open(png_path)
    if img.mode not in ("RGBA", "RGB"):
        img = img.convert("RGBA")
    from io import BytesIO

    buf = BytesIO()
    img.save(buf, format="WEBP", lossless=True, quality=100, method=6)
    payload = buf.getvalue()
    width, height = img.size
    fields = dict(CTEX_FIELDS)
    fields[0] = CTEX_MAGIC
    fields[2] = width
    fields[3] = height
    fields[10] = (height << 16) | width
    fields[13] = len(payload)
    header = struct.pack("<14I", *[fields.get(i, 0) for i in range(14)])
    data = header + payload

    os.makedirs(CTEX_CACHE, exist_ok=True)
    with open(cached_path, "wb") as f:
        f.write(data)
    return data


def make_import(res_path: str, ctex_rel: str, uid: str) -> bytes:
    body = (
        "[remap]\n\n"
        'importer="texture"\n'
        'type="CompressedTexture2D"\n'
        'uid="%s"\n'
        'path="res://%s"\n'
        "metadata={\n"
        '"vram_texture": false\n'
        "}\n"
    ) % (uid, ctex_rel)
    return body.encode("utf-8")


TEXTURES = {
    "enchantment_coin.png": "DonQuixote/images/enchantments/indestructible_coin.png",
    "icon.png": "DonQuixote/images/char/icon.png",
    "icon_outline.png": "DonQuixote/images/char/icon_outline.png",
    "char_select_icon.png": "DonQuixote/images/charui/char_select_icon.png",
    "char_select_bg.png": "DonQuixote/images/charui/char_select_bg.png",
    "map_marker.png": "DonQuixote/images/charui/map_marker.png",
    "idle.png": "DonQuixote/images/char/idle.png",
    "attack_f0.png": "DonQuixote/images/char/attack_f0.png",
    "attack_f1.png": "DonQuixote/images/char/attack_f1.png",
    "attack_f2.png": "DonQuixote/images/char/attack_f2.png",
    "attack_f3.png": "DonQuixote/images/char/attack_f3.png",
    "cast.png": "DonQuixote/images/char/cast.png",
    "hit.png": "DonQuixote/images/char/hit.png",
    "defend.png": "DonQuixote/images/char/defend.png",
    "dead.png": "DonQuixote/images/char/dead.png",
    "lance.png": "DonQuixote/images/relics/lance.png",
    "lance_outline.png": "DonQuixote/images/relics/lance_outline.png",
    "cookie.png": "DonQuixote/images/relics/cookie.png",
    "cookie_outline.png": "DonQuixote/images/relics/cookie_outline.png",
    "death_screen.png": "DonQuixote/images/ui/death_screen.png",
    "blood_crystal_icon.png": "DonQuixote/images/relics/blood_crystal_icon.png",
    "blood_crystal_outline.png": "DonQuixote/images/relics/blood_crystal_outline.png",
    "blood_crystal_big.png": "DonQuixote/images/relics/blood_crystal_big.png",
    "blood_instinct_icon.png": "DonQuixote/images/relics/blood_instinct_icon.png",
    "blood_instinct_outline.png": "DonQuixote/images/relics/blood_instinct_outline.png",
    "blood_instinct_big.png": "DonQuixote/images/relics/blood_instinct_big.png",
    "knight_instinct_icon.png": "DonQuixote/images/relics/knight_instinct_icon.png",
    "knight_instinct_outline.png": "DonQuixote/images/relics/knight_instinct_outline.png",
    "knight_instinct_big.png": "DonQuixote/images/relics/knight_instinct_big.png",
    "power_bleed.png": "DonQuixote/images/powers/bleed.png",
    "power_bleed_big.png": "DonQuixote/images/powers/bleed_big.png",
    "power_wavering.png": "DonQuixote/images/powers/wavering.png",
    "power_wavering_big.png": "DonQuixote/images/powers/wavering_big.png",
    "power_duel_glory.png": "DonQuixote/images/powers/duel_glory.png",
    "power_duel_glory_big.png": "DonQuixote/images/powers/duel_glory_big.png",
    "power_blood_feast.png": "DonQuixote/images/powers/blood_feast.png",
    "power_blood_feast_big.png": "DonQuixote/images/powers/blood_feast_big.png",
    "power_blood_impulse.png": "DonQuixote/images/powers/blood_impulse.png",
    "power_blood_impulse_big.png": "DonQuixote/images/powers/blood_impulse_big.png",
    'power_corruption.png': 'DonQuixote/images/powers/corruption.png',
    'power_corruption_big.png': 'DonQuixote/images/powers/corruption_big.png',
    'power_duel_target.png': 'DonQuixote/images/powers/duel_target.png',
    'power_duel_target_big.png': 'DonQuixote/images/powers/duel_target_big.png',
    'power_knight_dream.png': 'DonQuixote/images/powers/knight_dream.png',
    'power_knight_dream_big.png': 'DonQuixote/images/powers/knight_dream_big.png',
    'power_irresponsible_dream.png': 'DonQuixote/images/powers/irresponsible_dream.png',
    'power_irresponsible_dream_big.png': 'DonQuixote/images/powers/irresponsible_dream_big.png',
    'power_blood_thorns.png': 'DonQuixote/images/powers/blood_thorns.png',
    'power_blood_thorns_big.png': 'DonQuixote/images/powers/blood_thorns_big.png',
    'power_responsibility.png': 'DonQuixote/images/powers/responsibility.png',
    'power_responsibility_big.png': 'DonQuixote/images/powers/responsibility_big.png',
    'power_clotting_blood.png': 'DonQuixote/images/powers/clotting_blood.png',
    'power_clotting_blood_big.png': 'DonQuixote/images/powers/clotting_blood_big.png',
    'clot_spike_front.png': 'DonQuixote/images/vfx/clot_spike_front.png',
    'clot_spike_back.png': 'DonQuixote/images/vfx/clot_spike_back.png',
    'weapon_throw.png': 'DonQuixote/images/vfx/weapon_throw.png',
    'power_follower.png': 'DonQuixote/images/powers/follower.png',
    'power_follower_big.png': 'DonQuixote/images/powers/follower_big.png',
    'power_blood_bag.png': 'DonQuixote/images/powers/blood_bag.png',
    'power_blood_bag_big.png': 'DonQuixote/images/powers/blood_bag_big.png',
    'power_blood_armor.png': 'DonQuixote/images/powers/blood_armor.png',
    'power_blood_armor_big.png': 'DonQuixote/images/powers/blood_armor_big.png',
    'power_hard_blood.png': 'DonQuixote/images/powers/hard_blood.png',
    'power_hard_blood_big.png': 'DonQuixote/images/powers/hard_blood_big.png',
    'sancho_icon.png': 'DonQuixote/images/relics/sancho_icon.png',
    'sancho_outline.png': 'DonQuixote/images/relics/sancho_outline.png',
    'sancho_big.png': 'DonQuixote/images/relics/sancho_big.png',
    'dulcinea_icon.png': 'DonQuixote/images/relics/dulcinea_icon.png',
    'dulcinea_outline.png': 'DonQuixote/images/relics/dulcinea_outline.png',
    'dulcinea_big.png': 'DonQuixote/images/relics/dulcinea_big.png',
    'gullinbursti_icon.png': 'DonQuixote/images/relics/gullinbursti_icon.png',
    'gullinbursti_outline.png': 'DonQuixote/images/relics/gullinbursti_outline.png',
    'gullinbursti_big.png': 'DonQuixote/images/relics/gullinbursti_big.png',
    'nikolina_icon.png': 'DonQuixote/images/relics/nikolina_icon.png',
    'nikolina_outline.png': 'DonQuixote/images/relics/nikolina_outline.png',
    'nikolina_big.png': 'DonQuixote/images/relics/nikolina_big.png',
    'victory_icon.png': 'DonQuixote/images/relics/victory_icon.png',
    'victory_outline.png': 'DonQuixote/images/relics/victory_outline.png',
    'victory_big.png': 'DonQuixote/images/relics/victory_big.png',
    'moon_flight_icon.png': 'DonQuixote/images/relics/moon_flight_icon.png',
    'moon_flight_outline.png': 'DonQuixote/images/relics/moon_flight_outline.png',
    'moon_flight_big.png': 'DonQuixote/images/relics/moon_flight_big.png',
    'farewell_icon.png': 'DonQuixote/images/relics/farewell_icon.png',
    'farewell_outline.png': 'DonQuixote/images/relics/farewell_outline.png',
    'farewell_big.png': 'DonQuixote/images/relics/farewell_big.png',
    'card_good_idea_form.png': 'DonQuixote/images/cards/good_idea_form.png',
    # 一批卡的卡面插画（资源\卡牌\*.png 经 tools/CardImagesCreator/card_art.py 裁切）
    'card_hard_blood_slash.png': 'DonQuixote/images/cards/hard_blood_slash.png',
    'card_hard_blood_guard.png': 'DonQuixote/images/cards/hard_blood_guard.png',
    'card_be_pierced.png': 'DonQuixote/images/cards/be_pierced.png',
    'card_be_run_through.png': 'DonQuixote/images/cards/be_run_through.png',
    'card_blood_carousel.png': 'DonQuixote/images/cards/blood_carousel.png',
    'card_fragile_fragility.png': 'DonQuixote/images/cards/fragile_fragility.png',
    'card_hard_blood_cape14.png': 'DonQuixote/images/cards/hard_blood_cape14.png',
    'card_hard_blood_gouge.png': 'DonQuixote/images/cards/hard_blood_gouge.png',
    'card_feast_moment.png': 'DonQuixote/images/cards/feast_moment.png',
    'card_blood_desire.png': 'DonQuixote/images/cards/blood_desire.png',
    'card_hard_blood_wedge.png': 'DonQuixote/images/cards/hard_blood_wedge.png',
    'card_hard_blood_pile.png': 'DonQuixote/images/cards/hard_blood_pile.png',
    'card_blood_cheer.png': 'DonQuixote/images/cards/blood_cheer.png',
    'card_blood_joy.png': 'DonQuixote/images/cards/blood_joy.png',
    'card_crush.png': 'DonQuixote/images/cards/crush.png',
    'card_wound.png': 'DonQuixote/images/cards/wound.png',
    'card_hard_blood_pierce.png': 'DonQuixote/images/cards/hard_blood_pierce.png',
    'card_hard_blood_raid.png': 'DonQuixote/images/cards/hard_blood_raid.png',
    'card_hard_blood_track.png': 'DonQuixote/images/cards/hard_blood_track.png',
    'card_la_sangre.png': 'DonQuixote/images/cards/la_sangre.png',
    # 先古卡（250x351）：灼烧的饥渴 / 硬血奥义-球；抑制的血之渴望是普通攻击牌
    'card_burning_hunger.png': 'DonQuixote/images/cards/burning_hunger.png',
    'card_hard_blood_orb.png': 'DonQuixote/images/cards/hard_blood_orb.png',
    'card_blood_hunger.png': 'DonQuixote/images/cards/blood_hunger.png',
    # 0.0.7 新增 / 更新的卡图
    'card_blood_spread.png': 'DonQuixote/images/cards/blood_spread.png',
    'card_eternal_thirst.png': 'DonQuixote/images/cards/eternal_thirst.png',
    'card_weapon_maintenance.png': 'DonQuixote/images/cards/weapon_maintenance.png',
    'card_crimson_claw.png': 'DonQuixote/images/cards/crimson_claw.png',
    'card_energy_infusion.png': 'DonQuixote/images/cards/energy_infusion.png',
    'card_blood_absorb.png': 'DonQuixote/images/cards/blood_absorb.png',
    'card_call_of_kin.png': 'DonQuixote/images/cards/call_of_kin.png',
    # 补上之前只有立绘、没有卡面的六张
    'card_family_responsibility.png': 'DonQuixote/images/cards/family_responsibility.png',
    'card_follower_conversion.png': 'DonQuixote/images/cards/follower_conversion.png',
    'card_guard_riposte.png': 'DonQuixote/images/cards/guard_riposte.png',
    'card_reinforce_defense.png': 'DonQuixote/images/cards/reinforce_defense.png',
    'card_hazy_memory.png': 'DonQuixote/images/cards/hazy_memory.png',
    # 骑士长枪体系与新增卡（资源\卡牌 下 2026-11-19 新增的 5 张插画）
    'card_knight_lance.png': 'DonQuixote/images/cards/knight_lance.png',
    'card_blood_feast_lance.png': 'DonQuixote/images/cards/blood_feast_lance.png',
    'card_tactical_technique.png': 'DonQuixote/images/cards/tactical_technique.png',
    'card_endless_hunger.png': 'DonQuixote/images/cards/endless_hunger.png',
    'card_extraordinary_grit.png': 'DonQuixote/images/cards/extraordinary_grit.png',
    'card_listen_to_stories.png': 'DonQuixote/images/cards/listen_to_stories.png',
    'card_adventure_over.png': 'DonQuixote/images/cards/adventure_over.png',
    'card_sancho_idea.png': 'DonQuixote/images/cards/sancho_idea.png',
    # 资源\卡牌 下 2026-11-20 新裁剪的一批插画（11 张）
    'card_rotten_lazy_dream.png': 'DonQuixote/images/cards/rotten_lazy_dream.png',
    'card_knight_dream.png': 'DonQuixote/images/cards/knight_dream.png',
    'card_irresponsible_dream.png': 'DonQuixote/images/cards/irresponsible_dream.png',
    'card_set_out_on_adventure.png': 'DonQuixote/images/cards/set_out_on_adventure.png',
    'card_please_help.png': 'DonQuixote/images/cards/please_help.png',
    'card_look_back_father.png': 'DonQuixote/images/cards/look_back_father.png',
    'card_abandon_all.png': 'DonQuixote/images/cards/abandon_all.png',
    'card_unallowed_expectation.png': 'DonQuixote/images/cards/unallowed_expectation.png',
    'card_all_in_vain.png': 'DonQuixote/images/cards/all_in_vain.png',
    'card_strange_banquet.png': 'DonQuixote/images/cards/strange_banquet.png',
    'card_dreadful_disease.png': 'DonQuixote/images/cards/dreadful_disease.png',
    'card_hard_blood_form2.png': 'DonQuixote/images/cards/hard_blood_form2.png',
    'card_hard_blood_whip10.png': 'DonQuixote/images/cards/hard_blood_whip10.png',
    # 资源\卡牌 下 2026-11-24 新补的 8 张插画
    'card_latent_blood.png': 'DonQuixote/images/cards/latent_blood.png',
    'card_bloodfiend_might.png': 'DonQuixote/images/cards/bloodfiend_might.png',
    'card_freeze_blood_around_us.png': 'DonQuixote/images/cards/freeze_blood_around_us.png',
    'card_hard_blood_teaching.png': 'DonQuixote/images/cards/hard_blood_teaching.png',
    'card_shattered_hard_blood.png': 'DonQuixote/images/cards/shattered_hard_blood.png',
    'card_all_clotted_blood.png': 'DonQuixote/images/cards/all_clotted_blood.png',
    'card_selfish_wish_of_family.png': 'DonQuixote/images/cards/selfish_wish_of_family.png',
    'card_empty_comfort_dream.png': 'DonQuixote/images/cards/empty_comfort_dream.png',
    'card_knightly_protection.png': 'DonQuixote/images/cards/knightly_protection.png',
    'card_blood_sweep.png': 'DonQuixote/images/cards/blood_sweep.png',
    'card_sancho_blood_imitation.png': 'DonQuixote/images/cards/sancho_blood_imitation.png',
    'frame1.png': 'DonQuixote/images/cards/frame1.png',
    'frame2.png': 'DonQuixote/images/cards/frame2.png',
    'energy_icon.png': 'DonQuixote/images/ui/energy_icon.png',
    'energy_text_icon.png': 'DonQuixote/images/ui/energy_text_icon.png',
    # modding 界面「模组信息」面板的封面图：游戏固定读 res://<manifest.id>/mod_image.png
    'mod_image.png': 'DonQuixote/mod_image.png',
    'event_sancho.png': 'DonQuixote/images/events/event_sancho.png',
    'event_dream.png': 'DonQuixote/images/events/event_dream.png',
    'event_duel.png': 'DonQuixote/images/events/event_duel.png',
    'power_wound_destruction.png': 'DonQuixote/images/powers/wound_destruction.png',
    'power_wound_destruction_big.png': 'DonQuixote/images/powers/wound_destruction_big.png',
    'power_blood_recast.png': 'DonQuixote/images/powers/blood_recast.png',
    'power_blood_recast_big.png': 'DonQuixote/images/powers/blood_recast_big.png',
    'power_dreadful_disease.png': 'DonQuixote/images/powers/dreadful_disease.png',
    'power_dreadful_disease_big.png': 'DonQuixote/images/powers/dreadful_disease_big.png',
    'power_feast_total.png': 'DonQuixote/images/powers/feast_total.png',
    'power_feast_total_big.png': 'DonQuixote/images/powers/feast_total_big.png',
    'power_bloodfiend_might.png': 'DonQuixote/images/powers/bloodfiend_might.png',
    'power_bloodfiend_might_big.png': 'DonQuixote/images/powers/bloodfiend_might_big.png',
    'power_eternal_thirst.png': 'DonQuixote/images/powers/eternal_thirst.png',
    'power_eternal_thirst_big.png': 'DonQuixote/images/powers/eternal_thirst_big.png',
    'power_blood_feast_lock.png': 'DonQuixote/images/powers/blood_feast_lock.png',
    'power_blood_feast_lock_big.png': 'DonQuixote/images/powers/blood_feast_lock_big.png',
    # 展现洒落在战场上的潜伏之血 / 好点子形态 的能力图标
    'power_latent_blood.png': 'DonQuixote/images/powers/latent_blood.png',
    'power_latent_blood_big.png': 'DonQuixote/images/powers/latent_blood_big.png',
    'power_good_idea_form.png': 'DonQuixote/images/powers/good_idea_form.png',
    'power_good_idea_form_big.png': 'DonQuixote/images/powers/good_idea_form_big.png',
    # 血魔长老的本能（暂用血魔的本能美术占位）
    'elder_instinct_icon.png': 'DonQuixote/images/relics/elder_instinct_icon.png',
    'elder_instinct_outline.png': 'DonQuixote/images/relics/elder_instinct_outline.png',
    'elder_instinct_big.png': 'DonQuixote/images/relics/elder_instinct_big.png',
    # 蜜拉卡 / 硬血长柄刀 / 撕裂的血袋
    'miraka_icon.png': 'DonQuixote/images/relics/miraka_icon.png',
    'miraka_outline.png': 'DonQuixote/images/relics/miraka_outline.png',
    'miraka_big.png': 'DonQuixote/images/relics/miraka_big.png',
    'hard_blood_halberd_icon.png': 'DonQuixote/images/relics/hard_blood_halberd_icon.png',
    'hard_blood_halberd_outline.png': 'DonQuixote/images/relics/hard_blood_halberd_outline.png',
    'hard_blood_halberd_big.png': 'DonQuixote/images/relics/hard_blood_halberd_big.png',
    'torn_blood_bag_icon.png': 'DonQuixote/images/relics/torn_blood_bag_icon.png',
    'torn_blood_bag_outline.png': 'DonQuixote/images/relics/torn_blood_bag_outline.png',
    'torn_blood_bag_big.png': 'DonQuixote/images/relics/torn_blood_bag_big.png',
    # 破碎四散的硬血 / 所有凝固的血 的能力图标
    'power_shattered_hard_blood.png': 'DonQuixote/images/powers/shattered_hard_blood.png',
    'power_shattered_hard_blood_big.png': 'DonQuixote/images/powers/shattered_hard_blood_big.png',
    'power_all_clotted_blood.png': 'DonQuixote/images/powers/all_clotted_blood.png',
    'power_all_clotted_blood_big.png': 'DonQuixote/images/powers/all_clotted_blood_big.png',
    # 心-勇敢的骑士 / 望 / 援护防御 / 过人的毅力 的能力图标（资源\效果 同名图 64/256 缩放）
    'power_brave_heart.png': 'DonQuixote/images/powers/brave_heart.png',
    'power_brave_heart_big.png': 'DonQuixote/images/powers/brave_heart_big.png',
    'power_hope.png': 'DonQuixote/images/powers/hope.png',
    'power_hope_big.png': 'DonQuixote/images/powers/hope_big.png',
    'power_guard_assist.png': 'DonQuixote/images/powers/guard_assist.png',
    'power_guard_assist_big.png': 'DonQuixote/images/powers/guard_assist_big.png',
    'power_grit.png': 'DonQuixote/images/powers/grit.png',
    'power_grit_big.png': 'DonQuixote/images/powers/grit_big.png',
    # 拉曼却领舞台：两张能力卡的卡面 / 能力图标 / 战场背景
    'card_happy_smile_land.png': 'DonQuixote/images/cards/happy_smile_land.png',
    'card_nightmare_land.png': 'DonQuixote/images/cards/nightmare_land.png',
    'power_happy_smile_land.png': 'DonQuixote/images/powers/happy_smile_land.png',
    'power_happy_smile_land_big.png': 'DonQuixote/images/powers/happy_smile_land_big.png',
    'power_nightmare_land.png': 'DonQuixote/images/powers/nightmare_land.png',
    'power_nightmare_land_big.png': 'DonQuixote/images/powers/nightmare_land_big.png',
    'bg_happy_smile_land.png': 'DonQuixote/images/stage/happy_smile_land.png',
    'bg_nightmare_land.png': 'DonQuixote/images/stage/nightmare_land.png',
    # 白月骑士-巴里（敌怪）：逐帧贴图 + 专属战斗背景 + 能力图标。
    # 帧全部由 tools/barry_frames.py 归一化到同一张 422x546 画布，
    # barry_atk_f0..3 是意图 1 / 4 的挥砍，barry_oath_f0..15 是意图 3 的拉弓。
    'barry_idle.png': 'DonQuixote/images/creatures/barry_idle.png',
    'barry_hurt.png': 'DonQuixote/images/creatures/barry_hurt.png',
    'barry_guard.png': 'DonQuixote/images/creatures/barry_guard.png',
    'barry_die.png': 'DonQuixote/images/creatures/barry_die.png',
    'bg_white_moon.png': 'DonQuixote/images/creatures/bg_white_moon.png',
    'power_white_moon_knight.png': 'DonQuixote/images/powers/white_moon_knight.png',
    'power_white_moon_knight_big.png': 'DonQuixote/images/powers/white_moon_knight_big.png',
    'power_white_moon_duel.png': 'DonQuixote/images/powers/white_moon_duel.png',
    'power_white_moon_duel_big.png': 'DonQuixote/images/powers/white_moon_duel_big.png',
    # 意图 1 / 4 的挥砍帧（4 帧）与意图 3 的拉弓帧（16 帧）
    **{'barry_atk_f%d.png' % i: 'DonQuixote/images/creatures/barry_atk_f%d.png' % i for i in range(4)},
    **{'barry_oath_f%d.png' % i: 'DonQuixote/images/creatures/barry_oath_f%d.png' % i for i in range(16)},
    # 两个先古：地图节点小图标（+ 剪影描边）与事件立绘。
    # 地图图标路径是原版写死的 res://images/packed/map/ancients/ancient_node_<entry>.png，
    # 我们的 PCK 直接提供同名文件即可。
    # 注意：路径里的名字必须是 ModelId.Entry 的小写全称（DON_QUIXOTE_ANCIENT_BARRY），
    # 不是类名去掉前缀的短名 —— 原版是拿 Id.Entry.ToLowerInvariant() 拼的。
    'ancient_barry_node.png': 'images/packed/map/ancients/ancient_node_don_quixote_ancient_barry.png',
    'ancient_barry_node_outline.png': 'images/packed/map/ancients/ancient_node_don_quixote_ancient_barry_outline.png',
    'ancient_samson_node.png': 'images/packed/map/ancients/ancient_node_don_quixote_ancient_samson.png',
    'ancient_samson_node_outline.png': 'images/packed/map/ancients/ancient_node_don_quixote_ancient_samson_outline.png',
    'ancient_barry_portrait.png': 'images/events/don_quixote_ancient_barry.png',
    'ancient_samson_portrait.png': 'images/events/don_quixote_ancient_samson.png',
    # 先古事件界面的大背景（由 tools/ancient_bg.py 生成：2304x1296，
    # 多出来的 20% 是为了抵掉 NAncientBgContainer 的 0.89 缩放，见该脚本注释）
    'ancient_bg_barry.png': 'DonQuixote/images/events/bg_barry.png',
    'ancient_bg_samson.png': 'DonQuixote/images/events/bg_samson.png',
    # 先古的跑团历史小图标（AncientEventModel.RunHistoryIcon 固定读 ui/run_history/<entry>.png）
    'runhistory_barry.png': 'images/ui/run_history/don_quixote_ancient_barry.png',
    'runhistory_barry_outline.png': 'images/ui/run_history/don_quixote_ancient_barry_outline.png',
    'runhistory_samson.png': 'images/ui/run_history/don_quixote_ancient_samson.png',
    'runhistory_samson_outline.png': 'images/ui/run_history/don_quixote_ancient_samson_outline.png',
    # Boss 地图节点图标（EncounterModel.BossNodePath 会再拼 .png / _outline.png）
    'mapnode_white_moon.png': 'images/map/placeholder/don_quixote_white_moon_knight_encounter_icon.png',
    'mapnode_white_moon_outline.png': 'images/map/placeholder/don_quixote_white_moon_knight_encounter_icon_outline.png',
    # Boss 遭遇的跑团历史 / 房间图标（ImageHelper.GetRoomIconPath 用 encounter 的 Entry 拼）
    'runhistory_boss.png': 'images/ui/run_history/don_quixote_white_moon_knight_encounter.png',
    'runhistory_boss_outline.png': 'images/ui/run_history/don_quixote_white_moon_knight_encounter_outline.png',
    # 先古遗物「光荣的决斗」图标
    'glorious_duel_icon.png': 'DonQuixote/images/relics/glorious_duel_icon.png',
    'glorious_duel_outline.png': 'DonQuixote/images/relics/glorious_duel_outline.png',
    'glorious_duel_big.png': 'DonQuixote/images/relics/glorious_duel_big.png',
}
ANIM_DIR = os.path.join(MOD_ROOT, "assets", "animations")
AUDIO_DIR = os.path.join(MOD_ROOT, "assets", "audio")

# 自定义曲子：不经过 Godot 的导入管线，直接把原始 ogg 放进 PCK，
# 运行时用 AudioStreamOggVorbis.LoadFromFile("res://...") 读（见 DonQuixoteStageDirector.cs）。
AUDIO_PLAIN = {
    "lamancha_carnival.ogg": "DonQuixote/audio/lamancha_carnival.ogg",
    "hero_theme.ogg": "DonQuixote/audio/hero_theme.ogg",
}

# spine assets: the atlas page needs the texture import remap, everything else
# is shipped as a plain file that the spine resource loaders read directly.
SPINE_TEXTURES = {
    "don_quixote.png": "DonQuixote/animations/don_quixote.png",
    # 白月骑士-巴里：帧图集（tools/barry_spine.py）
    "barry.png": "DonQuixote/animations/barry.png",
}
SPINE_PLAIN = {
    "don_quixote.atlas": "DonQuixote/animations/don_quixote.atlas",
    "don_quixote.spatlas": "DonQuixote/animations/don_quixote.spatlas",
    "don_quixote.spjson": "DonQuixote/animations/don_quixote.spjson",
    "don_quixote.json": "DonQuixote/animations/don_quixote.json",
    "don_quixote_skel_data.tres": "DonQuixote/animations/don_quixote_skel_data.tres",
    "barry.atlas": "DonQuixote/animations/barry.atlas",
    "barry.spatlas": "DonQuixote/animations/barry.spatlas",
    "barry.spjson": "DonQuixote/animations/barry.spjson",
    "barry.json": "DonQuixote/animations/barry.json",
    "barry_skel_data.tres": "DonQuixote/animations/barry_skel_data.tres",
}

SCENE_RES = "DonQuixote/scenes/creature_visuals/don_quixote.tscn"
SPINE_SCENE_RES = "DonQuixote/scenes/creature_visuals/don_quixote_spine.tscn"
WHITE_MOON_SCENE_RES = "DonQuixote/scenes/creature_visuals/white_moon_knight.tscn"
WHITE_MOON_SPINE_SCENE_RES = "DonQuixote/scenes/creature_visuals/white_moon_knight_spine.tscn"
CHAR_SELECT_BG_RES = "DonQuixote/scenes/char_select_bg.tscn"
CHAR_ICON_SCENE_RES = "DonQuixote/scenes/character_icon.tscn"
MERCHANT_SCENE_RES = "DonQuixote/scenes/merchant/don_quixote_merchant.tscn"
REST_SCENE_RES = "DonQuixote/scenes/rest_site/don_quixote_rest_site.tscn"


def collect_files():
    """Return dict res_path -> bytes."""
    files = {}

    manifest = os.path.join(MOD_ROOT, "DonQuixote.json")
    with open(manifest, "rb") as f:
        files["DonQuixote.json"] = f.read()

    # localization: ONLY below the mod folder, never at the pck root
    loc_root = os.path.join(MOD_ROOT, "localization")
    for lang in sorted(os.listdir(loc_root)):
        lang_dir = os.path.join(loc_root, lang)
        if not os.path.isdir(lang_dir):
            continue
        for name in sorted(os.listdir(lang_dir)):
            if not name.endswith(".json"):
                continue
            with open(os.path.join(lang_dir, name), "rb") as f:
                files["DonQuixote/localization/%s/%s" % (lang, name)] = f.read()

    missing = [n for n in TEXTURES if not os.path.exists(os.path.join(ART, n))]
    if missing:
        raise SystemExit("missing art: %s (run make_art.py)" % missing)
    missing = [n for n in SPINE_TEXTURES if not os.path.exists(os.path.join(ANIM_DIR, n))]
    if missing:
        raise SystemExit("missing spine art: %s (run make_spine.py)" % missing)
    missing = [n for n in SPINE_PLAIN if not os.path.exists(os.path.join(ANIM_DIR, n))]
    if missing:
        raise SystemExit("missing spine file: %s (run make_spine.py)" % missing)

    texture_sources = [(os.path.join(ART, n), res) for n, res in TEXTURES.items()]
    texture_sources += [(os.path.join(ANIM_DIR, n), res) for n, res in SPINE_TEXTURES.items()]
    for src, res in texture_sources:
        with open(src, "rb") as f:
            raw = f.read()
        files[res] = raw
        digest = hashlib.md5(raw).hexdigest()
        ctex_rel = ".godot/imported/%s-%s.ctex" % (os.path.basename(res), digest)
        files[ctex_rel] = make_ctex(src)
        files[res + ".import"] = make_import(res, ctex_rel, uid_for(res))

    for name, res in SPINE_PLAIN.items():
        with open(os.path.join(ANIM_DIR, name), "rb") as f:
            files[res] = f.read()

    missing = [n for n in AUDIO_PLAIN if not os.path.exists(os.path.join(AUDIO_DIR, n))]
    if missing:
        raise SystemExit("missing audio: %s" % missing)

    for name, res in AUDIO_PLAIN.items():
        with open(os.path.join(AUDIO_DIR, name), "rb") as f:
            files[res] = f.read()

    with open(os.path.join(SCENES, "don_quixote_visuals.tscn"), "r", encoding="utf-8") as f:
        scene = f.read()
    scene = scene.replace("{{UID:scene}}", uid_for(SCENE_RES))
    scene = scene.replace("{{UID:idle}}", uid_for(TEXTURES["idle.png"]))
    files[SCENE_RES] = scene.encode("utf-8")

    with open(os.path.join(SCENES, "don_quixote_spine.tscn"), "r", encoding="utf-8") as f:
        spine_scene = f.read()
    spine_scene = spine_scene.replace("{{UID:scene_spine}}", uid_for(SPINE_SCENE_RES))
    spine_scene = spine_scene.replace(
        "{{UID:skel}}", uid_for("DonQuixote/animations/don_quixote_skel_data.tres")
    )
    files[SPINE_SCENE_RES] = spine_scene.encode("utf-8")

    # 白月骑士-巴里：spine 版视觉场景（骨架 barry_skel_data.tres）。
    with open(os.path.join(SCENES, "white_moon_knight_spine.tscn"), "r", encoding="utf-8") as f:
        white_moon_spine = f.read()
    white_moon_spine = white_moon_spine.replace(
        "{{UID:scene}}", uid_for(WHITE_MOON_SPINE_SCENE_RES)
    )
    white_moon_spine = white_moon_spine.replace(
        "{{UID:skel}}", uid_for("DonQuixote/animations/barry_skel_data.tres")
    )
    files[WHITE_MOON_SPINE_SCENE_RES] = white_moon_spine.encode("utf-8")

    # 白月骑士-巴里：逐帧贴图版视觉场景（Sprite2D，没有 spine）。
    # 现在默认走上面的 spine 版；这份留作回滚用（把 VisualsPath 改回来即可）。
    with open(os.path.join(SCENES, "white_moon_knight_visuals.tscn"), "r", encoding="utf-8") as f:
        white_moon_scene = f.read()
    white_moon_scene = white_moon_scene.replace(
        "{{UID:scene}}", uid_for(WHITE_MOON_SCENE_RES)
    )
    white_moon_scene = white_moon_scene.replace(
        "{{UID:idle}}", uid_for(TEXTURES["barry_idle.png"])
    )
    files[WHITE_MOON_SCENE_RES] = white_moon_scene.encode("utf-8")

    # 两个先古的事件背景场景（EventModel 在 EventLayoutType.Ancient 下必须能取到
    # res://scenes/events/background_scenes/<entry>.tscn，否则加载失败）。
    for src_name, res, tex_key in [
        ("ancient_bg_barry.tscn", "scenes/events/background_scenes/don_quixote_ancient_barry.tscn",
         "ancient_bg_barry.png"),
        ("ancient_bg_samson.tscn", "scenes/events/background_scenes/don_quixote_ancient_samson.tscn",
         "ancient_bg_samson.png"),
    ]:
        with open(os.path.join(SCENES, src_name), "r", encoding="utf-8") as f:
            ancient_bg = f.read()
        ancient_bg = ancient_bg.replace("{{UID:scene}}", uid_for(res))
        ancient_bg = ancient_bg.replace("{{UID:bg}}", uid_for(TEXTURES[tex_key]))
        files[res] = ancient_bg.encode("utf-8")

    # character select background: the game loads CharacterSelectBg as a scene,
    # so ship a tiny Control scene instead of the bare texture.
    with open(os.path.join(SCENES, "char_select_bg.tscn"), "r", encoding="utf-8") as f:
        bg_scene = f.read()
    bg_scene = bg_scene.replace("{{UID:bg_scene}}", uid_for(CHAR_SELECT_BG_RES))
    bg_scene = bg_scene.replace(
        "{{UID:bg_texture}}", uid_for(TEXTURES["char_select_bg.png"])
    )
    files[CHAR_SELECT_BG_RES] = bg_scene.encode("utf-8")

    # top bar portrait: CharacterModel.Icon instantiates this scene
    with open(os.path.join(SCENES, "character_icon.tscn"), "r", encoding="utf-8") as f:
        icon_scene = f.read()
    icon_scene = icon_scene.replace("{{UID:char_icon_scene}}", uid_for(CHAR_ICON_SCENE_RES))
    icon_scene = icon_scene.replace(
        "{{UID:char_icon_tex}}", uid_for(TEXTURES["icon.png"])
    )
    files[CHAR_ICON_SCENE_RES] = icon_scene.encode("utf-8")

    # merchant / rest site: the game loads these as scenes, so ship scenes that
    # reuse our spine rig with its relaxed (idle) loop.
    skel_uid = uid_for("DonQuixote/animations/don_quixote_skel_data.tres")

    with open(os.path.join(SCENES, "don_quixote_merchant.tscn"), "r", encoding="utf-8") as f:
        merchant_scene = f.read()
    merchant_scene = merchant_scene.replace("{{UID:merchant_scene}}", uid_for(MERCHANT_SCENE_RES))
    merchant_scene = merchant_scene.replace("{{UID:skel}}", skel_uid)
    files[MERCHANT_SCENE_RES] = merchant_scene.encode("utf-8")

    with open(os.path.join(SCENES, "don_quixote_rest_site.tscn"), "r", encoding="utf-8") as f:
        rest_scene = f.read()
    rest_scene = rest_scene.replace("{{UID:rest_scene}}", uid_for(REST_SCENE_RES))
    rest_scene = rest_scene.replace("{{UID:skel}}", skel_uid)
    files[REST_SCENE_RES] = rest_scene.encode("utf-8")

    return files


def read_template_header():
    with open(TEMPLATE_PCK, "rb") as f:
        head = f.read(112)
    if head[0:4] != b"GDPC":
        raise SystemExit("template pck is not GDPC")
    return head


def build(files):
    header = bytearray(read_template_header())
    file_base = 112
    struct.pack_into("<Q", header, 0x18, file_base)
    struct.pack_into("<Q", header, 0x20, 0)  # patched later
    print(
        "template flags=0x%x ver=%d"
        % (
            struct.unpack_from("<I", header, 0x14)[0],
            struct.unpack_from("<I", header, 0x04)[0],
        )
    )

    body = bytearray()
    entries = []
    for res in sorted(files):
        data = files[res]
        offset = file_base + len(body)
        body += data
        entries.append((res, offset - file_base, len(data), hashlib.md5(data).digest(), 0))

    dir_offset = file_base + len(body)
    directory = bytearray()
    directory += struct.pack("<I", len(entries))
    for res, off, size, md5, flags in entries:
        path = res.encode("utf-8")
        pad = (4 - (len(path) % 4)) % 4
        path_padded = path + b"\x00" * pad
        directory += struct.pack("<I", len(path_padded))
        directory += path_padded
        directory += struct.pack("<Q", off)
        directory += struct.pack("<Q", size)
        directory += md5
        directory += struct.pack("<I", flags)

    struct.pack_into("<Q", header, 0x20, dir_offset)

    os.makedirs(os.path.dirname(OUT_PCK), exist_ok=True)
    with open(OUT_PCK, "wb") as f:
        f.write(bytes(header))
        f.write(bytes(body))
        f.write(bytes(directory))
    print("wrote %s entries=%d size=%d" % (OUT_PCK, len(entries), os.path.getsize(OUT_PCK)))


if __name__ == "__main__":
    build(collect_files())
