using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Impermanence
{

    [BepInDependency(ItemAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]

    // Soft Dependencies
    //[BepInDependency(LookingGlass.PluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.DifferentModVersionsAreOk)]
    public class ImpermanencePlugin : BaseUnityPlugin
    {

        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Braquen";
        public const string PluginName = "Impermanance";
        public const string PluginVersion = "1.0.0";


        public static PluginInfo pluginInfo;
        public static AssetBundle AssetBundle;

        public void Awake()
        {
            Log.Init(Logger);

            pluginInfo = Info;
            AssetBundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(pluginInfo.Location), "impermanenceassetbundle"));

            if (ExtraItemsLunar.isEnabled)
            {
                GenericGameEvents.Init();
                ExtraItemsLunar.Init();
            }
        }

        // //TEST
        // private void Update()
        // {
        //     // This if statement checks if the player has currently pressed F2.
        //     if (Input.GetKeyDown(KeyCode.F2))
        //     {
        //         // Get the player body to use a position:
        //         var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

        //         // And then drop our defined item in front of the player.

        //         Log.Info($"Player pressed F2. Spawning our custom item at coordinates {transform.position}");
        //         PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(ExtraItemsLunar.itemDef.itemIndex), transform.position, transform.forward * 20f);
        //     }
        // }
    }
}

