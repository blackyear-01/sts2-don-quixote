import sys
from collections import Counter

sys.path.insert(0, r"D:\sts2mod\tools")
import pck

data = pck.load(r"D:\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck")
d = pck.parse_v3(data)
paths = [e["path"] for e in d["entries"]]
anim = [p for p in paths if p.startswith("animations/")]
print("anim files", len(anim))
print(Counter(p.rsplit(".", 1)[-1] for p in anim if "." in p))
print("ironclad:", [p for p in anim if "ironclad/" in p])
print("any json skeletons:", [p for p in anim if p.endswith(".json")][:20])
