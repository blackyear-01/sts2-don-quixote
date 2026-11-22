import sys

sys.path.insert(0, r"D:\sts2mod\tools")
import pck

data = pck.load(r"D:\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck")
d = pck.parse_v3(data)
paths = [e["path"] for e in d["entries"]]
for prefix in ("scenes/combat/energy_counters/", "scenes/rest_site/characters/", "scenes/merchant/characters/"):
    print("==", prefix)
    for p in paths:
        if p.startswith(prefix):
            print("  ", p)
