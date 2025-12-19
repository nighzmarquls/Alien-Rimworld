//using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{

    public class Ovomorph : XMTBase_Building
    {
        public int TimeLaid;

        int tempCheckTick = 0;

        float gestateProgress = 0f;

        bool accelerated = false;

        private CompHatchingEgg HatchingEgg;
        public bool Unhatched => (HatchingEgg == null)? true : HatchingEgg.UnHatched;

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

            if (HatchingEgg != null && Unhatched)
            {
                XMTHiveUtility.AddOvomorph(this, map);
            }
        }

        public void LayEgg(Pawn mother, Pawn father = null)
        {
            if (HatchingEgg.mother == null && Unhatched)
            {
                SetFaction(mother.Faction);
                SetParents(mother, father);
                StatDefOf.MarketValue.Worker.ClearCacheForThing(this);
            }
        }

        public void SetParents(Pawn mother, Pawn father = null)
        {
            //Log.Message("SetParents: mother:" + mother + "father: " + father);
            //("No HatchingEgg Comp Defined on SetParents for Ovomorph");
            
            if (HatchingEgg == null)
            {
                Log.Warning("No HatchingEgg Comp Defined on SetParents for Ovomorph");
                return;
            }
            if (HatchingEgg.Props == null)
            {
                Log.Warning("No HatchingEgg Props Defined on SetParents for Ovomorph");
                return;
            }

            HatchingEgg.mother = mother;
            HatchingEgg.father = father == null ? HatchingEgg.father : father;
            HatchingEgg.genes = new GeneSet();

            //Log.Message("Geneset: " + HatchingEgg.genes);

            if (mother != null && mother.genes != null)
            {
                //Log.Message("Mother ExtractGenes");
                BioUtility.ExtractCryptimorphGenesToGeneset(ref HatchingEgg.genes, mother.genes.GenesListForReading);
            }

            if (father != null && father != mother)
            {
                BioUtility.ExtractGenesToGeneset(ref HatchingEgg.genes, BioUtility.GetExtraHostGenes(father));
                if (father.genes != null)
                {
                    //Log.Message("Father ExtractGenes");
                    BioUtility.ExtractCryptimorphGenesToGeneset(ref HatchingEgg.genes, father.genes.GenesListForReading);
                }
            }

        }
        private Graphic _emptyGraphic;
        public override Graphic Graphic
        {
            get
            {
                if (Unhatched)
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

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if(Ready )
            {
                if (Unhatched && (XMTUtility.PlayerXenosOnMap(Map) || DebugSettings.ShowDevGizmos))
                {
                    Command_Action command_Action = new Command_Action();
                    command_Action.defaultLabel = "XMT_ForceHatch".Translate();
                    command_Action.icon = ContentFinder<Texture2D>.Get(base.Graphic.path + "_Empty"); ;
                    command_Action.action = delegate
                    {
                        HatchNow();
                    };
                    yield return command_Action;
                }
            }
            else if (DebugSettings.ShowDevGizmos)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "DEV: Force Maturity";
                command_Action.action = delegate
                {
                    gestateProgress = 1.0f;
                };
                yield return command_Action;
            }

            foreach(Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
        }
        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if(!Spawned)
            {
                return;
            }
      
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
            int ticks = Find.TickManager.TicksGame;
            if (ticks > tempCheckTick)
            {
                tempCheckTick = ticks += 2500;
                accelerated = Position.GetTemperature(Map) >= 30;
            }

            if (Unhatched)
            {
                if (gestateProgress >= 1f)
                {
                    IEnumerable<Pawn> PossibleHosts = GenRadial.RadialDistinctThingsAround(Position, Map, def.specialDisplayRadius, true).OfType<Pawn>()
                        .Where(x => XMTUtility.TriggersOvomorph(x));

                    foreach (Pawn host in PossibleHosts)
                    {
                        CompPawnInfo info = host.Info();
                        float bonusDodge = 0;
                        if (info != null)
                        {
                            bonusDodge += info.LarvaAwareness / 6;
                        }
                        if (Rand.Chance(SpringChance(host) - bonusDodge))
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
                        gestateProgress += (accelerated? 2f : 1f) / (XMTSettings.LaidEggMaturationTime * 60000f)*delta;
                    }
                    else
                    {
                        gestateProgress += (accelerated ? 0.2f : 0.1f) / (XMTSettings.LaidEggMaturationTime * 60000f)*delta;
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
            if(!Unhatched)
            {
                return;
            }

            HatchingEgg.UnHatched = false;
            StatDefOf.MarketValue.Worker.ClearCacheForThing(this);

            if (!Spawned)
            {
                return;
            }

            if (HatchingEgg.hatchedPawnKind == null)
            {
                Log.Warning("No Hatched Pawn Defined in Ovomorph");
                return;
            }

            if (HatchingEgg.mother != null && HatchingEgg.mother.Faction.IsPlayer)
            {
                Messages.Message("XMT_MotherEggHatch".Translate(HatchingEgg.mother.LabelShort), MessageTypeDefOf.PositiveEvent);
            }

            XMTHiveUtility.RemoveOvomorph(this, MapHeld);

            XMTUtility.WitnessOvomorph(PositionHeld, MapHeld, 0.1f, 0.1f);
            

            HatchingEgg.TrySpawnPawn(PositionHeld, HatchingEgg.hatchedPawnKind.race.race.lifeStageAges[0].minAge);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Spawned && Ready && Unhatched)
            {
                HatchNow();
            }

            base.Destroy(mode);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tempCheckTick, "tempCheckTick");
            Scribe_Values.Look(ref TimeLaid, "timeLaidTick");
            Scribe_Values.Look(ref gestateProgress, "gestateProgress");
            Scribe_Values.Look(ref accelerated, "accelerated");
        }

        public override string GetInspectString()
        {
            string description = base.GetInspectString(); ;
            string text = description;

            if (XMTUtility.PlayerXenosOnMap(MapHeld) || DebugSettings.ShowDevGizmos)
            {
                if (gestateProgress < 1)
                {

                    if (!Map.terrainGrid.TerrainAt(Position).affordances.Contains(InternalDefOf.Resin))
                    {
                        text += "EggProgress".Translate() + ": " + gestateProgress.ToStringPercent() + "\n" + "XMT_ReadyIn".Translate() + ": " + "PeriodDays".Translate((accelerated ? 5 : 10 * XMTSettings.LaidEggMaturationTime * (1f - gestateProgress)).ToString("F1")) + "\n";
                        text += "XMT_EggNeedResin".Translate();
                    }
                    else
                    {
                        text += "EggProgress".Translate() + ": " + gestateProgress.ToStringPercent() + "\n" + "XMT_ReadyIn".Translate() + ": " + "PeriodDays".Translate((accelerated ? 0.5 : 1 * XMTSettings.LaidEggMaturationTime * (1f - gestateProgress)).ToString("F1"));
                    }

                }
                else
                {
                    text += "XMT_Ready".Translate();
                }
            }
            
            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneControl))
            {
                return text;
            }

            if (!HatchingEgg.UnHatched || HatchingEgg.genes == null || !HatchingEgg.genes.GenesListForReading.Any())
            {
                return text;
            }

           
            if (!text.NullOrEmpty())
            {
                text += "\n";
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
                if (!instigator.IsSlave)
                {
                    SetFaction(instigator.Faction);
                }
                SetParents(instigator, pawn);
            }
            gestateProgress = 1;

            if (pawn.BodySize > 1)
            {
                int remainingBody = Mathf.FloorToInt(pawn.BodySize - 1);

                if (remainingBody > 0)
                {
                    //List<Ovomorph> list = new List<Ovomorph>();

                    IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(Position, remainingBody, false);

                    foreach(IntVec3 cell in cells)
                    {
                        if (remainingBody <= 0)
                        {
                            break;
                        }
                        Ovomorph egg = GenSpawn.Spawn(def, cell, Map, WipeMode.VanishOrMoveAside) as Ovomorph;
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