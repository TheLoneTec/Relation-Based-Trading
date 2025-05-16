using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RelationBasedTrading
{
    [StaticConstructorOnStartup]
    public static class TradingUtility
    {
        public static readonly Dictionary<ThingDef, TechLevel> ThingTechLevels = new Dictionary<ThingDef, TechLevel>();

        // Cache for items with no research requirements
        public static readonly HashSet<ThingDef> NoResearchItems = new HashSet<ThingDef>();

        static TradingUtility()
        {
            CacheTechLevels();
            Log.Message("[Relation Based Trading] Initialized and patched trader stock generation.");
        }

        private static void CacheTechLevels()
        {
            // Get all thing defs
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (thingDef.tradeability == Tradeability.None || thingDef.tradeability == Tradeability.None)
                    continue;

                // Determine tech level from research prerequisites
                TechLevel techLevel = DetermineTechLevelFromResearch(thingDef);

                // Store in cache
                ThingTechLevels[thingDef] = techLevel;

                // Check if it has no research requirements
                if (HasNoResearchRequirements(thingDef))
                {
                    NoResearchItems.Add(thingDef);
                }
            }

            Log.Message($"[Relation Based Trading] Cached tech levels for {ThingTechLevels.Count} tradeable items.");
            Log.Message($"[Relation Based Trading] Identified {NoResearchItems.Count} items with no research requirements.");
        }

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
            }

            return false;
        }

        private static TechLevel DetermineTechLevelFromResearch(ThingDef thingDef)
        {
            // Default to Industrial if we can't determine
            TechLevel result = TechLevel.Industrial;

            // First check if it has a valid tech level already
            if (thingDef.techLevel != TechLevel.Undefined && thingDef.techLevel != TechLevel.Animal)
            {
                return thingDef.techLevel;
            }

            // Check if it has a research prerequisite directly
            if (thingDef.researchPrerequisites != null && thingDef.researchPrerequisites.Any())
            {
                return thingDef.researchPrerequisites.Max(r => r.techLevel);
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
                }
                return result;
            }

            // If we still don't have a tech level, make an educated guess
            if (thingDef.IsWeapon || thingDef.IsApparel)
            {
                // Try to guess based on stuff categories or material
                if (thingDef.stuffCategories != null)
                {
                    if (thingDef.stuffCategories.Contains(StuffCategoryDefOf.Metallic))
                    {
                        return TechLevel.Industrial;
                    }
                    else if (thingDef.stuffCategories.Contains(StuffCategoryDefOf.Woody) ||
                             thingDef.stuffCategories.Contains(StuffCategoryDefOf.Stony))
                    {
                        return TechLevel.Medieval;
                    }
                }
            }

            // Natural resources, plants, animals, etc. are considered no research
            if (thingDef.race != null ||
                thingDef.IsStuff ||
                thingDef.IsPlant ||
                thingDef.IsRawFood() ||
                thingDef.IsIngestible && thingDef.ingestible.IsMeal)
            {
                return TechLevel.Undefined; // Special case for no research items
            }

            return result;
        }

        public static bool ShouldIncludeItemBasedOnRelationship(Thing thing, Faction faction)
        {
            if (faction == null || faction.IsPlayer)
                return true;

            // Get faction goodwill
            int goodwill = faction.GoodwillWith(Faction.OfPlayer);

            // Hostile factions - can't trade anyway
            if (goodwill < -75)
                return false;

            // Check if it's a no-research item (always available)
            if (NoResearchItems.Contains(thing.def))
                return true;

            // Get the tech level of the item
            if (!ThingTechLevels.TryGetValue(thing.def, out TechLevel techLevel))
            {
                // If not in cache, allow it by default
                return true;
            }

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
