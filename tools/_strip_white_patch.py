# -*- coding: utf-8 -*-
"""一次性脚本：给 make_art.py 加上「去除遗物贴图白色背景」逻辑。"""
import io
import os
import re

DonQuixote_path = os.path.join(r'D:\sts2mod\Don Quixote', 'tools', 'make_art.py')
with io.open(DonQuixote_path, 'r', encoding='utf-8') as DonQuixote_f:
    DonQuixote_text = DonQuixote_f.read()

DonQuixote_helper = u'''
def DonQuixote_strip_white(img: Image.Image, DonQuixote_threshold: int = 234) -> Image.Image:
    """把与图片边缘相连的近白色像素抠成透明，用于 jpg 遗物贴图去白底。"""
    from collections import deque

    DonQuixote_img = img.convert("RGBA")
    DonQuixote_w, DonQuixote_h = DonQuixote_img.size
    DonQuixote_px = DonQuixote_img.load()
    DonQuixote_seen = bytearray(DonQuixote_w * DonQuixote_h)

    def DonQuixote_is_white(DonQuixote_p):
        return (DonQuixote_p[0] >= DonQuixote_threshold
                and DonQuixote_p[1] >= DonQuixote_threshold
                and DonQuixote_p[2] >= DonQuixote_threshold)

    DonQuixote_queue = deque()
    for DonQuixote_x in range(DonQuixote_w):
        DonQuixote_queue.append((DonQuixote_x, 0))
        DonQuixote_queue.append((DonQuixote_x, DonQuixote_h - 1))
    for DonQuixote_y in range(DonQuixote_h):
        DonQuixote_queue.append((0, DonQuixote_y))
        DonQuixote_queue.append((DonQuixote_w - 1, DonQuixote_y))

    DonQuixote_hit = 0
    while DonQuixote_queue:
        DonQuixote_x, DonQuixote_y = DonQuixote_queue.popleft()
        if DonQuixote_x < 0 or DonQuixote_y < 0 or DonQuixote_x >= DonQuixote_w or DonQuixote_y >= DonQuixote_h:
            continue
        DonQuixote_idx = DonQuixote_y * DonQuixote_w + DonQuixote_x
        if DonQuixote_seen[DonQuixote_idx]:
            continue
        DonQuixote_seen[DonQuixote_idx] = 1
        DonQuixote_p = DonQuixote_px[DonQuixote_x, DonQuixote_y]
        if DonQuixote_p[3] == 0 or not DonQuixote_is_white(DonQuixote_p):
            continue
        DonQuixote_px[DonQuixote_x, DonQuixote_y] = (DonQuixote_p[0], DonQuixote_p[1], DonQuixote_p[2], 0)
        DonQuixote_hit += 1
        DonQuixote_queue.append((DonQuixote_x + 1, DonQuixote_y))
        DonQuixote_queue.append((DonQuixote_x - 1, DonQuixote_y))
        DonQuixote_queue.append((DonQuixote_x, DonQuixote_y + 1))
        DonQuixote_queue.append((DonQuixote_x, DonQuixote_y - 1))

    print("strip_white: %d px -> %d cleared" % (DonQuixote_w * DonQuixote_h, DonQuixote_hit))
    return DonQuixote_img


'''

if 'DonQuixote_strip_white(' not in DonQuixote_text:
    DonQuixote_text = DonQuixote_text.replace(
        '# 血液结晶（稀有遗物）',
        DonQuixote_helper.lstrip('\n') + '# 血液结晶（稀有遗物）',
        1)

DonQuixote_pattern = re.compile(
    r'Image\.open\(\s*(os\.path\.join\(DonQuixote_relic_src,.*?\))\.convert\((["\'])RGBA\2\)',
    re.S)


def DonQuixote_wrap(DonQuixote_match):
    return 'DonQuixote_strip_white(Image.open(%s).convert("RGBA"))' % DonQuixote_match.group(1)


DonQuixote_text, DonQuixote_count = DonQuixote_pattern.subn(DonQuixote_wrap, DonQuixote_text)
print('wrapped relic opens:', DonQuixote_count)

with io.open(DonQuixote_path, 'w', encoding='utf-8', newline='') as DonQuixote_f:
    DonQuixote_f.write(DonQuixote_text)
print('make_art.py patched')
