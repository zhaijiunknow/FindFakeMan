using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Interactables;
using Project.Gameplay.Scripts.Items;
using Project.Narrative.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 玩法场景的场景 UI 表现层（实现 Core 的 <see cref="ISceneUiView"/>）。
    ///
    /// 它不是自己搭界面，而是**长在 `Assets/Project/UI/Prefabs/Main.prefab` 这个预制体实例上**，
    /// 字段全部指向预制体里已有的节点（右侧 Health 的 Slider、Collect 标题、nothink 详情区、
    /// item 条四个槽位、收容三格），所以窗口外观和序幕里的终端完全一致，改预制体就能全局生效。
    ///
    /// 职责边界（设计规范 §7）：只做表现，不做任何交互/道具/分支判定。
    /// 槽位点击显示详情由 <see cref="ToolSlotButton"/> / <see cref="ContainmentSlotButton"/> 自己处理。
    /// </summary>
    public sealed class InvestigationHudView : MonoBehaviour, ISceneUiView
    {
        [Header("预制体节点：右侧 / 详情区")]
        [Tooltip("box/life：Health 是个 Slider，value = SAN 比例。")]
        [SerializeField] private Slider sanitySlider;
        [Tooltip("box/Collect 标题：把它写成「Collect 1/3」这种读数。")]
        [SerializeField] private TextMeshProUGUI containmentLabel;
        [Tooltip("nothink 上的详情区：检视信息、点槽位都往这里显示。")]
        [SerializeField] private ItemDetailPanel detailPanel;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private TextMeshProUGUI detailDescText;
        [SerializeField] private TextMeshProUGUI detailStatusText;

        [Header("预制体节点：工具栏四个槽位")]
        [Tooltip("itemButton1-4 自身的 Image（底板）：选中态在 item_noselect / item_select 之间切换。")]
        [SerializeField] private Image[] toolSlotFrames = new Image[0];
        [Tooltip("每个槽位里默认关闭的 item 子图：有工具时打开并显示图标。")]
        [SerializeField] private Image[] toolSlotIcons = new Image[0];
        [SerializeField] private Sprite itemNormalSprite;
        [SerializeField] private Sprite itemSelectedSprite;

        [Header("预制体节点：收容三格")]
        [Tooltip("boxButton1-3 里默认关闭的 box 子图：已收容时打开并显示线索图标。")]
        [SerializeField] private Image[] containmentIcons = new Image[0];

        [Header("拖拽影子（预制体里没有，构建工具加在窗口根节点上）")]
        [SerializeField] private Image toolDragGhost;
        [Tooltip("雷达（探测器）的动态效果：拖工具时加速扫描，判定有效时闪一下。")]
        [SerializeField] private RadarEffects radar;
        [SerializeField] private float defaultHintDuration = 2f;
        [SerializeField] private float resultHoldDuration = 3f;
        [SerializeField] private Color resultNormalColor = new Color(0.85f, 1f, 0.9f, 1f);
        [SerializeField] private Color resultWarnColor = new Color(1f, 0.55f, 0.5f, 1f);
        [SerializeField] private Color dragValidColor = new Color(0.55f, 1f, 0.7f, 0.95f);
        [SerializeField] private Color dragInvalidColor = new Color(1f, 0.45f, 0.45f, 0.95f);
        [SerializeField] private Color dragNeutralColor = new Color(1f, 1f, 1f, 0.95f);

        [Header("VN（玩法场景里播剧情时用，可留空）")]
        [SerializeField] private GameObject vnPanel;
        [SerializeField] private TextMeshProUGUI vnSpeakerText;
        [SerializeField] private TextMeshProUGUI vnBodyText;
        [SerializeField] private RectTransform choiceRoot;
        [SerializeField] private Button choiceButtonPrefab;

        [Header("调试")]
        [SerializeField] private bool logCalls;

        private int hintVersion;
        private int resultVersion;
        private readonly List<Button> choiceButtons = new List<Button>();

        // 提示/结果/证据复用 nothink 面板里已有的 Status / Desc / Name 三行，不再新建浮层。
        // 占用某一行的期间先把原文备份下来，计时结束再还原 —— 这样详情区自己写的状态不会被吃掉。
        private string evidenceLabel = string.Empty;
        private string directionHints = string.Empty;
        private bool detailVisible;
        private bool statusBusy;
        private bool descBusy;
        private string statusBackup = string.Empty;
        private string descBackup = string.Empty;
        private Color descBackupColor = Color.white;

        /// <summary>当前正在检视的道具（方向拖拽要用；没有就是 null）。</summary>
        public Item CurrentItem { get; private set; }

        /// <summary>当前正在检视的交互点（方向拖拽要用；没有就是 null）。</summary>
        public SimpleInteractable CurrentInteractable { get; private set; }

        /// <summary>方向拖拽的提示文字（例如「↑拾取 ↓丢弃 ←装备 →检视」），由 InspectorDragHandler 推过来。</summary>
        public void SetDirectionHints(string hints)
        {
            directionHints = hints ?? string.Empty;
        }

        private void Awake()
        {
            // 玩法场景的 UI 视图：注册成全局 ISceneUiView，UIManager 的所有表现调用都落到这里。
            Services.Register<ISceneUiView>(this);

            HideToolDrag();
            HideChoices();
            if (vnPanel != null)
            {
                vnPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Services.UnregisterInstance(this);
        }

        // ---- 检视 ----

        public void ShowInspector(Item item, SimpleInteractable interactable)
        {
            // 换目标时先把上一件东西的"正在被检视"清掉（规则靠它做"先看清才能用工具"的门槛）。
            if (CurrentInteractable != null && CurrentInteractable != interactable)
            {
                CurrentInteractable.SetInspected(false);
            }

            // 先让详情区按自己的规则填（图标 + 异常线索/工具耐久状态），再用交互物的文字覆盖名称与描述。
            detailPanel?.ShowItem(item);
            detailVisible = true;
            CurrentItem = item;
            CurrentInteractable = interactable;

            if (interactable != null)
            {
                interactable.SetInspected(true);
            }

            var displayName = item != null
                ? item.DisplayName
                : interactable != null ? interactable.InteractableId : string.Empty;
            var description = interactable != null && !string.IsNullOrWhiteSpace(interactable.Description)
                ? interactable.Description
                : item != null ? item.Description : string.Empty;

            if (detailNameText != null)
            {
                // 名称那一行顺带显示方向拖拽提示（由 InspectorDragHandler 推过来）。
                var title = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
                if (!string.IsNullOrWhiteSpace(directionHints))
                {
                    title = string.IsNullOrWhiteSpace(title) ? directionHints : $"{title}   {directionHints}";
                }

                if (!string.IsNullOrWhiteSpace(title))
                {
                    detailNameText.text = title;
                }
            }

            if (detailDescText != null)
            {
                detailDescText.text = description ?? string.Empty;
            }

            if (detailStatusText != null && interactable != null)
            {
                // 异常线索优先显示异常描述；其它情况保留详情区自己写的状态（工具耐久 / 【线索】）。
                var isAnomaly = item is ClueItem clueItem && clueItem.IsAnomaly;
                if (isAnomaly && !string.IsNullOrWhiteSpace(interactable.AnomalyDescription))
                {
                    detailStatusText.text = interactable.AnomalyDescription;
                }
            }

            if (logCalls)
            {
                Debug.Log($"[HUD] ShowInspector: {displayName}");
            }
        }

        public void HideInspector()
        {
            // 收起检视 = 这件东西不再"正在被检视"（门槛会跟着关上）。
            // HideInspector 是所有收面板路径的必经点，所以在这里清最保险。
            if (CurrentInteractable != null)
            {
                CurrentInteractable.SetInspected(false);
                // 收起检视同时退出放大：不然再点开同一件东西会立刻又切到特写。
                CurrentInteractable.SetZoomed(false);
            }

            detailPanel?.Clear();
            detailVisible = false;
            CurrentItem = null;
            CurrentInteractable = null;

            // 详情收起来之后，把证据读数放回 Name 那行。
            ApplyEvidenceToName();

            // 检视结束要回到 Exploration：InteractionManager.OnInteractableClicked 把状态切成了 Inspection，
            // 但项目里没有任何地方负责收尾。而"收起检视"是所有结束路径的必经点
            // （工具交互成功时 SampleInteractableRule.ShowSuccess 也会走到这里），所以退状态放在这里不会漏。
            // 只退一次：当前不是 Inspection 就什么都不做，否则状态会被来回翻。
            if (Services.TryGet<GameManager>(out var gameManager) && gameManager.CurrentState == GameState.Inspection)
            {
                gameManager.RevertState();
            }
        }

        // ---- 数值显示 ----

        public void SetSanity(int current, int max)
        {
            if (sanitySlider != null)
            {
                sanitySlider.value = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);
            }

            if (logCalls)
            {
                Debug.Log($"[HUD] SAN {current}/{max}");
            }
        }

        public void SetEvidence(int current, int goal)
        {
            // 证据读数常驻在 Name 那行；但详情区正在显示某个东西时不抢它。
            evidenceLabel = $"证据 {current}/{goal}";
            ApplyEvidenceToName();
        }

        /// <summary>没有详情在显示时，把证据读数写到 Name 行。</summary>
        private void ApplyEvidenceToName()
        {
            // 证据读数已经挪到 SmallApp 的笔记里：详情区（nothink）不再显示它 —— 这里故意什么都不写。
            // 数据没丢：evidenceLabel 仍在 UpdateEvidenceDisplay 里更新，笔记视图（CaseJournalView）读同一份。
            // 想临时看回详情区，就在下面恢复这句赋值：detailNameText.text = evidenceLabel;
        }

        public void SetContainment(int current, int max)
        {
            if (containmentLabel != null)
            {
                containmentLabel.text = max > 0 ? $"Collect {current}/{max}" : "Collect";
            }

            // 槽位里的小图标：已收容的格子打开并显示线索图标（图标从 InventoryManager 现取）。
            IReadOnlyList<Item> contained = null;
            if (Services.TryGet<InventoryManager>(out var inventoryManager))
            {
                contained = inventoryManager.GetContainmentItems();
            }

            for (var i = 0; i < containmentIcons.Length; i++)
            {
                var icon = containmentIcons[i];
                if (icon == null)
                {
                    continue;
                }

                var item = contained != null && i < contained.Count ? contained[i] : null;
                icon.sprite = item != null ? item.Icon : null;
                icon.gameObject.SetActive(i < current && item != null && item.Icon != null);
            }
        }

        public void SetToolbar(IReadOnlyList<ToolItem> tools, int selectedSlot)
        {
            for (var i = 0; i < toolSlotFrames.Length; i++)
            {
                var tool = tools != null && i < tools.Count ? tools[i] : null;

                var frame = toolSlotFrames[i];
                if (frame != null)
                {
                    // 底板只做选中态提示：选中 item_select、其它 item_noselect（预制体自带的素材）。
                    var sprite = i == selectedSlot ? itemSelectedSprite : itemNormalSprite;
                    if (sprite != null)
                    {
                        frame.sprite = sprite;
                    }
                }

                if (i < toolSlotIcons.Length && toolSlotIcons[i] != null)
                {
                    var icon = toolSlotIcons[i];
                    icon.sprite = tool != null ? tool.Icon : null;
                    icon.gameObject.SetActive(tool != null && tool.Icon != null);
                }
            }
        }

        // ---- 工具拖拽影子 ----

        public void ShowToolDrag(Sprite sprite, Vector2 position)
        {
            // 拖工具 = 雷达加速扫描。
            radar?.SetScanning(true);

            if (toolDragGhost == null)
            {
                return;
            }

            // 有的工具没有图标素材（紫外线灯/工具包），这时用底板图当影子，别让玩家什么都没看到。
            var visual = sprite != null ? sprite : itemSelectedSprite;
            toolDragGhost.gameObject.SetActive(true);
            toolDragGhost.sprite = visual;
            toolDragGhost.enabled = visual != null;
            toolDragGhost.color = dragNeutralColor;
            UpdateToolDrag(position);
        }

        public void UpdateToolDrag(Vector2 position)
        {
            if (toolDragGhost == null)
            {
                return;
            }

            var parent = toolDragGhost.rectTransform.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            var canvas = toolDragGhost.canvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, position, camera, out var local))
            {
                toolDragGhost.rectTransform.anchoredPosition = local;
            }
        }

        public void HideToolDrag()
        {
            radar?.SetScanning(false);

            if (toolDragGhost != null)
            {
                toolDragGhost.gameObject.SetActive(false);
            }
        }

        public void SetToolDragValidity(bool isValid)
        {
            // 工具对上目标了：让雷达闪一下（本关少有的正反馈）。
            if (isValid)
            {
                radar?.Ping();
            }

            if (toolDragGhost != null && toolDragGhost.gameObject.activeSelf)
            {
                toolDragGhost.color = isValid ? dragValidColor : dragInvalidColor;
            }
        }

        // ---- 提示 / 结果 ----

        public void SetHint(string content, float duration)
        {
            if (detailStatusText == null)
            {
                return;
            }

            // 提示占用 Status 那一行；开始占用时把原文备份，计时结束后还原。
            if (!statusBusy)
            {
                statusBackup = detailStatusText.text;
                statusBusy = true;
            }

            detailStatusText.text = content ?? string.Empty;
            hintVersion++;
            var version = hintVersion;

            if (string.IsNullOrEmpty(content))
            {
                statusBusy = false;
                detailStatusText.text = statusBackup ?? string.Empty;
                return;
            }

            ClearHintAfterAsync(version, duration > 0f ? duration : defaultHintDuration).Forget();
        }

        public void SetResult(string content, bool highlight)
        {
            if (detailDescText == null)
            {
                return;
            }

            // 结果占用 Desc 那一行（顺带换个颜色），同样备份原文后还原。
            if (!descBusy)
            {
                descBackup = detailDescText.text;
                descBackupColor = detailDescText.color;
                descBusy = true;
            }

            detailDescText.text = content ?? string.Empty;
            detailDescText.color = highlight ? resultWarnColor : resultNormalColor;
            resultVersion++;
            var version = resultVersion;

            if (string.IsNullOrEmpty(content))
            {
                descBusy = false;
                detailDescText.text = descBackup ?? string.Empty;
                detailDescText.color = descBackupColor;
                return;
            }

            ClearResultAfterAsync(version, resultHoldDuration).Forget();
        }

        private async UniTaskVoid ClearHintAfterAsync(int version, float delay)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: destroyCancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            if (version != hintVersion || detailStatusText == null)
            {
                return;
            }

            // 还原被提示占用的那一行（详情区自己写的状态，或空）。
            statusBusy = false;
            detailStatusText.text = statusBackup ?? string.Empty;
        }

        private async UniTaskVoid ClearResultAfterAsync(int version, float delay)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: destroyCancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            if (version != resultVersion || detailDescText == null)
            {
                return;
            }

            // 还原被结果占用的那一行（文本 + 颜色）。
            descBusy = false;
            detailDescText.text = descBackup ?? string.Empty;
            detailDescText.color = descBackupColor;
        }

        // ---- VN（可选）----

        public void SetVnVisible(bool visible)
        {
            if (vnPanel != null)
            {
                vnPanel.SetActive(visible);
            }
        }

        public void SetVnLine(string speakerName, string text)
        {
            if (vnSpeakerText != null)
            {
                vnSpeakerText.text = string.IsNullOrWhiteSpace(speakerName) ? "旁白" : speakerName;
            }

            if (vnBodyText != null)
            {
                vnBodyText.text = text ?? string.Empty;
            }
        }

        public void SetChoices(IReadOnlyList<VNChoiceViewData> choices)
        {
            HideChoices();
            if (choiceRoot == null || choiceButtonPrefab == null)
            {
                if (choices != null && choices.Count > 0)
                {
                    Debug.LogWarning("[HUD] 没有配置 choiceRoot / choiceButtonPrefab，选项显示不出来。", this);
                }

                return;
            }

            if (choices == null)
            {
                return;
            }

            foreach (var choice in choices)
            {
                var button = Instantiate(choiceButtonPrefab, choiceRoot);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = choice.Text;
                }

                var choiceId = choice.ChoiceId;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    HideChoices();
                    if (Services.TryGet<UIManager>(out var uiManager))
                    {
                        uiManager.SelectVNChoice(choiceId);
                    }
                });

                choiceButtons.Add(button);
            }
        }

        public void HideChoices()
        {
            foreach (var button in choiceButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            choiceButtons.Clear();
        }
    }
}
