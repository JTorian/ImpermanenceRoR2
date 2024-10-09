using RoR2;
using R2API;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using System;
using RoR2.UI;
using UnityEngine.UI;

namespace Impermanence
{
    public class ExtraItemsLunar
    {
        public static ItemDef itemDef;
        // public static BuffDef chestBuff;
        private static GameObject hudTimer;
        public static ConfigurableValue<bool> isEnabled = new(
            "Item: Impermanence",
            "Enabled",
            true,
            "Whether or not the item is enabled.",
            new List<string>()
            {
                "ITEM_EXTRAITEMSLUNAR_DESC"
            }
        );
        public static ConfigurableValue<float> baseTimer = new(
            "Item: Impermanence",
            "Base Time Limit",
            600f,
            "The time limit with one stack of impermanence.",
            new List<string>()
            {
                "ITEM_EXTRAITEMSLUNAR_DESC"
            }
        );
        public static ConfigurableValue<float> bonusChancePerStack = new(
            "Item: Impermanence",
            "Chance per Stack",
            20f,
            "The chance of getting bonus items as a percentage.",
            new List<string>()
            {
                "ITEM_EXTRAITEMSLUNAR_DESC"
            }
        );
        public static ConfigurableValue<float> timePerStack = new(
            "Item: Impermanence",
            "Time Decrease per Stack",
            15f,
            "The decrease in remaining time as a percentage.",
            new List<string>()
            {
                "ITEM_EXTRAITEMSLUNAR_DESC"
            }
        );

        public static float bonusChancePercent = bonusChancePerStack/100f;
        public static float timerDecreasePercent = timePerStack/100f;

        internal static void Init()
        {
            Debug.Log("Initializing Impermanence Item");
            //ITEM//
            itemDef = ScriptableObject.CreateInstance<ItemDef>();

            itemDef.name = "EXTRAITEMSLUNAR";
            itemDef.nameToken = "ITEM_EXTRAITEMSLUNAR_NAME";
            itemDef.pickupToken = "ITEM_EXTRAITEMSLUNAR_PICKUP";
            itemDef.descriptionToken = "ITEM_EXTRAITEMSLUNAR_DESC";
            itemDef.loreToken = "ITEM_EXTRAITEMSLUNAR_LORE";

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

            itemDef.pickupIconSprite = ImpermanencePlugin.AssetBundle.LoadAsset<Sprite>("Assets/Items/impermanence/Icon.png");
            itemDef.pickupModelPrefab = ImpermanencePlugin.AssetBundle.LoadAsset<GameObject>("Assets/Items/impermanence/Model.prefab");
            
            ModelPanelParameters ModelParams = itemDef.pickupModelPrefab.AddComponent<ModelPanelParameters>();

            ModelParams.minDistance = 5;
            ModelParams.maxDistance = 10;
            // itemDef.pickupModelPrefab.GetComponent<ModelPanelParameters>().cameraPositionTransform.localPosition = new Vector3(1, 1, -0.3f); 
            // itemDef.pickupModelPrefab.GetComponent<ModelPanelParameters>().focusPointTransform.localPosition = new Vector3(0, 1, -0.3f);
            // itemDef.pickupModelPrefab.GetComponent<ModelPanelParameters>().focusPointTransform.localEulerAngles = new Vector3(0, 0, 0);
            
            

            itemDef.canRemove = true;
            itemDef.hidden = false;

            ItemDisplayRuleDict displayRules = makeDisplayRules();
            ItemAPI.Add(new CustomItem(itemDef, displayRules));

            Debug.Log("Impermanence Initialized");

            //BUFF//
            // chestBuff = ScriptableObject.CreateInstance<BuffDef>();

            // chestBuff.name = "Lunar boon";
            // chestBuff.canStack = true;
            // chestBuff.isHidden = false;
            // chestBuff.isDebuff = false;
            // chestBuff.isCooldown = false;

            //HUD//
            hudTimer = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/UI/HudCountdownPanel.prefab").WaitForCompletion();
            hudTimer.transform.Find("Juice/Container/CountdownTitleLabel").GetComponent<LanguageTextMeshController>().token = "IMPERMANENCE_TIMER_FLAVOUR";
            var col = new Color32(0, 157, 255, 255);
            hudTimer.transform.Find("Juice/Container/Border").GetComponent<Image>().color = col;
            hudTimer.transform.Find("Juice/Container/CountdownLabel").GetComponent<HGTextMeshProUGUI>().color = col;

            
            Hooks();
        }

        public static void Hooks()
        {
            On.RoR2.CharacterBody.OnInventoryChanged += (orig, self) =>
            {
                self.AddItemBehavior<ImpermanenceBehaviour>(self.inventory.GetItemCount(itemDef));
                orig(self);
            };

            On.RoR2.PurchaseInteraction.OnInteractionBegin += (orig, self, activator) =>
            {
                if (!self.saleStarCompatible || !self.CanBeAffordedByInteractor(activator))
                {
                    orig(self, activator);
                    return;
                }

                CharacterBody body = activator.GetComponent<CharacterBody>();

                if (body)
                {
                    ImpermanenceBehaviour component = body.GetComponent<ImpermanenceBehaviour>();
                    if (!component)
                    {
                        orig(self, activator);
                        return;
                    }

                    int multiplier = component.TryDoubleItem();
                    if (multiplier > 1)
                    {   
                        Log.Debug("multiplying items");
                        Util.PlaySound("Play_item_proc_lowerPricedChest", body.gameObject);
                        

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

        public static string[] impermanenceDeathQuoteTokens = (from i in Enumerable.Range(0, 5) select "PLAYER_DEATH_QUOTE_EXTRAITEMSLUNAR_" + TextSerialization.ToStringInvariant(i)).ToArray();
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

            private bool bossDefeated = false;
            public bool countdown10Played = false;
            public uint countdown10ID;
            public HUD bodyHud;
            public GameObject hudPanel = null;

            public void Start()
            {
                foreach (HUD hud in HUD.readOnlyInstanceList)
                {
                    if (hud.targetBodyObject == body.gameObject)
                    {
                        bodyHud = hud;
                    }
                }
                
                //HOOKS//
                body.onInventoryChanged += Body_onInventoryChanged;
                Stage.onStageStartGlobal += Stage_onStageStartGlobal;
                On.RoR2.BossGroup.OnDefeatedServer += BossGroup_onDefeatedServer;
            }

            public void Update()
            {
                
                if (stack > 0 && !bossDefeated)
                {
                    countdownTimer -= Time.deltaTime;
                    
                    if (!diedFromTimer)
                    {
                        SetHudCountdownEnabled(true);
                        SetCountdownTime(countdownTimer);

                        //Make things more intense
                        if (countdownTimer <= 10f && !countdown10Played)
                        {
                            countdown10Played = true;
                            countdown10ID = Util.PlaySound("Play_UI_arenaMode_coundown_loop", body.gameObject);
                            
                        }

                        //Die
                        if (countdownTimer <= 0)
                        {
                            SetCountdownTime(0f);
                            diedFromTimer = true;
                            if (NetworkServer.active) body.healthComponent.Suicide();

                            //Make sure we don't have to hear the countdown forever
                            if (countdown10Played)
                            {
                                countdown10Played = false;
                                AkSoundEngine.StopPlayingID(countdown10ID);
                            }
                        }
                    }
                }
                else
                {

                    SetHudCountdownEnabled(false);
                    ResetTimer();

                    // body.RemoveBuff(chestBuff);
                    if (countdown10Played)
                    {
                        countdown10Played = false;
                        AkSoundEngine.StopPlayingID(countdown10ID);
                    }
                }
                
            }

            public void OnDestroy()
            {
                if (body) body.onInventoryChanged -= Body_onInventoryChanged;
                Stage.onStageStartGlobal -= Stage_onStageStartGlobal;
                On.RoR2.BossGroup.OnDefeatedServer -= BossGroup_onDefeatedServer;
            }

            public void Body_onInventoryChanged()
            {
                UpdateItemBasedInfo();
            }

            public void Stage_onStageStartGlobal(Stage stage)
            {
                ResetTimer();
                bossDefeated = false;
            }

            public void BossGroup_onDefeatedServer(On.RoR2.BossGroup.orig_OnDefeatedServer orig, BossGroup self)
            {
                //Ignore minibosses
                if (self.name != "SuperRoboBallEncounter" || self.name != "ShadowCloneEncounter(Clone)")
                {
                    bossDefeated = true;
                }
                orig(self);
            }

            public void UpdateItemBasedInfo()
            {
                if (!body) return;
                if (stack < 1) 
                {
                    countdownTimer = baseTimer;
                    return;
                }

                countdownTimer = Mathf.Min(baseTimer * Mathf.Pow( 1 - timerDecreasePercent, stack-1), countdownTimer); // Cut the timer down, unless the timer is already low enough
            }

            public void ResetTimer()
            {
                countdownTimer = baseTimer;
            }

            public int TryDoubleItem()
            {
                //No bonus after the boss is done
                if (!body || bossDefeated) return 1;

                float proc = bonusChancePercent * stack;

                //Essentially, if you have a > 100% chance, always double the item with a chance to TRIPLE
                int bonus = (int)MathF.Floor(proc);
                if (Util.CheckRoll((proc - bonus)*100f, body.master)) bonus += 1;
                
                Debug.Log("TryDoubleItem(): multiplier=" + (bonus+1) + " proc=" + proc);
                return bonus + 1;
            }

            public void SetHudCountdownEnabled(bool shouldEnableCountdownPanel)
            {   
                //thinking about this gives me a headache
                if ((hudPanel != null) != shouldEnableCountdownPanel)
                {
                    if (shouldEnableCountdownPanel)
                    {
                        RectTransform rectTransform = bodyHud.GetComponent<ChildLocator>().FindChild("TopCenterCluster") as RectTransform;
                        if (rectTransform)
                        {
                            hudPanel = Instantiate<GameObject>(hudTimer, rectTransform);
                        }
                    }
                    else
                    {  
                        Destroy(hudPanel);
                        hudPanel = null;
                    }
                }
            }

            public void SetCountdownTime(double secondsRemaining)
            {
                    hudPanel.GetComponent<TimerText>().seconds = secondsRemaining;
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

        public static ItemDisplayRuleDict makeDisplayRules()
        {
            ItemDisplayRule[] DefaultRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Head",
                            localPos = new Vector3(0f, 0.43f, 0f),
                            localAngles = new Vector3(0f,0f,0f),
                            localScale = new Vector3(0.05f,0.05f,0.05f),
                        },
            };
            ItemDisplayRule[] CommandoRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Head",
                            localPos = new Vector3(0f, 0.43f, 0f),
                            localAngles = new Vector3(0f,0f,0f),
                            localScale = new Vector3(0.05f,0.05f,0.05f),
                        },
            };
            ItemDisplayRule[] HuntressRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Head",
                            localPos = new Vector3(0f, 0.35f, -0.09f),
                            localAngles = new Vector3(345f,0f,0f),
                            localScale = new Vector3(0.05f,0.05f,0.05f),
                        },
            };
            ItemDisplayRule[] BanditRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Head",
                            localPos = new Vector3(0f, 0.23f, 0f),
                            localAngles = new Vector3(355f,0f,0f),
                            localScale = new Vector3(0.04f,0.04f,0.04f),
                        },
            };
            ItemDisplayRule[] MULTRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Head",
                            localPos = new Vector3(-1.44F, 3.71F, 0.37F),
                            localAngles = new Vector3(55F, 0F, 0F),
                            localScale = new Vector3(0.2F, 0.2F, 0.2F)
                        },
            };
            ItemDisplayRule[] EngiRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "MuzzleRight",
                            localPos = new Vector3(-0.1725F, -0.17242F, -0.29035F),
                            localAngles = new Vector3(315F, 270F, 180F),
                            localScale = new Vector3(0.031F, 0.03F, 0.031F)
                        },
            };
            ItemDisplayRule[] ArtiRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Chest",
                            localPos = new Vector3(-0.11179F, 0.33944F, -0.17685F),
                            localAngles = new Vector3(10F, 0F, 0F),
                            localScale = new Vector3(0.05F, 0.05F, 0.05F)
                        },
            };
            ItemDisplayRule[] MercRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Chest",
                            localPos = new Vector3(0.00322F, 0.28508F, -0.22849F),
                            localAngles = new Vector3(0F, 90F, 0F),
                            localScale = new Vector3(0.02F, 0.02F, 0.02F)
                        },
            };
            ItemDisplayRule[] REXRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "FlowerBase",
                            localPos = new Vector3(-0.66143F, 0.72885F, 0.48302F),
                            localAngles = new Vector3(0F, 180F, 0F),
                            localScale = new Vector3(0.05F, 0.05F, 0.05F)
                        },
            };
            ItemDisplayRule[] LoaderRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "MechBase",
                            localPos = new Vector3(0.15849F, 0.38956F, 0.42917F),
                            localAngles = new Vector3(0F, 180F, 0F),
                            localScale = new Vector3(0.02F, 0.02F, 0.02F)
                        },
            };
            ItemDisplayRule[] AcridRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "SpineChest1",
                            localPos = new Vector3(1.61135F, 3.34974F, 5.01534F),
                            localAngles = new Vector3(0F, 0F, 0F),
                            localScale = new Vector3(0.2F, 0.2F, 0.2F)
                        },
            };
            ItemDisplayRule[] CapRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "ClavicleL",
                            localPos = new Vector3(-0.00551F, 0.00861F, -0.13122F),
                            localAngles = new Vector3(270F, 0F, 0F),
                            localScale = new Vector3(0.05F, 0.05F, 0.05F)
                        },
            };
            ItemDisplayRule[] RailgunnerRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Backpack",
                            localPos = new Vector3(-0.16635F, 0.43282F, 0.00679F),
                            localAngles = new Vector3(0F, 0F, 0F),
                            localScale = new Vector3(0.05F, 0.05F, 0.05F)
                        },
            };
            ItemDisplayRule[] ViendRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Chest",
                            localPos = new Vector3(-0.13114F, 0.28689F, -0.30823F),
                            localAngles = new Vector3(290F, 0F, 0F),
                            localScale = new Vector3(0.03F, 0.05F, 0.03F)
                        },
            };
            ItemDisplayRule[] SeekerRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Pack",
                            localPos = new Vector3(-0.26289F, 0.1602F, -0.21764F),
                            localAngles = new Vector3(345F, 90F, 0F),
                            localScale = new Vector3(0.05F, 0.05F, 0.05F)
                        },
            };
            ItemDisplayRule[] CHEFRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Chest",
                            localPos = new Vector3(-0.3189F, 0.16636F, -0.2061F),
                            localAngles = new Vector3(0F, 0F, 90F),
                            localScale = new Vector3(0.05F, 0.05F, 0.05F)
                        },
            };
            ItemDisplayRule[] SonRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Pelvis",
                            localPos = new Vector3(0.05234F, 0.12436F, 0.11509F),
                            localAngles = new Vector3(5F, 225F, 0F),
                            localScale = new Vector3(0.05F, 0.05F, 0.05F)
                        },
            };

            ItemDisplayRule[] ScavRules = new ItemDisplayRule[]
            {
                        new ItemDisplayRule
                        {
                            ruleType = ItemDisplayRuleType.ParentedPrefab,
                            followerPrefab = itemDef.pickupModelPrefab,
                            childName = "Head",
                            localPos = new Vector3(2.87f, 6.93f, -2.89f),
                            localAngles = new Vector3(35f,0f,0f),
                            localScale = new Vector3(0.5f,0.75f,0.5f),
                        },
            };

            ItemDisplayRuleDict DisplayRules = new ItemDisplayRuleDict(DefaultRules);
            DisplayRules.Add("CommandoBody", CommandoRules);
            DisplayRules.Add("HuntressBody", HuntressRules);
            DisplayRules.Add("Bandit2Body", BanditRules);
            DisplayRules.Add("ToolbotBody", MULTRules);
            DisplayRules.Add("EngiBody", EngiRules);
            DisplayRules.Add("MageBody", ArtiRules);
            DisplayRules.Add("MercBody", MercRules);
            DisplayRules.Add("TreebotBody", REXRules);
            DisplayRules.Add("LoaderBody", LoaderRules);
            DisplayRules.Add("CrocoBody", AcridRules);
            DisplayRules.Add("CaptainBody", CapRules);
            DisplayRules.Add("RailgunnerBody", RailgunnerRules);
            DisplayRules.Add("VoidSurvivorBody", ViendRules);
            DisplayRules.Add("SeekerBody", SeekerRules);
            DisplayRules.Add("ChefBody", CHEFRules);
            DisplayRules.Add("FalseSonBody", SonRules);

            DisplayRules.Add("ScavBody", ScavRules);

            return DisplayRules;
        }
    }
}
