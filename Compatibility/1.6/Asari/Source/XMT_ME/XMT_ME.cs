
using HarmonyLib;
using Verse;

namespace XMT_ME
{
    [StaticConstructorOnStartup]
    public class XMT_ME
    {
        static XMT_ME()
        {
            Harmony harmony = new Harmony("XMT.XMTModME");
            harmony.PatchAll();
            Log.Message("[Alien|Rimworld] Mass Effect Renegade core harmony Patching ");
        }
    }
}
