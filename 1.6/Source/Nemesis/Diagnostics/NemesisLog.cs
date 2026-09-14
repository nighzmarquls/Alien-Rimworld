using Verse;

namespace Xenomorphtype
{
    internal static class NemesisLog
    {
        internal static void Detail(string context, string message)
        {
            if (XMTSettings.LogNemesis) Log.Message("[XMT][Nemesis][" + context + "] " + message);
        }
    }
}
