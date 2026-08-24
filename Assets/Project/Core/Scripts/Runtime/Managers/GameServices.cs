using Project.Core.Runtime.Framework;
using Project.Narrative.Scripts;

namespace Project.Core.Runtime.Managers
{
    /// <summary>
    /// 全局唯一的服务定位器：快捷访问各个 Manager（内部走 Services.Get&lt;T&gt;）。
    /// 继承 ManagerBehaviour（自带单例 + DontDestroyOnLoad），场景放一个即可。
    /// 用法：GameServices.Instance.Flags.Set(...) / GameServices.Instance.SceneFlow.LoadSceneAsync(...)
    /// </summary>
    public sealed class GameServices : ManagerBehaviour
    {
        public static GameServices Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        public GameManager GameManager => Services.Get<GameManager>();
        public FlagManager Flags => Services.Get<FlagManager>();
        public UIManager UI => Services.Get<UIManager>();
        public AudioManager Audio => Services.Get<AudioManager>();
        public InventoryManager Inventory => Services.Get<InventoryManager>();
        public SceneFlowManager SceneFlow => Services.Get<SceneFlowManager>();
        public SaveManager Save => Services.Get<SaveManager>();
        public SanityManager Sanity => Services.Get<SanityManager>();
        public EvidenceManager Evidence => Services.Get<EvidenceManager>();
        public BranchManager Branch => Services.Get<BranchManager>();
        public GameLoopManager GameLoop => Services.Get<GameLoopManager>();
        public InteractionManager Interaction => Services.Get<InteractionManager>();
        public VNDirector VN => Services.Get<VNDirector>();
        public CGManager CG => Services.Get<CGManager>();
    }
}
