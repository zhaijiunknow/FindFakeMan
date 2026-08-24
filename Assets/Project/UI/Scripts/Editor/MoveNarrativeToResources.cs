using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.UI.Editor
{
    /// <summary>把 Narrative/Data 下的 VN 章节资产移到 Resources/Narrative，使 Resources.LoadAll 能加载到。</summary>
    public static class MoveNarrativeToResources
    {
        [MenuItem("Tools/Narrative/Move Chapters To Resources")]
        public static void Run()
        {
            var srcDir = "Assets/Project/Narrative/Data";
            var dstDir = "Assets/Resources/Narrative";
            Directory.CreateDirectory(dstDir);

            foreach (var file in Directory.GetFiles(srcDir, "*.asset"))
            {
                var target = dstDir + "/" + Path.GetFileName(file);
                if (AssetDatabase.LoadAssetAtPath<Object>(target) != null) continue; // 已存在
                // 用复制而非移动：被引用的资产 MoveAsset 会被锁，复制不受影响。
                AssetDatabase.CopyAsset(file, target);
            }

            AssetDatabase.Refresh();
            Debug.Log("[Move] 已把 VN 章节资产复制到 Resources/Narrative。");
        }
    }
}
