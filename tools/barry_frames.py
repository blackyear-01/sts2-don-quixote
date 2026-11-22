# -*- coding: utf-8 -*-
"""白月骑士-巴里 逐帧动画素材生成。

原素材在 `资源\\敌怪`：
  - 白月骑士-闲置.webp (220x270)      静态
  - 白月骑士-防御.webp (219x270)      静态
  - 白月骑士-受伤.webp (211x270)      静态
  - 白月骑士-意图1、4.gif (232x270, 4 帧)
  - 白月骑士-意图3.gif   (197x269, 16 帧)

坑：两段 GIF 里的角色比静态图小一圈（约 0.56~0.58 倍），画布留白也不一样，
直接换贴图会让角色在切动作时突然变小 / 跳位。这里统一做三件事：
  1. 按「头的大小」把两段 GIF 放大回和闲置图一样的比例；
  2. 按「脸中心 + 脚底」定位，让所有动作踩在同一条地平线上；
  3. 输出到同一张画布：脸中心在画布水平中线，地平线在画布固定高度。

输出（末尾数字是帧号）：
  barry_idle.png / barry_guard.png / barry_hurt.png
  barry_atk_f0..f3.png      —— 意图 1、4 的攻击
  barry_oath_f0..f15.png    —— 意图 3 拉弓（0..9 举弓瞄准 / 10..15 松弦收势）
"""
import os
import cv2
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.dirname(ROOT), "资源", "敌怪")
OUT = os.path.join(ROOT, "assets", "art")

# (源文件名, 放大系数, 脸中心 x, 脸中心 y, 脸中心→脚底的距离, tag)
# 后三个都是**源图坐标**，放大系数会一起乘上去。
SOURCES = [
    ("白月骑士-闲置.webp", 1.0, 109.0, 72.0, 197.0, "idle"),
    ("白月骑士-防御.webp", 1.0, 123.0, 75.0, 197.0, "guard"),
    ("白月骑士-受伤.webp", 1.0, 124.5, 72.0, 197.0, "hurt"),
    ("白月骑士-意图1、4.gif", 1.0 / 0.58, 113.0, 117.0, 113.0, "atk"),
    ("白月骑士-意图3.gif", 1.0 / 0.56, 109.0, 152.0, 108.0, "oath"),
]

PAD_TOP = 6
PAD_BOTTOM = 6
PAD_SIDE = 6


def _load(name):
    im = Image.open(os.path.join(SRC, name))
    if name.lower().endswith(".gif"):
        frames = []
        for i in range(im.n_frames):
            im.seek(i)
            frames.append(np.array(im.convert("RGBA")))
        return frames
    return [np.array(im.convert("RGBA"))]


def _sharp(img, s):
    """只做等比缩放（LANCZOS4）。锐度不在这里统一，交给 _equalize 按动画逐段标定。"""
    if abs(s - 1.0) < 1e-3:
        return img
    h, w = img.shape[:2]
    nw, nh = max(1, int(round(w * s))), max(1, int(round(h * s)))
    return cv2.resize(img, (nw, nh), interpolation=cv2.INTER_LANCZOS4)


# 统一后的目标锐度（拉普拉斯方差，只统计不透明像素）。
# 静态图原生 1:1 大约 4200，两段 GIF 放大 1.75 倍后只有 800~1600，
# 差别肉眼可见（切动作时一软一硬）。这里把高的压下来、低的提上去，取中间值。
SHARP_TARGET = 2100.0
SHARP_SIGMA = 1.0
SHARP_MIN = -0.75   # 负数＝向模糊方向混合
SHARP_MAX = 1.9     # 正数＝向模糊反向叠加（反锐化）
# 段内逐帧微调的幅度上限。段间已经对齐到同一目标，段内只允许小幅修正，
# 否则会出现「这帧特别锐、下一帧特别糊」的闪烁感。
SHARP_LOCAL = 0.40


def _sharp_metric(rgb, alpha):
    g = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
    lap = cv2.Laplacian(g, cv2.CV_64F)
    return float(np.var(lap[alpha > 200])) if np.any(alpha > 200) else 0.0


def _equalize(frames):
    """给一段动画求一个统一的锐化/柔化系数，使其锐度落在 SHARP_TARGET。

    第一遍按整段求一个 a（保证段与段之间一致），第二遍在 a 的基础上给每帧
    一个很小的修正量（±SHARP_LOCAL），把段内因为源 GIF 压缩/动态模糊导致的
    锐度起伏压平，避免播放时忽软忽硬。
    """
    blur = [cv2.GaussianBlur(f[:, :, :3], (0, 0), SHARP_SIGMA) for f in frames]

    def apply(f, b, a):
        return np.clip(cv2.addWeighted(f[:, :, :3], 1.0 + a, b, -a, 0), 0, 255).astype(np.uint8)

    def metric_of(a):
        vals = []
        for f, b in zip(frames, blur):
            vals.append(_sharp_metric(apply(f, b, a), f[:, :, 3]))
        return float(np.mean(vals))

    lo, hi = SHARP_MIN, SHARP_MAX
    if metric_of(lo) > SHARP_TARGET:
        a = lo
    elif metric_of(hi) < SHARP_TARGET:
        a = hi
    else:
        for _ in range(22):
            mid = (lo + hi) * 0.5
            if metric_of(mid) < SHARP_TARGET:
                lo = mid
            else:
                hi = mid
        a = (lo + hi) * 0.5

    out = []
    local = []
    for f, b in zip(frames, blur):
        # 段内微调：在 [a-SHARP_LOCAL, a+SHARP_LOCAL] 里二分，把这一帧拉向目标。
        lo2, hi2 = a - SHARP_LOCAL, a + SHARP_LOCAL
        base = _sharp_metric(apply(f, b, lo2), f[:, :, 3])
        top = _sharp_metric(apply(f, b, hi2), f[:, :, 3])
        if base >= SHARP_TARGET:
            af = lo2
        elif top <= SHARP_TARGET:
            af = hi2
        else:
            for _ in range(16):
                mid = (lo2 + hi2) * 0.5
                if _sharp_metric(apply(f, b, mid), f[:, :, 3]) < SHARP_TARGET:
                    lo2 = mid
                else:
                    hi2 = mid
            af = (lo2 + hi2) * 0.5
        local.append(af)
        rgb = apply(f, b, af)
        out.append(np.dstack([rgb, f[:, :, 3]]))
    return out, a, metric_of(a), local


def _write(path, img):
    # img 是 PIL 顺序（RGBA），cv2.imencode 按 BGR(A) 理解数组，直接写会红蓝互换。
    cv2.imencode(".png", cv2.cvtColor(img, cv2.COLOR_RGBA2BGRA),
                 [cv2.IMWRITE_PNG_COMPRESSION, 9])[1].tofile(path)


def main():
    os.makedirs(OUT, exist_ok=True)

    prepared = []
    max_left = max_right = max_up = max_down = 0.0

    for name, scale, fx, fy, feet, tag in SOURCES:
        frames = [_sharp(f, scale) for f in _load(name)]
        # 每段动画统一一次锐度，保证切换动作时手感一致（GIF 放大 1.75 倍会明显更软）。
        frames, sharpen, achieved, local = _equalize(frames)
        print("  %-5s sharpen=%+.2f sharpness=%.0f  frame-a=%+.2f..%+.2f"
              % (tag, sharpen, achieved, min(local), max(local)))

        fx_s = fx * scale
        ground_s = (fy + feet) * scale
        h, w = frames[0].shape[:2]
        max_left = max(max_left, fx_s)
        max_right = max(max_right, w - fx_s)
        max_up = max(max_up, ground_s)
        max_down = max(max_down, h - ground_s)
        prepared.append((tag, frames, fx_s, ground_s))

    W = int(round(2 * max(max_left, max_right))) + PAD_SIDE * 2
    H = int(round(max_up + max_down)) + PAD_TOP + PAD_BOTTOM
    ground_y = PAD_TOP + max_up
    cx = W / 2.0
    print("canvas %dx%d  ground_y=%.1f  cx=%.1f" % (W, H, ground_y, cx))

    for tag, frames, fx_s, ground_s in prepared:
        for i, fr in enumerate(frames):
            canvas = np.zeros((H, W, 4), np.uint8)
            dx = int(round(cx - fx_s))
            dy = int(round(ground_y - ground_s))
            fh, fw = fr.shape[:2]
            x0, y0 = max(0, dx), max(0, dy)
            x1, y1 = min(W, dx + fw), min(H, dy + fh)
            if x1 > x0 and y1 > y0:
                canvas[y0:y1, x0:x1] = fr[y0 - dy:y1 - dy, x0 - dx:x1 - dx]
            if tag in ("idle", "guard", "hurt"):
                out = "barry_%s.png" % tag
            elif tag == "atk":
                out = "barry_atk_f%d.png" % i
            else:
                out = "barry_oath_f%d.png" % i
            _write(os.path.join(OUT, out), canvas)
        print("  %-5s %2d 帧 ok" % (tag, len(frames)))

    idle = cv2.imdecode(np.fromfile(os.path.join(OUT, "barry_idle.png"), np.uint8), cv2.IMREAD_UNCHANGED)
    ys, xs = np.where(idle[:, :, 3] > 8)
    print("idle bbox x[%d..%d] y[%d..%d]" % (xs.min(), xs.max(), ys.min(), ys.max()))
    print("角色在画布里的高度（头顶→脚底）= %d px，画布中心=(%.1f, %.1f)"
          % (ground_y - ys.min(), W / 2.0, H / 2.0))


if __name__ == "__main__":
    main()
