using System;
using UnityEngine;

namespace Project.UI.Panels
{
    /// <summary>
    /// 面板注册项：panelId → prefab → PanelKind。
    /// 挂在 PanelManager 的 panels 列表里做序列化注册，避免运行时查找依赖命名约定。
    /// </summary>
    [Serializable]
    public sealed class PanelEntry
    {
        [SerializeField] private string panelId;
        [SerializeField] private GameObject prefab;
        [SerializeField] private PanelKind kind = PanelKind.Page;

        public string PanelId => panelId;
        public GameObject Prefab => prefab;
        public PanelKind Kind => kind;
    }
}
