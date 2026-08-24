using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Core.Runtime.Managers
{
    public abstract class ManagerBehaviour : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        protected virtual void Awake()
        {
            // 单例：若同类型已有实例注册，销毁自己（保留全局第一个），避免重复 manager 导致状态分裂。
            if (Services.TryGet(GetType(), out var existing) && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            Services.Register(GetType(), this);

            if (dontDestroyOnLoad && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            Services.UnregisterInstance(this);
        }
    }
}
