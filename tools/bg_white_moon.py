# -*- coding: utf-8 -*-
"""白月骑士战斗背景高清化。

原图 `资源\\人物\\白月骑士战斗场景.png` 只有 900x506，直接丢给游戏的话，
全屏 TextureRect（KeepAspectCovered）会在 2560 宽的屏幕上把它拉到 ~2.9 倍，
等于「引擎再插值一次」，糊得厉害。

这里先自己放大到 3072x1728（= 1.2 x 2560x1600，和先古背景同一套规格），
用 LANCZOS4 + 反锐化 + 局部对比增强，把「一次插值」的损失做到最小。
原图的纵横比 1.778 和 16:9 一致，所以不需要裁切。
"""
import os
import cv2
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.dirname(ROOT), "资源", "人物", "白月骑士战斗场景.png")
OUT = os.path.join(ROOT, "assets", "art", "bg_white_moon.png")

W, H = 3072, 1728


def main():
    img = cv2.imdecode(np.fromfile(SRC, np.uint8), cv2.IMREAD_UNCHANGED)
    if img.shape[2] == 3:
        img = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
    h, w = img.shape[:2]
    print("src %dx%d" % (w, h))

    # 等比 cover（宽高比几乎一致，等价于直接缩放）
    s = max(W / w, H / h)
    nw, nh = int(round(w * s)), int(round(h * s))
    big = cv2.resize(img, (nw, nh), interpolation=cv2.INTER_LANCZOS4)
    x0 = (nw - W) // 2
    y0 = (nh - H) // 2
    big = big[y0:y0 + H, x0:x0 + W]

    # 反锐化：先压掉放大带来的糊，再稍微提一点局部对比。
    # 强度刻意保守 —— 加太猛会在拱顶/柱子边缘拉出白边。
    bgr = big[:, :, :3].astype(np.uint8)
    blur = cv2.GaussianBlur(bgr, (0, 0), 1.6)
    sharp = cv2.addWeighted(bgr, 1.3, blur, -0.3, 0)
    try:
        sharp = cv2.detailEnhance(sharp, sigma_s=8, sigma_r=0.18)
    except Exception as exc:  # detailEnhance 属于 contrib，缺了就只用反锐化
        print("detailEnhance 不可用：", exc)

    out = cv2.cvtColor(sharp, cv2.COLOR_BGR2BGRA)
    out[:, :, 3] = big[:, :, 3]
    cv2.imencode(".png", out, [cv2.IMWRITE_PNG_COMPRESSION, 9])[1].tofile(OUT)
    print("wrote %s %dx%d %.0fKB" % (OUT, out.shape[1], out.shape[0], os.path.getsize(OUT) / 1024))


if __name__ == "__main__":
    main()
