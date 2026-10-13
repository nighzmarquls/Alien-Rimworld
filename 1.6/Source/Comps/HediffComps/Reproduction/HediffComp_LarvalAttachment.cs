using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class HediffComp_LarvalAttachment : HediffComp_PawnAttachement
    {
        public GeneSet genes;
        public List<GeneDef> removedDefaultGenes = new List<GeneDef>();
        public Pawn mother;
        public Pawn father;
        public PawnKindDef kind;
        public bool spent;
        public string name;
        public float age;
        private int geneStorageVersion = 1;
        private int pawnStorageVersion = 1;

        [Unsaved(false)]
        private bool migrationAttempted;

        [Unsaved(false)]
        private bool handlingRemoval;

        [Unsaved(false)]
        internal Thing pendingBioContainer;
       
        public new HediffCompProperties_LarvalAttachment Props => (HediffCompProperties_LarvalAttachment)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref father, "father", saveDestroyedThings: false);
            Scribe_References.Look(ref mother, "mother", saveDestroyedThings: false);
            Scribe_Deep.Look(ref genes, "genes");
            Scribe_Collections.Look(ref removedDefaultGenes, "removedDefaultGenes", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) removedDefaultGenes ??= new List<GeneDef>();
            Scribe_Defs.Look(ref kind, "kind");
            Scribe_Values.Look(ref spent, "spent", defaultValue: false);
            Scribe_Values.Look(ref name, "name", defaultValue: "larva");
            Scribe_Values.Look(ref age, "age", defaultValue: 0.0f);
            Scribe_Values.Look(ref geneStorageVersion, "geneStorageVersion", 0);
            Scribe_Values.Look(ref pawnStorageVersion, "xmtAttachmentPawnStorageVersion", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && geneStorageVersion < 1)
            {
                genes = new HorrorGenePayload(BioUtility.NormalizeGenesForStorage(InternalDefOf.XMT_Starbeast_AlienRace, genes?.GenesListForReading)).ToGeneSet();
                geneStorageVersion = 1;
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                migrationAttempted = false;
                LongEventHandler.ExecuteWhenFinished(delegate
                {
                    // Generation must not run while Scribe is resolving the old save.
                    if (Scribe.mode == LoadSaveMode.Inactive && Pawn?.health?.hediffSet.hediffs.Contains(parent) == true)
                    {
                        EnsureAttachedPawn();
                    }
                });
            }

        }
        public override void CompPostMake()
        {
            base.CompPostMake();

            XMTHiveUtility.RemoveHost(parent.pawn, parent.pawn.Map);
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (attachedPawn?.GetComp<CompLarvalGenes>() is CompLarvalGenes larva)
            {
                larva.latched = true;
                spent = larva.spent;
                pawnStorageVersion = 1;
            }
        }

        private bool EnsureAttachedPawn()
        {
            if (pawnStorageVersion >= 1)
            {
                if (attachedPawn != null)
                {
                    return true;
                }
                if (!removed)
                {
                    Log.ErrorOnce($"[XMT][Attachment] Missing saved facehugger on {Pawn}; refusing to generate a duplicate from stale metadata.", Pawn.thingIDNumber ^ 173294601);
                }
                return false;
            }
            if (removed || migrationAttempted || Scribe.mode != LoadSaveMode.Inactive)
            {
                return false;
            }

            migrationAttempted = true;
            try
            {
                Pawn larvaPawn = attachedPawn;
                if (larvaPawn == null)
                {
                    PawnGenerationRequest request = new PawnGenerationRequest(kind ?? XenoPawnKindDefOf.XMT_Larva,
                        !spent ? mother?.Faction : null);
                    request.FixedBiologicalAge = Mathf.Max(0f, age);
                    larvaPawn = PawnGenerator.GeneratePawn(request);
                    if (!GetDirectlyHeldThings().TryAdd(larvaPawn))
                    {
                        // An existing generated pawn must not be regenerated on a later load.
                        pawnStorageVersion = 1;
                        Find.WorldPawns?.PassToWorld(larvaPawn, PawnDiscardDecideMode.KeepForever);
                        throw new InvalidOperationException("Reconstructed facehugger could not enter the attachment owner.");
                    }
                }

                CompLarvalGenes larva = larvaPawn.GetComp<CompLarvalGenes>();
                if (larva == null)
                {
                    throw new InvalidOperationException("Saved facehugger kind has no CompLarvalGenes.");
                }
                larva.mother = mother;
                larva.father = father;
                larva.ReplaceGenes(genes?.GenesListForReading, removedDefaults: removedDefaultGenes);
                // Older code stored the full displayed name as a string.
                if (!name.NullOrEmpty())
                {
                    larvaPawn.Name = new NameSingle(name);
                }
                spent |= Pawn.health.hediffSet.HasHediff(Props.embryoHediff ?? InternalDefOf.XMT_Embryo);
                larva.spent = spent;
                larva.latched = true;
                pawnStorageVersion = 1;
                if (XMTSettings.LogBiohorror)
                {
                    Log.Message($"[XMT][Attachment] Migrated legacy facehugger on {Pawn}: {larvaPawn}, spent={spent}.");
                }
                return true;
            }
            catch (Exception exception)
            {
                Log.Error($"[XMT][Attachment] Legacy facehugger recovery failed on {Pawn}; saved metadata is retained, but recovery is not guaranteed if the hediff is removed: {exception}");
                return false;
            }
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if (removed)
            {
                if (attachedPawn != null)
                {
                    LarvaRelease();
                }
                return;
            }
            if (!EnsureAttachedPawn())
            {
                return;
            }

            // Preserve HediffComp_SeverityModifierBase's 200-tick progression cadence.
            if (Pawn.IsHashIntervalTick(200, delta))
            {
                severityAdjustment += Props.severityPerDay * (200f / GenDate.TicksPerDay);
            }

            if(XMTUtility.IsXenomorphFriendly(parent.pawn) || XMTUtility.IsMorphing(Pawn))
            {
                LarvaRelease();
                return;
            }

            if (parent.Severity >= 0.75f && !spent)
            {
                ImplantEmbryo();

            }else if(parent.Severity >= 1.0f)
            {
                LarvaRelease();
                return;
            }

            if(parent.pawn.needs?.food != null)
            {
                parent.pawn.needs.food.CurLevel = parent.pawn.needs.food.MaxLevel;
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            LarvaRelease();
            if (Pawn.health.hediffSet.hediffs.Contains(parent))
            {
                Pawn.health.RemoveHediff(parent);
            }
        }

        public override void CompPostPostRemoved()
        {
            try
            {
                ReleaseAfterRemoval();
            }
            finally
            {
                base.CompPostPostRemoved();
            }
        }

        public override void Notify_SurgicallyRemoved(Pawn surgeon)
        {
            ReleaseAfterRemoval();
        }

        public override void Notify_SurgicallyReplaced(Pawn surgeon)
        {
            ReleaseAfterRemoval();
        }

        private void ReleaseAfterRemoval()
        {
            if (handlingRemoval || (removed && attachedPawn == null))
            {
                return;
            }

            if (!EnsureAttachedPawn() && attachedPawn == null)
            {
                if (!removed)
                {
                    Log.Error($"[XMT][Attachment] No facehugger could be recovered while removing the attachment from {Pawn}.");
                }
                return;
            }

            handlingRemoval = true;
            try
            {
                float larvaBonus = 0;
                float acidBonus = 0;
                Map map = Pawn.MapHeld;
                IntVec3 parentCell = Pawn.PositionHeld;
                if (!spent && !Pawn.Dead && map != null)
                {
                    XMTUtility.WitnessLarva(parentCell, map, 0.1f, out larvaBonus, 1f, knowledgeProfile: Props.attachKnowledgeProfile);
                    XMTUtility.WitnessAcid(parentCell, map, 0.1f, out acidBonus, 1f, knowledgeProfile: Props.acidKnowledgeProfile);
                    if (larvaBonus < 1)
                    {
                        LarvaSqueeze(4f);
                    }
                }

                if (LarvaRelease() is Pawn larvaPawn && !spent && larvaPawn.Spawned && larvaPawn.Map == map)
                {
                    if (Rand.Chance(larvaBonus))
                    {
                        larvaPawn.health.AddHediff(HediffDefOf.Anesthetic);
                    }
                    if (Rand.Chance(1 - acidBonus))
                    {
                        CompAcidBlood acid = larvaPawn.GetAcidBloodComp();
                        acid?.TrySplashAcidCell(GenRadial.RadialCellsAround(parentCell, 1.5f, true).RandomElement());
                    }
                    larvaPawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, 20 * larvaBonus));
                }
            }
            finally
            {
                LarvaRelease();
                handlingRemoval = false;
            }
        }
        public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
        {
            if (removed || !EnsureAttachedPawn())
            {
                return;
            }
            if (Pawn.IsWorldPawn())
            {
                if(spent)
                {
                    return;
                }

                if (XMTSettings.LogWorld)
                {
                    Log.Message("[XMT][World] " + Pawn + " has a larva attached and will develop an embryo.");
                }
                XenoformingUtility.ReleaseEmbryoOnWorld(Pawn);
                MarkSpent();
                return;
            }
            // Facehuggers retain their own tending thresholds, not the generic release threshold.
            if (Pawn.MapHeld == null)
            {
                return;
            }
            float witnessBonus;
            bool XenomorphTending = XMTUtility.WitnessLarva(parent.pawn.PositionHeld, parent.pawn.MapHeld, 0.1f, out witnessBonus, 1f, knowledgeProfile: Props.tendKnowledgeProfile);

            if (XenomorphTending)
            {
                //xenomorphs just make it worse.
                parent.Severity += 0.25f;
            }
            else if (parent.CurStageIndex < 1 || spent)
            {
                //if you catch it early enough or too late you can get the thing off.
                if( LarvaRelease() is Pawn larvaPawn)
                {
                    larvaPawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, 5));
                }
            }
            else if (parent.CurStageIndex < 2 && quality + witnessBonus >= Props.minimumTendToRemove)
            {
                //Larva removed before implantation but may still kill host.
                //Experience with Larva will mitigate this.
                if (witnessBonus < 1)
                {
                    LarvaSqueeze(8f);
                }

                if (LarvaRelease() is Pawn larvaPawn)
                {
                    larvaPawn.TakeDamage(new DamageInfo(DamageDefOf.Stun, 10*(quality+witnessBonus)));
                }
            }
            else if (quality <= Props.minimumTendToAvoidInjury)
            {

                //too late requires surgery or you will end up tearing their face off.
                LarvaSqueeze();
            }
          

            base.Pawn.health.Notify_HediffChanged(parent);
        }

        protected void LarvaSqueeze(float damage = 5f)
        {
            if (XMTSettings.LogBiohorror)
            {
                Log.Message("[XMT][Biohorror] " + parent + " is squeezing ");
            }
            XMTUtility.WitnessLarva(Pawn.PositionHeld, Pawn.MapHeld, 1f, knowledgeProfile: Props.squeezeKnowledgeProfile);

            parent.Severity += 0.25f;

            Messages.Message("XMT_ParasiteCrushing".Translate(Pawn.LabelShort).CapitalizeFirst(), MessageTypeDefOf.NegativeEvent);

            IEnumerable<BodyPartRecord> source = from x in Pawn.health.hediffSet.GetNotMissingParts()
                                                 where
                                                ( x.IsInGroup(BodyPartGroupDefOf.FullHead)
                                                || x.IsInGroup(BodyPartGroupDefOf.UpperHead)
                                                || x.IsInGroup(ExternalDefOf.HeadAttackTool)
                                                || XMTUtility.IsPartHead(x))
                                                && x.depth == BodyPartDepth.Outside
                                                 select x;

            DamageDef squeeze = DamageDefOf.Blunt;
            float amount = damage;
            float armorPenetration = 999f;
            if (source.Any())
            {
                foreach (BodyPartRecord part in source.ToList())
                {
                    if (Pawn.Dead || removed)
                    {
                        return;
                    }
                    base.Pawn.TakeDamage(new DamageInfo(squeeze, amount, armorPenetration, -1f, null, part, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
                }
            }

            amount = damage*2;
            source = from x in base.Pawn.health.hediffSet.GetNotMissingParts()
                     where
                    (x.IsInGroup(ExternalDefOf.Neck) )
                     select x;

            if (source.Any())
            {
                base.Pawn.TakeDamage(new DamageInfo(squeeze, amount, armorPenetration, -1f, null, source.RandomElement<BodyPartRecord>(), null, DamageInfo.SourceCategory.ThingOrUnknown, null));
            }
        }
        protected Thing LarvaRelease()
        {
            if (removed && attachedPawn == null)
            {
                return null;
            }
            if (!EnsureAttachedPawn() && attachedPawn == null)
            {
                return null;
            }
            Pawn larva = attachedPawn;
            CompLarvalGenes larvalGenes = larva.GetComp<CompLarvalGenes>();
            if (larvalGenes != null)
            {
                larvalGenes.spent = spent;
                larvalGenes.latched = false;
            }
            if (spent && larva.Faction?.IsPlayer == true)
            {
                larva.SetFaction(null);
            }
            if (!AttachmentUtility.TryReleaseHeldPawn(GetDirectlyHeldThings(), larva, Pawn, pendingBioContainer))
            {
                if (larvalGenes != null)
                {
                    larvalGenes.latched = true;
                }
                return null;
            }
            removed = true;
            return larva;
        }

        public override void PawnRelease(bool killPawn = false)
        {
            if (LarvaRelease() is Pawn released && killPawn && !released.Dead)
            {
                released.Kill(new DamageInfo(DamageDefOf.SurgicalCut, 10));
            }
        }

        private void MarkSpent()
        {
            spent = true;
            if (attachedPawn?.GetComp<CompLarvalGenes>() is CompLarvalGenes larva)
            {
                larva.spent = true;
            }
        }

        protected void ImplantEmbryo()
        {
            if (spent || removed || Pawn.Dead || !Pawn.health.hediffSet.hediffs.Contains(parent))
            {
                return;
            }

            if (Pawn.health.hediffSet.HasHediff(Props.embryoHediff ?? InternalDefOf.XMT_Embryo))
            {
                MarkSpent();
                return;
            }

            genes = attachedPawn?.GetComp<CompLarvalGenes>()?.CaptureGeneSet() ?? genes;
            CompLarvalGenes lineage = attachedPawn?.GetComp<CompLarvalGenes>();
            if (lineage != null) removedDefaultGenes = lineage.removedDefaultGenes.ToList();

            if (!Pawn.health.hediffSet.GetNotMissingParts().Any(part => part.depth == BodyPartDepth.Inside))
            {
                Log.ErrorOnce($"[XMT][Attachment] No viable internal body part for implantation on {Pawn}; facehugger remains unspent.", Pawn.thingIDNumber ^ 173294602);
                return;
            }

            Hediff hediff = BioUtility.MakeEmbryoPregnancy(Pawn, Props.embryoHediff);
            HediffComp_EmbryoPregnancy embryoPregnancy = hediff.TryGetComp<HediffComp_EmbryoPregnancy>();

            if (embryoPregnancy == null)
            {
                Log.ErrorOnce($"[XMT][Attachment] {hediff.def} has no embryo pregnancy comp; cannot implant on {Pawn}.", Pawn.thingIDNumber ^ 173294603);
                return;
            }

            if (embryoPregnancy != null)
            {
                embryoPregnancy.genes = new HorrorGenePayload(genes?.GenesListForReading).ToGeneSet();
                embryoPregnancy.removedDefaultGenes = removedDefaultGenes.ToList();

                if (embryoPregnancy.genes == null)
                {
                    embryoPregnancy.genes = new GeneSet();
                }

                if (mother != null)
                {
                    embryoPregnancy.mother = mother;
                    
                    if (Pawn.genes != null)
                    {

                        if (father == mother)
                        {
                            embryoPregnancy.father = Pawn;
                            
                            BioUtility.ExtractGenesToGeneset(ref embryoPregnancy.genes, BioUtility.GetCanonicalPawnGenes(Pawn));
                        }
                    }
                }
                else
                {
                    if (Pawn.genes != null)
                    {
                        embryoPregnancy.father = Pawn;
                        BioUtility.ExtractGenesToGeneset(ref embryoPregnancy.genes, BioUtility.GetCanonicalPawnGenes(Pawn));
                    }
                }
            }
            try
            {
                Pawn.health.hediffSet.AddDirect(hediff);
            }
            catch (Exception exception)
            {
                if (Pawn.health.hediffSet.hediffs.Contains(hediff))
                {
                    MarkSpent();
                }
                Log.Error($"[XMT][Attachment] Embryo add failed on {Pawn}: {exception}");
                return;
            }
            if (!Pawn.health.hediffSet.hediffs.Contains(hediff))
            {
                Log.Error($"[XMT][Attachment] Embryo implantation failed on {Pawn}; facehugger remains unspent.");
                return;
            }
            MarkSpent();
            ResearchUtility.ProgressEvolutionTech(250, Pawn);
            if (Pawn.MapHeld != null)
            {
                foreach (Pawn pawn in Pawn.MapHeld.mapPawns.AllHumanlikeSpawned)
                {
                    if (XMTUtility.IsXenomorph(pawn))
                    {
                        XMTUtility.GiveMemory(pawn, HorrorMoodDefOf.HostImpregnated);
                    }
                }
            }

        }
    }

    public class HediffCompProperties_LarvalAttachment : HediffCompProperties_PawnAttachement
    {
        public float minimumTendToAvoidInjury;
        public float minimumTendToRemove = 1;
        public float severityPerDay;
        public HediffDef embryoHediff;
        public KnowledgeProfileDef attachKnowledgeProfile;
        public KnowledgeProfileDef acidKnowledgeProfile;
        public KnowledgeProfileDef tendKnowledgeProfile;
        public KnowledgeProfileDef squeezeKnowledgeProfile;
        public HediffCompProperties_LarvalAttachment()
        {
            attackIfRemoved = false;
            compClass = typeof(HediffComp_LarvalAttachment);
        }
    }
}
