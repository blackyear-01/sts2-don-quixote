# -*- coding: utf-8 -*-
"""一次性脚本：为 gen_loc.py 增加 enchantments 表与「不可摧毁卡牌」条目。"""
import io
import os

DonQuixote_path = os.path.join(r'D:\sts2mod\Don Quixote', 'tools', 'gen_loc.py')
with io.open(DonQuixote_path, 'r', encoding='utf-8') as DonQuixote_f:
    DonQuixote_text = DonQuixote_f.read()

if 'enchantments' in DonQuixote_text:
    print('already patched')
    raise SystemExit(0)

DonQuixote_anchor = '        write_table(lang, "powers", powers)\n'
DonQuixote_block = u'''
        enchantments = {}
        for entry in DON_QUIXOTE_ENCHANTMENT_IDS:
            enchantments[entry + ".title"] = DonQuixote_sanitize(
                "不可摧毁卡牌" if zhs else "Indestructible Card")
            enchantments[entry + ".description"] = DonQuixote_sanitize(
                "此卡被消耗时，自动打出并进入弃牌堆，不会进入消耗牌堆，但是造成的伤害-80%"
                if zhs else
                "When this card would be Exhausted, it is auto-played and put into your discard pile instead, "
                "but it deals 80% less damage.")
            enchantments[entry + ".extraCardText"] = DonQuixote_sanitize(
                "不可摧毁卡牌" if zhs else "Indestructible Card")
        write_table(lang, "enchantments", enchantments)
'''
DonQuixote_text = DonQuixote_text.replace(
    DonQuixote_anchor,
    DonQuixote_anchor + DonQuixote_block,
    1)

DonQuixote_ids = u'''DON_QUIXOTE_ENCHANTMENT_IDS = (
    "DON_QUIXOTE_ENCHANTMENT_DON_QUIXOTE_INDESTRUCTIBLE_CARD",
    "DON_QUIXOTE_ENCHANTMENT_INDESTRUCTIBLE_CARD",
)


'''
DonQuixote_text = DonQuixote_text.replace('DP = "DON_QUIXOTE_POWER_"', DonQuixote_ids + 'DP = "DON_QUIXOTE_POWER_"', 1)

with io.open(DonQuixote_path, 'w', encoding='utf-8', newline='') as DonQuixote_f:
    DonQuixote_f.write(DonQuixote_text)
print('gen_loc.py patched')
