using System.IO;
using System.Reflection;
using COM3D2.MotionTimelineEditor;

namespace COM3D25.PostEffects.Plugin
{
    public static class PluginUtils
    {
        public static readonly string UserDataPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Config");

        public const string PluginVersion = PluginInfo.PluginVersion;

        public static string ConfigPath
        {
            get => MTEUtils.CombinePaths(UserDataPath, PluginInfo.PluginName + ".xml");
        }
    }
}
