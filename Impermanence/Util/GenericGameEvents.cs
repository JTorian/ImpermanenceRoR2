using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;

namespace Impermanence
{
    public class GenericGameEvents
    {
        public delegate void PlayerCharacterDeathEventHandler(DamageReport damageReport, ref string deathQuote);

        public static event PlayerCharacterDeathEventHandler OnPlayerCharacterDeath;

        internal static void ErrorHookFailed(string name)
        {
            Log.Error("generic game event '" + name + "' hook failed");
        }

        internal static void Init()
        {
            IL.RoR2.GlobalEventManager.OnPlayerCharacterDeath += (il) =>
            {
                ILCursor c = new ILCursor(il);

                int deathQuotePos = -1;

                if (c.TryGotoNext(
                    x => x.MatchLdstr("PLAYER_DEATH_QUOTE_VOIDDEATH"),
                    x => x.MatchStloc(out deathQuotePos)
                ) && c.TryGotoNext(
                    x => x.MatchLdsfld<GlobalEventManager>("standardDeathQuoteTokens")
                ) && c.TryGotoNext(
                    MoveType.Before,
                    x => x.MatchStloc(deathQuotePos)
                ))
                {
                    c.Emit(OpCodes.Ldarg_1);
                    c.EmitDelegate<System.Func<string, DamageReport, string>>((deathQuote, damageReport) =>
                    {
                        if (OnPlayerCharacterDeath != null) OnPlayerCharacterDeath(damageReport, ref deathQuote);
                        return deathQuote;
                    });
                }
                else ErrorHookFailed("on player character death");
            };
        }
    }
}