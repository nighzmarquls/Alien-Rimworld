using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PipeSystem;
using RimWorld;
using Verse;
using Xenomorphtype;

namespace XMT_VTE
{

    // The public VTE release still exposes the 1.6 API. Keep its names and signatures here.
    public static class VTEApiShim
    {
        public static Type HeaterType =>
            AccessTools.TypeByName("VanillaTemperatureExpanded.Buildings.Building_HeaterWithOffset");
        public static MethodInfo PushHeat => HeaterType == null ? null :
            AccessTools.Method(HeaterType, "PushHeat", new[] { typeof(IntVec3), typeof(float?) });
        public static bool Ready => PushHeat?.ReturnType == typeof(bool);
    }
}
