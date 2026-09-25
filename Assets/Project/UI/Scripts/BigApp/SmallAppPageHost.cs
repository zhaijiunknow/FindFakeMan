using System;
using System.Collections.Generic;
using Project.Core.Runtime.Framework;
using Project.Core.Runtime.Managers;
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

        /// <summary>「退出系统」的去处（和左侧栏退出键一致：回片头/主菜单）。</summary>
        private const string MainMenuSceneName = "OpeningCinematic";

        [Header("启动屏 → 菜单")]
        [Tooltip("启动屏那组对象（logo / 文字 / peopleicon）。点「进入系统」后隐藏。")]
        [SerializeField] private GameObject[] startScreen = Array.Empty<GameObject>();
        [Tooltip("「进入系统」按钮（预制体里叫 start）。")]
        [SerializeField] private Button startButton;
        [Tooltip("菜单根（预制体里叫 all_button，默认是关闭的）。")]
        [SerializeField] private GameObject menuRoot;
        [Tooltip("**只在菜单上**显示的显示组件（OKAS / system / System_2 / peopleicon ✓）——"
                 + "它们和菜单同进退 ✓：进子页时跟菜单一起收起来（否则会和页面正文大面积重叠 ✗）。")]
        [SerializeField] private GameObject[] menuExtras = Array.Empty<GameObject>();

        [Header("分页")]
        [SerializeField] private Page[] pages = Array.Empty<Page>();
        [Tooltip("进入系统后默认显示第几页。")]
        [SerializeField] private int defaultPage;
        [Tooltip("跳过启动屏：一打开就是菜单（玩法场景用 ✓；序幕/开场那种「终端启动」留着启动屏 ✓）。")]
        [SerializeField] private bool skipStartScreen;


        [Header("调试")]
        [SerializeField] private bool logCalls = true;

        /// <summary>接线只做一次 ✓（幂等 ✓ —— 以前分页按钮那套反复进入会叠监听 ✗，现在有闸 ✓）。</summary>
        private bool wired;
        private bool entered;
        private int currentPage = -1;
        private GameObject openedUi;
        private bool wasOpen;

        /// <summary>显示组件当前是否跟着菜单显示 ✓（用来避免每帧都 SetActive 一堆对象 ✗）。</summary>
        private bool extrasVisible = true;

        /// <summary>上次是"最小化"而不是"关闭"——决定重新打开时是留在当前页 ✓ 还是重演启动屏 ✓。</summary>
        private bool minimized;

        private void Awake()
        {
            // openedUI 是"窗口本体"（收起时会被 UIWindowManager SetActive(false)）——
            // 盯着它的开关，就能在"重新打开"时回到启动屏 ✓（控制台的手感：每次开机都从自检/启动页开始）。
            openedUi = FindByName("openedUI")?.gameObject;
            wasOpen = openedUi != null && openedUi.activeSelf;

            WireButtons();

            if (skipStartScreen)
            {
                // 玩法场景不走启动屏：一打开直接把菜单摆出来（页面都收着，让玩家自己点 ✓）。
                BackToMenu();
            }
            else
            {
                ShowStartScreen();
            }

            // 开局**强制**同步一次显示组件 ✓ —— 原因见 SyncMenuExtras 的注释 ✗（不强制的话，
            // 场景里存着"上次运行时的状态"✗、而 diff 又判定没变 ✗，它们就永远不刷新 ✓）。
            SyncMenuExtras(true);
        }

        private void Update()
        {
            if (openedUi == null)
            {
                return;
            }

            var isOpen = openedUi.activeSelf;
            if (isOpen && !wasOpen)
            {
                // 重新打开：最小化过来的就留在刚才那页 ✓；关闭过来的——有启动屏就重演启动屏 ✓，
                // 没有启动屏（玩法场景 ✓）就直接回到菜单 ✓。
                if (minimized)
                {
                    minimized = false;
                    if (logCalls)
                    {
                        Debug.Log("[SmallApp] 从最小化恢复（保留当前页）。");
                    }
                }
                else if (skipStartScreen)
                {
                    BackToMenu();
                }
                else
                {
                    ShowStartScreen();
                }
            }

            wasOpen = isOpen;

            // 显示组件（OKAS / system / System_2 / peopleicon）跟着菜单 `all_button` 走 ✓ —— 逻辑在 SyncMenuExtras ✓。
            SyncMenuExtras(false);
        }

        /// <summary>
        /// 显示组件**只跟 `menuRoot`（= `all_button`）走** ✓：菜单在 → 显示 ✓；进子页菜单收起 → 一起收 ✓
        ///（不然和页面正文大面积重叠 ✗）。
        ///
        /// 为什么要 <paramref name="force"/> ✗→✓：这四个物体在场景里存的是**上次运行时的状态** ✗，
        /// 而 <c>extrasVisible</c> 的初值是 true ✓ —— 一旦开局菜单就是开着的（skipStartScreen → BackToMenu ✓），
        /// diff 会判定"没变"✗、一次都不刷 ✓，于是它们永远停在场景里那个状态 ✗
        ///（= 菜单开着、那 4 个标识却不出现 ✓，就是之前看到的毛病 ✓）。所以 Awake 里必须强制刷一次 ✓。
        /// </summary>
        private void SyncMenuExtras(bool force)
        {
            if (menuRoot == null)
            {
                return;
            }

            var menuVisible = menuRoot.activeSelf;
            if (!force && menuVisible == extrasVisible)
            {
                return;
            }

            extrasVisible = menuVisible;
            foreach (var extra in menuExtras)
            {
                if (extra != null)
                {
                    extra.SetActive(menuVisible);
                }
            }
        }

        /// <summary>按名字找节点（含未激活 —— 分页按钮在 all_button 里、默认是关闭的 ✗）。</summary>
        private Transform FindByName(string objectName)
        {
            foreach (var t in transform.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                {
                    return t;
                }
            }

            return null;
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

        /// <summary>
        /// 切到**指定的页对象**（供 `ButtonAction.ShowSmallAppPage` 用 ✓）：
        /// 按钮上引用页对象就行 ✓，不用记它是第几页 ✓；互斥切换（关别的页 + 菜单让位）仍由 <see cref="ShowPage(int)"/> 做 ✓。
        /// </summary>
        public void ShowPage(GameObject page)
        {
            if (page == null || pages == null)
            {
                return;
            }

            for (var i = 0; i < pages.Length; i++)
            {
                if (pages[i].page == page)
                {
                    ShowPage(i);
                    return;
                }
            }

            Debug.LogWarning($"[SmallApp] ShowPage: {page.name} 不在分页表里 ✗（检查建造工具有没有把它接进 pages ✓）");
        }

        /// <summary>切到第 index 页（页面由建造工具建在 Content 下）。</summary>
        public void ShowPage(int index)
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            currentPage = Mathf.Clamp(index, 0, pages.Length - 1);

            // 进子页 = 整屏替换（参考图那样：页面 + 底部返回 ✓）：
            // 菜单网格要让位 ✗→✓，否则它占的下半屏会和页面文字叠在一起 ✗（返回时再露出来 ✓）。
            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }

            // 页面互斥：只开当前那一页 ✓。
            // （以前这里还顺手维护 pageButtonPlates 给按钮换底色 ✗ —— 选中态交给你在预制体里做精灵切换 ✓，
            //   那个列表早已没人看 ✓，已删 ✓。）
            for (var i = 0; i < pages.Length; i++)
            {
                var page = pages[i].page;
                if (page != null)
                {
                    page.SetActive(i == currentPage);
                }
            }

            if (logCalls)
            {
                Debug.Log($"[SmallApp] 切到分页：{pages[currentPage].title}");
            }
        }

        /// <summary>
        /// 从子页返回菜单（子页底部那个「返回」按钮调它 ✓）：
        /// 只把页面收掉、菜单留着 —— 和「退出系统」不同 ✗（那个是离开关卡 ✓）。
        /// </summary>
        public void BackToMenu()
        {
            entered = true;
            currentPage = -1;

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

            if (logCalls)
            {
                Debug.Log("[SmallApp] 返回菜单。");
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
            if (wired)
            {
                return;
            }

            wired = true;

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(EnterSystem);
                startButton.onClick.AddListener(EnterSystem);
            }

            // **分页按钮不在这里接线** ✗→✓ —— 它们由构建器接成 `ButtonAction.ShowSmallAppPage`
            //（`targetObject` 直接指到页对象 ✓，见日志 `[Villa] Btn_xxx → ButtonAction.ShowSmallAppPage ✓`）。
            // 以前这里额外挂了一个 `() => ShowPage(index)` 的 lambda ✗，三个毛病：
            //  ① 它没法 RemoveListener ✗（项目规矩是"固定方法 + Remove"✓），重复执行就会叠一层 ✓；
            //  ② 它和 ButtonAction 是**两条路** ✗，点一下可能两个处理器都跑 ✓；
            //  ③ 它维护的 pageButtonPlates（给按钮换底色那套 ✓）早就没人看了 ✗ —— 选中态你在预制体里做精灵切换 ✓。
            // 所以整块删掉 ✓：页按钮的行为完全交给 ButtonAction ✓，这里只管"非分页"的那几个 ✓。

            // 「退出系统」在预制体里也没接动作 ✗ —— 名字就在层级里，直接按名字找 ✓。
            WireByName("退出系统", ExitToMainMenu);

            // ⚠️ **红 / 绿 / 蓝 三键不在这里接** ✗→✓ —— 改回和 `OpeningCinematic` 那套一致 ✓：
            // 那个场景的实例没有本组件 ✓，三个键**只由 `UIWindowManager` 接** ✓
            //（红=收起 ✓ 绿=铺满矩形 ✓ 蓝=还原成窗口 ✓）。
            // 本组件再插一手就会双绑 ✗（绿会同时"铺满 + 切系统全屏"✗、蓝会同时"还原 + 收起"✗），
            // 所以这三行连同 CloseWindow / MinimizeWindow / ToggleFullScreen 一起停用 ✓（方法先留着 ✓ 以后想换语义还能用 ✓）。
            //
            // 副作用要知道 ✓：`minimized` 因此永远为 false ✓ → 窗口重开时一律走
            // `skipStartScreen → BackToMenu()` ✓（回到菜单 ✓，不再"回到刚才那一页"✗）—— 这正是 OpeningCinematic 的行为 ✓。
        }

        /// <summary>红点：关闭 —— 下次打开从头开始（启动屏）。</summary>
        private void CloseWindow()
        {
            minimized = false;
            SendToWindowManager("Collapse");
            if (logCalls)
            {
                Debug.Log("[SmallApp] 关闭（下次打开回到启动屏）。");
            }
        }

        /// <summary>蓝点：最小化 —— 收起窗口，但**不重置**页内状态 ✓（下次打开还在刚才那页）。</summary>
        private void MinimizeWindow()
        {
            minimized = true;
            SendToWindowManager("Collapse");
            if (logCalls)
            {
                Debug.Log("[SmallApp] 最小化（下次打开还在当前页）。");
            }
        }

        /// <summary>按名字找一个按钮并挂上点击（找不到就跳过，不报错 —— 预制体以后改名字也不至于崩）。</summary>
        private void WireByName(string objectName, UnityEngine.Events.UnityAction action)
        {
            foreach (var t in transform.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != objectName)
                {
                    continue;
                }

                var button = t.GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveListener(action);
                button.onClick.AddListener(action);
                return;
            }
        }

        /// <summary>「退出系统」= 离开关卡回主菜单（和左侧栏那个退出键同一个去处）。</summary>
        private static void ExitToMainMenu()
        {
            if (Services.TryGet<SceneFlowManager>(out var sceneFlow))
            {
                sceneFlow.LoadSceneAsync(MainMenuSceneName, SceneTransitionStyle.FullScreenCrt);
                Debug.Log($"[SmallApp] 退出系统 → 切场景 {MainMenuSceneName}。");
                return;
            }

            Debug.LogWarning("[SmallApp] 退出系统：场景里没有 SceneFlowManager，切不了场景。");
        }

        private static void ToggleFullScreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            Debug.Log($"[SmallApp] 窗口按钮：显示模式 → {(Screen.fullScreen ? "全屏" : "窗口")}");
        }

        /// <summary>把消息发给小软件自己的 UIWindowManager（按类型名找，避免引它的 namespace ✗）。</summary>
        private void SendToWindowManager(string methodName)
        {
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "UIWindowManager")
                {
                    behaviour.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
                    return;
                }
            }

            Debug.LogWarning($"[SmallApp] 没找到 UIWindowManager，{methodName} 没执行。");
        }
    }
}
