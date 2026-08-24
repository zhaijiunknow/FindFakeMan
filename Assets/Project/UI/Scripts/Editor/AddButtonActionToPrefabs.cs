using System.Linq;
using Project.UI.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Panels.Editor
{
    /// <summary>
    /// 给按钮预制体挂 ButtonAction 脚本（add_component 只支持场景对象，改预制体 asset 需走 PrefabUtility）。
    /// 用法：Tools → Panel Stack → Add ButtonAction To Prefabs。
    /// </summary>
    public static class AddButtonActionToPrefabs
    {
        private const string ButtonPrefabPath = "Assets/Project/UI/Prefabs/Button.prefab";

        [MenuItem("Tools/Panel Stack/Add ButtonAction To Prefabs")]
        public static void Run()
        {
            AddTo(ButtonPrefabPath);
            Debug.Log("[ButtonAction] 已为按钮预制体挂上 ButtonAction。");
        }

        private static void AddTo(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"[ButtonAction] 无法加载预制体：{prefabPath}");
                return;
            }

            var target = contents.transform.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.GetComponent<Button>() != null)?.gameObject;
            if (target == null)
            {
                Debug.LogWarning($"[ButtonAction] 预制体中没有带 Button 的节点：{prefabPath}");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            if (target.GetComponent<ButtonAction>() == null)
            {
                target.AddComponent<ButtonAction>();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
