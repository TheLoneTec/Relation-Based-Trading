using HarmonyLib;
using Verse;

namespace RelationBasedTrading
{
    public class RelationBasedTradingMod : Mod
    {
        public RelationBasedTradingMod(ModContentPack content) : base(content)
        {
            if (ModLister.GetActiveModWithIdentifier("skyarkhangel.HSK") != null)
                TradingUtility.HSKActive = true;

            var harmony = new Harmony("thelonetec.relationbasedtrading");
            harmony.PatchAll();

            Log.Message("[Relation Based Trading] Initialized and patched trader stock generation.");

            LongEventHandler.QueueLongEvent(TradingUtility.CacheTechLevels, "CacheTechLevels", false, null);
        }

    }
}
