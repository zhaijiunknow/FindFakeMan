using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Editor
{
    /// <summary>
    /// 把 `SmallApp.prefab` 的层级导成文本，方便规划「分页按钮 ↔ 页面」的对照表。
    ///
    /// 为什么需要它：那个预制体有 111 个对象，而且里面的分页按钮 **`actionType` 全是 0、`targetId` 全空** ✗
    /// —— 也就是说"点哪个按钮看哪一页"这件事在预制体里根本没接过 ✗，只能按名字/结构重新接，
    /// 那就必须先看清层级（谁是按钮、谁是被切换的页容器、`openedUI`/`Content` 下面挂了什么）。
    ///
    /// 输出：
    /// - Console 里分段打印（避免一条消息太长被截断 ✗）；
    /// - 同时写一份完整文本到**工程根目录** `SmallAppHierarchy.txt`（可以直接用编辑器/工具打开看 ✓）。
    ///
    /// 菜单：Tools/Project/UI/Print SmallApp Hierarchy
    /// </summary>
    public static class PrintSmallAppHierarchy
    {
        private const string PrefabPath = "Assets/Project/UI/Prefabs/SmallApp.prefab";
        private const string OutputFileName = "SmallAppHierarchy.txt";
        private const int ChunkLines = 40;

        [MenuItem("Tools/Project/UI/Print SmallApp Hierarchy")]
        private static void Print()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SmallApp] 找不到 {PrefabPath}");
                return;
            }

            var text = new StringBuilder();
            text.AppendLine($"# {PrefabPath} 层级");
            text.AppendLine("# 格式：缩进 = 父子关系；(inactive) = 默认关闭；[锚点 | 尺寸]；关键组件列表");
            Walk(prefab.transform, string.Empty, text);

            var lines = text.ToString().Split('\n');
            var path = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? ".", OutputFileName);
            File.WriteAllText(path, text.ToString());

            for (var i = 0; i < lines.Length; i += ChunkLines)
            {
                var chunk = new StringBuilder();
                for (var j = i; j < i + ChunkLines && j < lines.Length; j++)
                {
                    chunk.AppendLine(lines[j]);
                }

                Debug.Log($"[SmallApp] 层级 {i + 1}~{Mathf.Min(i + ChunkLines, lines.Length)}/{lines.Length}\n{chunk}");
            }

            Debug.Log($"[SmallApp] 完整层级已写到：{path}（共 {lines.Length} 行）");
        }

        private static void Walk(Transform t, string indent, StringBuilder sb)
        {
            var comps = new StringBuilder();
            foreach (var component in t.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                var typeName = component.GetType().Name;
                // Transform / RectTransform / CanvasRenderer 每层都有，不列（省行数）。
                if (typeName == "Transform" || typeName == "RectTransform" || typeName == "CanvasRenderer")
                {
                    continue;
                }

                comps.Append(typeName);

                // 按钮特别标注：能不能点（有没有 targetGraphic / 有没有接动作）
                if (component is Button button)
                {
                    comps.Append(button.targetGraphic == null ? "(无底板)" : "(可点)");
                }

                if (component is TextMeshProUGUI tmp)
                {
                    var content = string.IsNullOrEmpty(tmp.text) ? "(空)" : tmp.text.Replace("\n", " ");
                    if (content.Length > 18)
                    {
                        content = content.Substring(0, 18) + "…";
                    }

                    comps.Append($"「{content}」");
                }

                comps.Append(' ');
            }

            var rect = t as RectTransform;
            var geometry = rect != null
                ? $" [{rect.anchorMin.x:0.##},{rect.anchorMin.y:0.##}→{rect.anchorMax.x:0.##},{rect.anchorMax.y:0.##}"
                  + $" | {rect.sizeDelta.x:0}x{rect.sizeDelta.y:0}]"
                : string.Empty;

            var activeNote = t.gameObject.activeSelf ? string.Empty : " (inactive)";
            sb.AppendLine($"{indent}{t.name}{activeNote} | {comps}{geometry}");

            foreach (Transform child in t)
            {
                Walk(child, indent + "  ", sb);
            }
        }
    }
}
