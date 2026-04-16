# MoM 架构与视觉小说系统说明

本文档说明当前项目的 manager 协作方式、视觉小说系统使用流程，以及现阶段可直接调用的主要函数。文档基于当前代码实现，不包含尚未接入的未来工具链假设。

## 1. MoM 架构总览

本项目当前采用的是 MoM（manager-of-managers / manager registry）式架构：场景中存在多个职责独立的 manager，但不通过一个巨大的总管理器直接持有所有引用，而是通过 `Services` 静态注册表进行解耦访问。

核心文件：

- `Assets/Project/Core/Scripts/Runtime/Framework/Services.cs`
- `Assets/Project/Core/Scripts/Runtime/Managers/ManagerBehaviour.cs`
- `Assets/Project/Core/Scripts/Runtime/Managers/GameManager.cs`
- `Assets/Project/Core/Scripts/Runtime/Managers/SaveManager.cs`

### 1.1 Services 注册表

`Services` 是全局服务注册表，负责保存和查找 manager 实例。

常用函数：

| 函数 | 用途 |
| --- | --- |
| `Services.Register<T>(T service)` | 以泛型类型注册服务实例。 |
| `Services.Register(Type serviceType, object service)` | 以运行时类型注册服务实例。`ManagerBehaviour` 当前使用这个入口。 |
| `Services.TryGet<T>(out T service)` | 安全获取服务，找不到时返回 `false`。推荐大多数业务代码使用。 |
| `Services.Get<T>()` | 强制获取服务，找不到时抛异常。只适合确认服务必定存在的场景。 |
| `Services.Unregister<T>()` | 按类型注销服务。 |
| `Services.Unregister(Type serviceType)` | 按运行时类型注销服务。 |
| `Services.UnregisterInstance(object service)` | 注销所有指向该实例的注册项。`ManagerBehaviour.OnDestroy()` 当前使用这个入口。 |
| `Services.Clear()` | 清空全部注册表。通常只适合测试或全局重置。 |

推荐写法：

```csharp
if (Services.TryGet<GameManager>(out var gameManager))
{
    gameManager.SwitchState(GameState.Exploration);
}
```

除非能保证 manager 一定存在，否则优先使用 `TryGet<T>()`，避免运行时因为缺少场景对象导致异常。

### 1.2 ManagerBehaviour 生命周期

`ManagerBehaviour` 是 manager 的基础 MonoBehaviour。当前所有核心 manager 通过继承它获得自动注册能力。

关键行为：

- `Awake()` 中调用 `Services.Register(GetType(), this)`。
- 如果 `dontDestroyOnLoad == true` 且对象没有父级，则调用 `DontDestroyOnLoad(gameObject)`。
- `OnDestroy()` 中调用 `Services.UnregisterInstance(this)`，避免注册表保留已销毁对象。

因此，新增 manager 时通常只需要：

```csharp
public sealed class XxxManager : ManagerBehaviour
{
}
```

只要该对象存在于场景中，就会在 `Awake()` 后被 `Services.TryGet<XxxManager>()` 找到。

## 2. 核心 manager 职责

### 2.1 GameManager

文件：`Assets/Project/Core/Scripts/Runtime/Managers/GameManager.cs`

`GameManager` 负责全局游戏状态切换。

当前状态枚举位于 `Assets/Project/Core/Scripts/Runtime/Framework/GameState.cs`：

| 状态 | 值 | 说明 |
| --- | ---: | --- |
| `None` | 0 | 未设置状态。 |
| `Init` | 1 | 初始化。 |
| `Title` | 2 | 标题界面。 |
| `Exploration` | 3 | 探索状态。 |
| `Inspection` | 4 | 检查/调查状态。 |
| `Pause` | 5 | 暂停。 |
| `GameOver` | 6 | 游戏失败。 |
| `Victory` | 7 | 游戏胜利。 |
| `VisualNovel` | 8 | 视觉小说播放状态。 |

注意：`VisualNovel = 8` 是追加值，用于保持旧存档兼容性。不要随意重排旧枚举值。

常用函数：

| 函数 | 用途 | 常见调用方 |
| --- | --- | --- |
| `SwitchState(GameState newState)` | 切换当前游戏状态，记录 `PreviousState` 并触发 `OnStateChanged`。 | 系统逻辑、VNBridge、UI 按钮 |
| `RevertState()` | 回到上一个状态。 | VN 结束、临时界面关闭 |
| `StartNewGame()` | 删除所有存档并切到 `Init`。 | 主菜单 |
| `ContinueGame()` | 加载最新存档。 | 主菜单 |
| `TriggerGameOver()` | 切到 `GameOver`。 | 失败条件 |
| `TriggerVictory()` | 切到 `Victory`。 | 通关条件 |
| `QuickSave()` | 调用 `SaveManager.QuickSaveAsync()`。 | 快捷键、UI 按钮 |
| `QuickLoad()` | 调用 `SaveManager.QuickLoadAsync()`。 | 快捷键、UI 按钮 |

### 2.2 SaveManager

文件：`Assets/Project/Core/Scripts/Runtime/Managers/SaveManager.cs`

`SaveManager` 负责创建、写入、读取和应用存档。存档目录为：

```text
Application.persistentDataPath/Saves
```

存档聚合类型位于 `Assets/Project/Core/Scripts/Runtime/Framework/SaveData.cs`。当前 `SaveData` 包含：

- `slotId`
- `sceneName`
- `timestampTicks`
- `currentState`
- `sanity`
- `inventory`
- `flags`
- `branch`
- `evidence`
- `visualNovel`

常用函数：

| 函数 | 用途 |
| --- | --- |
| `SaveAsync(string slotId)` | 保存到指定槽位。 |
| `LoadAsync(string slotId)` | 从指定槽位加载并立即应用。 |
| `LoadLatestAsync()` | 加载时间戳最新的存档。 |
| `HasSave(string slotId)` | 判断指定槽位是否存在。 |
| `HasAnySave()` | 判断是否存在任意存档。 |
| `GetAllSaveSlots()` | 获取所有存档槽位信息。 |
| `DeleteSave(string slotId)` | 删除指定槽位。 |
| `DeleteAllSaves()` | 删除全部存档。 |
| `QuickSaveAsync()` | 保存到 `quick` 槽位。 |
| `QuickLoadAsync()` | 从 `quick` 槽位读取。 |
| `HasQuickSave()` | 判断快捷存档是否存在。 |
| `CreateSaveData()` | 从各 manager 收集当前状态，生成 `SaveData`。 |
| `ApplySaveData(SaveData data)` | 将 `SaveData` 应用回各 manager。 |

视觉小说相关存档行为：

- 保存时，如果存在 `VNDirector`，会把 `vnDirector.GetSaveData()` 写入 `SaveData.visualNovel`。
- 加载时，如果 `data.currentState == GameState.VisualNovel` 且存在 `VNDirector`，会调用 `vnDirector.LoadState(data.visualNovel)` 恢复 VN。
- 如果存档状态是 `VisualNovel` 但场景中没有 `VNDirector`，会回退到 `GameState.Exploration`。
- 如果加载的是非 VN 存档且存在 `VNDirector`，会调用 `vnDirector.LoadState(new VNSaveData { isPlaying = false })` 清理旧 VN 状态。

## 3. 视觉小说系统总览

视觉小说系统位于：

```text
Assets/Project/Narrative/Scripts
```

核心文件：

- `VNChapterConfig.cs`：作者配置数据结构。
- `VNRuntimeTypes.cs`：运行时存档和 UI 选项数据。
- `VNDirector.cs`：视觉小说播放状态机。
- `VNBridge.cs`：VN 到核心 manager/UI/音频/CG 的桥接层。
- `VNChapterStarter.cs`：场景启动器，可在场景开始时播放指定章节。

整体分层：

```text
VNChapterConfig  作者配置资产
        ↓
VNDirector       播放章节、序列、节点、选项
        ↓
VNBridge         调用 GameManager / UIManager / AudioManager / CGManager / FlagManager / InteractionManager
        ↓
Managers         展示 UI、播放音频、写入 flag、暂停交互、切换状态
```

## 4. VN 配置模型

文件：`Assets/Project/Narrative/Scripts/VNChapterConfig.cs`

### 4.1 VNChapterConfig

`VNChapterConfig` 是一个 ScriptableObject，可通过菜单创建：

```text
Project/Narrative/VN Chapter Config
```

字段含义：

| 字段/属性 | 用途 |
| --- | --- |
| `chapterId` / `ChapterId` | 章节唯一 ID。存档恢复会使用它查找章节。 |
| `title` / `Title` | 章节标题。 |
| `startSequenceId` / `StartSequenceId` | 章节开始时播放的 sequence ID。 |
| `setFlagsOnStart` / `SetFlagsOnStart` | 章节开始时设置为 true 的 flag。 |
| `clearFlagsOnStart` / `ClearFlagsOnStart` | 章节开始时设置为 false 的 flag。 |
| `sequences` / `Sequences` | 章节包含的序列列表。 |
| `endAction` / `EndAction` | 章节自然结束后的动作。 |

### 4.2 VNSequenceConfig

Sequence 是章节内的一段流程。

| 字段 | 用途 |
| --- | --- |
| `sequenceId` | 序列唯一 ID。 |
| `requiredFlags` | 进入该序列必须满足的 flag 条件。 |
| `blockedFlags` | 阻止进入该序列的 flag 条件。 |
| `nodes` | 该序列下的节点列表。 |
| `nextSequenceId` | 当前序列无法进入或节点结束后可跳转的下一个序列。 |

### 4.3 VNNodeConfig

Node 是实际显示的一句或一段 VN 内容。

| 字段 | 用途 |
| --- | --- |
| `nodeId` | 节点唯一 ID。 |
| `speakerId` | 说话人 ID。当前主要用于数据标识。 |
| `speakerName` | 显示用说话人名称。 |
| `text` | 对白文本。 |
| `secondsPerCharacter` | 打字机速度。 |
| `backgroundId` | 背景 ID，交给 `CGManager.ShowBackground()`。 |
| `cgId` | CG ID，交给 `CGManager.PlayCG()`。 |
| `screenEffectId` | 屏幕效果 ID；当前桥接为 `UIManager.PlayGlitchEffect(0.5f)`。 |
| `portraitId` | 立绘 ID。 |
| `portraitPosition` | 立绘位置：`Center` / `Left` / `Right`。 |
| `voiceId` | 语音 ID，交给 `AudioManager.PlayVoice()`。 |
| `sfxId` | 音效 ID，交给 `AudioManager.PlaySFX()`。 |
| `bgmId` | BGM ID，交给 `AudioManager.PlayBGM()`。 |
| `autoContinue` | 是否在显示后自动推进。 |
| `autoContinueDelay` | 自动推进延迟秒数。 |
| `waitForExternalSignal` | 是否等待外部系统调用 `NotifyExternalAdvanceReady()` 后再允许推进。 |
| `requiredFlags` | 节点显示所需 flag 条件。 |
| `blockedFlags` | 阻止显示该节点的 flag 条件。 |
| `setFlags` | 节点显示时设置为 true 的 flag。 |
| `clearFlags` | 节点显示时设置为 false 的 flag。 |
| `nextNodeId` | 指定下一个节点。为空时默认走列表中的下一个节点。 |
| `elseNodeId` | 当前节点条件不满足时跳转的备用节点。 |
| `choices` | 节点显示完成后可展示的选项。 |

### 4.4 VNChoiceConfig

Choice 是节点下的玩家选项。

| 字段 | 用途 |
| --- | --- |
| `choiceId` | 选项唯一 ID。 |
| `text` | 选项显示文本。 |
| `targetSequenceId` | 选择后跳转的目标序列。优先级高于 `targetNodeId`。 |
| `targetNodeId` | 选择后跳转的目标节点。 |
| `requiredFlags` | 选项出现所需 flag 条件。 |
| `blockedFlags` | 阻止选项出现的 flag 条件。 |
| `setFlags` | 选择后设置为 true 的 flag。 |
| `clearFlags` | 选择后设置为 false 的 flag。 |

### 4.5 VNEndAction

章节结束动作：

| 类型 | 用途 |
| --- | --- |
| `None` | 不执行额外动作。 |
| `ReturnToPreviousState` | 调用 `GameManager.RevertState()`。 |
| `SwitchGameState` | 切换到 `targetGameState`。 |
| `LoadScene` | 通过 `SceneFlowManager.LoadSceneAsync(targetSceneName)` 加载场景。 |
| `StartChapter` | 根据 `targetChapterId` 查找并启动下一章。 |

## 5. VN 运行流程

文件：`Assets/Project/Narrative/Scripts/VNDirector.cs`

`VNDirector` 是 VN 播放状态机，负责：

- 启动章节。
- 查找并播放 sequence。
- 查找并播放 node。
- 应用 flag。
- 展示对白、立绘、音频、CG。
- 展示并处理选项。
- 处理自动推进、外部信号等待、章节结束动作。
- 提供 VN 存档数据和恢复逻辑。

### 5.1 启动章节

```csharp
await vnDirector.StartChapter(chapterConfig);
```

启动时会：

1. 清理当前 VN 运行状态。
2. 设置 `isPlaying = true`。
3. 清空 visited node 记录。
4. 通过 `VNBridge.EnterVisualNovelState()` 进入 VN 状态。
5. 应用章节开始 flag。
6. 播放 `chapter.StartSequenceId`。

如果 `VNDirector` 的 `startupChapter` 字段不为空，`Start()` 会自动启动它。

### 5.2 播放序列

```csharp
await vnDirector.PlaySequence(sequenceId);
```

播放 sequence 时会：

1. 检查当前 chapter 是否存在。
2. 检查 sequenceId 是否有效，并用 visited set 防止 sequence 循环。
3. 查找目标 sequence。
4. 检查 sequence 的 required/blocked flags。
5. 条件通过时播放第一个符合条件的 node。
6. 条件不通过时尝试 `nextSequenceId`，否则结束章节。

### 5.3 播放节点

```csharp
await vnDirector.PlayNode(nodeId);
```

播放 node 时会：

1. 检查当前 sequence 是否存在。
2. 检查 nodeId 是否有效，并用 visited set 防止 node 跳转循环。
3. 查找目标 node。
4. 检查 node 的 required/blocked flags。
5. 条件不满足时跳到 `elseNodeId` 或默认下一个节点。
6. 条件满足时隐藏旧选项、应用节点 flag、展示节点内容。
7. 根据当前节点设置 `isWaitingForChoice`、`isWaitingForExternalSignal`。
8. 如果 `autoContinue == true` 且没有选项/外部等待，则延迟后自动 `Advance()`。

### 5.4 推进对白

```csharp
await vnDirector.Advance();
```

`Advance()` 的行为分两段：

1. 如果当前行还没完整显示：
   - 设置 `isLineFullyDisplayed = true`
   - 调用 `VNBridge.CompleteLine()`
   - 如果有可用选项，则展示选项
2. 如果当前行已经完整显示：
   - 若正在等待选项或外部信号，则不推进
   - 否则播放下一个节点

因此同一个“继续”按钮通常可以直接绑定到 `VNDirector.Advance()`。

### 5.5 选择选项

```csharp
await vnDirector.Choose(choiceId);
```

选择时会：

1. 校验当前是否正在播放、当前节点是否存在、choiceId 是否有效。
2. 只允许选择当前节点下且条件满足的 choice。
3. 应用 choice 的 set/clear flags。
4. 隐藏选项。
5. 如果配置了 `targetSequenceId`，跳到目标 sequence。
6. 否则跳到 `targetNodeId` 或默认下一个节点。

当前 `UIManager.SelectVNChoice(choiceId)` 会触发 `VNDirector.Choose(choiceId)` 的回调，并且是 one-shot：合法选项会先清空回调再执行，避免双击重复触发。

### 5.6 等待外部信号

如果节点设置了：

```csharp
waitForExternalSignal = true
```

则普通 `Advance()` 不会继续推进。外部系统需要在条件满足时调用：

```csharp
await vnDirector.NotifyExternalAdvanceReady();
```

该函数只会在 VN 正在播放且确实等待外部信号时生效。

## 6. VNBridge 桥接行为

文件：`Assets/Project/Narrative/Scripts/VNBridge.cs`

`VNBridge` 不保存章节状态，它负责把 VN 节点翻译成 manager 调用。

常用函数：

| 函数 | 行为 |
| --- | --- |
| `ConditionsMet(requiredFlags, blockedFlags)` | 使用 `FlagManager.Get()` 判断 flag 条件。没有 `FlagManager` 时，未设置 flag 视为 false。 |
| `ApplyFlags(setFlags, clearFlags)` | 使用 `FlagManager.Set()` 写入 flag。 |
| `EnterVisualNovelState()` | 暂停交互、切到 `GameState.VisualNovel`、显示 VN 面板。 |
| `ExitVisualNovelState()` | 隐藏 VN 选项和面板、恢复交互、调用 `GameManager.RevertState()`。 |
| `PresentNode(VNNodeConfig node)` | 根据 node 播放背景、CG、BGM、SFX、语音、立绘、对白。 |
| `CompleteLine()` | 调用 `UIManager.CompleteVNLine()`。 |
| `ShowChoices(choices, onSelected)` | 调用 `UIManager.ShowVNChoices()`。 |
| `HideChoices()` | 调用 `UIManager.HideVNChoices()`。 |

当前桥接到的 manager：

- `FlagManager`
- `InteractionManager`
- `GameManager`
- `UIManager`
- `CGManager`
- `AudioManager`

## 7. UIManager 中的 VN 接口

文件：`Assets/Project/Core/Scripts/Runtime/Managers/UIManager.cs`

当前 VN UI 仍是日志 stub，但接口已经固定下来，后续真实 UI 可以沿用这些函数进行绑定。

| 函数 | 用途 |
| --- | --- |
| `ShowVNPanel()` | 显示 VN 面板。当前输出日志。 |
| `HideVNPanel()` | 隐藏 VN 面板。当前输出日志。 |
| `ShowVNLine(string speakerName, string text, float secondsPerChar)` | 显示一行 VN 对白。当前输出日志。 |
| `CompleteVNLine()` | 立即完成当前对白显示。当前输出日志。 |
| `ShowVNChoices(IReadOnlyList<VNChoiceViewData> choices, Action<string> onSelected)` | 缓存选项和选择回调，并输出日志。 |
| `SelectVNChoice(string choiceId)` | UI 按钮可调用此函数选择选项。合法选择会触发缓存回调。 |
| `HideVNChoices()` | 清理当前选项和回调。 |
| `ShowVNPortrait(string portraitId, VNPortraitPosition position)` | 显示立绘。当前输出日志。 |
| `ClearVNPortraits()` | 清理立绘。当前输出日志。 |

后续接真实 UI 时，建议保持这些函数签名不变，把内部日志替换为实际面板、文本、按钮、立绘控制。

## 8. VN 存档与恢复

VN 存档类型位于 `Assets/Project/Narrative/Scripts/VNRuntimeTypes.cs`：

```csharp
public sealed class VNSaveData
{
    public bool isPlaying;
    public string chapterId;
    public string sequenceId;
    public string nodeId;
    public List<string> visitedNodeIds = new();
}
```

`VNDirector.GetSaveData()` 会保存：

- 当前是否正在播放。
- 当前 chapterId。
- 当前 sequenceId。
- 当前 nodeId。
- 已访问 nodeId 列表。

`VNDirector.LoadState(VNSaveData data)` 会：

1. 恢复 visited node 列表。
2. 如果 `data == null` 或 `data.isPlaying == false`，清理 VN 状态但不执行章节结束动作。
3. 通过 chapterId 查找章节。
4. 找不到章节时清理 VN 状态但不执行旧章节结束动作。
5. 找到章节后进入 VN 状态并恢复 sequence/node。
6. 恢复节点时会禁止 auto-continue，避免读档瞬间自动跳走。
7. 恢复后会补全当前行显示，并在需要时重新展示选项。

章节查找逻辑：

- 如果 `startupChapter.ChapterId` 匹配，则优先使用它。
- 否则通过 `Resources.LoadAll<VNChapterConfig>(string.Empty)` 查找全部 Resources 下的章节资产。

因此，如果希望 VN 存档能跨场景恢复章节，需要确保目标 `VNChapterConfig` 可被 `ResolveChapter()` 找到。

## 9. 最小使用流程

### 9.1 创建章节资产

在 Unity 中创建：

```text
Create > Project > Narrative > VN Chapter Config
```

填写：

1. `chapterId`：例如 `chapter_prologue`。
2. `title`：章节标题。
3. `startSequenceId`：起始 sequence 的 ID。
4. `sequences`：至少添加一个 `VNSequenceConfig`。
5. 在 sequence 中添加 nodes。
6. 在 node 中填写 `nodeId`、`speakerName`、`text` 等字段。
7. 如需选项，在 node 的 `choices` 中添加 `VNChoiceConfig`。

### 9.2 场景中放置 VNDirector

在场景中创建一个 GameObject，挂载：

```text
VNDirector
```

可选做法：

- 直接把章节资产拖到 `VNDirector.startupChapter`，场景开始时自动播放。
- 或者通过其他脚本手动调用 `StartChapter()`。

### 9.3 使用 VNChapterStarter 自动启动

也可以在场景中放置：

```text
VNChapterStarter
```

设置：

- `chapter`：要播放的 `VNChapterConfig`。
- `playOnStart`：是否在 `Start()` 自动播放。

`VNChapterStarter` 会查找场景中的 `VNDirector` 并调用：

```csharp
await director.StartChapter(chapter);
```

### 9.4 绑定继续按钮

继续按钮可调用：

```csharp
await vnDirector.Advance();
```

第一次点击通常补全当前文字；文字已完整后再次点击进入下一节点。

### 9.5 绑定选项按钮

真实 UI 接入时，每个选项按钮应调用：

```csharp
uiManager.SelectVNChoice(choiceId);
```

`UIManager` 会调用 `ShowVNChoices()` 里缓存的回调，最终进入 `VNDirector.Choose(choiceId)`。

### 9.6 外部事件推进

如果剧情节点需要等待玩法系统完成某件事，例如调查、动画、计时或解谜完成，则节点设置：

```text
waitForExternalSignal = true
```

外部系统完成后调用：

```csharp
await vnDirector.NotifyExternalAdvanceReady();
```

## 10. 可调用函数/API 清单

### 10.1 Services

| API | 用途 |
| --- | --- |
| `Services.TryGet<T>(out T service)` | 安全获取 manager。推荐默认使用。 |
| `Services.Get<T>()` | 强制获取 manager，缺失时报错。 |
| `Services.Register<T>(T service)` | 手动注册服务。 |
| `Services.Register(Type serviceType, object service)` | 以运行时类型注册服务。 |
| `Services.UnregisterInstance(object service)` | 注销指定实例。 |
| `Services.Clear()` | 清空注册表。 |

### 10.2 GameManager

| API | 用途 |
| --- | --- |
| `SwitchState(GameState newState)` | 切换游戏状态。 |
| `RevertState()` | 回到上一状态。 |
| `StartNewGame()` | 删除所有存档并进入初始化状态。 |
| `ContinueGame()` | 加载最新存档。 |
| `TriggerGameOver()` | 进入失败状态。 |
| `TriggerVictory()` | 进入胜利状态。 |
| `QuickSave()` | 快捷保存。 |
| `QuickLoad()` | 快捷读取。 |
| `OnStateChanged` | 状态变化事件，参数为旧状态和新状态。 |
| `CurrentState` | 当前状态。 |
| `PreviousState` | 上一个状态。 |

### 10.3 SaveManager

| API | 用途 |
| --- | --- |
| `SaveAsync(string slotId)` | 保存到指定槽位。 |
| `LoadAsync(string slotId)` | 读取并应用指定槽位。 |
| `LoadLatestAsync()` | 读取并应用最新槽位。 |
| `HasSave(string slotId)` | 检查指定槽位是否存在。 |
| `HasAnySave()` | 检查是否存在任意存档。 |
| `GetAllSaveSlots()` | 获取所有存档槽位信息。 |
| `DeleteSave(string slotId)` | 删除指定槽位。 |
| `DeleteAllSaves()` | 删除所有存档。 |
| `QuickSaveAsync()` | 保存到 `quick` 槽位。 |
| `QuickLoadAsync()` | 读取 `quick` 槽位。 |
| `HasQuickSave()` | 检查是否存在快捷存档。 |
| `CreateSaveData()` | 生成当前完整存档数据。 |
| `ApplySaveData(SaveData data)` | 应用存档数据。 |

### 10.4 VNDirector

| API | 用途 |
| --- | --- |
| `StartChapter(VNChapterConfig chapter)` | 启动指定章节。 |
| `PlaySequence(string sequenceId)` | 播放当前章节内的指定序列。 |
| `PlayNode(string nodeId)` | 播放当前序列内的指定节点。 |
| `Advance()` | 推进 VN：补全文字或进入下一节点。 |
| `Choose(string choiceId)` | 选择当前节点下的指定选项。 |
| `NotifyExternalAdvanceReady()` | 通知等待外部信号的节点可以继续。 |
| `GetSaveData()` | 获取 VN 存档数据。 |
| `LoadState(VNSaveData data)` | 恢复或清理 VN 状态。 |
| `IsPlaying` | 当前是否正在播放 VN。 |
| `CurrentChapterId` | 当前章节 ID。 |
| `CurrentSequenceId` | 当前序列 ID。 |
| `CurrentNodeId` | 当前节点 ID。 |

### 10.5 VNBridge

`VNBridge` 当前由 `VNDirector` 内部持有，一般不需要外部直接调用。它的主要 API 是：

| API | 用途 |
| --- | --- |
| `ConditionsMet(...)` | 检查 flag 条件。 |
| `ApplyFlags(...)` | 应用 set/clear flags。 |
| `EnterVisualNovelState()` | 进入 VN 状态。 |
| `ExitVisualNovelState()` | 退出 VN 状态。 |
| `PresentNode(VNNodeConfig node)` | 表现一个 VN 节点。 |
| `CompleteLine()` | 补全当前对白。 |
| `ShowChoices(...)` | 展示选项。 |
| `HideChoices()` | 隐藏选项。 |

### 10.6 UIManager 的 VN 相关 API

| API | 用途 |
| --- | --- |
| `ShowVNPanel()` | 显示 VN 面板。 |
| `HideVNPanel()` | 隐藏 VN 面板。 |
| `ShowVNLine(string speakerName, string text, float secondsPerChar)` | 显示对白。 |
| `CompleteVNLine()` | 补全文字。 |
| `ShowVNChoices(IReadOnlyList<VNChoiceViewData> choices, Action<string> onSelected)` | 展示并缓存选项。 |
| `SelectVNChoice(string choiceId)` | 选择指定选项，供 UI 按钮调用。 |
| `HideVNChoices()` | 隐藏并清理选项。 |
| `ShowVNPortrait(string portraitId, VNPortraitPosition position)` | 显示立绘。 |
| `ClearVNPortraits()` | 清理立绘。 |

### 10.7 VNChapterStarter

| API/字段 | 用途 |
| --- | --- |
| `chapter` | 要自动播放的章节。 |
| `playOnStart` | 是否在 `Start()` 中自动播放。 |

`VNChapterStarter` 适合临时测试或按场景自动播放某个章节。

## 11. 当前限制与注意事项

1. **VN UI 还不是真实界面**  
   `UIManager` 中的 VN 函数当前主要是 `Debug.Log`，只保存了选项回调所需的最小状态。后续需要把这些函数接到实际 Canvas、Text、Button、Portrait、Panel 上。

2. **不要随意修改 GameState 枚举值**  
   `GameState` 会写入存档。`VisualNovel = 8` 是追加值，旧值应保持稳定。

3. **章节 ID、序列 ID、节点 ID、选项 ID 需要保持唯一和稳定**  
   存档和跳转都依赖这些 ID。重命名 ID 会影响读档和跳转。

4. **VNDirector 已有循环保护，但数据仍应避免死循环**  
   sequence 和 node 播放链路中有 visited set 保护，会在检测到循环或无效 ID 时结束章节，但这属于兜底，不应依赖它作为正常流程。

5. **读档恢复不会触发 auto-continue**  
   恢复节点时会抑制自动推进，避免读档瞬间跳过当前节点。

6. **非 VN 存档会清理 VN 状态**  
   加载非 `GameState.VisualNovel` 的存档时，如果场景中有 `VNDirector`，会显式清理 VN 状态和 UI。

7. **VNBridge.ExitVisualNovelState() 会调用 RevertState()**  
   默认退出 VN 时会回到进入 VN 前的状态。若章节 `EndAction` 同时配置了切状态、切场景或启动下一章，需要注意状态流向是否符合预期。

8. **跨场景恢复章节需要章节资产可解析**  
   `VNDirector.ResolveChapter()` 会优先匹配 `startupChapter`，否则从 `Resources` 中查找 `VNChapterConfig`。如果章节不在可解析范围内，读档会清理 VN 状态。
