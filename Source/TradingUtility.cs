using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RelationBasedTrading
{
    [StaticConstructorOnStartup]
    public static class TradingUtility
    {
        public static readonly Dictionary<ThingDef, TechLevel> ThingTechLevels = new Dictionary<ThingDef, TechLevel>();

        // Cache for items with no research requirements
        public static readonly HashSet<ThingDef> NoResearchItems = new HashSet<ThingDef>();

        public static bool HSKActive = false;

        public static int techLevelFound;
        public static int researchPrerequisitesFound;
        public static int RecipeFound;
        public static int WeaponApparelFound;
        public static int itemsRejected;
        public static int itemsAccepted;

        public static Dictionary<TechLevel, RangeInt> scale = new Dictionary<TechLevel, RangeInt>()
        {
            { TechLevel.Undefined, new RangeInt(-75,-24) },
            { TechLevel.Neolithic, new RangeInt(-25,4) },
            { TechLevel.Medieval, new RangeInt(5,34) },
            { TechLevel.Industrial, new RangeInt(35,54) },
            { TechLevel.Spacer, new RangeInt(55,74) },
            { TechLevel.Ultra, new RangeInt(75,1000) }
        };

        static TradingUtility()
        {
            //CacheTechLevels();
            //Log.Message("[Relation Based Trading] Initialized and patched trader stock generation.");
            /*
            foreach (var item in scale)
            {
                Log.Message($"[Relation Based Trading] : TechLevel - {item.Key} : {item.Value.start} - {item.Value.length}");
            }
            */
        }

        public static void CacheTechLevels()
        {
            // Get all thing defs
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (thingDef.tradeability == Tradeability.None)
                    continue;

                // Determine tech level from research prerequisites
                TechLevel techLevel = DetermineTechLevelFromResearch(thingDef);

                // Store in cache
                ThingTechLevels[thingDef] = techLevel;

                // Check if it has no research requirements
                if (techLevel == TechLevel.Undefined /*HasNoResearchRequirements(thingDef)*/)
                {
                    NoResearchItems.Add(thingDef);
                }
            }

            Log.Message($"[Relation Based Trading] Cached tech levels for {ThingTechLevels.Count} tradeable items.");
            Log.Message($"[Relation Based Trading] Identified {NoResearchItems.Count} items with no research requirements.");
            Log.Message($"[Relation Based Trading] Identified {techLevelFound} items with techLevel. {researchPrerequisitesFound} items with research. {RecipeFound} items with recipes. {WeaponApparelFound} Weapons or Apparel.");
        }
        /*
        private static bool HasNoResearchRequirements(ThingDef thingDef)
        {
            // Check if it's a natural resource, plant, or animal
            if (thingDef.race != null ||
                thingDef.IsStuff ||
                thingDef.IsPlant ||
                thingDef.IsRawFood() ||
                thingDef.IsIngestible && thingDef.ingestible.IsMeal)
                return true;

            // Check direct research prerequisites
            if (thingDef.researchPrerequisites.NullOrEmpty())
            {
                // Check recipes that make this thing
                List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefs
                    .Where(r => r.products != null && r.products.Any(p => p.thingDef == thingDef))
                    .ToList();

                if (recipes.Any(r => r.researchPrerequisite != null))
                    return false;

                return true;
            }

            return false;
        }
        */
        private static TechLevel DetermineTechLevelFromResearch(ThingDef thingDef)
        {
            //Exception for silver
            if (thingDef == ThingDefOf.Silver)
                return TechLevel.Undefined;

            if (thingDef.deepCommonality > 0)
            {
                switch (thingDef.deepCommonality)
                {
                    case float i when i >= 1.5 : return TechLevel.Undefined;
                    case float i when i >= 1 : return TechLevel.Medieval;
                    case float i when i >= 0.5: return TechLevel.Industrial;
                    case float i when i >= 0: return TechLevel.Spacer;
                }
            }

            // Default to Industrial if we can't determine
            TechLevel result = TechLevel.Industrial;

            // First check if it has a valid tech level already
            if (thingDef.techLevel != TechLevel.Undefined && thingDef.techLevel != TechLevel.Animal)
            {
                techLevelFound++;
                return thingDef.techLevel;
            }

            // Check if it has a research prerequisite directly
            if (thingDef.researchPrerequisites != null && thingDef.researchPrerequisites.Any())
            {
                researchPrerequisitesFound++;
                return thingDef.researchPrerequisites.Max(r => r.techLevel);
            } 
            if (thingDef.recipeMaker != null)
            {
                if (thingDef.recipeMaker.researchPrerequisite != null)
                {
                    return thingDef.recipeMaker.researchPrerequisite.techLevel;
                }
                else if (!thingDef.recipeMaker.researchPrerequisites.NullOrEmpty())
                {
                    return thingDef.recipeMaker.researchPrerequisites.Max(r => r.techLevel);
                }
            }

            // Check recipes that make this thing
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.products != null && r.products.Any(p => p.thingDef == thingDef))
                .ToList();

            if (recipes.Any())
            {
                // Get the highest tech level from recipes that make this item
                foreach (RecipeDef recipe in recipes)
                {
                    if (recipe.researchPrerequisite != null)
                    {
                        TechLevel recipeTechLevel = recipe.researchPrerequisite.techLevel;
                        if (recipeTechLevel > result)
                            result = recipeTechLevel;
                    }
                    else if (!recipe.researchPrerequisites.NullOrEmpty())
                    {
                        TechLevel recipeTechLevel = recipe.researchPrerequisites.Max(r => r.techLevel);
                        if (recipeTechLevel > result)
                            result = recipeTechLevel;
                    }
                }
                RecipeFound++;
                return result;
            }

            // If we still don't have a tech level, make an educated guess
            if (thingDef.IsWeapon || thingDef.IsApparel)
            {
                // Try to guess based on stuff categories or material
                if (thingDef.stuffCategories != null)
                {
                    if (HSKActive && thingDef.stuffCategories.Any(t => t == StuffCategoryDefOfLocal.RareMetallic || t == StuffCategoryDefOfLocal.Precious || t == StuffCategoryDefOfLocal.HF))
                    {
                        WeaponApparelFound++;
                        return TechLevel.Spacer;
                    }
                    if (thingDef.stuffCategories.Any(t => t == StuffCategoryDefOf.Metallic || (HSKActive && t == StuffCategoryDefOfLocal.SolidMetallic) || (HSKActive && t == StuffCategoryDefOfLocal.HeavyMetallic)))
                    {
                        WeaponApparelFound++;
                        return TechLevel.Industrial;
                    }
                    else if (thingDef.stuffCategories.Any(t => t == StuffCategoryDefOf.Fabric || t == StuffCategoryDefOf.Leathery || (HSKActive && t == StuffCategoryDefOfLocal.StrongMetallic) || (HSKActive && t == StuffCategoryDefOfLocal.RuggedMetallic)))
                    {
                        WeaponApparelFound++;
                        return TechLevel.Medieval;
                    }
                    else if (thingDef.stuffCategories.Any(t => t == StuffCategoryDefOf.Woody || t == StuffCategoryDefOf.Stony || (HSKActive && t == StuffCategoryDefOfLocal.WoodLogs)))
                    {
                        WeaponApparelFound++;
                        return TechLevel.Neolithic;
                    }
                }
                
            }

            // Natural resources, plants, animals, etc. are considered no research
            if (thingDef.race != null ||
                thingDef.IsStuff ||
                thingDef.IsPlant ||
                thingDef.IsRawFood() ||
                (thingDef.IsIngestible && thingDef.ingestible.IsMeal))
            {
                return TechLevel.Undefined; // Special case for no research items
            }

            return result;
        }

        public static bool ShouldIncludeItemBasedOnRelationship(ThingDef def, Faction faction)
        {
            if (faction == null || faction.IsPlayer)
                return true;

            // Get faction goodwill
            int goodwill = faction.GoodwillWith(Faction.OfPlayer);

            // Hostile factions - can't trade anyway
            if (goodwill < -75)
                return false;

            // Check if it's a no-research item (always available)
            if (NoResearchItems.Contains(def))
                return true;

            // Get the tech level of the item
            if (!ThingTechLevels.TryGetValue(def, out TechLevel techLevel))
            {
                // If not in cache, allow it by default
                return true;
            }

            KeyValuePair<TechLevel, RangeInt> pair = scale.FirstOrFallback(tech => tech.Value.start <= goodwill && goodwill <= tech.Value.length,scale.First());

            if (techLevel <= pair.Key)
            {
                //if (debug)
                //    TradingUtility.itemsAccepted++;
                return true;
            }
            //if (debug)
            //    TradingUtility.itemsRejected++;
            return false;
            /*
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

            // Neutral relations (5 to 35)
            if (goodwill < 35)
            {
                // Medieval tech and below
                return techLevel <= TechLevel.Medieval;
            }

            // Good relations (35 to 55)
            if (goodwill < 55)
            {
                // Industrial tech and below
                return techLevel <= TechLevel.Industrial;
            }

            // Very good relations (55 to 75)
            if (goodwill < 75)
            {
                // Spacer tech and below
                return techLevel <= TechLevel.Spacer;
            }

            // Excellent relations (75+)
            // Ultra tech and below
            return techLevel <= TechLevel.Ultra;
            */
        }
    }

    [DefOf]
    public static class StuffCategoryDefOfLocal
    {
        [MayRequireAnyOf("skyarkhangel.HSK")]
        public static StuffCategoryDef RareMetallic;
        [MayRequireAnyOf("skyarkhangel.HSK")]
        public static StuffCategoryDef SolidMetallic;
        [MayRequireAnyOf("skyarkhangel.HSK")]
        public static StuffCategoryDef HeavyMetallic;
        [MayRequireAnyOf("skyarkhangel.HSK")]
        public static StuffCategoryDef StrongMetallic;
        [MayRequireAnyOf("skyarkhangel.HSK")]
        public static StuffCategoryDef RuggedMetallic;
        [MayRequireAnyOf("skyarkhangel.HSK")]
        public static StuffCategoryDef Precious;
        [MayRequireAnyOf("skyarkhangel.HSK")]
        public static StuffCategoryDef HF;
        [MayRequireAnyOf("skyarkhangel.HSK")]
        public static StuffCategoryDef WoodLogs;

        static StuffCategoryDefOfLocal()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(StuffCategoryDefOfLocal));
        }
    }
}
