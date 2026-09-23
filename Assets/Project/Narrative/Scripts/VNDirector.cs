using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using UnityEngine;

namespace Project.Narrative.Scripts
{
    public sealed class VNDirector : ManagerBehaviour, IVNSaveable
    {
        [SerializeField] private VNChapterConfig startupChapter;

        private readonly HashSet<string> visitedNodeIds = new();
        private readonly VNBridge bridge = new();

        private VNChapterConfig currentChapter;
        private VNSequenceConfig currentSequence;
        private VNNodeConfig currentNode;
        private bool isWaitingForChoice;
        private bool isLineFullyDisplayed;
        private bool isWaitingForExternalSignal;
        private bool isPlaying;

        [Header("快进 / 自动播放")]
        [Tooltip("快进的推进间隔（秒）。")]
        [SerializeField] private float skipAdvanceInterval = 0.04f;
        [Tooltip("自动播放时，一行显示完之后停留的秒数。")]
        [SerializeField] private float autoAdvanceDelay = 1.6f;
        [Tooltip("自动播放等待 UI 回报「这一行显示完了」的兜底上限（秒）。")]
        [SerializeField] private float autoLineWaitTimeout = 6f;

        private bool isSkipping;
        private bool isAutoPlaying;
        private bool isAutoDriverRunning;

        /// <summary>快进 / 自动播放 / 播放状态发生变化时触发，UI 用它刷新按钮表现。</summary>
        public event Action ModeChanged;

        public bool IsSkipping => isSkipping;
        public bool IsAutoPlaying => isAutoPlaying;
        public bool IsPlaying => isPlaying;

        private bool isInputLocked;

        /// <summary>过场演出期间锁住推进：点击推进、快进、自动播放都不生效。</summary>
        public bool IsInputLocked => isInputLocked;

        public void SetInputLocked(bool locked)
        {
            isInputLocked = locked;
        }
        public string CurrentChapterId => currentChapter != null ? currentChapter.ChapterId : string.Empty;
        public string CurrentSequenceId => currentSequence != null ? currentSequence.sequenceId : string.Empty;
        public string CurrentNodeId => currentNode != null ? currentNode.nodeId : string.Empty;


        public async UniTask StartChapter(VNChapterConfig chapter)
        {
            if (chapter == null)
            {
                return;
            }

            currentChapter = chapter;
            currentSequence = null;
            currentNode = null;
            isWaitingForChoice = false;
            isWaitingForExternalSignal = false;
            isLineFullyDisplayed = false;
            isPlaying = true;
            visitedNodeIds.Clear();
            RaiseModeChanged();

            bridge.EnterVisualNovelState();
            bridge.ApplyFlags(chapter.SetFlagsOnStart, chapter.ClearFlagsOnStart);
            await PlaySequence(chapter.StartSequenceId);
        }

        public async UniTask PlaySequence(string sequenceId)
        {
            await PlaySequence(sequenceId, new HashSet<string>());
        }

        private async UniTask PlaySequence(string sequenceId, HashSet<string> visitedSequenceIds)
        {
            if (currentChapter == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sequenceId) || !visitedSequenceIds.Add(sequenceId))
            {
                Debug.LogWarning($"VN sequence loop or invalid sequence detected: {sequenceId}");
                EndChapter();
                return;
            }

            currentSequence = FindSequence(sequenceId);
            if (currentSequence == null)
            {
                EndChapter();
                return;
            }

            if (!bridge.ConditionsMet(currentSequence.requiredFlags, currentSequence.blockedFlags))
            {
                if (!string.IsNullOrWhiteSpace(currentSequence.nextSequenceId))
                {
                    await PlaySequence(currentSequence.nextSequenceId, visitedSequenceIds);
                }
                else
                {
                    EndChapter();
                }
                return;
            }

            await PlayNode(GetFirstEligibleNodeId(currentSequence));
        }

        public async UniTask PlayNode(string nodeId)
        {
            await PlayNode(nodeId, new HashSet<string>());
        }

        private async UniTask PlayNode(string nodeId, HashSet<string> visitedNodeIdsInChain, bool suppressAutoContinue = false)
        {
            if (currentSequence == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(nodeId) || !visitedNodeIdsInChain.Add(nodeId))
            {
                Debug.LogWarning($"VN node loop or invalid node detected: {nodeId}");
                if (!string.IsNullOrWhiteSpace(currentSequence.nextSequenceId))
                {
                    await PlaySequence(currentSequence.nextSequenceId);
                }
                else
                {
                    EndChapter();
                }
                return;
            }

            currentNode = FindNode(currentSequence, nodeId);
            if (currentNode == null)
            {
                if (!string.IsNullOrWhiteSpace(currentSequence.nextSequenceId))
                {
                    await PlaySequence(currentSequence.nextSequenceId);
                }
                else
                {
                    EndChapter();
                }
                return;
            }

            if (!bridge.ConditionsMet(currentNode.requiredFlags, currentNode.blockedFlags))
            {
                var fallbackNodeId = !string.IsNullOrWhiteSpace(currentNode.elseNodeId)
                    ? currentNode.elseNodeId
                    : GetNextNodeId(currentNode);
                await PlayNode(fallbackNodeId, visitedNodeIdsInChain, suppressAutoContinue);
                return;
            }

            bridge.HideChoices();
            bridge.ApplyFlags(currentNode.setFlags, currentNode.clearFlags);

            // 先把本节点的状态摆好再表现：瞬显 UI 会在 PresentNode 里同步回调 NotifyLineDisplayed，
            // 那时 isWaitingForChoice / isLineFullyDisplayed 必须已经是本节点的值。
            isWaitingForChoice = HasEligibleChoices(currentNode);
            isWaitingForExternalSignal = currentNode.waitForExternalSignal;
            isLineFullyDisplayed = false;

            bridge.PresentNode(currentNode);
            visitedNodeIds.Add(currentNode.nodeId);

            if (!suppressAutoContinue && currentNode.autoContinue && !isWaitingForChoice && !isWaitingForExternalSignal)
            {
                // 快进 / 自动播放由各自的循环统一步进，这里不再叠加一次自动推进，避免推进两次跳行。
                if (!isSkipping && !isAutoPlaying)
                {
                    await UniTask.Delay((int)(Mathf.Max(0f, currentNode.autoContinueDelay) * 1000f));
                    await Advance();
                }
            }
        }

        public async UniTask Advance()
        {
            if (!isPlaying || currentNode == null || isInputLocked)
            {
                return;
            }

            if (!isLineFullyDisplayed)
            {
                CompleteCurrentLine();
                return;
            }

            if (isWaitingForChoice || isWaitingForExternalSignal)
            {
                return;
            }

            await PlayNode(GetNextNodeId(currentNode));
        }

        public async UniTask Choose(string choiceId)
        {
            if (!isPlaying || currentNode == null || string.IsNullOrWhiteSpace(choiceId))
            {
                return;
            }

            var choice = FindChoice(choiceId);
            if (choice == null)
            {
                return;
            }

            bridge.ApplyFlags(choice.setFlags, choice.clearFlags);
            isWaitingForChoice = false;
            bridge.HideChoices();

            if (!string.IsNullOrWhiteSpace(choice.targetSequenceId))
            {
                await PlaySequence(choice.targetSequenceId);
                return;
            }

            await PlayNode(!string.IsNullOrWhiteSpace(choice.targetNodeId) ? choice.targetNodeId : GetNextNodeId(currentNode));
        }

        public async UniTask NotifyExternalAdvanceReady()
        {
            if (!isPlaying || !isWaitingForExternalSignal)
            {
                return;
            }

            isWaitingForExternalSignal = false;
            await Advance();
        }

        // ---- 快进 / 自动播放 ----

        /// <summary>开关快进。与自动播放互斥（快进优先，打开快进会关掉自动）。</summary>
        public void SetSkipMode(bool enabled)
        {
            if (isSkipping == enabled)
            {
                return;
            }

            isSkipping = enabled;
            if (enabled)
            {
                isAutoPlaying = false;
            }

            Debug.Log(enabled ? "[VN] 快进：开" : "[VN] 快进：关");
            RaiseModeChanged();
            StartAutoDriver();
        }

        /// <summary>开关自动播放。与快进互斥。</summary>
        public void SetAutoMode(bool enabled)
        {
            if (isAutoPlaying == enabled)
            {
                return;
            }

            isAutoPlaying = enabled;
            if (enabled)
            {
                isSkipping = false;
            }

            Debug.Log(enabled ? "[VN] 自动播放：开" : "[VN] 自动播放：关");
            RaiseModeChanged();
            StartAutoDriver();
        }

        public void ToggleSkipMode()
        {
            SetSkipMode(!isSkipping);
        }

        public void ToggleAutoMode()
        {
            SetAutoMode(!isAutoPlaying);
        }

        /// <summary>
        /// UI 层确认当前行已经完整显示：接打字机时在打字结束回调里调；
        /// 瞬显 UI（当前实现）则在设置完文本后立刻调。
        /// </summary>
        public void NotifyLineDisplayed()
        {
            if (!isPlaying || currentNode == null || isLineFullyDisplayed)
            {
                return;
            }

            CompleteCurrentLine();
        }

        private void CompleteCurrentLine()
        {
            isLineFullyDisplayed = true;
            bridge.CompleteLine();
            if (isWaitingForChoice)
            {
                PresentChoices();
            }
        }

        private void ResetPlaybackModes()
        {
            if (!isSkipping && !isAutoPlaying)
            {
                return;
            }

            isSkipping = false;
            isAutoPlaying = false;
            RaiseModeChanged();
        }

        private void RaiseModeChanged()
        {
            ModeChanged?.Invoke();
        }

        private void StartAutoDriver()
        {
            if (isAutoDriverRunning || (!isSkipping && !isAutoPlaying))
            {
                return;
            }

            RunAutoDriverAsync().Forget();
        }

        /// <summary>
        /// 快进 / 自动播放唯一的步进循环：中途切换模式只改目标状态，不会产生互相打架的多条循环。
        /// 遇到选项停下等玩家（快进会顺手把自己关掉），遇到 waitForExternalSignal 节点等玩法系统给信号。
        /// </summary>
        private async UniTaskVoid RunAutoDriverAsync()
        {
            isAutoDriverRunning = true;
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                while (isPlaying && (isSkipping || isAutoPlaying))
                {
                    if (IsPaused())
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    if (isWaitingForChoice)
                    {
                        if (isSkipping)
                        {
                            SetSkipMode(false);
                            break;
                        }

                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    if (isWaitingForExternalSignal)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        continue;
                    }

                    if (isSkipping)
                    {
                        await Advance();
                        if (isPlaying && isSkipping && !isWaitingForChoice && !isWaitingForExternalSignal)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, skipAdvanceInterval)), cancellationToken: token);
                        }

                        continue;
                    }

                    await WaitLineDisplayedAsync(token);
                    if (!isPlaying || !isAutoPlaying || isWaitingForChoice || isWaitingForExternalSignal)
                    {
                        continue;
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, autoAdvanceDelay)), cancellationToken: token);
                    if (isPlaying && isAutoPlaying && !isWaitingForChoice && !isWaitingForExternalSignal)
                    {
                        await Advance();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 对象销毁时取消，正常路径。
            }
            finally
            {
                isAutoDriverRunning = false;
                if (!isPlaying)
                {
                    // 循环因为章节结束而退出时，把模式一起收干净，UI 才能灭掉高亮。
                    ResetPlaybackModes();
                }
            }
        }

        /// <summary>等 UI 报告这一行显示完；UI 没报（例如还没接打字机）时有兜底上限，避免永久卡住。</summary>
        private async UniTask WaitLineDisplayedAsync(CancellationToken token)
        {
            if (isLineFullyDisplayed)
            {
                return;
            }

            var elapsed = 0f;
            var timeout = Mathf.Max(0.1f, autoLineWaitTimeout);
            while (isPlaying && isAutoPlaying && !isLineFullyDisplayed && elapsed < timeout)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.unscaledDeltaTime;
            }

            if (isPlaying && !isLineFullyDisplayed)
            {
                CompleteCurrentLine();
            }
        }

        private static bool IsPaused()
        {
            return Services.TryGet<GameManager>(out var gameManager) && gameManager.CurrentState == GameState.Pause;
        }

        public VNSaveData GetSaveData()
        {
            return new VNSaveData
            {
                isPlaying = isPlaying,
                chapterId = CurrentChapterId,
                sequenceId = CurrentSequenceId,
                nodeId = CurrentNodeId,
                visitedNodeIds = new List<string>(visitedNodeIds)
            };
        }

        public void LoadState(VNSaveData data)
        {
            ResetPlaybackModes();
            visitedNodeIds.Clear();
            if (data?.visitedNodeIds != null)
            {
                foreach (var nodeId in data.visitedNodeIds)
                {
                    if (!string.IsNullOrWhiteSpace(nodeId))
                    {
                        visitedNodeIds.Add(nodeId);
                    }
                }
            }

            if (data == null || !data.isPlaying)
            {
                ClearChapterState(runEndAction: false);
                return;
            }

            var chapter = ResolveChapter(data.chapterId);
            if (chapter == null)
            {
                Debug.LogWarning($"VN chapter not found while loading: {data.chapterId}");
                ClearChapterState(runEndAction: false);
                return;
            }

            currentChapter = chapter;
            isPlaying = true;
            bridge.EnterVisualNovelState();
            LoadSequenceAndNode(data.sequenceId, data.nodeId).Forget();
        }

        private async UniTask LoadSequenceAndNode(string sequenceId, string nodeId)
        {
            currentSequence = FindSequence(sequenceId);
            if (currentSequence == null)
            {
                await PlaySequence(currentChapter.StartSequenceId);
                return;
            }

            var targetNodeId = string.IsNullOrWhiteSpace(nodeId) ? GetFirstEligibleNodeId(currentSequence) : nodeId;
            await PlayNode(targetNodeId, new HashSet<string>(), suppressAutoContinue: true);

            if (!isPlaying || currentNode == null || currentNode.nodeId != targetNodeId)
            {
                return;
            }

            // 正常情况下 PresentNode 里已经通过 NotifyLineDisplayed 补全了这一行并展示了选项；
            // 这里只是兜底，防止 UI 没有回报「显示完成」时读档卡在未完成状态。
            if (!isLineFullyDisplayed)
            {
                CompleteCurrentLine();
            }
        }

        private void PresentChoices()
        {
            var choices = GetEligibleChoices(currentNode);
            if (choices.Count == 0)
            {
                return;
            }

            var viewData = new List<VNChoiceViewData>(choices.Count);
            foreach (var choice in choices)
            {
                viewData.Add(new VNChoiceViewData(choice.choiceId, choice.text));
            }

            bridge.ShowChoices(viewData, id => Choose(id).Forget());
        }

        private void EndChapter()
        {
            ClearChapterState(runEndAction: true);
        }

        private void ClearChapterState(bool runEndAction)
        {
            var endAction = runEndAction && currentChapter != null ? currentChapter.EndAction : null;
            ResetPlaybackModes();
            isPlaying = false;
            isWaitingForChoice = false;
            isWaitingForExternalSignal = false;
            isLineFullyDisplayed = false;
            currentSequence = null;
            currentNode = null;
            bridge.ExitVisualNovelState();
            currentChapter = null;

            if (endAction != null)
            {
                HandleEndAction(endAction).Forget();
            }
        }

        private async UniTask HandleEndAction(VNEndAction endAction)
        {
            if (endAction == null)
            {
                return;
            }

            switch (endAction.actionType)
            {
                case VNEndActionType.ReturnToPreviousState:
                    if (Services.TryGet<GameManager>(out var revertGameManager))
                    {
                        revertGameManager.RevertState();
                    }
                    break;
                case VNEndActionType.SwitchGameState:
                    if (Services.TryGet<GameManager>(out var gameManager))
                    {
                        gameManager.SwitchState(endAction.targetGameState);
                    }
                    break;
                case VNEndActionType.LoadScene:
                    if (Services.TryGet<SceneFlowManager>(out var sceneFlowManager))
                    {
                        await sceneFlowManager.LoadSceneAsync(endAction.targetSceneName);
                    }
                    break;
                case VNEndActionType.StartChapter:
                    var nextChapter = ResolveChapter(endAction.targetChapterId);
                    if (nextChapter != null)
                    {
                        await StartChapter(nextChapter);
                    }
                    break;
            }
        }

        private VNChapterConfig ResolveChapter(string chapterId)
        {
            if (startupChapter != null && startupChapter.ChapterId == chapterId)
            {
                return startupChapter;
            }

            if (string.IsNullOrWhiteSpace(chapterId))
            {
                return null;
            }

            var chapters = Resources.LoadAll<VNChapterConfig>(string.Empty);
            foreach (var chapter in chapters)
            {
                if (chapter != null && chapter.ChapterId == chapterId)
                {
                    return chapter;
                }
            }

            return null;
        }

        private VNSequenceConfig FindSequence(string sequenceId)
        {
            if (currentChapter == null || string.IsNullOrWhiteSpace(sequenceId))
            {
                return null;
            }

            foreach (var sequence in currentChapter.Sequences)
            {
                if (sequence != null && sequence.sequenceId == sequenceId)
                {
                    return sequence;
                }
            }

            return null;
        }

        private static VNNodeConfig FindNode(VNSequenceConfig sequence, string nodeId)
        {
            if (sequence == null || sequence.nodes == null || sequence.nodes.Count == 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return sequence.nodes[0];
            }

            foreach (var node in sequence.nodes)
            {
                if (node != null && node.nodeId == nodeId)
                {
                    return node;
                }
            }

            return null;
        }

        private string GetFirstEligibleNodeId(VNSequenceConfig sequence)
        {
            if (sequence?.nodes == null)
            {
                return null;
            }

            foreach (var node in sequence.nodes)
            {
                if (node != null && bridge.ConditionsMet(node.requiredFlags, node.blockedFlags))
                {
                    return node.nodeId;
                }
            }

            return sequence.nodes.Count > 0 ? sequence.nodes[0].nodeId : null;
        }

        private string GetNextNodeId(VNNodeConfig node)
        {
            if (node == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(node.nextNodeId))
            {
                return node.nextNodeId;
            }

            if (currentSequence?.nodes == null)
            {
                return null;
            }

            for (var i = 0; i < currentSequence.nodes.Count - 1; i++)
            {
                if (currentSequence.nodes[i] == node)
                {
                    return currentSequence.nodes[i + 1]?.nodeId;
                }
            }

            return null;
        }

        private bool HasEligibleChoices(VNNodeConfig node)
        {
            return GetEligibleChoices(node).Count > 0;
        }

        private List<VNChoiceConfig> GetEligibleChoices(VNNodeConfig node)
        {
            var result = new List<VNChoiceConfig>();
            if (node?.choices == null)
            {
                return result;
            }

            foreach (var choice in node.choices)
            {
                if (choice != null && bridge.ConditionsMet(choice.requiredFlags, choice.blockedFlags))
                {
                    result.Add(choice);
                }
            }

            return result;
        }

        private VNChoiceConfig FindChoice(string choiceId)
        {
            if (currentNode?.choices == null)
            {
                return null;
            }

            foreach (var choice in currentNode.choices)
            {
                if (choice != null && choice.choiceId == choiceId && bridge.ConditionsMet(choice.requiredFlags, choice.blockedFlags))
                {
                    return choice;
                }
            }

            return null;
        }
    }
}
