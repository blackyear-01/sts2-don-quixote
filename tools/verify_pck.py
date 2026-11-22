import os
import sys

sys.path.insert(0, r"D:\sts2mod\tools")
import pck

PCK = r"D:\sts2mod\Don Quixote\build\DonQuixote.pck"
data = pck.load(PCK)
d = pck.parse_v3(data)
entries = d["entries"]
paths = [e["path"] for e in entries]
print("entries", len(paths), "dir_end==size", d["dir_end"] == len(data), len(data))
print("root localization leaks:", [p for p in paths if p.startswith("localization/")])
print("mod localization:", [p for p in paths if p.startswith("DonQuixote/localization/")])
print("imports:", len([p for p in paths if p.endswith(".import")]),
      "ctex:", len([p for p in paths if p.endswith(".ctex")]))
for e in entries:
    if e["path"].endswith(".tscn"):
        raw = data[e["offset"]:e["offset"] + e["size"]]
        print(raw.decode("utf-8")[:500])
