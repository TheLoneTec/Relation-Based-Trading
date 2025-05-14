using HarmonyLib;
using Verse;

namespace RelationBasedTrading
{
    public class RelationBasedTradingMod : Mod
    {
        public RelationBasedTradingMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("thelonetec.relationbasedtrading");
            harmony.PatchAll();
        }
    }
}
