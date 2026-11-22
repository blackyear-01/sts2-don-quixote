# -*- coding: utf-8 -*-
"""修正上一步正则造成的括号错位。"""
import io
import os
import re

DonQuixote_path = os.path.join(r'D:\sts2mod\Don Quixote', 'tools', 'make_art.py')
with io.open(DonQuixote_path, 'r', encoding='utf-8') as DonQuixote_f:
    DonQuixote_text = DonQuixote_f.read()

DonQuixote_pattern = re.compile(
    r'DonQuixote_strip_white\(Image\.open\((.*?)\)\)\.convert\("RGBA"\)\)',
    re.S)
DonQuixote_text, DonQuixote_count = DonQuixote_pattern.subn(
    lambda DonQuixote_m: 'DonQuixote_strip_white(Image.open(%s).convert("RGBA"))' % DonQuixote_m.group(1),
    DonQuixote_text)
print('fixed:', DonQuixote_count)

with io.open(DonQuixote_path, 'w', encoding='utf-8', newline='') as DonQuixote_f:
    DonQuixote_f.write(DonQuixote_text)
