using RoR2;
using RoR2.Navigation;
using R2API;
using R2API.Utils;
using R2API.Networking;
using R2API.Networking.Interfaces;
using UnityEngine;
using UnityEngine.Networking;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using RoR2.UI;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using RoR2.Projectile;
using System;

namespace Impermanence
{
    public class ExtraItemsLunar
    {
        public static ItemDef itemDef;
        // public static BuffDef chestBuff;

        public static ConfigurableValue<bool> isEnabled = new(
            "Item: Impermanence",
            "Enabled",
            true,
            "Whether or not the item is enabled.",
            new List<string>()
            {
                "ITEM_DEADLINE_DESC"
            }
        );
        public static ConfigurableValue<float> baseTimer = new(
            "Item: Impermanence",
            "Base Time Limit",
            730f,
            "The time limit with one stack of impermanence.",
            new List<string>()
            {
                "ITEM_DEADLINE_DESC"
            }
        );
        public static ConfigurableValue<float> bonusChancePerStack = new(
            "Item: Impermanence",
            "Chance per Stack",
            20f,
            "The chance of getting bonus items as a percentage.",
            new List<string>()
            {
                "ITEM_DEADLINE_DESC"
            }
        );
        public static ConfigurableValue<float> timePerStack = new(
            "Item: Impermanence",
            "Time Decrease per Stack",
            20f,
            "The decrease in remaining time as a percentage.",
            new List<string>()
            {
                "ITEM_DEADLINE_DESC"
            }
        );

        public static float bonusChancePercent = bonusChancePerStack/100f;
        public static float timerDecreasePercent = timePerStack/100f;


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
                ItemTag.InteractableRelated,
                ItemTag.AIBlacklist,
                ItemTag.CannotCopy,
                ItemTag.OnStageBeginEffect
            };


            itemDef.pickupIconSprite = Addressables.LoadAssetAsync<Sprite>("RoR2/DLC1/FragileDamageBonus/texDelicateWatchIcon.png").WaitForCompletion();
            itemDef.pickupModelPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/FragileDamageBonus/DisplayDelicateWatch.prefab").WaitForCompletion();

            itemDef.canRemove = true;
            itemDef.hidden = false;

            ItemDisplayRuleDict displayRules = new ItemDisplayRuleDict(null);
            ItemAPI.Add(new CustomItem(itemDef, displayRules));

            //BUFF//
            // chestBuff = ScriptableObject.CreateInstance<BuffDef>();

            // chestBuff.name = "Lunar boon";
            // chestBuff.canStack = true;
            // chestBuff.isHidden = false;
            // chestBuff.isDebuff = false;
            // chestBuff.isCooldown = false;

            Hooks();
        }

        public static void Hooks()
        {
            On.RoR2.CharacterBody.OnInventoryChanged += (orig, self) =>
            {
                orig(self);
                self.AddItemBehavior<ImpermanenceBehaviour>(self.inventory.GetItemCount(itemDef));
            };

            On.RoR2.PurchaseInteraction.OnInteractionBegin += (orig, self, activator) =>
            {
                if (!self.saleStarCompatible)
                {
                    orig(self, activator);
                    return;
                }
                CharacterBody body = activator.GetComponent<CharacterBody>();
                if (body)
                {
                    ImpermanenceBehaviour component = body.GetComponent<ImpermanenceBehaviour>();
                    int multiplier = component.TryDoubleItem();
                    if (component && multiplier > 1)
                    {   
                        Log.Debug("multiplying items");
                        // Util.PlaySound(RoR2.DLC2Content.Items.LowerPricedChests., body.gameObject);

                        //Flag to be doubled
                        ImpermanenceMultiplyItemBehaviour multiplyFlag = self.gameObject.AddComponent<ImpermanenceMultiplyItemBehaviour>();
                        multiplyFlag.multiplier = multiplier;
                    }
                }
                orig(self, activator);
            };

            On.RoR2.ChestBehavior.ItemDrop +=  (orig, self) =>
             {
                PurchaseInteraction purchaseInteraction = self.gameObject.GetComponent<PurchaseInteraction>();
                if (purchaseInteraction)
                {
                    ImpermanenceMultiplyItemBehaviour component = purchaseInteraction.GetComponent<ImpermanenceMultiplyItemBehaviour>();

                    if(component)
                    {
                        //It's lunar, so it should MULTIPLY sale star
                        self.dropCount *= component.multiplier;
                    }
                }
                orig(self);
            };

            GenericGameEvents.OnPlayerCharacterDeath += GenericGameEvents_OnPlayerCharacterDeath;
        }

        public static string[] impermanenceDeathQuoteTokens = (from i in Enumerable.Range(0, 5) select "PLAYER_DEATH_QUOTE_IMPERMANENCE_" + TextSerialization.ToStringInvariant(i)).ToArray();
        public static void GenericGameEvents_OnPlayerCharacterDeath(DamageReport damageReport, ref string deathQuote)
        {
            if (damageReport.victimBody)
                {
                    ImpermanenceBehaviour component = damageReport.victimBody.GetComponent<ImpermanenceBehaviour>();
                    if (component && component.diedFromTimer)
                    {
                        deathQuote = impermanenceDeathQuoteTokens[UnityEngine.Random.Range(0, impermanenceDeathQuoteTokens.Length)];
                    }
                }
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

            public List<PurchaseInteraction> interactions = new List<PurchaseInteraction>();

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
                        // body.RemoveBuff(chestBuff);
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

            public void UpdateItemBasedInfo()
            {
                if (!body) return;
                countdownTimer = Mathf.Min(baseTimer * Mathf.Pow( 1 - timerDecreasePercent, stack-1), countdownTimer); // Cut the timer down, unless the timer is already low enough
            }

            public void ResetTimer()
            {
                countdownTimer = baseTimer;
                countdownCalculated = false;
                countdownCalculationTimer = 0.5f;
            }

            public int TryDoubleItem()
            {
                if (!body) return 1;

                float proc = bonusChancePercent * stack;

                //Essentially, if you have a > 100% chance, always double the item with a chance to TRIPLE
                int bonus = (int)MathF.Floor(proc);
                if (Util.CheckRoll((proc - bonus)*100f, body.master)) bonus += 1;
                
                Debug.Log("TryDoubleItem(): multiplier=" + (bonus+1) + " proc=" + proc);
                return bonus + 1;
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
        public class ImpermanenceMultiplyItemBehaviour : MonoBehaviour
        {
            public int multiplier;
        }
    }
}
