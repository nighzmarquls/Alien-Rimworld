using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    public class HediffComp_EmbryoPregnancy : HediffComp_SeverityModifierBase
    {
        public GeneSet genes;
        public Pawn mother;
        public Pawn father;
        public bool unbirthed = true;
        float damageDealt = 0;
        public Pawn Host;

        HediffCompProperties_EmbryoPregnancy Props => props as HediffCompProperties_EmbryoPregnancy;
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref father, "father", saveDestroyedThings: false);
            Scribe_References.Look(ref mother, "mother", saveDestroyedThings: false);
            Scribe_References.Look(ref Host, "host", saveDestroyedThings: false);
            Scribe_Deep.Look(ref genes, "genes");
            Scribe_Values.Look(ref unbirthed, "unbirthed", defaultValue: true);
            Scribe_Values.Look(ref damageDealt, "chestburstDamage", defaultValue: 0);
        }

        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove)
                {
                    return true;
                }

                if (!unbirthed)
                {
                    return !unbirthed;
                }
                return false;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {

            base.CompPostTick(ref severityAdjustment);
            if (parent.Severity >= 1.0f && unbirthed)
            {
                if (parent.pawn.IsHashIntervalTick(60))
                {
                    TryBirth();
                }
            }
            else if (parent.pawn.IsHashIntervalTick(60))
            {
                IEnumerable<HediffComp_BuildingMorphing> morphers = Pawn.health.hediffSet.GetHediffComps<HediffComp_BuildingMorphing>();
                if (morphers.Any())
                {
                    Pawn.health.RemoveHediff(morphers.First().parent);
                }
            }

        }

        public override void Notify_Spawned()
        {
            base.Notify_Spawned();
            if (parent != null)
            {
                if (parent.pawn.MapHeld != null)
                {
                    
                    HiveUtility.AddImplantedHosts(parent.pawn, parent.pawn.MapHeld);
                    Host = parent.pawn;

                }
            }
        }
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (Host != null)
            {
                if (Host.MapHeld != null)
                {
                   
                    HiveUtility.RemoveImplantedHosts(Host, Host.MapHeld);

                    if(unbirthed)
                    {
                        float maturity = CalculateMaturity();
                        float childAge = 0;
                        XMTUtility.GetLifeStageByEmbryoMaturity(maturity, out childAge);
                        SpawnChild(childAge, maturity);
                    }
                    Host = null;
                }
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (unbirthed)
            {
                TryBirth();
            }
        }

        protected bool TryBirth()
        {
            PawnKindDef ChildKind = InternalDefOf.XMT_StarbeastKind;

            IEnumerable<BodyPartRecord> coreparts = from x in base.Pawn.health.hediffSet.GetNotMissingParts()
                                                    where
                                                    x.IsInGroup(BodyPartGroupDefOf.Torso) || x.IsCorePart
                                                    && x != parent.Part
                                                    select x;

            float maturity = CalculateMaturity();
            float childAge = 0;
            LifeStageDef ChildLifeStage = XMTUtility.GetLifeStageByEmbryoMaturity(maturity, out childAge);

            float relativeSize = (ChildKind.RaceProps.baseBodySize * ChildLifeStage.bodySizeFactor) / base.Pawn.BodySize;

            float ChestBurstTarget = Props.burstDamage*40*base.Pawn.BodySize;

            DamageDef damageType = DamageDefOf.Crush;
            float ArmorPenetration = 2f;
            DamageWorker.DamageResult result = base.Pawn.TakeDamage(new DamageInfo(damageType, ChestBurstTarget * relativeSize, ArmorPenetration, -1, null, coreparts.RandomElement<BodyPartRecord>()));
            damageDealt += result.totalDamageDealt;

            if (!base.Pawn.Dead)
            {
                XMTUtility.WitnessHorror(Pawn.PositionHeld, Pawn.MapHeld, 0.1f);
            }

            if ((damageDealt >= ChestBurstTarget || base.Pawn.Dead) && unbirthed)
            {
                SpawnChild(childAge, maturity);
                return true;
            }
            else
            {
                return false;
            }
        }

        protected void SpawnChild(float Age, float maturity)
        {

            if (!unbirthed)
            {
                return;
            }
            
            unbirthed = false;

            float headMaturity = maturity * Props.headMaturationRate;
            float coreMaturity = maturity * Props.coreMaturationRate;
            float legMaturity = maturity * Props.legMaturationRate;
            float armMaturity = maturity * Props.armMaturationRate;

            Faction     ChildFaction = (XMTUtility.PlayerXenosOnMap(parent.pawn.MapHeld)) ? Faction.OfPlayer : (mother != null) ? mother.Faction : null ;

            if (ModsConfig.IdeologyActive)
            {
                if(Pawn.Ideo is Ideo hostIdeo)
                {
                    if (hostIdeo.HasPrecept(XenoPreceptDefOf.XMT_Parasite_Reincarnation))
                    {
                        ChildFaction = Pawn.Faction;
                    }
                }
            }

            PawnGenerationRequest request = new PawnGenerationRequest(
                InternalDefOf.XMT_StarbeastKind, faction: ChildFaction, PawnGenerationContext.PlayerStarter,-1,true,false,true,false,false,0,false,true,false,false,false,false,false,false,true,0,0,null,0,null,null,null,null,0,Age,0,Gender.Female,null);

            request.ForceNoIdeo = true;
            request.ForceNoBackstory = true;
            request.ForceNoGear = true;
            request.ForceBaselinerChance = 1000;
            request.ForcedXenotype = XenotypeDefOf.Baseliner;

            Pawn child = PawnGenerator.GeneratePawn(request);

            if(child.story != null)
            {
                child.story.Childhood = XMTUtility.GetChildBackstory(maturity, Pawn.MapHeld, ChildFaction);
            }

            if (child?.needs?.food != null)
            {
                child.needs.food.CurLevel = 0.0f;
            }

            if (child.apparel != null)
            {
                child.apparel.DestroyAll();
            }

            if (child.relations != null)
            {
                if (mother != null)
                {
                    child.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                }

                if (father != null && father != mother)
                {
                    if (father.gender == Gender.Male && father.relations != null)
                    {
                        child.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);
                    }  
                }
                child.relations.AddDirectRelation(PawnRelationDefOf.ParentBirth, Pawn);
                Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Parasite_Birth, Pawn.Named(HistoryEventArgsNames.Doer), child.Named(HistoryEventArgsNames.Victim)), true);

            }

            if (genes == null)
            {
                genes = new GeneSet();
            }

            BioUtility.ClearGenes(ref child);
            child.genes.SetXenotype(XenotypeDefOf.Baseliner);
            BioUtility.ExtractGenesToGeneset(ref genes, InternalDefOf.XMT_Starbeast_AlienRace.alienRace.raceRestriction.geneList);
            BioUtility.ExtractGenesToGeneset(ref genes, BioUtility.GetExtraHostGenes(Pawn));
           
            if (Pawn.genes != null)
            {
                if(Pawn.genes.xenotypeName != null)
                {
                    child.genes.xenotypeName = Pawn.genes.xenotypeName;
                }
                BioUtility.ExtractCryptimorphGenesToGeneset(ref genes, Pawn.genes.GenesListForReading);
            }

            if (headMaturity >= 0.8f)
            {
                IEnumerable<BodyPartRecord> tailparts = from x in child.health.hediffSet.GetNotMissingParts()
                                                        where
                                                        x.IsInGroup(InternalDefOf.StarbeastTailAttackTool)
                                                        select x;
                foreach (BodyPartRecord tailpart in tailparts)
                {
                    Hediff maturityHediff = HediffMaker.MakeHediff(InternalDefOf.Overdeveloped, child, tailpart);
                    maturityHediff.Severity = 0.001f;

                    child.health.AddHediff(maturityHediff);
                }
            }

            if (headMaturity < 1)
            {
                IEnumerable<BodyPartRecord> headparts = from x in child.health.hediffSet.GetNotMissingParts()
                                                     where
                                                     x.IsInGroup(BodyPartGroupDefOf.FullHead)
                                                     || x.IsInGroup(ExternalDefOf.Neck)
                                                     select x;

                foreach(BodyPartRecord part in headparts)
                {
                   Hediff maturityHediff = HediffMaker.MakeHediff(InternalDefOf.Undeveloped, child, part);
                   maturityHediff.Severity = headMaturity;

                   child.health.AddHediff(maturityHediff);

                }
            }

            if(coreMaturity < 1)
            {
                IEnumerable<BodyPartRecord> coreparts = from x in child.health.hediffSet.GetNotMissingParts()
                                                     where
                                                     x.IsInGroup(BodyPartGroupDefOf.Torso)
                                                     && !x.IsInGroup(InternalDefOf.StarbeastTailAttackTool)
                                                        select x;

                foreach (BodyPartRecord part in coreparts)
                {
                    Hediff maturityHediff = HediffMaker.MakeHediff(InternalDefOf.Undeveloped, child, part);
                    maturityHediff.Severity = coreMaturity;

                    child.health.AddHediff(maturityHediff);
                }
            }

            if(legMaturity < 1)
            {
                IEnumerable<BodyPartRecord> legparts = from x in child.health.hediffSet.GetNotMissingParts()
                                                     where
                                                     x.IsInGroup(BodyPartGroupDefOf.Legs)
                                                     && !x.IsInGroup(InternalDefOf.StarbeastTailAttackTool)
                                                       select x;

                foreach( BodyPartRecord part in legparts)
                {
                    Hediff maturityHediff = HediffMaker.MakeHediff(InternalDefOf.Undeveloped, child, part);
                    maturityHediff.Severity = legMaturity;

                    child.health.AddHediff(maturityHediff);
                }
            }

            if(armMaturity < 1)
            {
                IEnumerable<BodyPartRecord> armparts = from x in child.health.hediffSet.GetNotMissingParts()
                                                     where
                                                     (x.IsInGroup(ExternalDefOf.Shoulders)
                                                     || x.IsInGroup(ExternalDefOf.Arms)
                                                     || x.IsInGroup(ExternalDefOf.Hands))
                                                     && !x.IsInGroup(InternalDefOf.StarbeastTailAttackTool)
                                                     select x;
                foreach(BodyPartRecord part in armparts)
                {
                    Hediff maturityHediff = HediffMaker.MakeHediff(InternalDefOf.Undeveloped, child, part);
                    maturityHediff.Severity = armMaturity;

                    child.health.AddHediff(maturityHediff);
                }
            }

            BioUtility.InsertGenesetToPawn(genes,ref child);

            BioUtility.InheritNonGenes(Pawn, ref child);

            HiveUtility.ChestburstBirth(child, mother);

            XMTUtility.TrySpawnPawnFromTarget(child, Pawn);

            if (child.Map != null)
            {
                XMTUtility.WitnessHorror(child.PositionHeld, child.MapHeld, 0.5f);
            }

            int progress = 250;
            ResearchUtility.ProgressEvolutionTech(progress, child);

            child.GetMorphComp().UpdateSkinByAge();

            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(XenoPreceptDefOf.XMT_Parasite_Birth, Pawn.Named(HistoryEventArgsNames.Doer), child.Named(HistoryEventArgsNames.Victim)), true);


            if (Pawn.Dead)
            {
                Pawn.health.RemoveHediff(parent);
            }
            else
            {
                XMTUtility.GiveInteractionMemory(Pawn, HorrorMoodDefOf.Chestburst, child);
            }
            
        }

        public float CalculateMaturity()
        {
            return Math.Max(parent.pawn.BodySize * parent.Severity, 0.001f);
        }

        public override float SeverityChangePerDay()
        {
            if(genes != null)
            {
                return (Props.severityPerDay / ((genes.ComplexityTotal + 1.0f) / 9.0f)) * XMTSettings.MaturationFactor;
            }

            return (Props.severityPerDay) * XMTSettings.MaturationFactor;
        }
    }

    public class HediffCompProperties_EmbryoPregnancy : HediffCompProperties
    {
        public float armMaturationRate;
        public float legMaturationRate;
        public float coreMaturationRate;
        public float headMaturationRate;

        public float burstDamage;
        public float severityPerDay;
        public HediffCompProperties_EmbryoPregnancy()
        {
            compClass = typeof(HediffComp_EmbryoPregnancy);
        }
    }
}

