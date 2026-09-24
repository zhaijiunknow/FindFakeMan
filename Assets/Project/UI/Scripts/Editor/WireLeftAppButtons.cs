using Project.UI.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Editor
{
    /// <summary>
    /// 把 LeftApp（左侧栏）里那两个按钮接上。
    ///
    /// 现状：LeftApp 自己不带按钮，那两个是嵌套的 `Button.prefab` 实例（子图分别被改名成
    /// `exit_icon` 和 `setting_icon`），它们继承的是 Button.prefab 里的默认动作
    /// （`actionType: 0` ShowPanel + 空的 targetId），所以点下去没有任何反应。
    /// `Main.prefab` 里那几条看似接线的覆盖指向的组件在 LeftApp 里**已经不存在**了（LeftApp 改成嵌套实例之前留下的），
    /// 所以也指望不上。
    ///
    /// 这里按需求覆盖两个实例上的 ButtonAction：
    ///   exit_icon    → LoadScene（回主菜单场景）
    ///   setting_icon → OpenWindow（打开 SmallApp，和主菜单里那个按钮一致）
    ///
    /// 菜单：Tools/Project/UI/Wire LeftApp Buttons
    /// 说明：改的是预制体 asset（不是场景对象），所以用 PrefabUtility.LoadPrefabContents / SaveAsPrefabAsset。
    /// </summary>
    public static class WireLeftAppButtons
    {
        private const string LeftAppPath = "Assets/Project/UI/Prefabs/LeftApp.prefab";

        /// <summary>回主菜单的场景名。Build Settings 里带菜单的场景是 OpeningCinematic，要换改这里。</summary>
        private const string MainMenuSceneName = "OpeningCinematic";

        [MenuItem("Tools/Project/UI/Wire LeftApp Buttons")]
        public static void Run()
        {
            var contents = PrefabUtility.LoadPrefabContents(LeftAppPath);
            if (contents == null)
            {
                Debug.LogError($"[LeftApp] 打不开预制体：{LeftAppPath}");
                return;
            }

            var wiredExit = 0;
            var wiredSettings = 0;

            try
            {
                var actions = contents.GetComponentsInChildren<ButtonAction>(true);
                if (actions.Length == 0)
                {
                    Debug.LogError($"[LeftApp] {LeftAppPath} 里找不到 ButtonAction（嵌套 Button.prefab 实例上应该各有一个）。");
                    return;
                }

                foreach (var action in actions)
                {
                    // ButtonAction.Awake 会 GetComponent<Button>()，所以它必须挂在带 Button 的物体上；
                    // 否则接好了也点不动 —— 这个坑值得直接报出来。
                    if (action.GetComponent<Button>() == null)
                    {
                        Debug.LogWarning($"[LeftApp] {PathOf(action.transform)} 上的 ButtonAction 找不到 Button 组件，"
                                         + "点不动（把 ActionType 配在带 Button 的那个物体上）。");
                    }

                    var isExit = HasDescendantNamed(action.transform, "exit");
                    var isSettings = HasDescendantNamed(action.transform, "setting");

                    if (isExit)
                    {
                        SetAction(action, ButtonAction.ActionType.LoadScene, MainMenuSceneName);
                        wiredExit++;
                        Debug.Log($"[LeftApp] 退出按钮 → LoadScene({MainMenuSceneName})（{PathOf(action.transform)}）");
                    }
                    else if (isSettings)
                    {
                        SetAction(action, ButtonAction.ActionType.OpenWindow, string.Empty);
                        wiredSettings++;
                        Debug.Log($"[LeftApp] 设置按钮 → OpenWindow（打开 SmallApp）（{PathOf(action.transform)}）");
                    }
                    else
                    {
                        Debug.LogWarning($"[LeftApp] 有 ButtonAction 既不像退出也不像设置，跳过：{PathOf(action.transform)}");
                    }
                }

                if (wiredExit == 0 || wiredSettings == 0)
                {
                    Debug.LogWarning($"[LeftApp] 没接全：退出 {wiredExit} 个，设置 {wiredSettings} 个。"
                                     + "如果嵌套实例的子物体没有 exit_icon / setting_icon 这两个名字，就需要手动在 Inspector 里选 ActionType。");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, LeftAppPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[LeftApp] 已保存：{LeftAppPath}（退出 {wiredExit}，设置 {wiredSettings}）");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void SetAction(ButtonAction action, ButtonAction.ActionType type, string targetId)
        {
            var so = new SerializedObject(action);
            // button 留空即可：ButtonAction.Awake 会自动找同物体上的 Button。
            so.FindProperty("button").objectReferenceValue = null;
            so.FindProperty("actionType").enumValueIndex = (int)type;
            so.FindProperty("targetId").stringValue = targetId ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool HasDescendantNamed(Transform root, string keyword)
        {
            if (root.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
