using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp
{
    /// <summary>
    /// 给整帧房间图层描边 —— 而且是**贴着不透明轮廓**的描边，透明处不描。
    ///
    /// 原理：把同一张图复制若干份、沿一圈方向各偏移几个像素、统一染色，然后**垫在原图下面**。
    /// 副本的"有像素处"跟着原图走，所以只有原图轮廓外侧那一圈会露出来 —— 正好是描边；
    /// 原图透明的地方副本也是透明的，所以不会描出矩形边。
    ///
    /// 为什么不用 shader：零 shader 依赖、URP/内置管线都一样；代价是每件家具多几张**同一张 sprite** 的
    /// Image（同图同材质，Unity 会合批，开销可接受）。副本的 raycastTarget 全是 false，不会影响点击。
    ///
    /// 注意：它生成的副本是**运行时**的（不写进场景）。想在编辑器里预览，右键组件选「重新生成描边」，
    /// 或者干脆用 Inspector 上的参数调好颜色/粗细再看。
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class SpriteOutline : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        /// <summary>描边副本用的 alpha 硬切 shader 名（Inspector 上没填时用 Shader.Find 找它）。</summary>
        private const string CutoutShaderName = "Project/UI Alpha Cutout";

        [Header("描边")]
        [Tooltip("描边颜色（会连副本的画面一起染色，但只有露在轮廓外的那一圈看得见）。")]
        [SerializeField] private Color color = new Color(0.42f, 0.66f, 1f, 0.85f);
        [Tooltip("描边粗细（像素）。")]
        [SerializeField, Range(1f, 12f)] private float width = 3f;
        [Tooltip("沿一圈采样几份副本：8 = 上下左右 + 四个斜角（够圆了）。")]
        [SerializeField, Range(4, 16)] private int samples = 8;
        [Tooltip("是否在 Awake 时自动生成。默认关：描边是整帧图的 N 份副本，常驻很浪费。")]
        [SerializeField] private bool generateOnAwake;
        [Tooltip("只在鼠标悬停时显示（进/出自动 Build/Clear）。家具整帧图就是靠这个。")]
        [SerializeField] private bool hoverOnly = true;

        [Header("Alpha 硬切（副本用）")]
        [Tooltip("副本用的 shader：Project/UI Alpha Cutout。留空会自动 Shader.Find 找它。")]
        [SerializeField] private Shader cutoutShader;
        [Tooltip("原图 alpha 低于它就当空白丢弃 —— 只保留实心轮廓，空白处不会叠出蓝雾。")]
        [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.5f;

        private Material cutoutMaterial;
        private bool warnedMissingShader;

        private readonly List<GameObject> copies = new List<GameObject>();
        private Image source;

        private void Awake()
        {
            source = GetComponent<Image>();
            if (generateOnAwake)
            {
                Build();
            }
        }

        private void OnDestroy()
        {
            Clear();

            if (cutoutMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(cutoutMaterial);
                }
                else
                {
                    DestroyImmediate(cutoutMaterial);
                }

                cutoutMaterial = null;
            }
        }

        /// <summary>
        /// 所有副本共用一份材质：每份副本各 new 一个会漏材质，而且 SetPass 次数也浪费。
        /// 没在 Inspector 上填 shader 就 Shader.Find 兜一下（编辑器里一定找得到；**打包前**要么把这个
        /// shader 加进 Graphics 的 Always Included Shaders，要么让建造工具把它引用到组件上）。
        /// </summary>
        private Material EnsureCutoutMaterial()
        {
            if (cutoutMaterial != null)
            {
                return cutoutMaterial;
            }

            var shader = cutoutShader != null ? cutoutShader : Shader.Find(CutoutShaderName);
            if (shader == null)
            {
                // 找不到就是"静默退化"：副本还是普通染色，空白处照样会有蓝雾。
                // 这行日志就是为了别让它悄悄失败（Shader.Find 在编辑器里应该找得到）。
                if (!warnedMissingShader)
                {
                    warnedMissingShader = true;
                    Debug.LogWarning($"[Outline] 找不到 shader「{CutoutShaderName}」，"
                                     + "描边副本退化成普通染色（空白处会有蓝雾）。检查 Assets/Project/Resource/UI/Shaders/UiAlphaCutout.shader 是否编译通过。");
                }

                return null;
            }

            cutoutMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            cutoutMaterial.SetFloat("_Cutoff", alphaCutoff);
            return cutoutMaterial;
        }

        /// <summary>悬停进来：长出描边（只在 <see cref="hoverOnly"/> 打开时）。</summary>
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (hoverOnly)
            {
                Build();
            }
        }

        /// <summary>移出去：把副本清掉，别让整帧图的描边常驻。</summary>
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (hoverOnly)
            {
                Clear();
            }
        }

        /// <summary>生成描边副本（幂等：重复调用会先清掉旧的）。编辑器里也可以用右键菜单调。</summary>
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

            Clear();

            var parent = transform.parent;
            if (parent == null)
            {
                return;
            }

            var myIndex = transform.GetSiblingIndex();
            var sourceRect = source.rectTransform;

            for (var i = 0; i < samples; i++)
            {
                var angle = Mathf.PI * 2f * i / samples;
                var offset = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * width;

                var go = new GameObject($"{name}_Outline{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = sourceRect.anchorMin;
                rect.anchorMax = sourceRect.anchorMax;
                rect.pivot = sourceRect.pivot;
                rect.offsetMin = sourceRect.offsetMin + offset; // 同尺寸，只平移
                rect.offsetMax = sourceRect.offsetMax + offset;
                rect.localScale = sourceRect.localScale;

                var image = go.GetComponent<Image>();
                image.sprite = source.sprite;
                image.color = color;
                image.type = Image.Type.Simple;
                image.preserveAspect = source.preserveAspect;
                image.raycastTarget = false;

                // alpha 硬切：副本只画"原图实心"的像素，家具图里的软 alpha / 空白处不再被染色叠成蓝雾。
                var cutout = EnsureCutoutMaterial();
                if (cutout != null)
                {
                    image.material = cutout;
                }

                // 垫在原图下面：每次都插到原图当前位置，于是副本依次排在原图之前。
                go.transform.SetSiblingIndex(myIndex);

                copies.Add(go);
            }
        }

        /// <summary>清掉生成的副本。</summary>
        public void Clear()
        {
            foreach (var copy in copies)
            {
                if (copy == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(copy);
                }
                else
                {
                    DestroyImmediate(copy);
                }
            }

            copies.Clear();
        }
    }
}
