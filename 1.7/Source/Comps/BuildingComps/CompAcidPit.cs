using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class CompProperties_AcidPit : CompProperties
    {
        public QueenIngestibleResourceDef inputs;
        public ThingFilter defaultInputs;
        public float baselineMassCapacity = 25f;
        public float jellyPerKg = 1f;
        public float lossMargin = 1.25f;
        public float conversionRate = 1f;
        public int rechargeTicksPerFueling = 7500;
        public float fueling = 25f;
        public int healingInterval = 500;
        public int healingAmount = 50;
        public float jellyPerHitPoint = 0.2f;
        public float splashDamage = 20f;
        public float activationRadius = 2f;
        public float targetPriorityFactor = 1f;
        public float splashRange = 3f;
        public float splashAngle = 60f;
        public float splashArmorPenetration = 9f;
        public float splashAcidFraction = 0.5f;
        public DamageDef splashDamageDef;
        public ThingDef splashFilth;
        public EffecterDef splashEffect;
        public int splashEffectDuration = 17;
        public string splashIconPath;
        public int splashCooldown = 150;
        public EffecterDef processingEffect;
        public ThingDef harvestProduct;
        public int harvestYield = 9;
        public int harvestWork = 2000;
        public CompProperties_AcidPit()
        {
            compClass = typeof(CompAcidPit);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
                yield return error;
            if (inputs?.product == null)
                yield return "Acid pit requires an input resource with a product.";
            if (conversionRate <= 0f)
                yield return "Acid pit conversion rate must be positive.";
            if (healingInterval < 1 || healingAmount < 1 || jellyPerHitPoint <= 0f)
                yield return "Acid pit healing interval, amount, and jelly cost must be positive.";
            if (harvestProduct == null || harvestYield < 1 || harvestWork < 1)
                yield return "Acid pit extraction requires a product, positive yield, and positive work.";
            if (splashDamageDef == null || splashFilth == null || splashIconPath.NullOrEmpty())
                yield return "Acid splash requires a damage def, filth def, and icon path.";
            if (activationRadius <= 0f || activationRadius > splashRange)
                yield return "Acid pit activation radius must be positive and within its splash range.";
            if (splashEffectDuration < 1)
                yield return "Acid splash effect duration must be positive.";
            if (splashRange <= 0f || splashAngle <= 0f || splashAngle > 360f || splashDamage < 0f
                || splashArmorPenetration < 0f || splashAcidFraction <= 0f || splashAcidFraction > 1f || splashCooldown < 0)
                yield return "Invalid acid splash range, angle, damage, cost, or cooldown.";
        }
    }

    public class CompAcidPit : ThingComp, IHiveInputProcessor, IThingHolder
    {
        private HiveInputSettings inputs;
        private ThingOwner<Thing> contents;
        private double acid;
        private double pricedCost;
        private double remainingCost;
        private double resinUnits;
        private double resinRemainder;
        private bool initialized;
        private bool processing = true;
        private bool allowAcidExtraction;
        private int processingGraphicStage;
        private int cooldownUntil;
        private Effecter processingEffect;
        public CompProperties_AcidPit Props => (CompProperties_AcidPit)props;
        public IEnumerable<QueenIngestibleResourceDef> InputResources
        {
            get { yield return Props.inputs; }
        }
        public new IThingHolder ParentHolder => parent.Spawned ? parent.Map : parent.ParentHolder;

        public HiveInputSettings Inputs
        {
            get
            {
                if (inputs != null)
                    return inputs;
                Props.defaultInputs?.ResolveReferences();
                return inputs = new HiveInputSettings(
                    d => d != Props.inputs.product && HiveInputSettings.PotentiallyConvertible(d,
                        new List<ResourceConversionEntry> { new ResourceConversionEntry { resourceDef = Props.inputs } }, parent),
                    d => Props.defaultInputs == null || Props.defaultInputs.Allows(d));
            }
        }

        private ThingOwner<Thing> Contents => contents ??= new ThingOwner<Thing>(this, false);

        public ThingOwner GetDirectlyHeldThings() => Contents;
        public void GetChildHolders(List<IThingHolder> holders) => ThingOwnerUtility.AppendThingHoldersFromThings(holders, Contents);
        public bool Busy => Contents.Count > 0;
        public bool HasMaterial => Busy || resinRemainder > 0d;
        public bool AcceptingMaterial => processing && !Busy;
        public double Capacity => AcidPitEconomy.Capacity(Props, pricedCost);
        public double Acid => acid;
        public float AcidFullness => Capacity > 0d ? Mathf.Clamp01((float)(acid / Capacity)) : 0f;
        public bool AllowAcidExtraction => allowAcidExtraction;
        public bool HarvestReady => parent.Spawned && !Busy && Capacity > 0d && acid >= Capacity - 0.0000001d;
        private int AcidGraphicStage => acid <= 0d ? 0 : acid >= Capacity - 0.01d ? 2 : 1;
        public int GraphicStage => HasMaterial ? Math.Max(1, Math.Max(processingGraphicStage, AcidGraphicStage)) : AcidGraphicStage;
        public double ProcessingAcidNeeded => Math.Max(0d, remainingCost - acid);
        public int ProcessingTicksRemaining => (int)Math.Min(int.MaxValue, Math.Ceiling(ProcessingAcidNeeded / Math.Max(0.000001d, Props.fueling * 60d / Math.Max(1, Props.rechargeTicksPerFueling))) * 60d);
        public bool ProcessingNeedsFuel => Busy && ProcessingAcidNeeded > (parent.GetComp<CompRefuelable>()?.Fuel ?? 0f);

        public bool CanAccept(Thing thing) => AcceptingMaterial && thing != null && !thing.Destroyed && thing.def.category == ThingCategory.Item && thing.def != Props.inputs.product && Inputs.Allows(thing) && ResourceHelperUtility.ValueFor(thing, Props.inputs) > 0f;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (initialized)
                EnsureEconomy();
            else
                pricedCost = AcidPitEconomy.CostPerKg(Props);
            if (!initialized)
            {
                initialized = true;
                acid = Capacity;
                parent.GetComp<CompRefuelable>()?.Refuel(Props.fueling);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Inputs.ExposeData();
            Scribe_Deep.Look(ref contents, "insertedMaterial", this);
            Scribe_Values.Look(ref acid, "acid");
            Scribe_Values.Look(ref pricedCost, "pricedCost");
            Scribe_Values.Look(ref remainingCost, "remainingCost");
            Scribe_Values.Look(ref resinUnits, "pendingResin");
            Scribe_Values.Look(ref resinRemainder, "resinRemainder");
            Scribe_Values.Look(ref initialized, "initialized");
            Scribe_Values.Look(ref processing, "processing", true);
            Scribe_Values.Look(ref allowAcidExtraction, "allowAcidExtraction");
            Scribe_Values.Look(ref processingGraphicStage, "processingGraphicStage");
            Scribe_Values.Look(ref cooldownUntil, "cooldownUntil");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Busy)
            {
                resinUnits = ResinYield(Contents[0]);
            }
        }

        private double ResinYield(Thing thing)
        {
            decimal mass = (decimal)ResourceHelperUtility.ValueFor(thing, Props.inputs);
            decimal unitMass = (decimal)Props.inputs.product.GetStatValueAbstract(StatDefOf.Mass);
            return (double)(mass * (decimal)Props.conversionRate / unitMass);
        }

        public bool TryHarvest(Pawn extractor)
        {
            EnsureEconomy();
            if (!HarvestReady || extractor?.Map != parent.Map
                || !extractor.CanReachImmediate(parent, PathEndMode.ClosestTouch))
            {
                return false;
            }

            acid = 0d;
            XMTUtility.DropAmountThing(Props.harvestProduct, Props.harvestYield, extractor.Position, extractor.Map);
            return true;
        }

        public bool Insert(Thing thing)
        {
            EnsureEconomy();
            if (!CanAccept(thing))
                return false;
            double mass = ResourceHelperUtility.ValueFor(thing, Props.inputs);
            if (thing.holdingOwner != null)
            {
                int count = thing.stackCount;
                if (thing.holdingOwner.TryTransferToContainer(thing, Contents, count, false) != count)
                    return false;
            }
            else
            {
                if (thing.Spawned)
                    thing.DeSpawn();
                if (!Contents.TryAdd(thing, false))
                    return false;
            }

            processingGraphicStage = Math.Max(processingGraphicStage, Math.Max(1, AcidGraphicStage));
            remainingCost = mass * pricedCost;
            resinUnits = ResinYield(thing);
            Digest();
            return true;
        }

        private void Digest()
        {
            if (!Busy)
                return;
            double paid = Math.Min(acid, remainingCost);
            acid -= paid;
            remainingCost -= paid;
            if (remainingCost > 0.0000001d)
                return;
            decimal exact = (decimal)resinUnits + (decimal)resinRemainder;
            int count = (int)Math.Min(int.MaxValue, Math.Floor(exact));
            resinRemainder = (double)(exact - count);
            Contents.ClearAndDestroyContents();
            if (resinRemainder <= 0d)
            {
                processingGraphicStage = 0;
            }
            resinUnits = 0d;
            remainingCost = 0d;
            if (count > 0)
                XMTUtility.DropAmountThing(Props.inputs.product, count, parent.Position, parent.Map);
        }

        private void EnsureEconomy()
        {
            double current = AcidPitEconomy.CostPerKg(Props);
            if (Math.Abs(current - pricedCost) > 0.00000001d)
            {
                acid = 0d;
                resinRemainder = 0d;
                pricedCost = current;
                if (Busy)
                {
                    double mass = ResourceHelperUtility.ValueFor(Contents[0], Props.inputs);
                    remainingCost = mass * pricedCost;
                    resinUnits = ResinYield(Contents[0]);
                }
            }
        }

        public override void CompTick()
        {
            if (!parent.Spawned)
                return;
            bool healing = parent.HitPoints < parent.MaxHitPoints;
            if (healing && parent.IsHashIntervalTick(Props.healingInterval))
                HealFromJelly();
            if (parent.IsHashIntervalTick(60))
            {
                EnsureEconomy();
                if (!healing)
                {
                    CompRefuelable fuel = parent.GetComp<CompRefuelable>();
                    float wanted = (float)Math.Min(Capacity - acid, Props.fueling * 60d / Math.Max(1, Props.rechargeTicksPerFueling));
                    float available = Math.Min(fuel?.Fuel ?? 0f, wanted);
                    if (available > 0f)
                    {
                        fuel.ConsumeFuel(available);
                        acid += available;
                    }
                }

                Digest();
            }

            TickProcessingEffect();
            if (!parent.IsHashIntervalTick(30) || !SplashReady)
                return;
            // Query only cells inside the configured trigger radius, after readiness/cooldown checks.
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, Props.activationRadius, true))
            {
                if (!cell.InBounds(parent.Map))
                    continue;
                List<Thing> things = cell.GetThingList(parent.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is not Pawn pawn || pawn.Dead || !pawn.HostileTo(parent) || AcidUtility.IsAcidImmune(pawn))
                        continue;
                    IntVec3 aim = cell == parent.Position ? cell + parent.Rotation.FacingCell : cell;
                    if (Splash(aim))
                        return;
                }
            }
        }

        private void HealFromJelly()
        {
            CompRefuelable fuel = parent.GetComp<CompRefuelable>();
            int affordable = Mathf.FloorToInt((fuel?.Fuel ?? 0f) / Props.jellyPerHitPoint);
            int amount = Math.Min(parent.MaxHitPoints - parent.HitPoints, Math.Min(Props.healingAmount, affordable));
            if (amount <= 0)
                return;
            fuel.ConsumeFuel(amount * Props.jellyPerHitPoint);
            parent.HitPoints += amount;
            parent.GetComp<CompPawnPreservation>()?.HealToBuildingHealth();
        }

        private bool SplashReady => parent.Spawned && acid >= Capacity * Props.splashAcidFraction && Find.TickManager.TicksGame >= cooldownUntil;

        public bool CanSplashAt(IntVec3 cell) => parent.Spawned && cell.IsValid && cell != parent.Position && cell.InBounds(parent.Map) && cell.InHorDistOf(parent.Position, Props.splashRange) && cell.CanBeSeenOverFast(parent.Map) && GenSight.LineOfSight(parent.Position, cell, parent.Map, true);
        public bool Splash(LocalTargetInfo target)
        {
            EnsureEconomy();
            if (!SplashReady || (target.HasThing && target.Thing.Map != parent.Map) || !CanSplashAt(target.Cell))
                return false;
            Map map = parent.Map;
            List<IntVec3> cells = AcidPitSpray.AffectedCells(parent.Position, target.Cell, map, Props.splashRange, Props.splashAngle);
            if (cells.Count == 0)
                return false;
            acid -= Capacity * Props.splashAcidFraction;
            cooldownUntil = Find.TickManager.TicksGame + Props.splashCooldown;
            if (Props.splashEffect != null)
            {
                TargetInfo source = new TargetInfo(parent.Position, map);
                TargetInfo destination = new TargetInfo(target.Cell, map);
                Effecter effect = Props.splashEffect.Spawn(source, destination);
                map.effecterMaintainer.AddEffecterToMaintain(effect, source, destination, Props.splashEffectDuration);
            }

            // Snapshot before damage: death, knockback, or despawning may mutate a cell's thing list.
            var caught = new HashSet<Pawn>();
            foreach (IntVec3 cell in cells)
            {
                bool occupied = false;
                foreach (Thing thing in cell.GetThingList(map))
                    if (thing is Pawn pawn && !pawn.Dead)
                    {
                        occupied = true;
                        caught.Add(pawn);
                    }

                if (!occupied)
                    FilthMaker.TryMakeFilth(cell, map, Props.splashFilth);
            }

            foreach (Pawn pawn in caught)
            {
                if (!pawn.Spawned || pawn.Map != map || pawn.Dead || AcidUtility.IsAcidImmune(pawn))
                    continue;
                // Area hits do not use the blood-splash dodge roll or immobile-target multiplier.
                pawn.TakeDamage(new DamageInfo(Props.splashDamageDef, Props.splashDamage, Props.splashArmorPenetration, -1f, parent));
            }

            return true;
        }

        private void TickProcessingEffect()
        {
            if (HasMaterial)
            {
                processingEffect ??= Props.processingEffect?.Spawn();
                processingEffect?.EffectTick(parent, parent);
            }
            else
                ClearProcessingEffect();
        }

        private void ClearProcessingEffect()
        {
            processingEffect?.Cleanup();
            processingEffect = null;
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            ClearProcessingEffect();
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            string text = "XMT_AcidInspect".Translate(AcidFullness.ToStringPercent());
            if (parent.HitPoints < parent.MaxHitPoints)
                text += "\n" + "XMT_AcidHealing".Translate();
            if (!Busy)
            {
                if (resinRemainder > 0d)
                {
                    text += "\n" + "XMT_AcidRetainedResidue".Translate(resinRemainder.ToString("0.######"));
                }
                return text;
            }
            text += "\n" + "XMT_AcidDigesting".Translate(Contents[0].LabelShortCap, ProcessingTicksRemaining.ToStringTicksToPeriod());
            if (ProcessingNeedsFuel)
                text += "\n" + "XMT_AcidProcessingNeedsJelly".Translate();
            return text;
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
        {
            if (myPawn == null || parent.Faction != Faction.OfPlayer)
                yield break;
            if (XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished == true)
            {
                string harvestReason = Busy ? "XMT_AcidOccupied".Translate().ToString()
                    : !HarvestReady ? "XMT_AcidHarvestNotFull".Translate().ToString()
                    : myPawn.Downed || myPawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)
                        || !myPawn.CanReserveAndReach(parent, PathEndMode.ClosestTouch, Danger.Some)
                        ? "XMT_AcidCannotExtract".Translate().ToString() : null;
                string harvestLabel = "XMT_AcidHarvestOrder".Translate();
                if (harvestReason != null) harvestLabel += ": " + harvestReason;
                var harvestOption = new FloatMenuOption(harvestLabel, harvestReason != null ? null : () =>
                {
                    Job harvestJob = JobMaker.MakeJob(XenoWorkDefOf.XMT_ExtractAcid, parent);
                    harvestJob.playerForced = true;
                    myPawn.jobs.TryTakeOrderedJob(harvestJob, JobTag.Misc);
                }, Props.harvestProduct.uiIcon, Color.white, revalidateClickTarget: parent);
                yield return harvestReason == null
                    ? FloatMenuUtility.DecoratePrioritizedTask(harvestOption, myPawn, parent) : harvestOption;
            }
            Job job = AcidDisposalUtility.FindJobForPit(myPawn, this, out string reason);
            string label = "XMT_AcidRecycleOrder".Translate();
            if (reason != null)
                label += ": " + reason;
            var option = new FloatMenuOption(label, reason != null ? null : () => myPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc), ContentFinder<Texture2D>.Get("UI/Designators/Haul"), Color.white, revalidateClickTarget: parent);
            if (reason == null)
                option = FloatMenuUtility.DecoratePrioritizedTask(option, myPawn, parent);
            yield return option;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Empty acid",
                    action = () =>
                    {
                        EnsureEconomy();
                        acid = 0d;
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Fill acid",
                    action = () =>
                    {
                        EnsureEconomy();
                        acid = Capacity;
                    }
                };
            }

            if (parent.Faction != Faction.OfPlayer)
                yield break;
            if (XenoGeneDefOf.XMT_Acid_Utilization?.IsFinished == true)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "XMT_AcidAllowExtraction".Translate(),
                    defaultDesc = "XMT_AcidAllowExtractionDesc".Translate(Props.harvestYield),
                    icon = Props.harvestProduct.uiIcon,
                    isActive = () => allowAcidExtraction,
                    toggleAction = () => allowAcidExtraction = !allowAcidExtraction
                };
            }
            yield return new Command_Toggle
            {
                defaultLabel = "XMT_AcidProcessing".Translate(),
                defaultDesc = "XMT_AcidProcessingDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Haul"),
                isActive = () => processing,
                toggleAction = () => processing = !processing
            };
            if (!XMTUtility.QueenIsPlayer())
                yield break;
            var command = new Command_Target
            {
                defaultLabel = "XMT_AcidSplash".Translate(),
                defaultDesc = "XMT_AcidSplashDesc".Translate(Props.splashRange, Props.splashDamage, Props.splashAcidFraction.ToStringPercent()),
                icon = ContentFinder<Texture2D>.Get(Props.splashIconPath),
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = true,
                    canTargetBuildings = false,
                    canTargetItems = false,
                    validator = t => CanSplashAt(t.Cell)
                },
                onUpdate = t =>
                {
                    if (CanSplashAt(t.Cell))
                        GenDraw.DrawFieldEdges(AcidPitSpray.AffectedCells(parent.Position, t.Cell, parent.Map, Props.splashRange, Props.splashAngle));
                },
                action = t => Splash(t)
            };
            if (!SplashReady)
                command.Disable("XMT_AcidNotReady".Translate());
            yield return command;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            ClearProcessingEffect();
            if (previousMap != null)
                Contents.TryDropAll(parent.Position, previousMap, ThingPlaceMode.Near);
            base.PostDestroy(mode, previousMap);
        }
    }
}
