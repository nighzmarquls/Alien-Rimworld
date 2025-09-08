
using UnityEngine;
using Verse;


namespace Xenomorphtype
{
    internal class XMTMod : Mod
    {
        public static XMTSettings Settings;

        public XMTMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<XMTSettings>();
        }

        public override string SettingsCategory()
        {
            return "Alien | Rimworld";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
            Settings.Write();
        }
    }
}
