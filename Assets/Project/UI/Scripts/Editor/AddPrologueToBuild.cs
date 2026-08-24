using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.UI.Editor
{
    /// <summary>把 Px2050_Prologue（以及其它游戏场景）加入 Build Settings，否则 SceneManager.LoadSceneAsync 按名加载不了。</summary>
    public static class AddPrologueToBuild
    {
        [MenuItem("Tools/Prologue/Add Scenes To Build Settings")]
        public static void Run()
        {
            const string prologue = "Assets/Project/UI/Scenes/Px2050_Prologue.unity";
            var wanted = new[]
            {
                "Assets/Project/UI/Scenes/OpeningCinematic.unity",
                prologue,
            };

            var current = EditorBuildSettings.scenes.ToList();
            foreach (var path in wanted)
            {
                if (current.Any(s => s.path == path)) continue;
                current.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = current.ToArray();
            Debug.Log("[AddToBuild] 已把 Px2050_Prologue 等场景加入 Build Settings。");
        }
    }
}
