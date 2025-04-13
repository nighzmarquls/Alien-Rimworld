using System.Collections.Generic;
using System.Linq;
using System.Text;
//using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using static HarmonyLib.Code;

namespace Xenomorphtype
{

    public class Ovamorph : XMTBase_Building
    {
        public int TimeLaid;

        float gestateProgress = 0f;

        private CompHatchingEgg HatchingEgg;
        public bool CanFire => (HatchingEgg == null)? false : HatchingEgg.UnHatched;

        public bool Ready => gestateProgress >= 1f;

        List<string> tmpGeneLabelsDesc = new List<string>();

        public void ForceProgress(float progress = 1.0f)
        {
            gestateProgress = progress;
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            HatchingEgg = this.GetComp<CompHatchingEgg>();

            if (HatchingEgg != null && CanFire)
            {
                HiveUtility.AddOvamorph(this, map);
            }
            
            if(XMTUtility.PlayerXenosOnMap(map))
            {
                SetFaction(Find.FactionManager.OfPlayer);
            }
            else
            {
                SetFaction(null);
            }
        }

        public void LayEgg(Pawn mother, Pawn father = null)
        {
            if (HatchingEgg.mother == null)
            {
                SetFaction(mother.Faction);
                SetParents(mother, father);
                HatchingEgg.UnHatched = true;
            }
        }

        public void SetParents(Pawn mother, Pawn father = null)
        {
            //Log.Message("SetParents: mother:" + mother + "father: " + father);
            //("No HatchingEgg Comp Defined on SetParents for Ovamorph");
            
            if (HatchingEgg == null)
            {
                Log.Warning("No HatchingEgg Comp Defined on SetParents for Ovamorph");
                return;
            }
            if (HatchingEgg.Props == null)
            {
                Log.Warning("No HatchingEgg Props Defined on SetParents for Ovamorph");
                return;
            }

            HatchingEgg.mother = mother;
            HatchingEgg.father = father == null ? HatchingEgg.father : father;
            HatchingEgg.genes = new GeneSet();

            //Log.Message("Geneset: " + HatchingEgg.genes);

            if (mother != null && mother.genes != null)
            {
                //Log.Message("Mother ExtractGenes");
                BioUtility.ExtractGenesToGeneset(ref HatchingEgg.genes, mother.genes.GenesListForReading);
            }

            if (father != null && father != mother)
            {
                BioUtility.ExtractGenesToGeneset(ref HatchingEgg.genes, BioUtility.GetExtraHostGenes(father));
                if (father.genes != null)
                {
                    //Log.Message("Father ExtractGenes");
                    BioUtility.ExtractGenesToGeneset(ref HatchingEgg.genes, father.genes.GenesListForReading);
                }
            }

        }
        private Graphic _emptyGraphic;
        public override Graphic Graphic
        {
            get
            {
                if (CanFire)
                {
                    return base.Graphic;
                }
                else
                {
                    if (_emptyGraphic is null)
                    {
                        var data = new GraphicData();
                        data.CopyFrom(def.graphicData);
                        data.texPath += "_Empty";
                        _emptyGraphic = data.GraphicColoredFor(this);
                    }
                    return _emptyGraphic;
                }
            }
        }

        public override void Tick()
        {
            base.Tick();
            if(HatchingEgg == null)
            {
                Log.WarningOnce("No HatchingEgg Comp Defined",098765678);
                return;
            }
            if(HatchingEgg.Props == null)
            {
                Log.WarningOnce("No HatchingEgg Props Defined",098765678);
                return;
            }

            if (CanFire && Spawned)
            {
                if (gestateProgress >= 1f)
                {
                    IEnumerable<Pawn> PossibleHosts = GenRadial.RadialDistinctThingsAround(Position, Map, 1.5f, true).OfType<Pawn>()
                        .Where(x => XMTUtility.IsHost(x));

                    foreach (Pawn host in PossibleHosts)
                    {

                        if (Rand.Chance(SpringChance(host)))
                        {
                            HatchNow();
                            break;
                        }
                    }
                }
                else
                {
                    if (Map.terrainGrid.TerrainAt(Position).affordances.Contains(InternalDefOf.Resin))
                    {
                        gestateProgress += 1f / (XMTSettings.LaidEggMaturationTime * 60000f);
                    }
                }
            }
        }

        protected virtual float SpringChance(Pawn p)
        {
            float num = 1f;
            if (p.kindDef.immuneToTraps)
            {
                return 0f;
            }

            num *= this.GetStatValue(StatDefOf.TrapSpringChance) * p.GetStatValue(StatDefOf.PawnTrapSpringChance);
            return Mathf.Clamp01(num);

            
        }
        public void HatchNow()
        {
            if(!CanFire)
            {
                return;
            }

            HatchingEgg.UnHatched = false;
            if (HatchingEgg.hatchedPawnKind == null)
            {
                Log.Warning("No Hatched Pawn Defined in Ovamorph");
                return;
            }

            if (HatchingEgg.mother != null && HatchingEgg.mother.Faction.IsPlayer)
            {
                Log.Message(HatchingEgg.mother + " has had her ovamorph hatch!");
            }

            HiveUtility.RemoveOvamorph(this, Map);

            XMTUtility.WitnessOvamorph(Position, Map, 0.1f, 0.1f);

            HatchingEgg.TrySpawnPawn(Position, HatchingEgg.hatchedPawnKind.race.race.lifeStageAges[0].minAge);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {

            HatchNow();

            base.Destroy(mode);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref TimeLaid, "timeLaidTick");
            Scribe_Values.Look(ref gestateProgress, "gestateProgress");
        }

        public override string GetInspectString()
        {
            string description = base.GetInspectString(); ;
            string text = description;

            if (gestateProgress < 1)
            {
                if (XMTUtility.PlayerXenosOnMap(Map))
                {
                    text += "EggProgress".Translate() + ": " + gestateProgress.ToStringPercent() + "\n" + "HatchesIn".Translate() + ": " + "PeriodDays".Translate((XMTSettings.LaidEggMaturationTime * (1f - gestateProgress)).ToString("F1")) ;
                    if (!Map.terrainGrid.TerrainAt(Position).affordances.Contains(InternalDefOf.Resin))
                    {
                        text += "\n" + "cannot mature off resin.";
                    }
                }
            }
            
            
            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneControl))
            {
                return text;
            }

           
            if (HatchingEgg.genes == null || !HatchingEgg.genes.GenesListForReading.Any())
            {
                return text;
            }

           
            if (!text.NullOrEmpty())
            {
                text += "\n\n";
            }


            tmpGeneLabelsDesc.Clear();

            for (int i = 0; i < HatchingEgg.genes.GenesListForReading.Count; i++)
            {
                tmpGeneLabelsDesc.Add(HatchingEgg.genes.GenesListForReading[i].label);
            }

            return text + ("Genes".Translate().CapitalizeFirst() + ":\n" + tmpGeneLabelsDesc.ToLineList("  - ", capitalizeItems: true));
        }
  
        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            if(instigator == null)
            {
                SetParents(HatchingEgg.mother, pawn);
            }
            else
            {
                SetFaction(instigator.Faction);
                SetParents(instigator, pawn);
            }
            gestateProgress = 1;

            if (pawn.BodySize > 1)
            {
                int remainingBody = Mathf.FloorToInt(pawn.BodySize - 1);

                if (remainingBody > 0)
                {
                    //List<Ovamorph> list = new List<Ovamorph>();

                    IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Position, remainingBody, false);

                    foreach(IntVec3 cell in cells)
                    {
                        if (remainingBody <= 0)
                        {
                            break;
                        }
                        Ovamorph egg = GenSpawn.Spawn(def, cell, Map, WipeMode.VanishOrMoveAside) as Ovamorph;
                        egg.gestateProgress = 1;

                        if (instigator == null)
                        {
                            egg.SetParents(HatchingEgg.mother, pawn);
                        }
                        else
                        {
                            egg.SetFaction(instigator.Faction);
                            egg.SetParents(instigator, pawn);
                        }
                        remainingBody -= 1;
                    }
                }
            }
        }
    }
}