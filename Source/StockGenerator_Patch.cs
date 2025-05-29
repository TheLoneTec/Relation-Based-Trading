using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Noise;

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
            int goodwill = faction.GoodwillWith(Faction.OfPlayer);
            KeyValuePair<TechLevel, RangeInt> pair = TradingUtility.scale.FirstOrFallback(tech => tech.Value.start <= goodwill && goodwill <= tech.Value.length, TradingUtility.scale.First());

            //Log.Message($"Calling postfix for {__instance.GetType().FullName}:Postfix({forTile},{faction}) - Expected up to:{pair.Key}");
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
            //Log.Message("Items Accepted: " + TradingUtility.itemsAccepted + ". Items Rejected: " + TradingUtility.itemsRejected);
            //TradingUtility.itemsAccepted = 0;
            //TradingUtility.itemsRejected = 0;
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

    // Additional patch for Faction.TryAffectGoodwillWith
    [HarmonyPatch(typeof(Faction), "TryAffectGoodwillWith")]
    public static class Faction_TryAffectGoodwillWith_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Faction other,
      int goodwillChange)
        {
            //Log.Message("Faction_TryAffectGoodwillWith_Patch Entered");

            int goodwill = other.GoodwillWith(Faction.OfPlayer);

            KeyValuePair<TechLevel, RangeInt> before = TradingUtility.scale.FirstOrFallback(
                tech => tech.Value.start <= goodwill - goodwillChange && goodwill - goodwillChange <= tech.Value.end,
                TradingUtility.scale.First());

            KeyValuePair<TechLevel, RangeInt> after = TradingUtility.scale.FirstOrFallback(
                tech => tech.Value.start <= goodwill && goodwill <= tech.Value.end,
                TradingUtility.scale.First());

            //Log.Message("TechLevel allowed changed from: " + before.Key.ToString() + " to " + after.Key.ToString());

            if (before.Key != after.Key)
            {
                //Log.Message("Relation changed enough to be a different tech level");
                foreach (Settlement settlement in Find.WorldObjects.Settlements.Where(s => s.Faction == other))
                {
                    //Log.Message("Clearing Stock for : " + settlement.Name);
                    settlement.trader.TryDestroyStock();
                }
            }

        }
    }
}
