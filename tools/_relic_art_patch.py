# -*- coding: utf-8 -*-
"""一次性脚本：接入飞天而奔月 / 此去永相别 的新贴图，并把家人系列改回 png。"""
import io
import os

DonQuixote_tools = r'D:\sts2mod\Don Quixote\tools'

DonQuixote_make_path = os.path.join(DonQuixote_tools, 'make_art.py')
with io.open(DonQuixote_make_path, 'r', encoding='utf-8') as DonQuixote_f:
    DonQuixote_make = DonQuixote_f.read()

DonQuixote_make = DonQuixote_make.replace("'二代眷属-桑丘', 'jpg'", "'二代眷属-桑丘', 'png'")
DonQuixote_make = DonQuixote_make.replace("'二代眷属-杜尔西内亚', 'jpg'", "'二代眷属-杜尔西内亚', 'png'")
DonQuixote_make = DonQuixote_make.replace("'三代眷属-古良布洛', 'jpg'", "'三代眷属-古良布洛', 'png'")
DonQuixote_make = DonQuixote_make.replace("'三代眷属-尼古莉娜', 'jpg'", "'三代眷属-尼古莉娜', 'png'")
DonQuixote_make = DonQuixote_make.replace(
    '# 追加：事件遗物贴图（家人系列为 jpg，胜利之证为 png）',
    '# 追加：事件遗物贴图（全部为 png）')

DonQuixote_old_start = DonQuixote_make.index('# 飞天若奔月 / 此去永相别 暂无专属贴图')
DonQuixote_old_end = DonQuixote_make.index('# 决斗宣告卡面')
DonQuixote_new_block = u'''# 飞天而奔月 / 此去永相别：使用 D:\\sts2mod\\资源\\遗物 里的专属三张图
for DonQuixote_key, DonQuixote_name in (
    ('moon_flight', '飞天而奔月'),
    ('farewell', '此去永相别'),
):
    save(fit(DonQuixote_strip_white(Image.open(
        os.path.join(DonQuixote_relic_src, DonQuixote_name + '-图标.png')).convert('RGBA')), RELIC_ICON_SIZE),
        DonQuixote_key + '_icon.png')
    save(fit(DonQuixote_strip_white(Image.open(
        os.path.join(DonQuixote_relic_src, DonQuixote_name + '-描边.png')).convert('RGBA')), RELIC_ICON_SIZE),
        DonQuixote_key + '_outline.png')
    save(fit(DonQuixote_strip_white(Image.open(
        os.path.join(DonQuixote_relic_src, DonQuixote_name + '-大图.png')).convert('RGBA')), (256, 256)),
        DonQuixote_key + '_big.png')

'''
DonQuixote_make = DonQuixote_make[:DonQuixote_old_start] + DonQuixote_new_block + DonQuixote_make[DonQuixote_old_end:]

with io.open(DonQuixote_make_path, 'w', encoding='utf-8', newline='') as DonQuixote_f:
    DonQuixote_f.write(DonQuixote_make)
print('make_art.py updated')

DonQuixote_loc_path = os.path.join(DonQuixote_tools, 'gen_loc.py')
with io.open(DonQuixote_loc_path, 'r', encoding='utf-8') as DonQuixote_f:
    DonQuixote_loc = DonQuixote_f.read()

DonQuixote_before = DonQuixote_loc
DonQuixote_loc = DonQuixote_loc.replace('"飞天若奔月"', '"飞天而奔月"')
if DonQuixote_loc == DonQuixote_before:
    print('gen_loc.py: no title replacement needed')
else:
    with io.open(DonQuixote_loc_path, 'w', encoding='utf-8', newline='') as DonQuixote_f:
        DonQuixote_f.write(DonQuixote_loc)
    print('gen_loc.py: title renamed to 飞天而奔月')
