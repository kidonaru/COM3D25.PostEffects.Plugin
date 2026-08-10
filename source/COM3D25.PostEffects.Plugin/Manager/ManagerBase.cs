using COM3D2.MotionTimelineEditor;
using UnityEngine.SceneManagement;

namespace COM3D25.PostEffects.Plugin
{
    public class ManagerBase : IManager
    {
        protected static PostEffectsPlugin plugin => PostEffectsPlugin.instance;
        protected static ConfigManager configManager => ConfigManager.instance;
        protected static Config config => ConfigManager.instance.config;
        protected static WindowManager windowManager => WindowManager.instance;
        protected static PostEffectManager postEffectManager => PostEffectManager.instance;

        public virtual void Init()
        {
        }

        public virtual void PreUpdate()
        {
        }

        public virtual void Update()
        {
        }

        public virtual void LateUpdate()
        {
        }

        public virtual void OnLoad()
        {
        }

        public virtual void OnPluginDisable()
        {
        }

        public virtual void OnChangedSceneLevel(Scene scene, LoadSceneMode sceneMode)
        {
        }
    }
}
