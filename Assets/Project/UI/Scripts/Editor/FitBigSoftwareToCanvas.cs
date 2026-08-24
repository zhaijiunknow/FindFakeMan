using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Panels.Editor
{
    /// <summary>
    /// 把 BigSoftware 页面按「实际 Canvas」重新适配：
    /// 读取场景中 Canvas 的 CanvasScaler.referenceResolution（如 800×600），
    /// 把 BigSoftware 各元素（Game/Nothink/LeidaBack/BtnSettings）的 m_SizeDelta.x/y 和 anchoredPosition
    /// 从 1920×1080 基准换算到该画布尺寸；stretch 型元素（BackGround、Leida 子图）填满父。
    /// 用法：Tools → Panel Stack → Fit BigSoftware To Canvas。
    /// </summary>
    public static class FitBigSoftwareToCanvas
    {
        [MenuItem("Tools/Panel Stack/Fit BigSoftware To Canvas")]
        public static void Fit()
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Fit] 场景中未找到 Canvas。");
                return;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            // 直接采用 Canvas 的参考分辨率作为设计画布大小（忽略 uiScaleMode，用户以此布置素材）。
            var target = scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f);
            if (target.x <= 1f || target.y <= 1f)
            {
                target = new Vector2(1920f, 1080f);
            }

            var big = GameObject.Find("BigSoftware");
            if (big == null)
            {
                Debug.LogWarning("[Fit] 未找到 BigSoftware。");
                return;
            }

            var root = (RectTransform)big.transform;
            root.SetParent(canvas.transform, false);
            Stretch(root);

            // 1920×1080 基准 → 实际画布。横纵分别缩放，匹配画布（非等比，与参考图 stretch 一致）。
            var fx = target.x / 1920f;
            var fy = target.y / 1080f;

            var centerAnchored = new[] { "Game", "Nothink", "LeidaBack", "BtnSettings" };
            foreach (var r in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (r == root)
                {
                    continue;
                }

                var name = r.name;
                if (centerAnchored.Contains(name))
                {
                    r.sizeDelta = new Vector2(r.sizeDelta.x * fx, r.sizeDelta.y * fy);
                    r.anchoredPosition = new Vector2(r.anchoredPosition.x * fx, r.anchoredPosition.y * fy);
                }
                else if (name is "BackGround" or "Leida" or "Label")
                {
                    // 摊平填满父：BackGround 填满画布、Leida 填满 LeidaBack、Label 填满按钮。
                    var parent = (RectTransform)r.parent;
                    if (parent != null)
                    {
                        r.anchorMin = Vector2.zero;
                        r.anchorMax = Vector2.one;
                        r.offsetMin = Vector2.zero;
                        r.offsetMax = Vector2.zero;
                        r.pivot = new Vector2(0.5f, 0.5f);
                        r.anchoredPosition = Vector2.zero;
                    }
                }
            }

            EditorUtility.SetDirty(canvas.gameObject);
            EditorUtility.SetDirty(big);
            Debug.Log($"[Fit] BigSoftware 已按 Canvas 参考分辨率 {target.x}×{target.y} 重新适配。");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.SetParent(rect.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
