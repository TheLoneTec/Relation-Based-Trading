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
                if (TradingUtility.ShouldIncludeItemBasedOnRelationship(thing, faction))
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

            // Skip if it's a no-research item (always available)
            if (TradingUtility.NoResearchItems.Contains(td))
                return true;

            // Get the tech level of the item
            if (!TradingUtility.ThingTechLevels.TryGetValue(td, out TechLevel techLevel))
            {
                // If not in cache, allow it by default
                return __result;
            }

            // Get faction goodwill
            int goodwill = faction.GoodwillWith(Faction.OfPlayer);

            // Very poor relations (-75 to -25)
            if (goodwill < -25)
            {
                // Only items with no research requirements
                return false;
            }

            // Poor relations (-25 to 5)
            if (goodwill < 5)
            {
                // Neolithic tech
                return techLevel <= TechLevel.Neolithic;
            }

            // Neutral relations (5 to 25)
            if (goodwill < 25)
            {
                // Medieval tech and below
                return techLevel <= TechLevel.Medieval;
            }

            // Good relations (25 to 50)
            if (goodwill < 50)
            {
                // Industrial tech and below
                return techLevel <= TechLevel.Industrial;
            }

            // Very good relations (50 to 75)
            if (goodwill < 75)
            {
                // Spacer tech and below
                return techLevel <= TechLevel.Spacer;
            }

            // Excellent relations (75+)
            // Ultra tech and below
            return techLevel <= TechLevel.Ultra;
        }
    }
}
