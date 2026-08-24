using System.Linq;
using Project.Core.Runtime.Managers;
using Project.Gameplay.Scripts.Items;
using Project.UI.Scripts;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.BigApp.Editor
{
    /// <summary>
    /// 把 BigApp 的 box/item 槽位接到物品系统：
    ///   - boxButton1-3 → ContainmentSlotButton，绑定 Stage2Breach/Items 的三个线索（BloodyWatch/CameraRecord/LivingPhoto）
    ///   - itemButton1-4 → ToolSlotButton，绑定三个工具（Detector/ToolKit/UVLight）到装备槽 0/1/2
    ///   - 在 BigApp 主面板中央建固定 ItemDetailPanel（图标/名称/描述/状态）
    ///   - 场景放一个 InventoryManager（会 Services 自动注册）
    /// 用法：Tools → Panel Stack → Bind BigApp Slots。
    /// </summary>
    public static class BindBigAppSlots
    {
        private const string ItemsRoot = "Assets/Samples/Samples/Stage2Breach/Items/";
        private const int UiLayer = 5;

        [MenuItem("Tools/Panel Stack/Bind BigApp Slots")]
        public static void Bind()
        {
            var detail = BuildDetailPanel();

            // 收容箱 3 格（运行时动态填线索，默认为空）
            BindBox("boxButton1", 0, detail);
            BindBox("boxButton2", 1, detail);
            BindBox("boxButton3", 2, detail);

            // 工具栏 4 格（运行时动态填已携带工具，默认为空）
            BindItem("itemButton1", 0, detail);
            BindItem("itemButton2", 1, detail);
            BindItem("itemButton3", 2, detail);
            BindItem("itemButton4", 3, detail);

            EnsureInventoryManager();

            if (detail != null) EditorUtility.SetDirty(detail.gameObject);
            Debug.Log("[Bind] BigApp 槽位已绑定动态读取（收容箱/工具栏默认空，按槽位读运行时数据）。");
        }

        private static void BindBox(string buttonName, int slotIndex, ItemDetailPanel detail)
        {
            var go = FindByName(buttonName, "boxcontent");
            if (go == null) return;

            RemoveButtonAction(go);
            var slot = go.GetComponent<ContainmentSlotButton>() ?? go.AddComponent<ContainmentSlotButton>();
            var so = new SerializedObject(slot);
            so.FindProperty("slotIndex").intValue = slotIndex;
            so.FindProperty("detail").objectReferenceValue = detail;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindItem(string buttonName, int slotIndex, ItemDetailPanel detail)
        {
            var go = FindByName(buttonName, "boxcontent");
            if (go == null) return;

            RemoveButtonAction(go);
            var slot = go.GetComponent<ToolSlotButton>() ?? go.AddComponent<ToolSlotButton>();
            var so = new SerializedObject(slot);
            so.FindProperty("slotIndex").intValue = slotIndex;
            so.FindProperty("detail").objectReferenceValue = detail;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveButtonAction(GameObject go)
        {
            var existing = go.GetComponent<ButtonAction>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static GameObject FindByName(string name, string contentParent)
        {
            var candidates = Object.FindObjectsOfType<GameObject>(true)
                .Where(g => g.name == name);
            // 优先在 BigApp 下（路径含 BigApp）的。
            var big = candidates.FirstOrDefault(g => g.transform != null &&
                g.transform.GetComponentsInParent<Transform>(true).Any(p => p.name == "BigApp"));
            return big != null ? big : candidates.FirstOrDefault();
        }

        // ---------- 详情区：直接适配 nothink 的子物体 ----------

        private static ItemDetailPanel BuildDetailPanel()
        {
            var bigApp = Object.FindObjectsOfType<GameObject>(true)
                .FirstOrDefault(g => g.name == "BigApp");
            if (bigApp == null)
            {
                Debug.LogWarning("[Bind] 未找到 BigApp，详情区未适配。");
                return null;
            }

            // 幂等：清除之前独立新建的 DetailPanel（现在改为挂在 nothink 上）。
            var oldDetail = bigApp.transform.Find("DetailPanel");
            if (oldDetail != null)
            {
                Object.DestroyImmediate(oldDetail.gameObject);
            }

            var nothink = bigApp.transform.Find("nothink");
            if (nothink == null)
            {
                Debug.LogWarning("[Bind] 未找到 BigApp/nothink，详情区未适配。");
                return null;
            }

            // 读取 nothink 的子物体（Name/Desc/Status 均为 TextMeshProUGUI），把 ItemDetailPanel 挂到 nothink 上。
            var panel = nothink.GetComponent<ItemDetailPanel>() ?? nothink.gameObject.AddComponent<ItemDetailPanel>();
            var nameTmp = nothink.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var descTmp = nothink.Find("Desc")?.GetComponent<TextMeshProUGUI>();
            var statusTmp = nothink.Find("Status")?.GetComponent<TextMeshProUGUI>();

            var so = new SerializedObject(panel);
            so.FindProperty("icon").objectReferenceValue = null;
            so.FindProperty("nameText").objectReferenceValue = nameTmp;
            so.FindProperty("descText").objectReferenceValue = descTmp;
            so.FindProperty("statusText").objectReferenceValue = statusTmp;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Bind] ItemDetailPanel 已挂到 nothink，字段指向 Name/Desc/Status。");
            return panel;
        }

        private static void EnsureInventoryManager()
        {
            if (Object.FindObjectOfType<InventoryManager>() == null)
            {
                new GameObject("InventoryManager").AddComponent<InventoryManager>();
            }
        }

        private static Sprite LoadSprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            s.name = System.IO.Path.GetFileNameWithoutExtension(path);
            return s;
        }
    }
}
