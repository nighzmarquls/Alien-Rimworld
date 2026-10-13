
using HarmonyLib;
using Verse;


namespace XMT_CE
{
    [StaticConstructorOnStartup]
    public static class XMT_CE
    {
        static XMT_CE()
        {
            Harmony harmony = new Harmony("XMT.XMTModCE");
            harmony.PatchAll();
            Xenomorphtype.PenetrationUtility.Resolver = AttachmentPenetrationCE.Resolve;
            Log.Message("[Alien|Rimworld] combat extended harmony Patching ");
        }

      
    }

}
