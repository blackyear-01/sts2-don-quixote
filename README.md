# 堂吉诃德 Don Quixote — 杀戮尖塔 2 人物 Mod

当前状态：人物可在选人界面选择并进入游戏；`攻击` / `防御` 起始卡可用；起始遗物「晾衣杆长枪」已注册；战斗立绘走**手写 Spine 4.2 JSON 骨架**（无需 Godot 导入步骤）。

---

## 一、目录结构

```
Don Quixote\
├─ DonQuixote.csproj            编译工程（引用游戏 DLL + RitsuLib）
├─ DonQuixote.json              Mod 清单（id=DonQuixote, pck_name=DonQuixote）
├─ src\                         C# 源码
│  ├─ DonQuixoteModEntry.cs     [ModInitializer] 入口，注册程序集 + Harmony 补丁
│  ├─ DonQuixoteCharacter.cs    人物本体（血量/初始卡/图标/场景路径/AssetProfile）
│  ├─ DonQuixoteCardPool.cs     卡池 / DonQuixoteRelicPool.cs / DonQuixotePotionPool.cs
│  ├─ DonQuixoteCombatPose.cs   Harmony：完全格挡播放 defend，真正掉血播放 Hit
│  ├─ DonQuixoteVisualsFactory.cs  返回 Spine 场景（失败回退 2D 立绘场景）
│  ├─ DonQuixoteCombatCues.cs   2D 回退时用的 VisualCueSet
│  ├─ DonQuixoteSpineProbe.cs   启动时打印 spine 资源加载自检日志
│  ├─ DonQuixoteForeignAssetGuard.cs  兼容层：让工坊 3764112372 的 AssetCache 补丁跳过 res://DonQuixote/ 资源
│  ├─ Cards\DonQuixoteStrike.cs / DonQuixoteDefend.cs
│  └─ Relics\DonQuixoteStartRelic.cs
├─ assets\
│  ├─ art\                      由 make_art.py 生成的派生贴图（选人图标 132x195 等）
│  ├─ animations\               Spine：don_quixote.spjson + .atlas + .spatlas + .png + _skel_data.tres
│  └─ scenes\                   don_quixote_spine.tscn（SpineSprite）、don_quixote_visuals.tscn（2D 回退）
├─ localization\zhs|eng\        characters.json / cards.json / relics.json
├─ tools\
│  ├─ make_art.py               从 ..\资源\人物 生成派生贴图（战斗立绘原样导出；闲置 GIF 抽帧）
│  ├─ make_spine.py             用同一批立绘拼图集，生成 Spine 骨架 JSON / atlas / .tres / .tscn
│  ├─ build_pck.py              手写 Godot 4 PCK v3（贴图同时写入 .ctex 与 .import）
│  └─ verify_pck.py             校验 pck 结构与内容
└─ build\DonQuixote.pck         打包产物
```

## 二、完整构建流程

```powershell
$py = 'C:\Users\ASUS\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'

# 1) 从原始素材重新生成派生贴图
#    四张战斗立绘（闲置GIF/受伤/防御/攻击GIF）原样导出，不改像素、不翻转；
#    朝向一律由 make_spine.py 的附件变换决定（现在不镜像 = 保持原方向）。
#    闲置 GIF（54 帧 @50ms）会导出为 idle_f0..f53.png 并按 0.5 缩放
#    （原图 591x1104 是其他立绘的两倍分辨率），同时写出 idle_meta.json 记录帧时长。
& $py 'D:\sts2mod\Don Quixote\tools\make_art.py'

# 2) 生成 Spine 骨架 + 图集 + 场景
& $py 'D:\sts2mod\Don Quixote\tools\make_spine.py'

# 3) 编译 DLL
#    注意：程序集简单名是 DonQuixoteCiallo（为了不和创意工坊 3764112372 那份
#    Donquixote.dll 撞名，.NET 判重不区分大小写），构建后会把产物再复制一份
#    成 bin\Release\DonQuixote.dll —— 部署时用这个文件名，别用 DonQuixoteCiallo.dll。
$env:DOTNET_CLI_HOME='D:\sts2mod\tools\clihome'
$env:NUGET_PACKAGES='D:\sts2mod\tools\pkgs'
dotnet build 'D:\sts2mod\Don Quixote\DonQuixote.csproj' -c Release

# 4) 打包
#    build_pck.py 依赖 Pillow：本机只有 3.11 装了 PIL，用 py -3.11 跑
#    （README 里旧的 .dsh python 路径已失效）
py -3.11 -u 'D:\sts2mod\Don Quixote\tools\build_pck.py'
py -3.11 -u 'D:\sts2mod\Don Quixote\tools\verify_pck.py'

# 5) 部署（游戏目录不在工作区内，需要授权）
$dst = 'D:\Steam\steamapps\common\Slay the Spire 2\mods\Don Quixote'
Copy-Item 'D:\sts2mod\Don Quixote\bin\Release\DonQuixote.dll' $dst -Force
Copy-Item 'D:\sts2mod\Don Quixote\build\DonQuixote.pck'         $dst -Force
Copy-Item 'D:\sts2mod\Don Quixote\DonQuixote.json'              $dst -Force
```

## 三、自己调 Spine 时要遵守的约定

**骨架结构（`assets\animations\don_quixote.spjson`，纯文本 JSON，可直接改）**

- `bones`: `root` → `body`（所有位移/旋转都作用在 `body` 上）
- `slots`: `body_slot`（挂在 `body`），初始 attachment 为 `idle`
- `skins`: 必须保留 `default` 皮肤，里面按姿势名放 region attachment
  （`idle` + `idle1..idle53` 闲置呼吸帧 / `attack0..3` / `hit` / `defend` / `dead` / `cast`）
- 闲置是**逐帧动画**：`Idle` 动画的 `body_slot.attachment` 时间轴每 0.05s 换一帧附件
  （共 54 帧 = 2.7s 循环），帧时长来自 `assets/art/idle_meta.json`。
  换闲置 GIF 后只要重跑 make_art.py + make_spine.py，帧数与节奏会自动跟随。
- `skeleton.spine` 固定为 `4.2.43`（与游戏内置 spine-godot 运行时一致）

**必须存在的动画名**（大小写敏感，缺一个就会静默回退到默认姿势）

| 用途 | 动画名 |
| --- | --- |
| 立绘待机（战斗循环） | `Idle`, `idle`, `idle_loop` |
| 休息处放松 | `Relaxed`, `relaxed`, `relaxed_loop` |
| 攻击 | `Attack`, `attack` |
| 受击 | `Hit`, `hit`, `hurt` |
| 施法 | `Cast`, `cast` |
| 死亡 | `Dead`, `dead`, `die` |
| 复活 | `Revive`, `revive` |
| 完全格挡（本 Mod 自定义） | `Defend`, `defend` |

> `defend` 与 `idle_loop` 由 `DonQuixoteCombatPose.cs` 直接调用：
> `SetAnimation("defend", false, 0)` 然后 `AddAnimation("idle_loop", 0.5f, true, 0)`。
> 改动画时长时，同时改 `DonQuixoteCombatPose.DonQuixote_DefendLength`，并把
> `DonQuixote_IsGuarding` 里的 `0.65` 秒窗口一起调整，避免 Hit 提前覆盖防御姿势。

**贴图约定**

- 图集页 `don_quixote.png` 必须与 `don_quixote.atlas` 里写的尺寸/区域一致；
  `don_quixote.atlas` 是 Spine 文本图集，`don_quixote.spatlas` 只是把它包一层的 JSON
  （`{"atlas_data": "...", "source_path": "res://DonQuixote/animations/don_quixote.atlas", ...}`）。
- 每个 region 的 `xy` 是**左上角原点**（游戏内置 spine 运行时按左上读取，已用官方
  守望者 `watcher.spatlas` 对照验证）。标准 Spine 编辑器导出的是左下角原点，
  直接拿来用会导致所有姿势采样错位。由 `make_spine.py` 直接写左上坐标，手工改图集时注意。
- 图集页用 `page.paste(img, box)` 无遮罩直拷；不要用带遮罩的 paste 或 alpha_composite，
  它们会把半透明边缘改写几个像素（实测每帧约 12 个像素偏差）。
- 原点 = 脚底中心：`don_quixote_spine.tscn` 里 `Visuals`（SpineSprite）在 `(0,0)`、
  `scale = 0.52`，`Bounds` 为 `(-140,-300)..(140,0)`，`CenterPos (0,-150)`、
  `IntentPos (0,-330)`。要放大/缩小整体，就调这个 `scale`。

**如果想改用 Spine 编辑器产出的正式工程**：把导出的 `skeleton.json` 覆盖
`assets\animations\don_quixote.spjson`、导出的 `.atlas` 覆盖 `don_quixote.atlas`
（再用 `make_spine.py` 里同样的包装方式重生成 `don_quixote.spatlas`，贴图放同目录），
然后跑「打包 → 部署」两步即可；不需要 Godot 导入。

## 四、正在凝固的血：45° 贯穿视觉

中了「正在凝固的血」（`DonQuixotePowerClottingBlood`）的**怪物**会被一根血刺贯穿，
实现在 `src\DonQuixoteClotSpike.cs`：

- 素材 `资源\正在凝固的血-显示效果.png`（11×279 竖直血刺）由 `make_art.py` 从正中间切成
  两半：`clot_spike_front.png`（上半 139px）、`clot_spike_back.png`（下半 140px），像素不动。
- 刺挂成 `NCreatureVisuals.Bounds` 的子节点，位置 = Bounds 中心、倾角 `-45°`，
  长度 = 身体高度 × 1.35，因此怪物移动/缩放会自动跟随。
- **层次**：原版角色场景里身体节点（`%Visuals`）排在 `Bounds` 之前，所以
  `Bounds` 子树里的节点默认画在身体之上。下半截设 `ZIndex = -1` 压到身体背后，
  上半截保持 `ZIndex = 0` 盖在身体前面 → 中段被身体挡住，看起来像刺穿过去。
- 状态检测走 `Creature.Powers` 里是否有 `DonQuixotePowerClottingBlood`，用 0.1s 的
  `Timer` 轮询，所以上状态/掉状态、怪物死亡都会自动显示或隐藏。
- 可调常量（`DonQuixoteClotSpike` 顶部）：`DonQuixote_TiltDegrees`（倾角，正负号决定朝哪边倒）、
  `DonQuixote_LengthRatio`（刺长 / 身高）、`DonQuixote_PierceHeightRatio`（贯穿点高度比例）。

## 五、流血的触发点

流血的口径是**「敌方的每一段攻击 / 每一次非攻击行动各结算一次」**：
多段攻击（例如 5×3）会触发 3 次、扣 3 层；非攻击行动（格挡、上状态、塞牌、自愈、召唤…）一次行动只触发 1 次。
眩晕（`StunIntent` / `STUN_MOVE`）和睡眠（`SleepIntent` / `SLEEP_MOVE`）只是「跳过回合」，不算行动，不触发。

实现分两层（都在 `src\Powers\DonQuixotePowers.cs`）：

1. **行动编号**：`DonQuixoteEnemyMovePrefixPatch` 在 `MonsterModel.PerformMove` 的 prefix 里把
   `DonQuixote_MoveSerial` +1，同时用 `NextMove.Intents` 判断这次是不是眩晕/睡眠（`DonQuixote_CurrentMoveInert`）。
2. **结算**：非攻击钩子都调用 `DonQuixote_TryClaimAction(Owner)`，只有「本序号还没结算过」的那一次能拿到 true。
   攻击走 `DonQuixote_MarkActionSettled(Owner)`：**每段都触发，但不拦截下一段**，只负责让同一次行动的其它钩子不补刀。

| 行动类型 | 钩子 |
| --- | --- |
| 攻击（**每段各触发一次，各扣 1 层**） | `BeforeDamageReceived`（`dealer == 自己`、`props` 含 `ValueProp.Move`、`Owner.Monster.IsPerformingMove`） |
| 获得格挡 | `AfterBlockGained` |
| 给自己上状态 | `AfterPowerAmountChanged`（`applier == 自己` 且是怪物） |
| 给玩家塞牌（策略 / 状态牌意图） | `AfterCardGeneratedForCombat`（`creator == null`） |
| **上面都没覆盖到的行动**（自愈、召唤、抢钱、纯台词…） | `DonQuixoteEnemyMoveSettledPatch`：`CombatHistory.MonsterPerformedMove` 的 postfix 兜底 |

**为什么攻击要额外判 `ValueProp.Move` + `IsPerformingMove`**：`BeforeDamageReceived` 在玩家回合里也会响
（怪物的尖刺 / 反伤打玩家时的 `dealer` 就是那只怪）。这两个条件把「怪物自己行动打出来的攻击段」和
「回合外的反伤」区分开，后者不算敌方行动，不触发流血。

**先格挡后攻击**这类行动（攻击之前已经因为加格挡结算过一次）：`DonQuixote_SettledByOtherHook` 会让
第一段不再补，之后每段照常触发，总次数仍然等于攻击段数。

玩家身上的流血是另一套口径：只在**打出 / 消耗卡牌**时触发（`AfterCardPlayed` / `AfterCardExhausted`），
敌方行动不会触发玩家身上的流血。施加流血时若「目标 == 施加者」自己，则不吃「复还的血之冲动」的强度/层数加成。

### 触发与扣层

`DonQuixote_TriggerBleed(ctx, target, consumeLayers)` 的第三个参数决定「这次触发扣不扣层」：

- **卡牌上的「触发对方 X 次流血效果」默认不扣层**（血之欢喜、球），只是额外结算 X 次伤害；
  例外是卡面明写「并减少 N 层」的（桩的升级效果：触发 3 次并扣 3 层），传 `true`；
- 敌方行动、打出/消耗卡牌造成的自然触发照常扣 1 层；
- 撕裂的血袋给的「额外触发 1 次」也不额外扣层（一次行动仍然只掉 1 层）。

### 减伤与遗物加成

- 所有凝固的血：`DonQuixote_BleedHitDamage` 按能力层数（50 / 75）削减单次流血伤害。
  流血是绕过 `ModifyDamageMultiplicative` 的「生命伤害」，所以只能在这一处处理。
- 蜜拉卡：`DonQuixote_ApplyBleed` 在算完「复还的血之冲动」之后额外 +1 级，所以那 1 级不吃冲动的乘算。
- 撕裂的血袋：额外触发由 `DonQuixote_ConsumeTornBagCharges` 合并进同一次结算（`times` 循环），
  不会递归调用钩子，层数也仍然只掉 1。

> ⚠️ 早先的实现是「每个伤害/格挡/状态事件各触发一次」，多段攻击会连触 4 次。
> 那版在联机里会数据不同步：`DonQuixote_TriggerBleed` 有 `DonQuixote_Triggering` 防重入闸，
> 后一段触发是否被吞掉取决于两端的异步时序，两端流血层数就会不一样，进而让 RitsuLib 的
> 模型身份序号错位、后面所有同步行动解析失败。现在统一成「一次行动一次」，不再依赖时序。

## 六、右键附魔（不可摧毁卡牌）的联网同步

**不要用 RitsuLib 的右键绑定（`ModRightClickRegistry.Register<CardModel>`）**。那条路要把点到的模型换成
跨端身份令牌（`ModModelIdentityRegistry` 里的全局自增序号）再让各端解析回来；只要两端状态有过一点点差异，
序号就错位，客户端解析失败后会**静默跳过**——主机扣了 30 血宴并附魔，客户端什么都没做，
于是 `StateDivergence` 直接断线（队友日志里 `RitsuLibManagedGameAction ... CombatPlayPhaseOnly` 那条 checksum 就是它）。

现在的做法（`src\DonQuixoteEnchantAction.cs`）：

- 自己注册一个托管网络动作 `donquixote/indestructible_enchant`（`GameActionType.CombatPlayPhaseOnly`），
  载荷只有「玩家 NetId + 牌堆 + 堆内序号 + 牌 id」——这些信息两端本来就一致。
- `DonQuixoteRightClick` 只注册一个高优先级的 `IModRightClickHandler`（优先级 1000），
  本地判定通过就发起这个行动并消费掉输入，RitsuLib 内置处理器不会再插手。
- 行动执行时各端按同一规则重新找那张牌（序号对不上就按牌 id 找第一张未附魔的），再扣血宴、附魔、加消耗血宴总数，
  任何一步不满足就整体放弃 —— 要么两端都做，要么都不做。
- 单机（没有 `NetService`）时直接本地结算；联机下入队失败就放弃并打 warn，绝不做单端状态修改。

附魔状态本身只认 `card.Enchantment is DonQuixoteIndestructibleCard`。之前还有一份
「按玩家 + 卡牌条目记账」的本地字典（`DonQuixote_Charges`）和按条目给手牌补附魔（`DonQuixote_EnchantHandCopies`），
那份数据不会同步，会出现「主机认为带不可摧毁、客户端认为不带」→ 消耗时一端进弃牌堆另一端进消耗堆。已全部删除。

## 七、写遗物 / 卡牌时的联机安全约定

联机不同步的根源几乎都是「某份状态只有一端有、或者两端按不同时序改」。写新内容时按下面几条来：

| 禁止 | 改用 |
| --- | --- |
| `static` 可变字段记战斗状态（曾经的 `DonQuixoteMoonFlightState`） | 挂在角色身上的能力（如 `DonQuixotePowerMoonFlight`），战斗结束自动清 |
| 遗物上的普通字段记跨回合计数 | `[SavedProperty]` 属性 —— 联机开战时远端玩家的遗物是用主机发来的序列化数据重建的，没序列化的字段会被清零 |
| 自己数回合 / 数层数 | `CombatState.RoundNumber`（参与校验和，两端一致） |
| `AfterPlayerTurnStart` 做「回合开始改状态」 | `AfterSideTurnStart`（玩家侧 + 参与者包含自己） |
| `Random.Shared` / `Guid` 当随机源 | `RunState.Rng.CombatCardSelection` / `CombatTargets`（同步随机源） |
| 在遗物上缓存「这张牌附魔过」这类卡牌状态 | 直接读卡牌自身（`card.Enchantment`），卡牌状态本来就在存档与校验和里 |

已按这套约定落地的例子：

- `DonQuixoteHardBloodHalberd`（硬血长柄刀）：`DonQuixote_FeastConsumed` 是 `[SavedProperty]`，
  `BeforeCombatStart` 复位（「消耗血宴总数」是每场战斗的能力）。
- `DonQuixoteTornBloodBag`（撕裂的血袋）：`DonQuixote_ChargesUsed` + `DonQuixote_ChargeRound` 都是
  `[SavedProperty]`，按回合号自然复位；额外触发合并进同一次流血结算，不做递归。
- `DonQuixoteNikolina`：同样在 `BeforeCombatStart` 复位换算额度，否则第二场战斗起永远凑不满 20 点。
- 血魔的本能 / 血魔长老的本能：「第二回合开始后血宴为 0 才给虚弱易伤」用 `RoundNumber >= 2` 判定。

## 八、内容注册约定（卡池 / 遗物池 / 欧洛巴斯）

**唯一注册入口是类上的特性**，不要再去 `TypeListCardPoolModel.CardTypes` /
`TypeListRelicPoolModel.RelicTypes` 里把同一批类型列一遍：

```csharp
[RegisterCard(typeof(DonQuixoteCardPool))]      // 卡
[RegisterRelic(typeof(DonQuixoteRelicPool))]    // 遗物
```

RitsuLib 用 `ModHelper.AddModelToPool` 把这些类型注入 `AllCards` / `AllRelics`，
而 `CardPoolModel.AllCards` 的实现是 `GenerateAllCards() ++ ModHelper.ConcatModelsFromMods(...)`
（`ConcatModelsFromMods` 不做去重）。两边都写 = 同一张卡/同一件遗物在池里出现两次，
表现为奖励权重翻倍、一次奖励里出现两张相同的卡。RitsuLib 已把这两个 `CardTypes` /
`RelicTypes` 标成 `[Obsolete]`，原因就是「和注入重复」。本 Mod 已按此约定清理，
启动日志里 `AutoRegister ... 55 operation(s), 55 succeeded, 0 failed` 即为注册成功。

**本 Mod 当前内容清单**（截至本次新增）

| 类型 | 名称 | 说明 |
| --- | --- | --- |
| 卡（稀有/Power） | 展现洒落在战场上的潜伏之血 | 血宴获取量 +50%（升级 +100%），`DonQuixotePowerLatentBlood` |
| 卡（稀有/Power） | 好点子形态 | 每消耗 20 点血宴获得 1 点能量（升级 15），`DonQuixotePowerGoodIdeaForm` |
| 遗物 | 血魔长老的本能 | 「血魔的本能」的欧洛巴斯精炼形态 |

**欧洛巴斯精炼**：`TouchOfOrobas` 原版只认硬编码字典，本 Mod 用 RitsuLib 的
`[RegisterTouchOfOrobasRefinement(typeof(DonQuixoteBloodElderInstinct))]` 挂在
`DonQuixoteBloodInstinct` 上；拿「欧洛巴斯之触」时初始遗物会被替换成「血魔长老的本能」。

**血宴加成只有一处入口**：所有血宴来源都走 `DonQuixoteBloodMagic.DonQuixote_GainBloodFeast`，
潜伏之血的百分比加成在函数开头统一乘算，新增血宴来源时不要再自己加一遍。

## 九、卡面文本：关键词与换行

**关键词不要写进 description。** `CardModel.GetFormattedDescription` 会按
`CardKeywordOrder.beforeDescription`（虚无 / 狡猾 / 保留 / 固有 / 不可打出）和
`afterDescription`（消耗 / 永恒）**自动**把关键词文本拼在描述前后，所以只要卡上
`CanonicalKeywords` 或 `OnUpgrade()` 里 `AddKeyword` 过，卡面就会自己显示「保留 / 固有」。
再在 `localization/*/cards.json` 里写一遍 `[gold]保留[/gold]。` 就会显示两遍。
升级才获得的关键词同样靠 `AddKeyword`（`OnUpgrade` 里显式调用），不要在
`{IfUpgraded:show:...}` 里补文字。

**换行必须写 `\n`（反斜杠 n）。** 写成 `/n`（正斜杠）不会报错，但会把 `/n`
原样渲染到卡面上。`{IfUpgraded:show:\n...}` 里也一样，参数解析后就是普通换行符。

## 十、卡面插画（CustomPortraitPath）

卡牌插画铺进卡框窗口后，游戏**不会**再按窗口形状做遮罩；而每张牌还会按类型盖一块
装饰边框（`card_portrait_border_<type>_s`）：

| 卡型 | 装饰边框的开口 |
| --- | --- |
| 技能牌（skill） | U 形，底部平直 |
| 攻击牌（attack） | U 形，底部 V 形（中间深、两边浅） |
| 能力牌（power） | U 形，底部碗形（中间最深） |

插画区域固定 250x190（屏幕 (25,43)-(275,233)），装饰边框 275x210
（(12.5,47)-(287.5,257)）画在插画**上面**。开口以外的部分（左右两条臂的外侧、
底部两角）是透明的，会直接把插画露出来 —— 白底插画就在这些位置露出白块，
看起来像「插画超出卡框」。只切四角是修不好的，因为露出来的区域是**按卡型变化**的。

用 `tools\CardImagesCreator\card_art.py`，它会按卡型套真实开口遮罩：

```bat
cd tools\CardImagesCreator
py -3.11 card_art.py cards\GoodIdeaForm.png --type power ^
    --out "..\..\Don Quixote\assets\art\card_good_idea_form.png" --preview 预览.png
```

- `--type skill|attack|power|ancient`：决定用哪张开口遮罩（`masks\opening_<type>.png`，
  从游戏资源量出来的，`extract_borders.py` 可重新生成）；`ancient` 是先古卡，
  走另一套整卡版式，见下一条；
- **遮罩只取「开口内部」**：边框臂和底部斜条是实心的，插画切到它们下面也看不见，
  所以不必保留；把环带也并进来反而会让插画沿着环带外缘多留一条窄边，
  在角上「支棱」出来；
- 被切掉的部分默认涂成**卡框本色**（`--fill frame`）：从 `frame1.png` 的
  不透明像素向内扩散取色，切口看不出来，也不会透出战场背景；
  - **必须套游戏那层 HSV 材质**：卡框节点（`Frame`）挂着卡池的
    `card_frame_orange_mat`（`shaders/hsv.gdshader`，h=0.12 s=1.5 v=1.2），
    装饰边框挂着 `card_banner_common_mat`（h=1.0 s=0.0 v=0.85，去色压暗）；
    插画本身不吃这层材质。所以填充取色前要先用同样的变换把卡框过一遍
    （`card_art.FRAME_TINT`），否则填出来是未着色的深褐、旁边却是金橙色的
    卡框，卡角看起来就压了一块暗色、像卡面超出边框；
  - 为什么不能留透明：插画槽本身是**镂空**的（`frame1.png` 里 alpha=0），
    透明处在游戏里露出来的是插画槽后面的东西 —— 卡影 + 战斗背景，
    看着就是「卡角发黑」；
  - `--fill none` 输出纯透明（alpha 与 RGB 都清 0，做图用）；
    `--fill bleed` 涂成装饰边框的青色（会在卡角变成突兀色块，一般别用）；
    也可 `--fill #RRGGBB`；
- `--zoom`：攻击牌默认 0.95（底部 V 形收得更狠，主体稍小不容易被切），其余 1.0；
- `--focus 0.5,0.35` 指定裁切焦点；`--none` 输出直角矩形（对比用）；
- `--preview` 出一张「卡框 + 插画 + 装饰边框」的合成图，顺序与游戏一致；
- 批量：`run_card_art.bat`（无参）按 `cards\_cards.json` 逐张按各自卡型处理；
  `run_card_art.bat power` 则是整批强制用同一种卡型。
- 只允许对原图做**缩放 / 裁剪**，不要重绘或抹掉画面内容。

**先古卡（`--type ancient`，250x351）**

先古卡不是「卡框 + 小插画」，而是整张卡面都是插画（`AncientPortrait` 节点
299x420），上面只盖一圈细描边（`ancient_card_border`）。所以：

```bat
py -3.11 card_art.py cards\BurningHunger.png --type ancient --focus 0.1,0.5 ^
    --out "..\..\Don Quixote\assets\art\card_burning_hunger.png" --preview 预览.png
```

- 输出固定 **250x351**，按 250:351 等比裁剪后缩放，**不做非等比拉伸**
  （「主体不被压扁」优先于「画面全都要」）；
- 细描边会把插画四周压掉一点（左右各约 2.7%、上下各约 1.9%），
  主体别贴边；`zoom` 只在原图比目标框更大时才有意义，
  原图已经不够大时它会自动退回「整张图等比缩放」，不会把画面拉变形；
- 先古卡在游戏里显示的是 `AncientPortrait`，纹理仍取 `CardModel.Portrait`，
  所以代码里照样用 `CustomPortraitPath` 指过去即可，不用 `CustomBetaPortraitPath`。

`cards\_cards.json` 是「文件名 → 卡型」表，value 可以只写卡型，也可以写成
`{"type": "skill", "zoom": 1.0, "focus": "0.5,0.45"}` 单独覆盖：

```json
{ "HardBloodSlash.png": "attack", "GoodIdeaForm.png": "power",
  "BurningHunger.png": { "type": "ancient", "focus": "0.1,0.5" } }
```

原图放 `cards\`，示例图在 `samples\`。改完记得重新 `build_pck.py`，贴图是打进 pck 的；
`build_pck.py` 的 `TEXTURES` 里也要有 `card_<key>.png → DonQuixote/images/cards/<key>.png`，
否则不会被打进包。卡牌侧再加：

```csharp
public override string CustomPortraitPath => "res://DonQuixote/images/cards/<key>.png";
```

0.0.6 这一批新增的 8 张卡面：`blood_cheer` / `blood_joy` / `crush` / `wound` /
`hard_blood_pierce` / `hard_blood_raid` / `hard_blood_track` / `la_sangre`；
其余新卡（令伤口撕裂、侧耳倾听、冒险结束了、手足们在呼唤我、爹死亡、
于镜中无法呈现倒影之人、桑丘之血-仿制、让我们以枪决斗吧）暂时用占位卡图。

## 十一、排查

- 游戏日志：`C:\Users\ASUS\AppData\Roaming\SlayTheSpire2\logs\godot.log`
- 启动时会打印自检行：`[DonQuixote] ... ResourceLoader.Exists(...) / is_skeleton_data_loaded=true / scene visuals node=SpineSprite`；
  看到 `using spine combat visuals` 说明走的是 Spine，`using 2d combat visuals` 说明回退到了 PNG 立绘。
- `ext_resource, invalid UID: uid://...` 是手写 uid 未进 Godot uid 缓存导致的提示，会自动按文本路径加载，可忽略。
