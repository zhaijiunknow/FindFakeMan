using System.IO;
using Project.Gameplay.Scripts.Case;
using UnityEditor;
using UnityEngine;

namespace Project.Gameplay.Editor
{
    /// <summary>
    /// 生成**读数种类表**资产 ✓（`Assets/Project/Gameplay/ScriptableObjects/Case/ReadingKindTable.asset` ✓）。
    ///
    /// 为什么要有这个菜单 ✗→✓：表是**数据** ✓，理应在 Inspector 里改 ✓；
    /// 但资产必须由 Unity 创建 ✓（手写 YAML 要提前知道脚本的 GUID ✗ —— 脚本还没导入时那玩意儿不存在 ✗）。
    /// 所以留一个一次性菜单 ✓：点一下生成 ✓，生成出来就是**已经填好**的表 ✓（不是空表 ✗）。
    ///
    /// 生成后记得把它连到 `CaseDirector` 的「读数种类表」字段 ✓；
    /// **不连也能跑** ✓ —— 那时用 <see cref="ReadingKindTable.CreateDefaultInstance"/> 的兜底内容 ✓。
    /// </summary>
    public static class CreateReadingKindTableMenu
    {
        private const string FolderPath = "Assets/Project/Gameplay/ScriptableObjects/Case";
        private const string AssetPath = FolderPath + "/ReadingKindTable.asset";

        [MenuItem("Tools/Project/Case/Create Reading Kind Table")]
        public static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ReadingKindTable>(AssetPath);
            if (existing != null)
            {
                // **不覆盖** ✗：里面可能已经是你手写的内容 ✓ —— 只选中它 ✓，让你自己看 ✓。
                Selection.activeObject = existing;
                Debug.Log($"[Case] 读数种类表已经存在 ✓：{AssetPath}（没有覆盖 ✓）");
                return;
            }

            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
                AssetDatabase.Refresh();
            }

            // 用兜底表的内容初始化 ✓ —— 免得生成一张空表 ✗ 让人以为功能坏了 ✗。
            var defaults = ReadingKindTable.CreateDefaultInstance();
            var asset = ScriptableObject.CreateInstance<ReadingKindTable>();
            EditorUtility.CopySerialized(defaults, asset);
            Object.DestroyImmediate(defaults);

            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;

            Debug.Log($"[Case] 读数种类表已生成 ✓：{AssetPath}"
                      + "（5 种读数 ✓ + 4 条家具专属文案 ✓ —— 之后加新读数就在这里加一行 ✓）");
        }
    }
}
