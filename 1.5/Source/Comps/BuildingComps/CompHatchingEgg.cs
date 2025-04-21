using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    
    public class CompHatchingEgg : CompHiveGeneHolder
    {
        public CompHatchingEggProperties Props => (CompHatchingEggProperties)this.props;

        public Pawn mother;
        public Pawn father;

        public bool UnHatched = true;
        public PawnKindDef hatchedPawnKind => Props.hatchedPawnKind;

        public void TrySpawnPawn(IntVec3 position, float age)
        {
            TrySpawnPawn(position,age,parent.Map);
        }
        public void TrySpawnPawn(IntVec3 position, float age, Map map)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(hatchedPawnKind, parent.Faction);
            request.FixedBiologicalAge = age;
            Pawn pawn = PawnGenerator.GeneratePawn(request);

            CompLarvalGenes larvalGenes = pawn.GetComp<CompLarvalGenes>();

            if (larvalGenes != null)
            {
                if (mother != null)
                {
                    larvalGenes.mother = mother;
                    larvalGenes.father = father;
                    larvalGenes.genes = genes;
                }
            }
            pawn = GenSpawn.Spawn(pawn, position, map) as Pawn;

            if (pawn != null)
            {
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Ovamorph_Hatched, pawn.Named(HistoryEventArgsNames.Doer)));
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);

            if (UnHatched)
            {
                if (dinfo != null)
                {
                    Thing instigator = dinfo.Value.Instigator;
                    if (instigator != null)
                    {
                        Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Ovamorph_Destroyed, instigator.Named(HistoryEventArgsNames.Doer), parent.Named(HistoryEventArgsNames.Victim)), true);
                    }
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref mother, "mother");
            Scribe_References.Look(ref father, "father");

            Scribe_Values.Look(ref UnHatched, "UnHatched", defaultValue: true);
        }

        public override void Notify_AbandonedAtTile(int tile)
        {
            base.Notify_AbandonedAtTile(tile);
            XenoformingUtility.HandleXenoformingImpact(parent);
        }
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
        {
            if (!UnHatched)
            {
                yield break;
            }

            if (parent.Faction == null || !parent.Faction.IsPlayer)
            {
                yield break;
            }

            if (myPawn != null && !XMTUtility.IsXenomorph(myPawn))
            {
                yield break;
            }
            Ovamorph ovamorph = parent as Ovamorph;
            if (ovamorph != null)
            {
                if (ovamorph.CanFire)
                {
                    bool ready = ovamorph.Ready;
                    FloatMenuOption HatchOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Hatch", delegate
                    {
                        if (ready)
                        {
                            ovamorph.HatchNow();
                        }
                    }, priority: MenuOptionPriority.Default), myPawn, parent);

                    if (!ready)
                    {
                        HatchOption.Disabled = true;
                        HatchOption.tooltip = "cannot hatch, still gestating.";
                    }
                    yield return HatchOption;
                }
            }
        }
    }

    public class CompHatchingEggProperties : CompProperties
    {
        public PawnKindDef hatchedPawnKind;

        public CompHatchingEggProperties()
        {
            this.compClass = typeof(CompHatchingEgg);
        }

        public CompHatchingEggProperties(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }


}
