using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class CompPetrolsump : CompOrganicGrowth
    {
        private bool HeatFedThisHour;
        private CompPetrolsumpProperties Props => (CompPetrolsumpProperties)props;
        public int SporeMinHP => Props.HitpointsPerSpore;
        protected override ThingDef FeedingFilth => ThingDefOf.Filth_Fuel;
        protected override void FeedSpecial()
        {
            if (HeatFedThisHour) ApplyGrowth(0.1f);
            HeatFedThisHour = false;
        }
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (dinfo.Def == DamageDefOf.Burn || dinfo.Def == DamageDefOf.Bomb)
            {
                dinfo.SetAmount(0);
                absorbed = true;
                HeatFedThisHour = true;
                return;
            }

            if (parent.HitPoints > Props.HitpointsPerSpore)
            {
                if (dinfo.Amount < Props.HitpointsPerSpore)
                {
                    dinfo.SetAmount(Props.HitpointsPerSpore);
                }

                ReleaseSpore(parent.Position, parent.Map);
            }
            base.PostPreApplyDamage(ref dinfo, out absorbed);
        }
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            if (!parent.Spawned || parent.Destroyed) return;
            List<IntVec3> cells = GenRadial.RadialCellsAround(parent.Position, 1.5f, false).Where(cell => cell.InBounds(parent.Map)).ToList();
            cells.Shuffle();
            
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            DropFuel(Mathf.RoundToInt(totalDamageDealt / 2));

            

            int fireCount = 0;
            foreach (IntVec3 cell in cells)
            {
                FireUtility.TryStartFireIn(cell, parent.Map, 0.5f, parent);
                fireCount++;
                if(fireCount >= Props.maxFires)
                {
                    break;
                }
            }
        }

        public void DropFuel(int stackTotal)
        {
            DropFuel(stackTotal, parent.Map);
        }
        public void DropFuel(int stackTotal, Map currentmap)
        {
            ThingDef fuelDef = ThingDefOf.Chemfuel;

            XMTUtility.DropAmountThing(fuelDef, stackTotal, parent.Position, currentmap, ThingDefOf.Filth_Fuel);

            
        }

        public void ReleaseSpore(IntVec3 cell,  Map targetMap)
        {            
            if (targetMap == null || !cell.InBounds(targetMap) || Props.sporeKindDef == null) return;
            IEnumerable<Thing> spores = targetMap.listerThings.ThingsOfDef(Props.sporeKindDef.race);

            if(spores.Count() >= Props.maxSporeCount)
            {
                return;
            }
            PawnGenerationRequest request = new PawnGenerationRequest(Props.sporeKindDef, parent.Faction);
            request.FixedBiologicalAge = 0;
            Pawn spore = PawnGenerator.GeneratePawn(request);

            GenSpawn.Spawn(spore, cell, targetMap);
        }


    }
    public class CompPetrolsumpProperties : CompProperties_OrganicGrowth
    {
        public int maxFires = 3;
        public float minTemperature = 20f;
        public int HitpointsPerSpore = 50;
        public PawnKindDef sporeKindDef;
        public int maxSporeCount = 16;
        public float maxSizeChange = 0.01f;
        public int breathticks = 120;
        public CompPetrolsumpProperties() : base(typeof(CompPetrolsump)) { }
        public CompPetrolsumpProperties(Type type) : base(type) { }
    }
}