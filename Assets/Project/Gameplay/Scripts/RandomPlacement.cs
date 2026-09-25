using Project.Gameplay.Scripts.Case;
using UnityEngine;

namespace Project.Gameplay.Scripts
{
    /// <summary>
    /// 把一张**整帧图层**在指定的**不规则四边形**内随机摆一个位置（水渍用 ✓，别的可动物件也能用 ✓）。
    ///
    /// 取点方式：**四角 + 面积均匀**。
    /// 先把四边形拆成两个三角形（a,b,c）和（a,c,d），按面积随机挑一个 ✓，
    /// 再在三角形内取重心坐标 ✓ —— 这对不规则四边形（斜的、不等边的 ✓）同样成立 ✓。
    /// **不能**用"对角线插值"那一套 ✗：那只吃了两个角 ✗，等于偷偷假设它是正规矩形 ✗，
    /// 斜四边形上取点会偏出边界 ✗，而且分布也不均 ✗。
    ///
    /// 关键：图层命中是 **alpha 命中**（只在不透明像素上算 ✓），而它是"矩形 → 贴图 UV"映射的 ✓ ——
    /// 整块矩形一起平移时，**画出来的位置和能点到的位置永远一致** ✓；悬停描边也跟同一块矩形走 ✓。
    ///
    /// 随机走项目的随机桶（<see cref="CaseRandomBuckets"/>）：同一颗种子总摆在同一处 ✓，可复现 ✓。
    /// </summary>
    public sealed class RandomPlacement : MonoBehaviour
    {
        [Header("随机范围：四边形四角（房间视图局部像素，0,0 = 画面中心）")]
        [Tooltip("四个角，**按周长顺序**给（顺时针或逆时针都行 ✓）—— 凸四边形按下标顺序 ✓。"
                 + "现在是美术那边定好的水渍范围（沿地毯一带的斜四边形 ✓）。")]
        [SerializeField] private Vector2 cornerA = new Vector2(-89.7f, -345.3f);
        [SerializeField] private Vector2 cornerB = new Vector2(340.5f, -248.9f);
        [SerializeField] private Vector2 cornerC = new Vector2(222.7f, -238.7f);
        [SerializeField] private Vector2 cornerD = new Vector2(-426.6f, -347.7f);

        [Header("图里「东西」的实际位置")]
        [Tooltip("不透明像素包围盒的中心（归一化 0~1）。建造工具按 PNG 的 alpha 算好填进来 ✓。")]
        [SerializeField] private Vector2 opaqueCenter = new Vector2(0.5f, 0.5f);

        [Header("随机")]
        [Tooltip("随机分组名：同组共用一个流 ✓（不同的东西用不同的名字，互不干扰 ✓）。")]
        [SerializeField] private string randomGroup = "placement";
        [Tooltip("关掉就原地不动（调试用）。")]
        [SerializeField] private bool randomizeOnStart = true;
        [SerializeField] private bool logPlacement = true;

        private RectTransform rect;
        private Vector2 baseAnchored;
        private bool placed;

        private void Awake()
        {
            rect = (RectTransform)transform;
            baseAnchored = rect.anchoredPosition;
        }

        private void Update()
        {
            if (placed || !randomizeOnStart)
            {
                return;
            }

            // 等布局算好再落点：AspectRatioFitter 在布局阶段才生效，
            // 太早取 rect.rect.size 会拿到 0 ✗（那样换算出来的偏移就是错的 ✗）。
            if (rect == null)
            {
                rect = (RectTransform)transform;
            }

            if (rect.rect.size.x <= 1f)
            {
                return;
            }

            PlaceRandom();
        }

        [ContextMenu("随机摆一次")]
        public void PlaceRandom()
        {
            if (rect == null)
            {
                rect = (RectTransform)transform;
            }

            placed = true;

            var rng = CaseRandomBuckets.Bucket(string.IsNullOrEmpty(randomGroup) ? "placement" : randomGroup);
            var target = SampleQuad(rng, cornerA, cornerB, cornerC, cornerD);

            // 图层是整帧图：图中"东西"的中心在 opaqueCenter 处。
            // 枢轴在矩形中心 ✓，所以它相对矩形中心的偏移 = (归一化 - 0.5) * 尺寸 ✓。
            var size = rect.rect.size;
            var currentCenter = new Vector2((opaqueCenter.x - 0.5f) * size.x, (opaqueCenter.y - 0.5f) * size.y);

            rect.anchoredPosition = baseAnchored + (target - currentCenter);

            if (logPlacement)
            {
                Debug.Log($"[Placement] {name} 随机落点 ({target.x:0.#}, {target.y:0.#})，"
                          + $"anchoredPosition → ({rect.anchoredPosition.x:0.#}, {rect.anchoredPosition.y:0.#})"
                          + $"，分组 {randomGroup}");
            }
        }

        // ---------- Scene 视图里可视化范围 ----------

        [Header("调试可视化")]
        [Tooltip("勾上：不选中这个物体也在 Scene 视图里画范围 ✓（调那四个角时很方便 ✓）。")]
        [SerializeField] private bool alwaysDrawRange = true;

        private void OnDrawGizmosSelected()
        {
            DrawRange();
        }

        private void OnDrawGizmos()
        {
            if (alwaysDrawRange)
            {
                DrawRange();
            }
        }

        /// <summary>
        /// 在 Scene 视图里画出：① 随机范围四边形（蓝框 + 四个角点 ✓）② 水渍"东西"当前落在哪（橙点 ✓）。
        ///
        /// 注意这是 **UI 物体** ✓：四边形的坐标是"相对房间视图中心的 px" ✓，
        /// 得先挪到父级局部空间、再换算到世界空间才能画 ✓（父级枢轴不一定在中心 ✗）。
        /// </summary>
        private void DrawRange()
        {
            if (rect == null)
            {
                rect = (RectTransform)transform;
            }

            var a = ToWorld(cornerA);
            var b = ToWorld(cornerB);
            var c = ToWorld(cornerC);
            var d = ToWorld(cornerD);

            Gizmos.color = new Color(0.35f, 0.75f, 1f, 1f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);

            Gizmos.color = new Color(0.35f, 0.9f, 0.6f, 1f);
            Gizmos.DrawSphere(a, 4f);
            Gizmos.DrawSphere(b, 4f);
            Gizmos.DrawSphere(c, 4f);
            Gizmos.DrawSphere(d, 4f);

            // 水渍本体（不透明像素中心）现在在哪 —— 也就是"能点到的位置" ✓
            var size = rect.rect.size;
            var currentCenter = new Vector2((opaqueCenter.x - 0.5f) * size.x, (opaqueCenter.y - 0.5f) * size.y);
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 1f);
            Gizmos.DrawSphere(ToWorld(rect.anchoredPosition + currentCenter), 6f);
        }

        private Vector3 ToWorld(Vector2 roomViewPoint)
        {
            if (rect == null)
            {
                return Vector3.zero;
            }

            var parent = rect.parent as RectTransform;
            if (parent == null)
            {
                return rect.TransformPoint(roomViewPoint);
            }

            var local = parent.rect.center + roomViewPoint;
            return parent.TransformPoint(new Vector3(local.x, local.y, 0f));
        }

        /// <summary>
        /// 四边形内**按面积均匀**取点：拆成两个三角形 → 按面积挑一个 → 三角形内取重心坐标 ✓。
        /// 这样无论四边形怎么歪，点都落在它内部 ✓，且分布均匀 ✓。
        /// </summary>
        private static Vector2 SampleQuad(CaseRandom rng, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var areaAbc = TriangleArea(a, b, c);
            var areaAcd = TriangleArea(a, c, d);
            var total = areaAbc + areaAcd;

            if (total <= 0.0001f)
            {
                return a; // 退化成一个点时，别算出 NaN ✗
            }

            Vector2 p0, p1, p2;
            if (rng.NextDouble() * total <= areaAbc)
            {
                p0 = a;
                p1 = b;
                p2 = c;
            }
            else
            {
                p0 = a;
                p1 = c;
                p2 = d;
            }

            // 三角形内均匀取点：在单位三角形里随机，超出就镜像回来 ✓（经典的 sqrt 法等价写法 ✓）
            var r1 = rng.NextDouble();
            var r2 = rng.NextDouble();
            if (r1 + r2 > 1.0)
            {
                r1 = 1.0 - r1;
                r2 = 1.0 - r2;
            }

            return p0 + (float)r1 * (p1 - p0) + (float)r2 * (p2 - p0);
        }

        private static float TriangleArea(Vector2 a, Vector2 b, Vector2 c)
        {
            return Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
        }
    }
}
