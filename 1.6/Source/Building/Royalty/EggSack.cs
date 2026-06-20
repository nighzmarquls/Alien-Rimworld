

using AlienRace;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class EggSack : HibernationCocoon, IThingHolderWithDrawnPawn
    {
        CompMatureMorph containedMorph = null;
        CompOvomorphLayer layer = null;
        CompRefuelable refuelable = null;
        public Pawn Occupant = null;
        bool empty = true;
        int attempts = 2;

        const int maxBacklog = 6;

        int currentBacklog = 0;

        int CheckInterval = 1000;

        [Unsaved(false)]
        private Graphic TopGraphic;

        public override bool CanOpen => false;

        public float HeldPawnDrawPos_Y => DrawPos.y - 0.03658537f;
        public float HeldPawnBodyAngle => Rotation.AsAngle;
        public PawnPosture HeldPawnPosture => PawnPosture.Standing;

        public override string Label
        {
            get
            {
                return Occupant?.Label ?? base.Label;
            }
        }

        public override string LabelNoCount
        {
            get
            {
                return Occupant?.LabelNoCount ?? base.LabelNoCount;
            }
        }

        public override string LabelCapNoCount
        {
            get
            {
                if(Occupant == null)
                {
                    return base.LabelCapNoCount;
                }

                return Occupant.LabelCapNoCount;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref currentBacklog, "currentBacklog", 0);
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad)
            {
                if (ContainedThing == null)
                {
                    Destroy();
                    return;
                }
                else
                {
                    RegisterOccupant(ContainedThing);
                }
            }
            refuelable = GetComp<CompRefuelable>();
        }
        protected void RegisterOccupant(Thing thing)
        {
            if (thing == null)
            {
                return;
            }

            containedMorph = thing.TryGetComp<CompMatureMorph>();
            layer = thing.TryGetComp<CompOvomorphLayer>();
            if (thing is Pawn pawn)
            {
                Occupant = pawn;
                empty = false;

                Occupant.health.GetOrAddHediff(InternalDefOf.XMT_Enthroned);

                CompStealth compStealth = Occupant.GetComp<CompStealth>();
                if (compStealth != null)
                {
                    compStealth.ForceVisible();
                }

                AlienPartGenerator.AlienComp.RegenerateAddonsForced(Occupant);
                Occupant.Drawer?.renderer?.SetAllGraphicsDirty();
                if (Spawned && Map != null)
                {
                    Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
                }

                XMTUtility.DeclareQueen(Occupant);
            }
        }

        private Pawn GetOccupant()
        {
            Pawn pawn = ContainedThing as Pawn ?? Occupant;
            if (pawn != null && pawn != Occupant)
            {
                RegisterOccupant(pawn);
            }

            return pawn;
        }

        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (base.TryAcceptThing(thing, allowSpecialEffects))
            {
                RegisterOccupant(thing);
                return true;
            }
            return false;
        }

        public bool TryEnthronePawn(Pawn pawn)
        {
            if (pawn == null || (ContainedThing != null && ContainedThing != pawn))
            {
                return false;
            }

            if (!pawn.Spawned && Spawned && Map != null)
            {
                IntVec3 warmupCell = InteractionCell;
                if (!warmupCell.IsValid || !warmupCell.InBounds(Map) || !warmupCell.Standable(Map))
                {
                    warmupCell = CellFinder.RandomClosewalkCellNear(Position, Map, 2);
                }

                GenSpawn.Spawn(pawn, warmupCell, Map, WipeMode.VanishOrMoveAside);
            }

            if (pawn.Spawned && !pawn.DeSpawnOrDeselect())
            {
                return false;
            }

            if (TryAcceptThing(pawn, allowSpecialEffects: false) && ContainedThing == pawn)
            {
                if (pawn.jobs.curJob != null)
                {
                    pawn.jobs.curJob.Clear();
                }
                return true;
            }

            return false;
        }

        private bool TryContainGeneratedQueen(Pawn queen)
        {
            if (queen == null || ContainedThing != null)
            {
                return false;
            }

            if (queen.Spawned && !queen.DeSpawnOrDeselect())
            {
                return false;
            }

            if (GetDirectlyHeldThings().TryAddOrTransfer(queen, canMergeWithExistingStacks: false))
            {
                if (queen.jobs.curJob != null)
                {
                    queen.jobs.curJob.Clear();
                }
                RegisterOccupant(queen);
                return true;
            }

            return false;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (Faction == null || !Faction.IsPlayer)
            {
                yield break;
            }

            Pawn occupant = GetOccupant();
            if (occupant?.Faction == Faction.OfPlayer)
            {
                CompOvomorphLayer occupantLayer = occupant.GetComp<CompOvomorphLayer>();
                if (occupantLayer != null)
                {
                    foreach (Gizmo gizmo in occupantLayer.GetThroneManagementGizmos(this))
                    {
                        yield return gizmo;
                    }
                }

                CompGeneManipulator geneManipulator = occupant.GetComp<CompGeneManipulator>();
                if (geneManipulator != null && XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_GeneSelfExpression))
                {
                    yield return geneManipulator.CreateSelfGeneExpressionAction();
                }
            }

            Command_Action command_Action = new Command_Action();
            command_Action.action = Open;
            command_Action.defaultLabel = "CommandPodEject".Translate();
            command_Action.defaultDesc = "CommandPodEjectDesc".Translate();

            command_Action.hotKey = KeyBindingDefOf.Misc8;
            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Abilities/Starbeast_Leap");
            yield return command_Action;
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();

            if (Faction == null || !Faction.IsPlayer)
            {
                return text;
            }

            if (!text.NullOrEmpty())
            {
                text += "\n";
            }

            text += "XMT_EggSackProduction".Translate(currentBacklog, maxBacklog);

            if (Occupant == null || layer == null)
            {
                text += "\n" + "XMT_EggSackNutritionUnknown".Translate();
                return text;
            }

            if (Occupant.needs?.food == null)
            {
                text += "\n" + "XMT_EggSackNutritionSufficient".Translate();
                return text;
            }

            float foodCost = layer.FoodCost;
            if (Occupant.needs.food.CurLevel >= foodCost)
            {
                text += "\n" + "XMT_EggSackNutritionSufficientAmount".Translate(Occupant.needs.food.CurLevel.ToString("F1"), foodCost.ToString("F1"));
            }
            else
            {
                text += "\n" + "XMT_EggSackNutritionInsufficientAmount".Translate(Occupant.needs.food.CurLevel.ToString("F1"), foodCost.ToString("F1"));
            }

            return text;
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            Pawn aggressor = dinfo.Instigator as Pawn;

            if (aggressor != null)
            {
                if (aggressor.Dead)
                {
                    return;
                }


                if (XMTUtility.IsXenomorph(aggressor))
                {
                    return;
                }

                CompPawnInfo info = aggressor.Info();

                if (info != null)
                {
                    info.ApplyThreatPheromone(this, 1, 10);

                    if (XenoformingUtility.XenoformingMeets(10))
                    {
                        if (XenoformingUtility.QueenCalledForAid(aggressor))
                        {
                            if (ModsConfig.RoyaltyActive)
                            {
                                FleckMaker.Static(Position, Map, FleckDefOf.PsycastAreaEffect, 10f);
                            }
                        }
                    }

                }
            }
        }
        public override void EjectContents()
        {
            if (Occupant != null)
            {
                Hediff enthroned = Occupant.health.hediffSet.GetFirstHediffOfDef(InternalDefOf.XMT_Enthroned);
                if (enthroned != null)
                {
                    Occupant.health.RemoveHediff(enthroned);
                }

                Occupant.health.GetOrAddHediff(RoyalEvolutionDefOf.XMT_TornEggSack);
            }
            base.EjectContents();
        }


        public void Initialize()
        {
            IEnumerable<Thing> possibleMaker = GenRadial.RadialDistinctThingsAround(Position, Map, 5f, true);
            foreach (Thing thing in possibleMaker)
            {
                if(!empty)
                {
                    return;
                }
                if (thing is Pawn pawn)
                {
                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        if (!XMTUtility.IsQueen(pawn))
                        {
                            continue;
                        }
                        if (TryEnthronePawn(pawn))
                        {
                            Find.Selector.Select(this, playSound: false, forceDesignatorDeselect: false);
                            return;
                        }
                    }

                }
            }

            if (attempts > 0)
            {
                attempts--;
            }
            else
            {
                Pawn Queen = XenoformingUtility.GenerateFeralQueen();
                XenoformingUtility.EnsureQueenHasOvoThrone(Queen);
                if (Faction != null && Queen.Faction != Faction)
                {
                    Queen.SetFaction(Faction);
                }
                if (TryContainGeneratedQueen(Queen))
                {
                    return;
                }
            }

        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            Pawn occupant = GetOccupant();
            if (containedMorph == null && occupant == null && empty)
            {
                Initialize();
                return;
            }

            if (this.IsHashIntervalTick(CheckInterval))
            {
                if (occupant != null)
                {
                    if (currentBacklog >= maxBacklog)
                    {
                        TryLayOvamorph();
                    }
                    else
                    {
                        currentBacklog++;
                    }
                    if (occupant.needs != null)
                    { 
                        if(occupant.needs.rest != null)
                        {
                            occupant.needs.rest.CurLevel += 0.1f;
                        }
                    }
                    
                }

            }
           
        }


        protected void TryLayOvamorph()
        {
            float nutritionPerJelly = InternalDefOf.Starbeast_Jelly.GetStatValueAbstract(StatDefOf.Nutrition);

            bool usesFood = true;
            bool canLay = true;
            if (Occupant.needs.food != null)
            {
                if(Occupant.needs.food.CurLevel < layer.FoodCost)
                {
                    canLay = false;
                }
            }
            else
            {
                usesFood = false;
            }

            if (canLay)
            {
                IntVec3 layingCell = InteractionCell + (Rotation.Opposite.AsIntVec3 * 5);
 
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(layingCell, 1.5f, true))
                {
                    if (cell.GetEdifice(Map) == null)
                    {
                        layer.LayOvomorph(cell, this);
                        break;
                    }
                }

                Occupant.needs.mood.CurLevel += 0.1f;
            }
            
            if (usesFood)
            {
                float nutritionNeeded = Occupant.needs.food.MaxLevel - Occupant.needs.food.CurLevel;
                float jellyNeeded = nutritionNeeded / nutritionPerJelly;

                if (refuelable.Fuel >= jellyNeeded)
                {
                    refuelable.ConsumeFuel(jellyNeeded);
                }
                else
                {
                    jellyNeeded = refuelable.Fuel;
                    refuelable.ConsumeFuel(jellyNeeded);
                }

                Occupant.needs.food.CurLevel += jellyNeeded * nutritionPerJelly;
            }
        }
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            if (TopGraphic == null)
            {
                TopGraphic = def.building.gibbetCageTopGraphicData.GraphicColoredFor(this);
            }

            Rot4 rotation = Rotation;
            Vector3 TopDrawLoc = drawLoc;

            TopDrawLoc.y += 1.5f;
            TopGraphic.Draw(TopDrawLoc, rotation, this);

            Pawn pawn = GetOccupant();
            if (pawn != null)
            {
                Vector3 pawnDrawLoc = drawLoc;
                pawnDrawLoc.y += 1.6f;

                if (rotation == Rot4.East)
                {

                    pawnDrawLoc.x += 1.5f;
                    pawnDrawLoc.z += 0.75f;
                }
                else if (rotation == Rot4.West)
                {

                    pawnDrawLoc.x -= 1.5f;
                    pawnDrawLoc.z += 0.75f;
                }
                else if(rotation == Rot4.North)
                {
                    pawnDrawLoc.z += 1.4f;
                }

                pawn.Drawer.renderer.DynamicDrawPhaseAt(DrawPhase.Draw, pawnDrawLoc, rotation, neverAimWeapon: true);
            }

        }


    }
}
