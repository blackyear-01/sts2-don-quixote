# -*- coding: utf-8 -*-
"""先古事件背景图生成。

原版 `NAncientBgContainer` 在 16:9 下会把内容整体缩到 0.89 并下移 40px
（见 MegaCrit.Sts2.Core.Nodes.Screens.NAncientBgContainer.OnWindowChange），
所以背景图必须比容器大一圈才能真的铺满屏幕 —— 否则四周会露出黑边。

这里按 1.2 倍的出血量输出（3072x1728 = 2560x1600 * 1.2），
配合 tscn 里 anchor 取 -0.10 / 1.10，屏幕（2560x1600）上看到的就是 1:1 像素 ——
容器在该比例下会被缩到 0.934 并左移 56px、下移 68px，1.2 倍刚好盖住。

为什么用 3072 而不是 1920 系列：图片如果比屏幕小，Godot 会再放大一次，
等于「源图放大 + 引擎放大」两层插值，糊两遍。直接出到 1.2 倍屏宽就只有源图那一次。

构图策略：原图纵横比不一定是 16:9，直接拉伸会变形、直接裁切会切掉角色。
所以统一用「同图高斯模糊铺底 + 原图等比放大做前景」，接缝处做羽化。
"""
import os
import cv2
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.dirname(ROOT), "资源", "先古")
OUT = os.path.join(ROOT, "assets", "art")

W, H = 3072, 1728


def _load(path):
    data = np.fromfile(path, dtype=np.uint8)
    img = cv2.imdecode(data, cv2.IMREAD_UNCHANGED)
    if img is None:
        raise RuntimeError("读取失败: " + path)
    if img.shape[2] == 3:
        img = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
    return img


def _sharp_resize(img, w, h):
    """LANCZOS4 缩放 + 轻度反锐化，缓解放大后的发糊。"""
    out = cv2.resize(img, (w, h), interpolation=cv2.INTER_LANCZOS4)
    blur = cv2.GaussianBlur(out, (0, 0), 1.1)
    out = cv2.addWeighted(out, 1.5, blur, -0.5, 0)
    return np.clip(out, 0, 255).astype(np.uint8)


def _cover(img, w, h, top_bias=0.5):
    ih, iw = img.shape[:2]
    scale = max(w / iw, h / ih)
    nw, nh = int(round(iw * scale)), int(round(ih * scale))
    resized = cv2.resize(img, (nw, nh), interpolation=cv2.INTER_LANCZOS4)
    x = (nw - w) // 2
    y = int(round((nh - h) * top_bias))
    return resized[y:y + h, x:x + w].copy()


def _paste_feathered(base, fg, x, y, feather=110):
    """把 fg 贴到 base 上，左右边缘做线性羽化，避免硬边。"""
    fh, fw = fg.shape[:2]
    alpha = fg[:, :, 3].astype(np.float32) / 255.0
    mask = np.ones(fw, dtype=np.float32)
    n = min(feather, fw // 3)
    if n > 0:
        ramp = np.linspace(0.0, 1.0, n, dtype=np.float32)
        mask[:n] = ramp
        mask[fw - n:] = ramp[::-1]
    mask2d = np.repeat(mask[None, :], fh, axis=0) * alpha
    roi = base[y:y + fh, x:x + fw].astype(np.float32)
    fgc = fg[:, :, :3].astype(np.float32)
    out = roi * (1 - mask2d[:, :, None]) + fgc * mask2d[:, :, None]
    base[y:y + fh, x:x + fw] = np.clip(out, 0, 255).astype(np.uint8)
    return base


def _backdrop(img, top_bias=0.5, blur=34, dim=0.42, sat=0.85):
    bg = _cover(img, W, H, top_bias).astype(np.float32)
    bg = cv2.GaussianBlur(bg, (0, 0), blur)
    hsv = cv2.cvtColor(np.clip(bg, 0, 255).astype(np.uint8), cv2.COLOR_BGR2HSV).astype(np.float32)
    hsv[:, :, 1] *= sat
    bg = cv2.cvtColor(np.clip(hsv, 0, 255).astype(np.uint8), cv2.COLOR_HSV2BGR).astype(np.float32)
    bg *= (1.0 - dim)
    return np.clip(bg, 0, 255).astype(np.uint8)


def _vignette(canvas, strength=0.35):
    h, w = canvas.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cx, cy = w / 2.0, h / 2.0
    d = np.sqrt(((xx - cx) / cx) ** 2 + ((yy - cy) / cy) ** 2) / 1.4142
    mask = np.clip(1.0 - strength * d ** 2, 0, 1)[:, :, None]
    return np.clip(canvas.astype(np.float32) * mask, 0, 255).astype(np.uint8)


def barry():
    """巴里：竖构图立绘（1350x1800）。

    原图是竖的，做全屏背景只能 cover 裁切。取景偏上（top_bias=0.22），
    这样头部落在上三分之一、视线略向下，像一张真正的横版立绘。
    最后只压一层很淡的暗角，保持画面亮度。
    """
    src = _load(os.path.join(SRC, "白月骑士-巴里.jpg"))
    canvas = _cover(src, W, H, top_bias=0.22)
    canvas = _sharp_resize(canvas, W, H)
    return _vignette(canvas, 0.26)


def samson():
    """参孙：横构图（828x523），按高度贴合，两侧用模糊底补满。"""
    src = _load(os.path.join(SRC, "参孙？？？.png"))
    if src.shape[2] == 4:
        # PNG 带 alpha：把它压到模糊底上，避免透明处露黑
        bg = _backdrop(src, top_bias=0.5, blur=42, dim=0.52, sat=0.9)
        canvas = bg
    else:
        canvas = _backdrop(src, top_bias=0.5, blur=42, dim=0.52, sat=0.9)

    ih, iw = src.shape[:2]
    fh = H
    fw = int(round(iw * (fh / ih)))
    fg = _sharp_resize(src, fw, fh)
    x = (W - fw) // 2
    canvas = _paste_feathered(canvas, fg, max(0, x), 0, feather=120)
    return _vignette(canvas, 0.28)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in [("ancient_bg_barry.png", barry), ("ancient_bg_samson.png", samson)]:
        img = fn()
        path = os.path.join(OUT, name)
        ok = cv2.imencode(".png", img, [cv2.IMWRITE_PNG_COMPRESSION, 9])[1]
        ok.tofile(path)
        print("%s %dx%d %.0fKB" % (name, img.shape[1], img.shape[0], os.path.getsize(path) / 1024))


if __name__ == "__main__":
    main()
