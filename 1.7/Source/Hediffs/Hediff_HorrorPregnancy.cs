using RimWorld;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class Hediff_HorrorPregnancy : HediffWithParents
    {
        public override string LabelBase => MutationTissueAssessment.HealthLabel(this, base.LabelBase);
        public override string Description => MutationTissueAssessment.HealthDescription(this, base.Description);

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!HorrorPregnancyUtility.UseVanillaGeneInspection(pawn)) yield break;
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
        }
    }
}
