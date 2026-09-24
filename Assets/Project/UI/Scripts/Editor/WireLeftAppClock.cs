using Project.UI.BigApp;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Project.UI.Editor
{
    /// <summary>
    /// 把 LeftApp 里的 `Time` 接成本机时间。
    ///
    /// 做在**预制体**里（不是某个场景），所以所有用到 LeftApp 的面板都会显示 ——
    /// 序幕终端和玩法窗口用的都是含 LeftApp 的 `Main.prefab`，因此一处改、处处生效。
    ///
    /// 菜单：Tools/Project/UI/Wire LeftApp Clock
    /// </summary>
    public static class WireLeftAppClock
    {
        private const string LeftAppPath = "Assets/Project/UI/Prefabs/LeftApp.prefab";
        private const string TimeObjectName = "Time";

        [MenuItem("Tools/Project/UI/Wire LeftApp Clock")]
        public static void Run()
        {
            var contents = PrefabUtility.LoadPrefabContents(LeftAppPath);
            if (contents == null)
            {
                Debug.LogError($"[Clock] 打不开预制体：{LeftAppPath}");
                return;
            }

            try
            {
                var timeTransform = FindDeep(contents.transform, TimeObjectName);
                if (timeTransform == null)
                {
                    Debug.LogError($"[Clock] {LeftAppPath} 里找不到名为 {TimeObjectName} 的物体。");
                    return;
                }

                var tmp = timeTransform.GetComponent<TextMeshProUGUI>();
                if (tmp == null)
                {
                    Debug.LogError($"[Clock] {TimeObjectName} 上没有 TextMeshProUGUI（旧的 UnityEngine.UI.Text 不支持），" +
                                   "先在预制体里把它换成 TextMeshPro - Text (UI)。");
                    return;
                }

                var clock = timeTransform.GetComponent<LocalClockText>();
                if (clock == null)
                {
                    clock = timeTransform.gameObject.AddComponent<LocalClockText>();
                }

                var so = new SerializedObject(clock);
                so.FindProperty("target").objectReferenceValue = tmp;
                so.FindProperty("format").stringValue = "HH:mm:ss";
                so.FindProperty("showDate").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, LeftAppPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Clock] {LeftAppPath} 的 {TimeObjectName} 已接成本机时间（HH:mm:ss）。" +
                          "所有用到 LeftApp 的面板都会显示；格式/日期开关在那个组件的 Inspector 上。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
