using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

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
            var pawn = PawnGenerator.GeneratePawn(request);

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

            GenSpawn.Spawn(pawn, position, map);

        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref mother, "mother");
            Scribe_References.Look(ref father, "father");

            Scribe_Values.Look(ref UnHatched, "UnHatched", defaultValue: true);
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

            if(myPawn != null && !XMTUtility.IsXenomorph(myPawn))
            {
                yield break;
            }

            FloatMenuOption ImplantOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Hatch", delegate
            {
                Ovamorph ovamorph = parent as Ovamorph;
                if(ovamorph != null)
                {
                    ovamorph.HatchNow();
                }
            }, priority: MenuOptionPriority.Default), myPawn, parent);

            yield return ImplantOption;
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
