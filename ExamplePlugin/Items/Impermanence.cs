using R2API;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace Impermanence
{
    internal class Impermanence
    {
        public static ItemDef itemDef;
        public static BuffDef chestBuff;
        public static BuffDef deadlineDebuff;


        public static DamageAPI.ModdedDamageType damageType;

        internal static void Init()
        {
            //ITEM//
            itemDef.name = "ITEM_DEADLINE_NAME";
            itemDef.nameToken = "ITEM_DEADLINE_NAME";
            itemDef.pickupToken = "ITEM_DEADLINE_PICKUP";
            itemDef.descriptionToken = "ITEM_DEADLINE_DESC";
            itemDef.loreToken = "ITEM_DEADLINE_LORE";

            ItemTierCatalog.availability.CallWhenAvailable(() =>
            {
                if (itemDef) itemDef.tier = ItemTier.Lunar;
            });

            itemDef.tags = new ItemTag[]
            {
                ItemTag.Utility,
                ItemTag.InteractableRelated
            };


            itemDef.pickupIconSprite = Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/MiscIcons/texMysteryIcon.png").WaitForCompletion();
            itemDef.pickupModelPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Mystery/PickupMystery.prefab").WaitForCompletion();

            ItemDisplayRuleDict displayRules = new ItemDisplayRuleDict(null);
            ItemAPI.Add(new CustomItem(itemDef, displayRules));

            //BUFF//
            


            Hooks();
        }
        public static void Hooks()
        {
            //Set timer on stage start
            Stage.onStageStartGlobal += (stage) =>
            {
                foreach (NetworkUser user in NetworkUser.readOnlyInstancesList)
                {
                    CharacterMaster master = user.masterController.master ?? user.master;
                    if (master && master.inventory && master.inventory.GetItemCount(itemDef) > 0)
                    {
                        StartClock(master.inventory, master.GetBody());
                    }
                }
            };

            //Set timer on item pickup
            On.RoR2.Inventory.GiveItem_ItemIndex_int += (orig, self, index, count) =>
            {
                
            };

        }

        public static void StartClock(Inventory inventory, CharacterBody body)
        {
            
        }
    }
}
