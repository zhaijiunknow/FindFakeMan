using UnityEngine;
using Project.Core.Runtime.Framework;

namespace Project.Core.Runtime.Managers
{
    public abstract class ManagerBehaviour : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        protected virtual void Awake()
        {
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
