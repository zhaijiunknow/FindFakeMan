using System.Collections.Generic;
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

        public bool IsPlaying => isPlaying;
        public string CurrentChapterId => currentChapter != null ? currentChapter.ChapterId : string.Empty;
        public string CurrentSequenceId => currentSequence != null ? currentSequence.sequenceId : string.Empty;
        public string CurrentNodeId => currentNode != null ? currentNode.nodeId : string.Empty;

        private async void Start()
        {
            if (startupChapter != null)
            {
                await StartChapter(startupChapter);
            }
        }

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
            bridge.PresentNode(currentNode);
            visitedNodeIds.Add(currentNode.nodeId);
            isWaitingForChoice = HasEligibleChoices(currentNode);
            isWaitingForExternalSignal = currentNode.waitForExternalSignal;
            isLineFullyDisplayed = false;

            if (!suppressAutoContinue && currentNode.autoContinue && !isWaitingForChoice && !isWaitingForExternalSignal)
            {
                await UniTask.Delay((int)(Mathf.Max(0f, currentNode.autoContinueDelay) * 1000f));
                await Advance();
            }
        }

        public async UniTask Advance()
        {
            if (!isPlaying || currentNode == null)
            {
                return;
            }

            if (!isLineFullyDisplayed)
            {
                isLineFullyDisplayed = true;
                bridge.CompleteLine();
                if (isWaitingForChoice)
                {
                    PresentChoices();
                }
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

            isLineFullyDisplayed = true;
            bridge.CompleteLine();
            if (isWaitingForChoice)
            {
                PresentChoices();
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
