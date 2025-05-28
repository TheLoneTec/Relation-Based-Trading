using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RelationBasedTrading
{
    [HarmonyPatch]
    public static class StockGenerator_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in GenTypes.AllTypes.Where(t => t.IsClass && !t.IsAbstract && typeof(StockGenerator).IsAssignableFrom(t)))
            {
                var method = AccessTools.Method(type, nameof(StockGenerator.GenerateThings));
                yield return method;
            }
        }
        
        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result,
            int forTile,
            Faction faction,
            StockGenerator __instance)
        {
            Log.Message($"Calling postfix for {__instance.GetType().FullName}:Postfix({forTile},{faction})");
            if (faction == null || faction.IsPlayer)
            {
                foreach (Thing thing in __result)
                {
                    yield return thing;
                }
                yield break;
            }

            // Filter the generated things based on faction relationship
            foreach (Thing thing in __result)
            {
                if (TradingUtility.ShouldIncludeItemBasedOnRelationship(thing.def, faction))
                {
                    yield return thing;
                }
            }
        }
    }

    // Additional patch for TraderKindDef.WillTrade to ensure consistency
    [HarmonyPatch(typeof(TraderKindDef), "WillTrade")]
    public static class TraderKindDef_WillTrade_Patch
    {
        [HarmonyPostfix]
        public static bool Postfix(bool __result, ThingDef td, TraderKindDef __instance)
        {
            Faction faction = FactionUtility.DefaultFactionFrom(__instance.faction);

            if (!__result || faction == null || faction.IsPlayer)
                return __result;

            return TradingUtility.ShouldIncludeItemBasedOnRelationship(td, faction);
        }
    }
}
