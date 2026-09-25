using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 给整帧房间图层描边 —— 贴着**不透明轮廓**，透明处不描，而且**一个像素都不碰原图** ✓。
    ///
    /// 做法：在原图**下面**垫一份同 sprite / 同 rect 的副本 ✓，副本用 `Project/UI Sprite Outline`
    /// 那个 shader ✓ —— shader 里算"膨胀后的轮廓 **减去自己**" ✓，所以只有原图外侧那一圈会露出来 ✓，
    /// 原图（包括它半透明的软边）不会被叠色 ✓。
    ///
    /// 为什么不再"偏移叠 8 份"✗（这个类的老做法 ✓）：副本垫在原图下面时，原图**实心**处盖得住它们 ✓，
    /// 但原图**软边**（alpha 0.5~1）盖不住 ✗ → 描边色从软边透上来 ✓ = "挂上描边之后原图轻微变色"✗；
    /// 而且一层家具要 8 个 Image ✗。现在只要 **1 个副本 + 1 个 draw** ✓，还能顺便做淡入淡出 / 柔光 ✓。
    ///
    /// 注意：副本是**运行时**生成的（不写进场景 ✓）。编辑器里想看效果：右键组件 →「重新生成描边」✓，
    /// 或者打开 `hoverOnly` 关掉看常驻效果 ✓。
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class SpriteOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>描边 shader 名（Inspector 上没填时用 Shader.Find 找它 ✓）。</summary>
        private const string OutlineShaderName = "Project/UI Sprite Outline";

        [Header("描边")]
        [Tooltip("描边颜色 ✓（直接驱动 shader 的 _Color ✓，换色不用动材质 ✓）。")]
        [SerializeField] private Color color = new Color(0.42f, 0.66f, 1f, 0.85f);
        [Tooltip("描边粗细（**UI 像素** ✓ —— 运行时按图片缩放换算成 texel ✓）。")]
        [SerializeField, Range(0f, 16f)] private float width = 3f;
        [Tooltip("边缘软硬 ✓：0.05 = 硬边（锐利 ✓），0.5 = 很柔 ✓。")]
        [SerializeField, Range(0.001f, 0.5f)] private float softness = 0.18f;
        [Tooltip("悬停时淡入用几秒 ✓（0 = 立刻出现 ✗）。")]
        [SerializeField, Range(0f, 0.6f)] private float fadeDuration = 0.12f;
        [Tooltip("把轮廓也算进自己身上一点 = 外发光 ✓。0 = 纯描边 ✓。")]
        [SerializeField, Range(0f, 1f)] private float glow;

        [Header("显示时机")]
        [Tooltip("只在鼠标悬停时显示 ✓（家具整帧图就是靠这个 ✓ —— 副本常驻也没关系了 ✓，shader 只画那一圈 ✓）。")]
        [SerializeField] private bool hoverOnly = true;
        [Tooltip("是否在 Awake 时就把副本建好 ✓（省掉第一次悬停时的分配 ✓）。"
                 + "**默认关** ✗→✓：水渍那种「运行时才被 RandomPlacement 挪走」的物件 ✓，"
                 + "在 Awake 建副本会停在**移动前**的位置 ✗；改成悬停时再建（见 OnPointerEnter ✓）就永远对得上 ✓。")]
        [SerializeField] private bool buildOnAwake;

        [Header("shader")]
        [Tooltip("副本用的 shader：Project/UI Sprite Outline。留空会自动 Shader.Find 找它 ✓。")]
        [SerializeField] private Shader outlineShader;
        [Tooltip("原图 alpha 低于它就当空白 ✓（软阴影、噪点不会被算成轮廓 ✓）。")]
        [SerializeField, Range(0f, 1f)] private float cutoff = 0.5f;

        private Image source;
        private Image outline;
        private Material material;
        private bool warnedMissingShader;

        /// <summary>当前 / 目标描边强度 ✓（hoverOnly 时做淡入淡出 ✓）。</summary>
        private float current;
        private float target;

        private void Awake()
        {
            source = GetComponent<Image>();
            target = hoverOnly ? 0f : 1f;
            current = target;

            if (buildOnAwake)
            {
                Build();
            }
        }

        private void OnEnable()
        {
            // 关掉再打开时把状态摆正 ✓（对象被 SetActive(false) 过的话，副本可能被清掉了 ✓）。
            // **选中状态不重置** ✓ —— 它属于"玩家的选中"✓，不属于这个组件的生命周期 ✓。
            if (buildOnAwake && source != null && outline == null)
            {
                Build();
            }

            RefreshTarget();
        }

        private void OnDestroy()
        {
            Clear();

            if (material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }

                material = null;
            }
        }

        private void Update()
        {
            // 物件在运行时可能被挪走 ✓（水渍的 RandomPlacement 就是在 Update 里落点的 ✓）——
            // 只要描边亮着，就每帧把副本对齐过去 ✓。几步属性赋值 ✓ 很便宜 ✓，
            // 但能彻底消掉「位置变了、描边还在原地」✗ 这类问题（也不依赖 Awake 顺序 ✓）。
            if (current > 0.0001f)
            {
                SyncRect();
            }

            if (Mathf.Approximately(current, target))
            {
                return;
            }

            current = fadeDuration <= 0f
                ? target
                : Mathf.MoveTowards(current, target, Time.unscaledDeltaTime / fadeDuration);

            ApplyMaterial();
        }

        /// <summary>鼠标是不是停在这件东西上 ✓。</summary>
        private bool hovered;

        /// <summary>
        /// 是不是「被选中」 ✓ —— 选中时描边**常驻** ✓（不再只是悬停才亮 ✓）。
        /// 选中比悬停优先级高 ✓：玩家要一眼看出"现在操作的是这件"✓。
        /// </summary>
        private bool selected;

        /// <summary>选中 / 取消选中 ✓（HUD 切换选中目标时调 ✓）。</summary>
        public void SetSelected(bool on)
        {
            if (selected == on)
            {
                return;
            }

            selected = on;
            RefreshTarget();
        }

        /// <summary>算描边该不该亮 ✓（选中恒亮 ✓；其余看 hoverOnly + 有没有悬停 ✓）。</summary>
        private void RefreshTarget()
        {
            target = selected || !hoverOnly || hovered ? 1f : 0f;
        }

        /// <summary>悬停进来：朝 1 淡入 ✓。</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (!hoverOnly)
            {
                return;
            }

            // **每次都重新对一次位置** ✓ —— `Build()` 是幂等的 ✓（会重建 rect ✓），
            // 所以物件在运行时被挪动过（水渍的 RandomPlacement ✓）也不会描在旧位置 ✗。
            Build();
            RefreshTarget();
        }

        /// <summary>移出去：朝 0 淡出 ✓（副本留着 ✓，不销毁 —— 下次悬停不用重新分配 ✓）。</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (hoverOnly)
            {
                RefreshTarget();
            }
        }

        /// <summary>
        /// 建/刷新那份副本 ✓（幂等 ✓；编辑器里也能从右键菜单调 ✓）。
        /// 挂在原图**下面**（同一个父节点、同一个 sibling 位置 ✓），只比原图多一个 shader ✓。
        /// </summary>
        [ContextMenu("重新生成描边")]
        public void Build()
        {
            if (source == null)
            {
                source = GetComponent<Image>();
            }

            if (source == null || source.sprite == null)
            {
                return;
            }

            var shader = outlineShader != null ? outlineShader : Shader.Find(OutlineShaderName);
            if (shader == null)
            {
                // 找不到就**别建副本** ✗ —— 建了只会变成"整张图染成描边色"✗（老做法那种糊法 ✓）。
                if (!warnedMissingShader)
                {
                    warnedMissingShader = true;
                    Debug.LogWarning($"[Outline] 找不到 shader「{OutlineShaderName}」✗，描边没生成。"
                                     + "检查 Assets/Project/Resource/UI/Shaders/UiSpriteOutline.shader 是否编译通过 ✓。");
                }

                return;
            }

            if (material == null)
            {
                material = new Material(shader) { hideFlags = HideFlags.DontSave };
            }

            if (outline == null)
            {
                var go = new GameObject($"{name}_Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform.parent, false);
                outline = go.GetComponent<Image>();
                outline.raycastTarget = false;
            }

            SyncRect();

            outline.sprite = source.sprite;
            outline.type = Image.Type.Simple;
            outline.preserveAspect = source.preserveAspect;
            outline.material = material;

            // 垫在原图下面 ✓：用同一个 sibling 下标插进去，于是它排在原图之前 ✓（uGUI 按层级顺序画 ✓）。
            outline.transform.SetSiblingIndex(transform.GetSiblingIndex());

            ApplyMaterial();
        }

        /// <summary>
        /// 把副本的矩形对齐到原图 ✓（锚点 / 偏移 / 枢轴 / 缩放 ✓）。
        ///
        /// 家具图层用的是**拉伸锚点** ✓，所以"物件被移动"表现为 `offsetMin / offsetMax` 的变化 ✓ ——
        /// 逐项抄过来它就跟着走 ✓（水渍整块平移时 ✓，画出来的位置 / alpha 命中 / 描边三者始终一致 ✓）。
        /// </summary>
        private void SyncRect()
        {
            if (source == null || outline == null)
            {
                return;
            }

            var sourceRect = source.rectTransform;
            var outlineRect = outline.rectTransform;
            outlineRect.anchorMin = sourceRect.anchorMin;
            outlineRect.anchorMax = sourceRect.anchorMax;
            outlineRect.pivot = sourceRect.pivot;
            outlineRect.offsetMin = sourceRect.offsetMin;
            outlineRect.offsetMax = sourceRect.offsetMax;
            outlineRect.localScale = sourceRect.localScale;
        }

        /// <summary>把当前参数写进材质 ✓（颜色 / 粗细 / 深浅 / 强度 ✓）。</summary>
        private void ApplyMaterial()
        {
            if (material == null)
            {
                return;
            }

            material.SetColor("_Color", color);
            material.SetFloat("_OutlineWidth", TexelsFor(width));
            material.SetFloat("_Softness", softness);
            material.SetFloat("_Cutoff", cutoff);
            material.SetFloat("_Glow", glow);
            material.SetFloat("_Alpha", current);

            if (outline != null)
            {
                outline.enabled = current > 0.0001f || !Application.isPlaying;
            }
        }

        /// <summary>
        /// **UI 像素 → texel** ✓：shader 在贴图空间采样 ✓，而美术希望按屏幕上的像素调粗细 ✓。
        /// 家具图是整帧 3840 宽、显示在 ~1300px 的房间里 ✓，1 texel ≈ 0.34px ✓ ——
        /// 不换算的话"3px"会变成 1px ✓，看起来像没生效 ✗。
        /// </summary>
        private float TexelsFor(float pixels)
        {
            if (source == null || source.sprite == null)
            {
                return pixels;
            }

            var spriteWidth = source.sprite.rect.width;
            var rectWidth = source.rectTransform.rect.width;
            if (spriteWidth <= 0.01f || rectWidth <= 0.01f)
            {
                return pixels;
            }

            return pixels * spriteWidth / rectWidth;
        }

        /// <summary>销毁副本 ✓（材质留着复用 ✓）。</summary>
        public void Clear()
        {
            if (outline == null)
            {
                return;
            }

            var go = outline.gameObject;
            outline = null;

            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }
    }
}
