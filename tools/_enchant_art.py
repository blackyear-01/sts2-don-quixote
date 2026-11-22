# -*- coding: utf-8 -*-
import io
import os

from PIL import Image

DonQuixote_MOD = r'D:\sts2mod\Don Quixote'
DonQuixote_art = os.path.join(DonQuixote_MOD, 'assets', 'art')
DonQuixote_src = r'D:\sts2mod\资源\效果\不可摧毁的硬币.png'

DonQuixote_img = Image.open(DonQuixote_src).convert('RGBA')
DonQuixote_side = max(DonQuixote_img.size)
DonQuixote_square = Image.new('RGBA', (DonQuixote_side, DonQuixote_side), (0, 0, 0, 0))
DonQuixote_square.paste(
    DonQuixote_img,
    ((DonQuixote_side - DonQuixote_img.width) // 2, (DonQuixote_side - DonQuixote_img.height) // 2))
DonQuixote_out = DonQuixote_square.resize((64, 64), Image.LANCZOS)
DonQuixote_out.save(os.path.join(DonQuixote_art, 'enchantment_coin.png'))
print('enchantment_coin', DonQuixote_out.size)

DonQuixote_pck_path = os.path.join(DonQuixote_MOD, 'tools', 'build_pck.py')
with io.open(DonQuixote_pck_path, 'r', encoding='utf-8') as DonQuixote_f:
    DonQuixote_pck = DonQuixote_f.read()

DonQuixote_anchor = 'TEXTURES = {\n'
DonQuixote_line = '    "enchantment_coin.png": "DonQuixote/images/enchantments/indestructible_coin.png",\n'
if 'enchantment_coin.png' not in DonQuixote_pck:
    DonQuixote_pck = DonQuixote_pck.replace(DonQuixote_anchor, DonQuixote_anchor + DonQuixote_line, 1)
    with io.open(DonQuixote_pck_path, 'w', encoding='utf-8', newline='') as DonQuixote_f:
        DonQuixote_f.write(DonQuixote_pck)
    print('build_pck: texture entry added')
else:
    print('build_pck: texture entry already present')

DonQuixote_make_path = os.path.join(DonQuixote_MOD, 'tools', 'make_art.py')
with io.open(DonQuixote_make_path, 'r', encoding='utf-8') as DonQuixote_f:
    DonQuixote_make = DonQuixote_f.read()

DonQuixote_block = u'''
# 附魔「不可摧毁卡牌」图标：使用 D:\\sts2mod\\资源\\效果\\不可摧毁的硬币.png
DonQuixote_coin = Image.open(os.path.join(DonQuixote_power_src, '不可摧毁的硬币.png')).convert('RGBA')
DonQuixote_coin_side = max(DonQuixote_coin.size)
DonQuixote_coin_square = Image.new('RGBA', (DonQuixote_coin_side, DonQuixote_coin_side), (0, 0, 0, 0))
DonQuixote_coin_square.paste(
    DonQuixote_coin,
    ((DonQuixote_coin_side - DonQuixote_coin.width) // 2,
     (DonQuixote_coin_side - DonQuixote_coin.height) // 2))
save(DonQuixote_coin_square.resize((64, 64), Image.LANCZOS), 'enchantment_coin.png')
'''
if 'enchantment_coin.png' not in DonQuixote_make:
    DonQuixote_make = DonQuixote_make.rstrip() + '\n' + DonQuixote_block
    with io.open(DonQuixote_make_path, 'w', encoding='utf-8', newline='') as DonQuixote_f:
        DonQuixote_f.write(DonQuixote_make)
    print('make_art: block appended')
else:
    print('make_art: block already present')
