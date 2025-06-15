using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{ 
    public class HorrorChild
    {
        public PawnKindDef childKind;
        public float gestationFactor;
        public float probability;
        public float essenceMinimum = 0;
        public float essenceMaximum = float.MaxValue;
    }
    public class HediffComp_HorrorPregnant : HediffComp_SeverityModifierBase
    {
        HediffCompProperties_Comp_HorrorPregnant Props => props as HediffCompProperties_Comp_HorrorPregnant;
        PawnKindDef childKind;
        bool birthed = false;
        float gestationFactor = 1;
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Defs.Look(ref childKind, "childKind");
            Scribe_Values.Look(ref gestationFactor, "gestationFactor", defaultValue: 1);
            Scribe_Values.Look(ref birthed, "birthed", defaultValue: false);
        }

        public override bool CompShouldRemove => (base.CompShouldRemove || birthed);
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);

            SelectChildKind();

            if(Pawn.health.hediffSet.HasPregnancyHediff())
            {
                Hediff pregnancy = Pawn.health.hediffSet.GetFirstHediff<Hediff_Pregnant>();

                if (pregnancy != null)
                {
                    Pawn.health.RemoveHediff(pregnancy);
                }
            }

        }

        protected float GetMotherEssence()
        {
            return BioUtility.GetXenomorphInfluence(Pawn);
        }

        protected void SelectChildKind()
        {
            float essence = GetMotherEssence();
            foreach(HorrorChild baby in Props.babies)
            {
                if(essence >= baby.essenceMinimum && essence <= baby.essenceMaximum)
                {
                    if(Rand.Chance(baby.probability))
                    {
                        childKind = baby.childKind;
                        gestationFactor = baby.gestationFactor;
                        break;
                    }
                }
            }
        }
        public override float SeverityChangePerDay()
        {
            return 1 / (Pawn.RaceProps.gestationPeriodDays * gestationFactor);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if(parent.Severity >= 1)
            {
                BirthChild();
            }
        }

        private void BirthChild()
        {
            if(birthed)
            {
                return;
            }
            if(childKind == null)
            {
                Log.Error("No Child Kind when birthing.");
                return;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(childKind, Pawn.Faction);
            request.FixedBiologicalAge = 0;
            Pawn child = PawnGenerator.GeneratePawn(request);

            if (child != null)
            {
                if(child.relations != null)
                {
                    child.relations.AddDirectRelation(PawnRelationDefOf.ParentBirth, Pawn);
                }

                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Parasite_Birth, Pawn.Named(HistoryEventArgsNames.Doer), child.Named(HistoryEventArgsNames.Victim)), true);
                
                CompLarvalGenes larvalGenes = child.GetComp<CompLarvalGenes>();

                if (larvalGenes != null)
                {
                    larvalGenes.mother = Pawn;

                    BioUtility.ExtractGenesToGeneset(ref larvalGenes.genes, BioUtility.GetExtraHostGenes(Pawn));
                    if (Pawn.genes != null)
                    {
                        BioUtility.ExtractGenesToGeneset(ref larvalGenes.genes, Pawn.genes.GenesListForReading);
                    }
                }

                XMTUtility.TrySpawnPawnFromTarget(child, Pawn);
            }
            birthed = true;
        }
    }

    public class HediffCompProperties_Comp_HorrorPregnant : HediffCompProperties
    {
        public List<HorrorChild> babies;
        public HediffCompProperties_Comp_HorrorPregnant()
        {
            compClass = typeof(HediffComp_HorrorPregnant);
        }
    }
}
