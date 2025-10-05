

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
            containedMorph = thing.TryGetComp<CompMatureMorph>();
            layer = thing.TryGetComp<CompOvomorphLayer>();
            if (thing is Pawn pawn)
            {
                Occupant = pawn;

                Occupant.health.GetOrAddHediff(InternalDefOf.XMT_Enthroned);

                CompStealth compStealth = Occupant.GetComp<CompStealth>();
                if (compStealth != null)
                {
                    compStealth.ForceVisible();
                }
            }
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

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (Faction == null || !Faction.IsPlayer)
            {
                yield break;
            }

            Command_Action command_Action = new Command_Action();
            command_Action.action = Open;
            command_Action.defaultLabel = "CommandPodEject".Translate();
            command_Action.defaultDesc = "CommandPodEjectDesc".Translate();

            command_Action.hotKey = KeyBindingDefOf.Misc8;
            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Abilities/Starbeast_Leap");
            yield return command_Action;
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
                if (thing is Pawn pawn)
                {
                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        if (!XMTUtility.IsQueen(pawn))
                        {
                            continue;
                        }

                        bool flag = pawn.DeSpawnOrDeselect();
                        if (TryAcceptThing(pawn) && flag)
                        {
                            if (Occupant.jobs.curJob != null)
                            {
                                Occupant.jobs.curJob.Clear();
                            }
                            Find.Selector.Select(this, playSound: false, forceDesignatorDeselect: false);
                            return;
                        }
                    }

                }
                if (attempts > 0)
                {
                    attempts--;
                }
            }

        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (containedMorph == null)
            {
                Initialize();
                return;
            }

            if (this.IsHashIntervalTick(CheckInterval))
            {
                if (Occupant != null)
                {
                    if (currentBacklog >= maxBacklog)
                    {
                        TryLayOvamorph();
                    }
                    else
                    {
                        currentBacklog++;
                    }
                    if (Occupant.needs != null)
                    { 
                        if(Occupant.needs.rest != null)
                        {
                            Occupant.needs.rest.CurLevel += 0.1f;
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
                        layer.LayOvomorph(cell);
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

            Rot4 rotation = Rotation;
            if (ContainedThing is Pawn pawn)
            {
                
                Vector3 s = new Vector3(def.graphicData.drawSize.x * 0.8f, 1f, def.graphicData.drawSize.y * 0.8f);
                Vector3 drawPos = DrawPos;
                drawPos.y -= HeldPawnDrawPos_Y;

                Vector3 pawnDrawLoc = DrawPos;

                if (rotation == Rot4.East)
                {

                    pawnDrawLoc.x += 1.5f;
                    pawnDrawLoc.y += 1.0f;
                    pawnDrawLoc.z += 0.75f;
                }
                else if (rotation == Rot4.West)
                {

                    pawnDrawLoc.x -= 1.5f;
                    pawnDrawLoc.y += 1.0f;
                    pawnDrawLoc.z += 0.75f;
                }
                else if(rotation == Rot4.North)
                {
                    pawnDrawLoc.y += 0.5f;
                    pawnDrawLoc.z += 1.4f;
                }
                else
                {
                    pawnDrawLoc.y += 0.25f;
                }

                pawn.Drawer.renderer.RenderPawnAt(pawnDrawLoc, rotation, neverAimWeapon: true);
            }

            if (TopGraphic == null)
            {
                TopGraphic = def.building.gibbetCageTopGraphicData.GraphicColoredFor(this);
            }

            Vector3 TopDrawLoc = drawLoc;

            TopDrawLoc.y += 1.5f;
            TopGraphic.Draw(TopDrawLoc, rotation, this);

        }


    }
}
