using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>
    /// CRT 扫描线屏幕特效覆盖层：全屏 Image + 横向黑色扫描线贴图（Tiled 平铺成细网格），
    /// 支持显示/隐藏、荧光闪烁、扫描线缓慢滚动。
    /// </summary>
    public sealed class ScreenFxOverlay : MonoBehaviour
    {
        private const float ScanlineHeight = 8f; // 与贴图 height 一致，滚动按此周期无缝循环

        [SerializeField] private Image image;
        [Tooltip("闪烁时 alpha 在 1-alphaDelta ~ 1 之间波动")] [SerializeField] private float flickerAlphaDelta = 0.25f;
        [SerializeField] private float flickerDuration = 0.06f;
        [Tooltip("扫描线滚动速度(像素/秒)，缓慢")] [SerializeField] private float scrollSpeed = 2f;

        private Tween flickerTween;
        private CancellationTokenSource scrollCts;
        private UniTask scrollTask;

        private void Awake()
        {
            if (image == null) image = GetComponent<Image>();
            if (image != null && image.sprite == null)
            {
                image.sprite = CreateScanlineSprite();
                image.type = Image.Type.Tiled; // 平铺成细网格（Simple 只拉伸一次）
            }
        }

        private void OnDestroy()
        {
            flickerTween?.Kill();
            scrollCts?.Cancel();
        }

        /// <summary>显示屏幕特效层。</summary>
        public void SetFxOn()
        {
            gameObject.SetActive(true);
            PlayFlicker();
            StartScroll();
        }

        /// <summary>隐藏屏幕特效层并停止闪烁/滚动。</summary>
        public void SetFxOff()
        {
            flickerTween?.Kill();
            flickerTween = null;
            scrollCts?.Cancel();
            scrollCts = null;
            if (image != null)
            {
                image.color = Color.white;
                image.rectTransform.anchoredPosition = new Vector2(image.rectTransform.anchoredPosition.x, 0f);
            }

            gameObject.SetActive(false);
        }

        /// <summary>轻微的 CRT 荧光闪烁。持续播放直到 SetFxOff。</summary>
        public void PlayFlicker()
        {
            if (image == null) return;
            flickerTween?.Kill();
            flickerTween = image.DOFade(1f - flickerAlphaDelta, flickerDuration)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StartScroll()
        {
            scrollCts?.Cancel();
            scrollCts = new CancellationTokenSource();
            scrollTask = ScrollLoopAsync(scrollCts.Token);
        }

        /// <summary>扫描线缓慢下移，8px 周期无缝循环（伪造 CRT 滚动）。</summary>
        private async UniTask ScrollLoopAsync(CancellationToken ct)
        {
            if (image == null) return;
            float baseX = image.rectTransform.anchoredPosition.x;
            float offset = 0f;
            while (!ct.IsCancellationRequested)
            {
                offset = (offset + scrollSpeed * Time.deltaTime) % ScanlineHeight;
                image.rectTransform.anchoredPosition = new Vector2(baseX, -offset);
                await UniTask.Yield(ct);
            }
        }

        /// <summary>生成一张横向扫描线贴图：每 8px 一条黑色细线，配合 Tile 平铺成可见的 CRT 细网格。</summary>
        private static Sprite CreateScanlineSprite()
        {
            const int width = 2;
            const int height = 8;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ScanlineTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isScan = y == 0;
                    tex.SetPixel(x, y, isScan ? new Color(0f, 0f, 0f, 0.55f) : new Color(0f, 0f, 0f, 0f));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
        }
    }
}
