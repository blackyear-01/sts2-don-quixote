---
name: spine-authoring
description: 在本项目（Slay the Spire 2 模组 Don Quixote）里查看、校验、制作 Spine 角色动画时使用：需要渲染骨架预览、排查动画/附件/图集不一致、或按项目约定接入战斗/商店/火堆姿势时加载。
---

# Spine 动画工作流（Don Quixote）

## 先决条件

- 资产目录：`D:\sts2mod\Don Quixote\assets\animations\`
  - `don_quixote.spjson` / `don_quixote.json`（骨架）
  - `don_quixote.spatlas` / `don_quixote.atlas`（图集，**本项目的 `.spatlas` 是 `{"atlas_data": "..."}` 的 JSON 包装**，里面才是 libgdx 文本）
  - `don_quixote.png`（图集页，2048×7578）
  - `don_quixote_skel_data.tres`（Godot `SpineSkeletonDataResource`）
  - `barry.*` 是白月骑士（巴里）用的另一套骨架，与主角共用 `DonQuixoteWhiteMoonPose` 之类代码
- **硬规则：永远不要运行 `D:\sts2mod\Don Quixote\tools\make_spine.py`**，它会覆盖用户手改的骨架。不要手写 `.spskel`（二进制），只维护 `.spjson` + `.spatlas`。

## 先跑这两个工具

```powershell
# 看动画清单与时长
python 'D:\sts2mod\Don Quixote\tools\spine_render.py' --list

# 渲染某一动作的逐帧拼图（默认输出到 D:\sts2mod\tools\dump\spine）
python 'D:\sts2mod\Don Quixote\tools\spine_render.py' --anim attack --frames 6 --scale 0.4
python 'D:\sts2mod\Don Quixote\tools\spine_render.py' --all --frames 4 --gif

# 静态体检（0 ERROR 才算过）
python 'D:\sts2mod\Don Quixote\tools\spine_lint.py'
```

`spine_render.py` 关键参数：`--anim`（可重复）、`--frames`、`--scale`、`--extra`（画布留白，spine 单位，默认 170）、`--no-fit`（不按内容裁边）、`--gif`、`--out`。
`spine_lint.py` 会自动对 `assets/animations/*.spjson` 逐个配对同名 `.spatlas`，并扫 `src/**/*.cs` 里 `SetAnimation("…")` 与 `DonQuixote_*Anim` 常量做覆盖检查。

## 本项目的骨架事实

- Spine 版本 `4.2.43`，`skeleton.width/height` = 512/600（巴里是 422/546）。
- 结构是**逐帧序列图**，不是真骨骼动画：`bones` 只有 `root`、`body`；`slots` 只有 `body_slot`；动画靠 `slots.body_slot.attachment` 每 0.05s 换一个 region 附件（`idle`、`idle1..idle53` 等）。加新动作 = 往图集里加区域 + 在动画里加 attachment 关键帧。
- 已有动画名（含别名，别名都要保留，代码里两种大小写都在用）：
  `Idle/idle/idle_loop`、`Relaxed/relaxed/relaxed_loop`、`Attack/attack`、`Hit/hit/hurt`、`Cast/cast`、`Dead/dead/die`、`Revive/revive`、`Defend/defend`、`LancePose/lance_pose`、`HeavyHit/heavy_hit`
- 坐标系：spine y 轴向上，渲染到 Godot/PIL 时要翻转（`spine_render.py` 里的 `flip` 矩阵就是干这个）。

## 对齐 Valencina 模组的做法（可借鉴）

参考 `D:\Steam\steamapps\workshop\content\2868840\3747747045`：

1. **角色表现走 `NCreatureVisuals` 子类**（`Valencina.ValencinaCode.Character.ValencinaCreatureVisuals`），场景挂脚本而不是纯 `.tres` 路径；本项目对应 `DonQuixoteCombatPose`。
2. **姿势播放器把「动画名」和「时间点」都暴露成导出属性**：`LucioAssistVisuals` 有 `AttackAnimation`、`GuardAnimation`、`AttackMiddleCueSeconds`、`AttackEndCueSeconds`、`AttackImpactSeconds`、`GuardImpactSeconds`，配合 `PlayAttackAsync(onImpact, onStartCue, onMiddleCue, onEndCue)`。本项目 `DonQuixoteCombatPose` 也应把命中帧/收招帧做成常量，方便调手感。
3. **资源布局**：`Valencina/animations/characters/<角色>/` 下放 `.json` + `.atlas` + `.png` + `.png.import` + `_skel_data.tres`，与本项目一致；他们的 `.spskel` 是编辑器导出的二进制，本项目不用跟着做。
4. 他们真实骨骼数是 36 根（`thumb_father`），带 mesh 附件——如果以后想给堂吉诃德做真正的骨骼动画（挥手、受击摆动、武器分离），需要把整身图拆成头/躯干/手臂/腿等部件再重做骨架，届时 `spine_lint.py` 与 `spine_render.py` 仍可直接用（已支持多骨骼、`rotate/translate/scale` 时间线、mesh 之外的 region 附件）。

## 收尾检查清单

- [ ] `spine_render.py --anim <新动作>` 逐帧看起来没有跳帧/错位/被裁
- [ ] `spine_lint.py` 报 `ERROR=0`
- [ ] 新动画名在 `src` 里的常量与骨架一致（lint 会替你对）
- [ ] `.spatlas` 与 `.png` 一起更新，`build_pck.py` 的 TEXTURES/场景清单没漏
- [ ] 没有碰 `make_spine.py`、没有改 `.skskel`
