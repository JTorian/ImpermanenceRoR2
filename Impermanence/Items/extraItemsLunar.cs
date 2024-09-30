using R2API;
using RoR2;
using RoR2.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace Impermanence
{
    public class ExtraItemsLunar
    {
        public static ItemDef itemDef;
        public static BuffDef chestBuff;

        public const float baseTimer = 730f; //At one item, the timer is 15 minutes. SHOULD be more than enough


        public static DamageAPI.ModdedDamageType damageType;

        internal static void Init()
        {
            //ITEM//
            itemDef = ScriptableObject.CreateInstance<ItemDef>();

            itemDef.name = "DEADLINE";
            itemDef.nameToken = "ITEM_DEADLINE_NAME";
            itemDef.pickupToken = "ITEM_DEADLINE_PICKUP";
            itemDef.descriptionToken = "ITEM_DEADLINE_DESC";
            itemDef.loreToken = "ITEM_DEADLINE_LORE";

            itemDef.AutoPopulateTokens();

            ItemTierCatalog.availability.CallWhenAvailable(() =>
            {
                if (itemDef) itemDef.tier = ItemTier.Lunar;
            });

            itemDef.tags = new ItemTag[]
            {
                ItemTag.Utility,
                ItemTag.InteractableRelated
            };


            itemDef.pickupIconSprite = Addressables.LoadAssetAsync<Sprite>("RoR2/DLC1/FragileDamageBonus/texDelicateWatchIcon.png").WaitForCompletion();
            itemDef.pickupModelPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/FragileDamageBonus/DisplayDelicateWatch.prefab").WaitForCompletion();

            itemDef.canRemove = true;
            itemDef.hidden = false;

            ItemDisplayRuleDict displayRules = new ItemDisplayRuleDict(null);
            ItemAPI.Add(new CustomItem(itemDef, displayRules));

            //BUFF//
            chestBuff = ScriptableObject.CreateInstance<BuffDef>();

            chestBuff.name = "Lunar boon";
            chestBuff.canStack = true;
            chestBuff.isHidden = false;
            chestBuff.isDebuff = false;
            chestBuff.isCooldown = true;

            On.RoR2.CharacterBody.OnInventoryChanged += (orig, self) =>
            {
                orig(self);
                self.AddItemBehavior<ImpermanenceBehaviour>(self.inventory.GetItemCount(itemDef));
            };

            Hooks();

        }

        public class ImpermanenceBehaviour : CharacterBody.ItemBehavior
        {

            public float countdownTimer = baseTimer;
            public bool diedFromTimer = false;

            public bool countdownCalculated = false;
            public float countdownCalculationTimer = 0.5f;
            public float countdownCalculationInterval = 4f;

            public bool countdown10Played = false;
            public uint countdown10ID;

            public void Start()
            {
                //HOOKS//
                body.onInventoryChanged += Body_onInventoryChanged;
                Stage.onStageStartGlobal += Stage_onStageStartGlobal;

                diedFromTimer = false;
            }

            public void Update()
            {


                if (!countdownCalculated)
                {
                    countdownCalculationTimer -= Time.deltaTime;
                    if (countdownCalculationTimer <= 0)
                    {
                        countdownCalculated = true;
                        if (countdownCalculated)
                        {
                            UpdateItemBasedInfo();
                            countdownCalculationTimer += countdownCalculationInterval;
                        }
                    }
                }

                else
                {
                    if (stack > 0)
                    {
                        countdownTimer -= Time.deltaTime;


                        if (countdownTimer <= 10f && !countdown10Played)
                        {
                            countdown10Played = true;
                            //countdown10ID = Util.PlaySound("MysticsItems_Play_riftLens_countdown_10", body.gameObject);
                        }

                        if (countdownTimer <= 0 && !diedFromTimer)
                        {
                            diedFromTimer = true;
                            if (NetworkServer.active) body.healthComponent.Suicide();
                        }
                    }
                    else
                    {
                        if (countdown10Played)
                        {
                            countdown10Played = false;
                            ResetTimer();
                            //AkSoundEngine.StopPlayingID(countdown10ID);
                        }
                    }
                }
            }

            public void OnDestroy()
            {
                if (body) body.onInventoryChanged -= Body_onInventoryChanged;
            }

            public void Body_onInventoryChanged()
            {
                UpdateItemBasedInfo();
            }

            public void Stage_onStageStartGlobal(Stage stage)
            {
                ResetTimer();
            }

            public void On_RoR2_PurchaseInteraction_OnInteractionBegin()
            {
                
            }

            public void UpdateItemBasedInfo()
            {
                if (!body) return;
                countdownTimer = Mathf.Min(baseTimer * (Mathf.Pow(0.8f, stack-1)), countdownTimer); // Cut the timer down, unless the timer is already low enough
            }

            public void ResetTimer()
            {
                countdownTimer = baseTimer;
                countdownCalculated = false;
                countdownCalculationTimer = 0.5f;
            }

            public void OnEnable()
            {
                InstanceTracker.Add(this);
            }

            public void OnDisable()
            {
                InstanceTracker.Remove(this);
            }
        }
        public static void Hooks()
        {

            //Handle purchases
            On.RoR2.PurchaseInteraction.OnInteractionBegin += (orig, self, activator) =>
            {

                Log.Debug("PurchaseInteraction_OnInteractionBegin " + self.gameObject.name);

                // Server/host side only
                if (!NetworkServer.active) { orig(self, activator); return; }

                // Check from orig
                if (!self.CanBeAffordedByInteractor(activator))
                {
                    orig(self, activator);
                    return;
                }

                var player_body = activator.GetComponent<CharacterBody>();
                if (player_body != null) { orig(self, activator); return; }

                int stacks = player_body.inventory.GetItemCount(itemDef);

                // Check the interactable can be doubled, and if the player has the buff
                if (
                    player_body.inventory == null
                    || stacks < 1
                    || self == null
                    || !self.saleStarCompatible)
                { orig(self, activator); return; }

                TryDoubleItem(self, player_body, stacks);
                { orig(self, activator); return; }
            };

        }

        public static void TryDoubleItem(PurchaseInteraction chest, CharacterBody player, int stacks)
        {
            float proc = (0.2f * stacks);
            if (Util.CheckRoll(proc, player.master))
            {
                Log.Debug("Double!");
                Chat.AddMessage("Double!");
            }
        }

    }
}
