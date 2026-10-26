using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    // Optional per-condition examination state. Ordinary mutations use existing visibility.
    public class HediffComp_MedicalReveal : HediffComp
    {
        public bool revealed;
        public override void CompExposeData() => Scribe_Values.Look(ref revealed, "medicallyRevealed", false);
    }
    public class HediffCompProperties_MedicalReveal : HediffCompProperties
    {
        public HediffCompProperties_MedicalReveal() { compClass = typeof(HediffComp_MedicalReveal); }
    }
}
