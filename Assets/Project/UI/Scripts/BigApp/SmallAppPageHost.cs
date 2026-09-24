using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 小软件的分页切换器：把 `Content/all_button` 里那 6 个按钮接成"点谁显示谁"。
    ///
    /// 为什么需要它：预制体里这 6 个按钮（`潜入`/`异常相册`/`道具`/`设置`/`系统备份`/`退出系统`）
    /// **动作全是空的** ✗，而且 `Content` 下面**根本没有页面容器** ✗ —— 只有启动屏和按钮网格。
    /// 所以"点按钮看一页"这件事得在这里补：页面由建造工具新建（挂在 `Content` 下），
    /// 这个组件负责"显示哪一页 + 按钮选中态 + 启动屏流程"。
    ///
    /// 启动屏流程（预制体自带）：`Content` 里先是 logo（`OKAS`/`system`/`System_2`/`peopleicon`）
    /// 和「进入系统」按钮（`start`）；点它之后才显示 `all_button` 菜单和默认页 ✓。
    ///
    /// 挂在 **SmallApp 根**上（不是 `openedUI` 里）：根节点一直激活，窗口每次收起再展开
    /// 也不会丢状态（`openedUI` 收起时会被 SetActive(false) ✗）。
    /// </summary>
    public sealed class SmallAppPageHost : MonoBehaviour
    {
        /// <summary>一个分页：按钮 + 页面 + 标题（日志和选中态都用到）。</summary>
        [Serializable]
        public struct Page
        {
            public string title;
            public Button button;
            public GameObject page;
        }

        [Header("启动屏 → 菜单")]
        [Tooltip("启动屏那组对象（logo / 文字 / peopleicon）。点「进入系统」后隐藏。")]
        [SerializeField] private GameObject[] startScreen = Array.Empty<GameObject>();
        [Tooltip("「进入系统」按钮（预制体里叫 start）。")]
        [SerializeField] private Button startButton;
        [Tooltip("菜单根（预制体里叫 all_button，默认是关闭的）。")]
        [SerializeField] private GameObject menuRoot;

        [Header("分页")]
        [SerializeField] private Page[] pages = Array.Empty<Page>();
        [Tooltip("进入系统后默认显示第几页。")]
        [SerializeField] private int defaultPage;

        [Header("按钮选中态")]
        [Tooltip("选中页的按钮底板颜色。")]
        [SerializeField] private Color selectedColor = new Color(0.62f, 0.74f, 1f, 1f);
        [Tooltip("未选中页的按钮底板颜色（预制体本来就是淡蓝底，这里压暗一点做区分）。")]
        [SerializeField] private Color normalColor = new Color(0.62f, 0.74f, 1f, 0.45f);

        [Header("调试")]
        [SerializeField] private bool logCalls = true;

        private readonly List<Image> pageButtonPlates = new();
        private bool entered;
        private int currentPage = -1;

        private void Awake()
        {
            WireButtons();
            ShowStartScreen();
        }

        /// <summary>点「进入系统」：收起启动屏、露出菜单，并显示默认页。</summary>
        public void EnterSystem()
        {
            entered = true;
            if (logCalls)
            {
                Debug.Log("[SmallApp] 进入系统：显示分页菜单。");
            }

            foreach (var item in startScreen)
            {
                if (item != null)
                {
                    item.SetActive(false);
                }
            }

            if (menuRoot != null)
            {
                menuRoot.SetActive(true);
            }

            ShowPage(defaultPage);
        }

        /// <summary>切到第 index 页（页面由建造工具建在 Content 下）。</summary>
        public void ShowPage(int index)
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            currentPage = Mathf.Clamp(index, 0, pages.Length - 1);
            for (var i = 0; i < pages.Length; i++)
            {
                var page = pages[i].page;
                if (page != null)
                {
                    page.SetActive(i == currentPage);
                }

                var plate = i < pageButtonPlates.Count ? pageButtonPlates[i] : null;
                if (plate != null)
                {
                    plate.color = i == currentPage ? selectedColor : normalColor;
                }
            }

            if (logCalls)
            {
                Debug.Log($"[SmallApp] 切到分页：{pages[currentPage].title}");
            }
        }

        /// <summary>回到启动屏（窗口每次展开都从头开始看，符合"终端控制台"的手感）。</summary>
        public void ShowStartScreen()
        {
            entered = false;
            currentPage = -1;

            foreach (var item in startScreen)
            {
                if (item != null)
                {
                    item.SetActive(true);
                }
            }

            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }

            if (pages == null)
            {
                return;
            }

            foreach (var page in pages)
            {
                if (page.page != null)
                {
                    page.page.SetActive(false);
                }
            }
        }

        /// <summary>窗口展开时调（建造工具会把 start 的按钮行为改成调用它 / 或由 ButtonAction 触发）。</summary>
        public bool HasEntered => entered;

        private void WireButtons()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(EnterSystem);
                startButton.onClick.AddListener(EnterSystem);
            }

            pageButtonPlates.Clear();
            if (pages == null)
            {
                return;
            }

            for (var i = 0; i < pages.Length; i++)
            {
                var index = i;
                var button = pages[i].button;
                if (button == null)
                {
                    pageButtonPlates.Add(null);
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowPage(index));

                // 底板就是按钮自己的 Image（预制体里每个分页按钮都是 Image + Button）。
                pageButtonPlates.Add(button.targetGraphic as Image ?? button.GetComponent<Image>());
            }
        }
    }
}
