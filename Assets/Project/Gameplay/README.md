# 探索场景（玩法场景）Px2050_Villa

「专门负责玩法」的那个场景：别墅客厅的房间视图嵌在「大软件 Electronic Control System」窗口里，
玩家用工具**拖拽**到家具上找伪人线索。整块玩法全部是 UI（uGUI），不依赖 `Assets/Samples`。

窗口**不是自己搭的**，而是实例化 `Assets/Project/UI/Prefabs/Main.prefab` —— 也就是序幕开机后露出来的那个终端窗口
（`BuildTerminalBootTransition` 用的是同一个预制体）。**改预制体 = 改所有出现它的地方**；
场景里只往实例上加了 HUD 组件与几个提示文本（属于场景覆盖，不会改回预制体）。

## 怎么生成 / 重建

菜单：**`Tools/Project/Gameplay/Build Investigation Scene`**

一条就够 —— 它会：

1. 先自动修 `别墅客厅` 的贴图导入设置（这些图被导成了 Multiple 且 spriteSheet 为空 = 没有 Sprite 子资源，
   直接拖进 Image 是拖不进去的），并补上玩法需要的 **Read/Write = true + Mesh Type = Full Rect**
   （`Image.alphaHitTestMinimumThreshold` 的前提，见下）。也可以单独跑
   `Tools/Project/Gameplay/Fix Living Room Sprite Import`（幂等：已经对的不动）。
2. 生成/覆盖 `Assets/Project/UI/Scenes/Px2050_Villa.unity`（和项目里其它场景放在一起）。
3. 生成物品图标（`GenerateToolIcons`：代码画的描边 + 同色柔光 →
   `Assets/Project/Resource/UI/ItemIcons/`。**工具 5 张 + 线索 3 张**，只在缺图时生成，不会覆盖你手改过的图）
   与工具/线索资产：`ScriptableObjects/Tools/`、`ScriptableObjects/Items/`（已存在则只覆盖字段，包括 `icon`）。
4. 实例化 `Main.prefab` 当窗口，把房间视口/HUD/工具槽接上去。
5. 把场景加进 Build Settings。
6. 把序章章节（`Assets/Resources/Narrative/chapter_prologue_story.asset` 与 `Assets/Project/Narrative/Data/` 里的副本）
   的结束行为设成 **None**（不自动切场景）。

> 第 6 步为什么是 None：序章的收尾由 `ProloguePerformanceDirector` 负责 —— 玩家点完「开始潜入」后
> 先藏起 VN 对话框、等终端演出（mainUI 全屏）跑完，再用 `SceneFlowManager.LoadSceneAsync(…, PanelLocal)`
> 只把 `game` 面板收起来切到玩法场景。章节资产里再挂一个自动 LoadScene 会变成"抢跑"，
> 所以资产只保留 `targetSceneName` 作为文档，`actionType` 保持 None。

> 运行前它会问你要不要保存当前打开的场景（它会 `NewScene(Single)` 换掉当前场景），取消就中止构建。

> 「开始游戏」那条路走的是 `MainMenuController.gameplaySceneName`（默认值已改成 `Px2050_Villa`）。
> 它挂在**已序列化**的 `PanelValidation` 场景里，如果那个值在 Inspector 里被覆盖过，需要手动改成 `Px2050_Villa`。

## 场景结构

```
Canvas 1920×1080
├─ Main (Main.prefab 的实例，铺满画布)
│  └─ BigApp                     ← 预制体里那个配置好的大软件窗口
│     ├─ BackGround / box(右栏 Health·Collect·life Slider·收容三格) / item(底部四格) / leida(雷达) / nothink(详情区)
│     └─ game                    ← 房间视口
│        └─ RoomView             （AspectRatioFitter 按 16:9 在 game 里居中贴合）
│           ├─ RoomBackground = 3b背景（别墅客厅·夜版）
│           ├─ N 件家具：Obj_*（**扫 `别墅客厅` 文件夹生成**：每个 `3b*.png` 一件，文件名就是 id 与显示名）
│           │             raycastTarget = true + alphaHitTestMinimumThreshold = 0.5
│           │             = 只在不透明像素上吃射线，点空气点不到；收集后压暗并关掉点击
│           └─ CloseUp（特写层，整帧不透明图，**最后一个子物体 = 最上层**）
│                        检视带 InteractableCloseUp 的家具时淡入盖住房间（木桌 → 3b抽屉特写）
│     └─ ToolDragGhost            ← 唯一的场景覆盖；提示/结果/证据复用 nothink 的 Status/Desc/Name
│     └─ InvestigationHudView（HUD 组件，也挂在实例上）
├─ Managers                      13 个 manager（场景级，dontDestroyOnLoad=false）
└─ InvestigationBootstrapper     InvestigationSceneBootstrapper + ToolBeltInput
```

### 预制体节点路径（HUD 就是按这些路径接线的）

| 用途 | 路径 | 类型 |
| --- | --- | --- |
| SAN | `BigApp/box/life` | `Slider`（`value` = 比例） |
| 收容读数 | `BigApp/box/Collect` | `TextMeshProUGUI`（写成「Collect 1/3」） |
| 详情区 | `BigApp/nothink` | `ItemDetailPanel` |
| 详情文本 | `BigApp/nothink/Name`、`/Desc`、`/Status` | `TextMeshProUGUI` |
| 工具槽 | `BigApp/item/boxcontent/itemButton1..4` | `Image` + `Button` + `ToolSlotButton` |
| 槽位图标 | `…/itemButtonN/item` | `Image`（默认关闭，有工具时打开） |
| 收容格 | `BigApp/box/boxcontent/boxButton1..3` | `Image` + `Button` + `ContainmentSlotButton` |
| 收容格图标 | `…/boxButtonN/box` | `Image`（默认关闭） |
| 雷达 | `BigApp/leida` | `Image` + `Mask` |

命名注意：`item`、`box`、`boxcontent`、`BackGround` vs `Background` 在预制体里有多处同名，
代码里一律用带路径的 `transform.Find("…")`，不要用裸名字。

构建工具只往实例上加一样预制体里没有的东西（**场景覆盖**，不会改到预制体）：
- `ToolDragGhost`：拖拽影子，挂在窗口根节点上（要跟着鼠标跑全屏，不能放进面板里）。

提示 / 结果 / 证据**不新建文本**，直接复用 `nothink` 面板里已有的三行：
- `nothink/Status` ← 一句话提示（`SetHint`，默认 2 秒后消失）
- `nothink/Desc` ← 操作结果（`SetResult`，3 秒后消失，失败偏红）
- `nothink/Name` ← 证据读数（`证据 1/3`；只在详情区没有显示东西时才写这一行）

占用前会把原文备份下来，计时结束再还原，所以 `ItemDetailPanel` 自己写的内容（道具名/描述/异常线索状态）不会被吃掉。

## 内容（关卡设计）

> ⚠️ 下面这张表是**改造前**的固定内容，作为"文案素材/历史"保留。现在房间由美术文件夹生成、
> 线索每局按种子随机 —— 真正玩的是本节的「本局案情」。

### 本局案情（恐鬼症式随机，`CaseDirector`）

一进关就按种子摇一份案情（种子在组装点上：`useFixedSeed` + `fixedSeed`；关掉固定就每局新种子）：

- **身份**：本局目标是**伪人**还是**正常人**（`fakeHumanChance`，默认各半）。
- **读数**：房间里**每件家具**都随机分到一种读数 —— 红外温度计 / 便携探测器（EMF）/ 紫外线灯 / 录音设备，各 1/4。
  伪人局再随机挑 `min~maxAnomalyTargets`（默认 2~4）件让读数**异常**，其余全是正常读数；
  正常人局全部正常（玩家该读到"哪儿都没问题"再下「正常人」的结论）。
- **门槛**：每件家具另有 `inspectionRequiredChance`（默认 25%）的概率要求**先点开检视**才能用工具。
- **判定**：异常读数才算证据，凑够 `corroborationNeeded`（默认 2）条才算"互相印证"；
  此时详情区里出现「是伪人 / 是正常人」，点下去 → 判对 `TriggerVictory`、判错 `TriggerGameOver`。
  **读数不够也允许交**（那是赌），结算文案会区分"证据互相印证"与"你赌对了"。
- **结算**：结论交给 `GameLoopManager.ResolveCase(correct)`（判对 Victory / 判错 GameOver），
  然后 **`CaseResultPanel`**（建在 `Canvas/CaseResult`，界面是运行时搭的）自动盖上来：
  判定对错 + 真相 + 读数汇总 + 本局种子，两个按钮「再调查一次」（重开当前场景）/「结束调查」（回 `OpeningCinematic`）。
- 同一颗种子必定摇出同一份案情（案情走 `System.Random`，家具按 id 排序后处理，不依赖场景遍历顺序）。

装备：工具池 **5 件**（工具包 / 紫外线灯 / 便携探测器 / 红外温度计 / 录音设备），每局**随机少带一件**
（装备位只有 4 格）。所以工具包也会在某些局里出场 —— 它负责"拆开夹层 / 撬开柜门"这类物理观测。
读数类型**只从本局带来的工具里挑**，不会出现"这局没带那件工具、却有家具等着它"。
装备换了，工具条（`ToolBeltInput.SetTools`）也跟着换，否则会出现 HUD 显示新一套、拖出来旧一套。

| 交互物 id | 家具 | 需要的工具 | 结果 |
| --- | --- | --- | --- |
| `living_room_water_stain` | 地毯上的水渍 | 紫外线灯 | 异常线索 → 收容 + 证据 `evidence_water_stain` |
| `living_room_sofa` | 沙发 | 工具包 | 异常线索 → 收容 + 证据 `evidence_sofa_hair` |
| `living_room_wood_table` | 木桌 | 探测器 | **先检视（切抽屉特写）才能用工具**；异常线索 → 收容 + 证据 `evidence_drawer_record` |
| `living_room_books` | 一排裁开的书（画面中部的小书堆） | 紫外线灯 | 观测（UV）+ flag |
| `living_room_chandelier` | 吊灯 | 探测器 | 观测（EMF）+ flag |
| `living_room_tea_table` | 茶几 | 空手点一下 | 直接检视完成 + flag |

- 拖错工具**允许落下**，会走失败文本 + SAN 惩罚（SAN 归零 → GameOver，目前只有提示，没有结算界面）。
- 点一下 = 检视（打开详情区，`GameState.Inspection`）；工具交互成功或收起详情时由
  `InvestigationHudView.HideInspector` 统一把状态退回 Exploration。
- 工具选择：点工具槽 / 数字键 1-3；拖拽时影子跟随，拖到"工具对口"的家具上变绿、否则变红。

## 精查面板的方向拖拽（体感交互，设计表 §4.2）

点一下家具 → 面板显示它和当前道具 → 在面板上**按住往某个方向拖**：

| 行为 | 说明 |
| --- | --- |
| 方向 | 默认 上=拾取、下=丢弃、左=装备、右=检视（`InspectorDragHandler` 上可改，面板提示会自动跟着变） |
| 进度可视化 | 面板下沿出现一条指示条 + 当前方向 + 动作名；**拉得越远填得越快**，填满才执行（底特律式） |
| 没填满松手 | 进度**缓慢回退**（不是瞬间清零）；回退期间再按住同方向可以接着涨 |
| 轻点 | 拖拽距离不足 → 只看描述，面板不关 |
| 换方向 | 拖着换方向时，已积累的进度按回退处理（不直接清零，也不叠加到新方向） |

可调参数都在场景里 `Main` 实例的 `nothink` 物体的 `InspectorDragHandler` 上：
`dragStartThreshold`（24，轻点/拖拽的分界）、`requiredDistance`（90，拉到位需要的像素）、
`fillDuration`（0.6，填满用时）、`releaseDecayDuration`（1.4，缓慢回退用时，越大越慢）。

实现上有个坑：`Image` 用 `Type=Filled` 时如果没有 sprite，`fillAmount` 是不起作用的 ——
所以**圆环走 `fillAmount`（它有自己的贴图）**，雷达扇形走**旋转 + 颜色**。

## 手势表盘 / 雷达动效

进度表盘（`DirectionMeter`）**挂在雷达（`leida`）下面**（中间垫一层 `MeterBox` 内缩 8%，再用
`AspectRatioFitter` 锁成正圆 —— 内缩是必须的，因为 `leida` 上有 `Mask`，超出圆盘的部分会被裁掉）：
一个圆环 + 四个方向箭头（当前方向那个会点亮，并随进度放大到 1.35 倍）+ 环中央的动作名。

**进度和扫描是同一件事**：`ringFill` 每帧把自己的 `localEulerAngles.z` 设成 `-雷达扫描角`
（圆环贴图旋转对称，转它看不出来），于是进度弧**从雷达当前扫到的位置开始长出来** ——
看起来就是"雷达扫出的轨迹变成了进度"，不需要 shader。松手没填满时，弧线原地缓慢回退，扇形降回常速。

雷达（`leida` 圆盘）也不再是静态图：

- **常驻慢速扫描**：扇形贴图（`RadarSweep.png`，前缘最亮、外缘略淡）放在圆盘里旋转；圆盘上本来就有 `Mask`，
  扇形会被裁进圆盘范围，正好是雷达的观感；
- **拖工具时加速**（`scanningMultiplier` 3 倍）并换成更亮的颜色；
- **判定工具有效时闪一下**（`Ping()`，由 `UIManager.ShowToolValidity(true)` 触发）。

贴图全部由构建工具用代码生成到 `Assets/Project/Resource/UI/Gesture/`（`DetroitRing.png` / `DetroitArrow.png` /
`RadarSweep.png`），形状想改就改建造工具里对应的生成方法；颜色/速度在 Inspector 上：
`DirectionMeter` 的 `DirectionProgressView`、`leida/Image` 的 `RadarEffects`
（`sweepSpeed`、`scanningMultiplier`、`alwaysSweep`、`idleColor`/`scanningColor`/`pingColor`、`pingDuration`）。

## 家具为什么"图自己就是按钮"（alpha 命中）

每件家具是一张**整帧 3840×2160** 的图层图（只画了它自己，其余全透明），铺满整个 `RoomView`。
直接给这张图开 `raycastTarget` 的话，它的矩形是整屏 —— 点空处也会被最上面那件家具吃掉。
所以用的是 **alpha 命中**：

```
layer.raycastTarget = true;
layer.alphaHitTestMinimumThreshold = 0.5f;   // 只在 alpha ≥ 0.5 的像素上吃射线
```

前提（缺一个就静默失效/报错）：贴图 **Read/Write = true**，sprite **Mesh Type = Full Rect**。
这两项由 `FixLivingRoomSpriteImport` + `LivingRoomSpritePostprocessor` 保证，运行构建工具时会自动补齐。

代价与注意点：

- 贴图可读 = 多一份内存：6 张家具图层各 3840×2160 RGBA32 ≈ 33 MB，合计约 **200 MB**。
  所以导入工具**只给夜版 `3b` 的家具图**开（`3a` 变体和背景都不用，背景是纯底图不吃射线）；
  历史上被全量开过的图会被它关回去。这是这个方案唯一真正的成本。
- 每次射线只在指针那一个像素上采样，**不存在逐像素全图扫描**的性能问题。
- 重叠处谁在前谁先接：靠前那张在不透明像素上会挡住后面的；透明处自动穿透到底下的家具（正是想要的）。
- 指针从家具的实心处滑到透明处就会触发 `OnPointerExit`（悬停高亮会闪一下），有镂空的美术（比如吊灯的分支之间）会看到。
- 收集后 `SimpleInteractableVisualState` 把图层压暗并把 `raycastTarget` 关掉，所以查过的东西不会再被点。
- 判定的形状就是美术的轮廓 —— 想让玩家**看见**这个轮廓，可以给图层加
  `UI/Scripts/BigApp/SpriteOutline.cs`（按轮廓偏移叠几份染色副本，`raycastTarget = false`）。
  它默认不自动生成：建议只在悬停时 `Build()` / 移出时 `Clear()`，否则 6 件家具各 8 份整帧副本的 overdraw 很浪费。

## 特写切换（切过去看放大图）

家具上挂一个 `InteractableCloseUp`（只放数据：特写图 + 淡入淡出时长），
`RoomView/CloseUp` 上的 `RoomCloseUpView` 就会在**检视这件家具时**把那张整帧图淡入盖住房间，
收起检视（`InvestigationHudView.CurrentInteractable` 变空）再淡出回来。

- 目前接了 **木桌 → `3b抽屉特写`**：抽屉锁着，先"看清"里面有什么，再决定用哪把工具。
- 特写必须是**和房间图层同尺寸（3840×2160）的整帧图**，才是"切过去"而不是缩放位移。
- 特写在 `RoomView` 的**最后一个子物体**上（所以是最上层），整帧不透明 ⇒ 不需要 Read/Write、不需要 alpha 命中。
- 显示时会打开 `raycastTarget`，防止隔着特写点到下面看不见的家具；**点一下特写 = 收起检视，回到房间**。
- 它同时是**落点代理**（`IInteractableHitProxy`）：特写显示期间，它就是底下那件家具。
  工具拖拽的落点解析（`ToolBeltInput.FindInteractableUnderPointer`）认到代理就用它代表的交互物。
  为什么必须要这一层：家具的点击判定是贴着自己那张透明图的 alpha 算的，**特写盖住之后屏幕上的画面
  和底下那些图的形状已经对不上了** —— 不认代理的话，玩家在抽屉正中松手会落在底下图的透明处，
  变成"松手的地方没有可以调查的东西"。同理，"能不能用"的提示也走这条查找，所以是准的。
- 特写自己**不实现拖拽**，所以方向手势照常冒泡到 `game` 上的 `InspectorDragHandler`：
  在抽屉特写上「上=拾取」就能把记录拿走。
- 想给别的家具也加：加组件 + 填 `closeUpSprite` 即可，不用改代码。

## 检视门槛（先看清，才能用工具）

`SampleInteractableRule.requiresInspection` 勾上之后，这件东西**必须先在检视里被打开过**，工具才生效。

- 现在勾在**木桌**上：抽屉锁着，不放大看不清里面有什么，所以先点开（切到抽屉特写）再拖探测器。
- "正在被检视"记在交互物自己身上（`SimpleInteractable.IsInspected`，**不序列化**，属于运行时状态），
  由 UI 在开关详情区时写：`InvestigationHudView.ShowInspector` 置位、`HideInspector` 清位。
  `HideInspector` 是所有收面板路径的必经点，所以在这里清最保险。
- 没检视就上工具：**只给提示**（`inspectionRequiredText`，默认「先放大看清这里，再用工具。」），
  **不扣 SAN、不算用错工具、不消耗耐久** —— 这是流程要求，不是玩家的错。
- 顺带的好处：拖拽时的"能不能用"提示（绿/红）也是查同一个 `CanUseTool`，所以门槛会自己教给玩家：
  没点开时拖过去是红的，点开后再拖就绿了。

## 相关脚本

| 文件 | 作用 |
| --- | --- |
| `Gameplay/Scripts/InvestigationSceneBootstrapper.cs` | 关卡组装点：初始化 manager、装备工具、设初始 flag、切 Exploration |
| `Gameplay/Scripts/Tools/ToolBeltInput.cs` | `IToolInputService`：工具槽选择 + 拟真拖拽全流程 |
| `UI/Scripts/BigApp/InvestigationHudView.cs` | 场景 UI 表现层（`ISceneUiView`），只管显示；字段指向 Main.prefab 的节点 |
| `UI/Scripts/BigApp/ItemDetailPanel.cs` | 预制体自带的详情区（异常线索/工具耐久的文案在它里面） |
| `UI/Scripts/BigApp/InspectorCloseButton.cs` | 可选：想让别的按钮"收起检视并退回探索"时挂上即可（当前场景没用到） |
| `Gameplay/Scripts/Interactables/SimpleInteractablePointerHandler.cs` | UI 版点击/悬停（原来的 `SimpleInteractableClickHandler` 要 Collider 且指针在 UI 上会 return，UI 玩法用不了） |
| `Gameplay/Scripts/Interactables/SimpleInteractableVisualState.cs` | 已收集后压暗图层 + 关掉点击（持续校正，防止悬停高亮把它盖回来） |
| `Gameplay/Scripts/Interactables/InteractableCloseUp.cs` | 家具的「特写切换」配置（只放数据：特写图 + 淡入淡出时长） |
| `Gameplay/Scripts/Interactables/IInteractableHitProxy.cs` | 落点代理接口：盖住房间的整帧图（特写层）用它告诉工具拖拽"我代表哪件家具" |
| `UI/Scripts/BigApp/RoomCloseUpView.cs` | 挂在 RoomView/CloseUp 上：按 HUD 的当前交互物淡入/淡出特写，并作为落点代理 |
| `UI/Scripts/BigApp/SpriteOutline.cs` | 可选：按轮廓给 Image 生成染色描边副本（默认不挂，建议只在悬停时生成） |
| `Gameplay/Editor/GenerateToolIcons.cs` | 代码生成物品图标：工具 5 张（工具包/紫外线灯/便携探测器/温度计/录音设备）+ 线索 3 张（水渍/头发/记录），并改写资产的 `icon`；菜单 `Tools/Project/Gameplay/Generate Item Icons` |
| `Gameplay/Editor/BuildInvestigationScene.cs` | 上面的建造工具 |
| `Gameplay/Editor/FixLivingRoomSpriteImport.cs` | 修贴图导入设置：Single + Read/Write + Full Rect（+ `AssetPostprocessor` 防止复发） |

## 已知缺口

- 玩法场景里还没接 VN（`InvestigationHudView` 的 VN 成员都实现了，字段留空；接个面板 + `VNChoiceButton` 预制体就能用）。
- `BigApp.prefab` 自身是"半成品"（`ItemDetailPanel` 四个字段全空、`boxButton1-3` 缺 `slotIndex`、`itemButton4` 是 `ButtonAction` 占位）；
  能用是因为 `Main.prefab` 把这些覆盖补上了。**要改窗口请改 `Main.prefab`，别只改 `BigApp.prefab`。**
- `life` 那条 Slider 的 `Fill` 用的是竖向四颗心的素材，但填充方向是 Horizontal —— SAN 掉一格时是"从右往左切"而不是"整颗心熄灭"，
  想改就在 `Main.prefab → BigApp → box → life → Fill` 上把 Fill Method 改成 Vertical。
- `GameState.GameOver` 没有结算界面。
- **详情区没有图标位**：`ItemDetailPanel.icon` 在 `BigApp.prefab` 和 `Main.prefab` 里都是空的
  （预制体只覆盖了 `nameText`/`descText`/`statusText`），而且 `nothink` 下只有 `Name`/`Desc`/`Status`
  三行文字、没有能放图的子物体。**所以线索图标现在只出现在收容格里**（`containmentIcons`），
  工具图标出现在工具槽和拖拽影子两处。想在详情区也显示图标，得先往 `nothink` 里加一个 Image（约 40×40，
  会挤占文字宽度）再把 `ItemDetailPanel.icon` 接上。
- 这个场景的 manager 是**场景级**的；从序幕进来时，序幕那边 `DontDestroyOnLoad` 的
  `UIManager/GameManager/InventoryManager/FlagManager` 会赢过新场景的同名 manager（重复守卫会销毁后来者）。
  单次流程没问题，但"新游戏"会复用旧库存 —— 彻底解决需要做一个常驻管理场景。
