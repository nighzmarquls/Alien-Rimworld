
using HarmonyLib;
using Verse;


namespace XMT_CE
{
    [StaticConstructorOnStartup]
    public static class XMT_CE
    {
        static XMT_CE()
        {
            var harmony = new Harmony("XMT.XMTModCE");
            LongEventHandler.QueueLongEvent(delegate
            {
                harmony.PatchAll();
                Log.Message("[Alien|Rimworld] combat extended harmony Patching ");
            }, "XMTCE_LoadPatching", doAsynchronously: true, null);
        }

      
    }

}
