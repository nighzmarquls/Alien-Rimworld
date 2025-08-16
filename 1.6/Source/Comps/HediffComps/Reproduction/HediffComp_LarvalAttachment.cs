﻿using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Xenomorphtype
{
    internal class HediffComp_LarvalAttachment : HediffComp_SeverityModifierBase
    {
        public GeneSet genes;
        public Pawn mother;
        public Pawn father;
        public PawnKindDef kind;
        public bool spent;
        public bool removed;
        public string name;
        public float age;
       
        public HediffCompProperties_LarvalAttachment Props => (HediffCompProperties_LarvalAttachment)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref father, "father", saveDestroyedThings: false);
            Scribe_References.Look(ref mother, "mother", saveDestroyedThings: false);
            Scribe_Deep.Look(ref genes, "genes");
            Scribe_Defs.Look(ref kind, "kind");
            Scribe_Values.Look(ref spent, "spent", defaultValue: false);
            Scribe_Values.Look(ref removed, "removed", defaultValue: false);
            Scribe_Values.Look(ref name, "name", defaultValue: "larva");
            Scribe_Values.Look(ref age, "age", defaultValue: 0.0f);

        }
        public override bool CompShouldRemove
        {
            get
            {
                if (base.CompShouldRemove)
                {
                    return true;
                }
                return removed;
            }
        }

        public override void CompPostMake()
        {
            base.CompPostMake();

            HiveUtility.RemoveHost(parent.pawn, parent.pawn.Map);
            CompPawnInfo info = Pawn.GetComp<CompPawnInfo>();
            XMTUtility.WitnessLarva(parent.pawn.PositionHeld, parent.pawn.MapHeld, 0.25f, 1f);
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
         
            if (parent.Severity >= 0.75f && !spent)
            {
                ImplantEmbryo();

            }else if(parent.Severity >= 1.0f)
            {
                LarvaRelease();
            }
            else if (XMTUtility.IsMorphing(Pawn))
            {
                LarvaRelease();
            }
        }

        public override float SeverityChangePerDay()
        {
            return Props.severityPerDay;
        }
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            LarvaRelease();
            base.Pawn.health.RemoveHediff(parent);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            if (spent)
            {
                return;
            }

            if (Pawn.MapHeld == null)
            {
                return;
            }

            float larvaBonus = 0;
            float acidBonus = 0;

            if (!Pawn.Dead)
            {
                XMTUtility.WitnessLarva(Pawn.PositionHeld, Pawn.MapHeld, 0.1f, out larvaBonus, 1f);
                XMTUtility.WitnessAcid(Pawn.PositionHeld, Pawn.MapHeld, 0.1f, out acidBonus, 1f);


                if (!spent)
                {
                    if (larvaBonus < 1)
                    {
                        LarvaSqueeze(8f);
                    }
                }
            }


            if (LarvaRelease() is Pawn larvaPawn)
            {
                if (larvaBonus > 0.5f)
                {
                    larvaPawn.health.AddHediff(HediffDefOf.Anesthetic);
                }

                if (acidBonus < 1 )
                {
                    CompAcidBlood acid = larvaPawn.TryGetComp<CompAcidBlood>();

                    if (acid != null)
                    {
                        acid.TrySplashAcid();
                    }
                }
            }
        }
        public override void CompTended(float quality, float maxQuality, int batchPosition = 0)
        {
            if (Pawn.IsWorldPawn())
            {
                if(spent)
                {
                    return;
                }

                spent = true;
                if (XMTSettings.LogWorld)
                {
                    Log.Message(Pawn + " has a larva attached and will develop an embryo.");
                }
                XenoformingUtility.ReleaseEmbryoOnWorld(Pawn);
                return;
            }
            base.CompTended(quality, maxQuality, batchPosition);
            float witnessBonus;
            bool XenomorphTending = XMTUtility.WitnessLarva(parent.pawn.PositionHeld, parent.pawn.MapHeld, 0.1f, out witnessBonus, 1f);

            if (XenomorphTending)
            {
                //xenomorphs just make it worse.
                parent.Severity += 0.25f;
            }
            else if (parent.CurStageIndex < 1 || spent)
            {
                //if you catch it early enough or too late you can get the thing off.
                LarvaRelease();
            }
            else if (parent.CurStageIndex < 2 && quality + witnessBonus >= Props.minimumTendToRemove)
            {
                //Larva removed before implantation but may still kill host.
                //Experience with Larva will mitigate this.
                if (witnessBonus < 1)
                {
                    LarvaSqueeze(8f);
                }
                LarvaRelease();
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
                Log.Message(parent + " is squeezing ");
            }
            XMTUtility.WitnessLarva(Pawn.PositionHeld, Pawn.Map, 1f);

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
                foreach (BodyPartRecord part in source)
                {
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

            if (XMTSettings.LogBiohorror)
            {
                Log.Message(parent + " is releasing ");
            }

            if (removed)
            {
                return null;
            }

            Faction spawnFaction = (mother != null && !spent) ? mother.Faction : null;
            PawnGenerationRequest request = new PawnGenerationRequest(kind, spawnFaction);
            request.FixedBiologicalAge = age;

  
            Pawn larva = PawnGenerator.GeneratePawn(request);

            CompLarvalGenes larvalGenes = larva.GetComp<CompLarvalGenes>();
 
            if (larvalGenes != null)
            {
                larvalGenes.mother = mother;
                larvalGenes.father = father;
                larvalGenes.genes = genes;
                larvalGenes.spent = spent;
                larva.Named(name);

                removed = true;

                return XMTUtility.TrySpawnPawnFromTarget(larva, Pawn);
            }

            return null;
           
        }

        protected void ImplantEmbryo()
        {
            if(spent)
            {
                return;
            }

            foreach(Pawn pawn in parent.pawn.MapHeld.mapPawns.AllHumanlikeSpawned)
            {
                if(XMTUtility.IsXenomorph(pawn))
                {
                    XMTUtility.GiveMemory(pawn, HorrorMoodDefOf.HostImpregnated);
                }    
            }

            Hediff hediff = BioUtility.MakeEmbryoPregnancy(Pawn, Props.embryoHediff);
            HediffComp_EmbryoPregnancy embryoPregnancy = hediff.TryGetComp<HediffComp_EmbryoPregnancy>();

            if (embryoPregnancy != null)
            {
                embryoPregnancy.genes = genes;

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
                            
                            BioUtility.ExtractCryptimorphGenesToGeneset(ref embryoPregnancy.genes, Pawn.genes.GenesListForReading);
                        }
                    }
                }
                else
                {
                    if (Pawn.genes != null)
                    {
                        embryoPregnancy.father = Pawn;
                        BioUtility.ExtractCryptimorphGenesToGeneset(ref embryoPregnancy.genes, Pawn.genes.GenesListForReading);
                    }
                }
            }
            int progress = 250;
            XMTResearch.ProgressEvolutionTech(progress, Pawn);
            Pawn.health.hediffSet.AddDirect(hediff);
            spent = true;

        }
    }

    public class HediffCompProperties_LarvalAttachment : HediffCompProperties
    {
        public float minimumTendToAvoidInjury;
        public float minimumTendToRemove = 1;
        public float severityPerDay;
        public HediffDef embryoHediff;
        public HediffCompProperties_LarvalAttachment()
        {
            compClass = typeof(HediffComp_LarvalAttachment);
        }
    }
}
