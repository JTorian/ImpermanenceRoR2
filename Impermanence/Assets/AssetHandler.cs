using R2API;
using System.IO;
using UnityEngine;

namespace Impermanence
{
    public static class AssetHandler
    {
        public static AssetBundle bundle;
        public const string bundleName = "assets";

        public static string AssetBundlePath
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(ImpermanencePlugin.pluginInfo.Location), bundleName);
            }
        }

        public static void Init()
        {
            bundle = AssetBundle.LoadFromFile(AssetBundlePath);
        }
    }
}
