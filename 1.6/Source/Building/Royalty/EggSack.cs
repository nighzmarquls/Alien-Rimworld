

using AlienRace;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class EggSack : SelfOccupyingBuilding, IThingHolderWithDrawnPawn
    {
        CompOvomorphLayer layer = null;
        CompRefuelable refuelable = null;
        public Pawn Occupant = null;

        public const int MaxBacklog = 6;

        int currentBacklog = 0;
        RecipeDef pendingMechGestationRecipe;

        int CheckInterval = 1000;

        private static Texture2D GestateTexture => ContentFinder<Texture2D>.Get("UI/Abilities/GestateMechanoid");

        [Unsaved(false)]
        private Graphic TopGraphic;

        public override bool CanOpen => false;
        protected override bool DestroyWhenEmptyAfterLoad => true;
        protected override bool HasFallbackOccupant => true;

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
            Scribe_Defs.Look(ref pendingMechGestationRecipe, "pendingMechGestationRecipe");
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            refuelable = GetComp<CompRefuelable>();
        }

        protected override bool CanOccupy(Pawn pawn)
        {
            return base.CanOccupy(pawn) && XMTUtility.IsQueen(pawn);
        }

        protected override Pawn GenerateFallbackOccupant()
        {
            Pawn queen = XenoformingUtility.GenerateFeralQueen();
            XenoformingUtility.EnsureQueenHasOvoThrone(queen);
            return queen;
        }

        protected override void Notify_OccupantRegistered(Pawn pawn)
        {
            layer = pawn.TryGetComp<CompOvomorphLayer>();
            Occupant = pawn;

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

        private Pawn GetOccupant()
        {
            Pawn pawn = ContainedThing as Pawn ?? Occupant;
            if (pawn != null && pawn != Occupant)
            {
                Notify_OccupantRegistered(pawn);
            }

            return pawn;
        }

        public bool TryEnthronePawn(Pawn pawn)
        {
            return TryAssignOccupant(pawn);
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

                CompQueenAssimilation assimilation = occupant.GetComp<CompQueenAssimilation>();
                if (assimilation != null)
                {
                    foreach (Gizmo gizmo in assimilation.CompGetGizmosExtra())
                    {
                        if (gizmo is Gizmo_QueenResources)
                        {
                            yield return gizmo;
                        }
                    }
                }

                if (occupant.mechanitor != null)
                {
                    foreach (Gizmo gizmo in occupant.mechanitor.GetGizmos())
                    {
                        yield return gizmo;
                    }
                }

                if (assimilation != null && QueenMechGestationUtility.MechMaterialResource != null && assimilation.ResourceUnlocked(QueenMechGestationUtility.MechMaterialResource))
                {
                    yield return CreateGestateMechanoidAction(occupant);
                }
            }

            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
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

            text += "XMT_EggSackProduction".Translate(currentBacklog, MaxBacklog);
            if (pendingMechGestationRecipe != null)
            {
                ThingDef product = QueenMechGestationUtility.ProductFor(pendingMechGestationRecipe);
                text += "\n" + "XMT_EggSackPendingMechGestation".Translate(product?.LabelCap ?? pendingMechGestationRecipe.LabelCap, ReserveEggCostFor(pendingMechGestationRecipe));
            }

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

        private Command_Action CreateGestateMechanoidAction(Pawn occupant)
        {
            return new Command_Action
            {
                defaultLabel = "XMT_GestateMechanoid".Translate(),
                defaultDesc = "XMT_GestateMechanoidThroneDesc".Translate(),
                icon = GestateTexture,
                action = delegate
                {
                    OpenMechGestationMenu(occupant);
                }
            };
        }

        private void OpenMechGestationMenu(Pawn occupant)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            if (pendingMechGestationRecipe != null)
            {
                options.Add(new FloatMenuOption("XMT_CancelMechGestation".Translate(), delegate
                {
                    pendingMechGestationRecipe = null;
                }));
            }

            foreach (RecipeDef recipe in QueenMechGestationUtility.EligibleRecipes(occupant, ovothroneAssisted: true).OrderBy(recipe => recipe.LabelCap.ToString()))
            {
                ThingDef product = QueenMechGestationUtility.ProductFor(recipe);
                int materialCost = Mathf.CeilToInt(QueenMechGestationUtility.MaterialCost(occupant, recipe));
                int eggCost = ReserveEggCostFor(recipe);
                string optionLabel = "XMT_GestateMechanoidThroneOption".Translate(product.LabelCap, materialCost, eggCost);

                if (!QueenMechGestationUtility.CanGestate(occupant, recipe, out string reason, ovothroneAssisted: true))
                {
                    options.Add(new FloatMenuOption(optionLabel + " (" + reason + ")", null));
                    continue;
                }

                options.Add(new FloatMenuOption(optionLabel, delegate
                {
                    ScheduleThroneMechGestation(recipe);
                }));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("XMT_NoMechGestationOptions".Translate(), null));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ScheduleThroneMechGestation(RecipeDef recipe)
        {
            pendingMechGestationRecipe = recipe;
            ThingDef product = QueenMechGestationUtility.ProductFor(recipe);
            Messages.Message("XMT_MechGestationQueued".Translate(product?.LabelCap ?? recipe.LabelCap, ReserveEggCostFor(recipe)), this, MessageTypeDefOf.TaskCompletion, false);
        }

        private bool TryGestateThroneMech(Pawn occupant, RecipeDef recipe)
        {
            if (!CanSpendReserveEggsFor(recipe, out string reason))
            {
                Messages.Message(reason, this, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!TryFindThroneOutputCell(out IntVec3 cell))
            {
                Messages.Message("CannotGenericWorkCustom".Translate("NoPath".Translate()), this, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!QueenMechGestationUtility.TryFinishGestation(occupant, recipe, cell, Map, out Pawn _, out reason, ovothroneAssisted: true))
            {
                if (!reason.NullOrEmpty())
                {
                    Messages.Message(reason, this, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            SpendReserveEggsFor(recipe);
            pendingMechGestationRecipe = null;
            return true;
        }

        private bool TryFindThroneOutputCell(out IntVec3 outputCell)
        {
            IntVec3 layingCell = InteractionCell + (Rotation.Opposite.AsIntVec3 * 5);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(layingCell, 1.5f, true))
            {
                if (cell.InBounds(Map)
                    && cell.Standable(Map)
                    && cell.GetEdifice(Map) == null
                    && cell.GetFirstPawn(Map) == null)
                {
                    outputCell = cell;
                    return true;
                }
            }

            outputCell = IntVec3.Invalid;
            return false;
        }

        private bool CanSpendReserveEggsFor(RecipeDef recipe, out string reason)
        {
            int cost = ReserveEggCostFor(recipe);
            if (currentBacklog < cost)
            {
                reason = "XMT_MechGestationNeedsReserveEggs".Translate(cost, currentBacklog, MaxBacklog);
                return false;
            }

            reason = null;
            return true;
        }

        private void SpendReserveEggsFor(RecipeDef recipe)
        {
            currentBacklog = Mathf.Max(0, currentBacklog - ReserveEggCostFor(recipe));
        }

        private int ReserveEggCostFor(RecipeDef recipe)
        {
            ThingDef product = QueenMechGestationUtility.ProductFor(recipe);
            float bodySize = product?.race == null ? 1f : product.race.baseBodySize;
            if (bodySize > MaxBacklog)
            {
                return MaxBacklog;
            }

            return Mathf.Clamp(Mathf.CeilToInt(bodySize), 1, MaxBacklog);
        }

        public void DebugSetReserveEggs(int count)
        {
            currentBacklog = Mathf.Clamp(count, 0, MaxBacklog);
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
        protected override void TickSelfOccupyingBuilding(int delta)
        {
            Pawn occupant = GetOccupant();
            if (this.IsHashIntervalTick(CheckInterval))
            {
                if (occupant != null)
                {
                    if (pendingMechGestationRecipe != null)
                    {
                        if (currentBacklog >= ReserveEggCostFor(pendingMechGestationRecipe))
                        {
                            TryGestateThroneMech(occupant, pendingMechGestationRecipe);
                        }
                        else
                        {
                            currentBacklog = Mathf.Min(MaxBacklog, currentBacklog + 1);
                        }
                    }
                    else if (currentBacklog >= MaxBacklog)
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
