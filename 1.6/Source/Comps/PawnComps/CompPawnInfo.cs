using AlienRace;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static AlienRace.AlienPartGenerator;

namespace Xenomorphtype
{
    public class CompPawnInfo : ThingComp
    {
        public DirectionalOffset headOffset
        {
            set
            {
                if (value == null)
                {
                    headOffset_north = Vector3.zero;
                    headOffset_south = Vector3.zero;
                    headOffset_east = Vector3.zero;
                    headOffset_west = Vector3.zero;
                }
                else
                {
                    headOffset_north.x = value.north.offset.x;
                    headOffset_north.y = value.north.layerOffset;
                    headOffset_north.z = value.north.offset.y;

                    headOffset_east.x = value.east.offset.x;
                    headOffset_east.y = value.east.layerOffset;
                    headOffset_east.z = value.east.offset.y;

                    headOffset_west.x = value.west.offset.x;
                    headOffset_west.y = value.west.layerOffset;
                    headOffset_west.z = value.west.offset.y;

                    headOffset_south.x = value.south.offset.x;
                    headOffset_south.y = value.south.layerOffset;
                    headOffset_south.z = value.south.offset.y;
                }
            }
        }
        public Vector3 headOffset_north;
        public Vector3 headOffset_south;
        public Vector3 headOffset_east;
        public Vector3 headOffset_west;
        public enum PheromoneType
        {
            Lover = 0,
            Friend,
            Threat,
            None
        }

        // Xenomorph Pheromones

        float _friendlyPheromone = 0;
        float _loverPheromone = 0;
        float _threatPheromone = 0;

        float _traumaRelief = 0;
        float LoverPheromone => _loverPheromone;
        float FriendlyPheromone => _friendlyPheromone;
        float ThreatPheromone => _threatPheromone;

        public bool extractJelly;
        public bool extractResin;
        public bool extractAcid;
        public PheromoneType StrongestPheromone
        {
            get
            {
                if (ThreatPheromone >= FriendlyPheromone && ThreatPheromone >= LoverPheromone)
                {
                    return PheromoneType.Threat;
                }

                if (FriendlyPheromone > LoverPheromone && FriendlyPheromone > ThreatPheromone)
                {
                    return PheromoneType.Friend;
                }

                if (LoverPheromone >= FriendlyPheromone && LoverPheromone >= ThreatPheromone)
                {
                    return PheromoneType.Lover;
                }

                return PheromoneType.None;
            }
        }
        float totalPheromone => LoverPheromone + FriendlyPheromone + ThreatPheromone;
        bool pheromonesPresent => LoverPheromone != 0 || FriendlyPheromone != 0 || ThreatPheromone != 0;

        bool isAware => ParentPawn != null && KnowledgeUtility.GetAssessment(ParentPawn).categories.Values.Any(value => value.effective > 0f);

        // Xenomorph Naivety System
        float ovomorphAwareness = 0;
        float larvaAwareness = 0;
        float horrorAwareness = 0;
        float acidAwareness = 0;
        float psychicAwareness = 0;
        float obsession = 0;
        List<PawnKnowledgeRecord> knowledgeRecords = new List<PawnKnowledgeRecord>();
        float cryptimorphTrauma;
        bool knowledgeMigrated;
        float Obsession => ParentPawn == null ? obsession : KnowledgeUtility.GetAssessment(ParentPawn).obsessionPressure;
        Pawn ParentPawn => parent as Pawn;

        internal float RawTrauma { get => cryptimorphTrauma; set => cryptimorphTrauma = Mathf.Max(0f, value); }
        internal float RawObsession { get => obsession; set => obsession = value; }
        internal PawnKnowledgeRecord RawKnowledge(KnowledgeCategoryDef category, bool create = false)
        {
            if (category == null) return null;
            knowledgeRecords ??= new List<PawnKnowledgeRecord>();
            PawnKnowledgeRecord record = knowledgeRecords.FirstOrDefault(entry => entry.category == category);
            if (record == null && create)
            {
                record = new PawnKnowledgeRecord { category = category };
                knowledgeRecords.Add(record);
            }
            return record;
        }

        public float OvomorphAwareness => ParentPawn == null ? 0f : KnowledgeUtility.GetEffectiveKnowledge(ParentPawn, KnowledgeDefOf.XMT_Knowledge_Ovomorph);
        public float LarvaAwareness => ParentPawn == null ? 0f : KnowledgeUtility.GetEffectiveKnowledge(ParentPawn, KnowledgeDefOf.XMT_Knowledge_Larva);
        public float HorrorAwareness => ParentPawn == null ? 0f : KnowledgeUtility.GetEffectiveKnowledge(ParentPawn, KnowledgeDefOf.XMT_Knowledge_Adult);
        public float AcidAwareness => ParentPawn == null ? 0f : KnowledgeUtility.GetEffectiveKnowledge(ParentPawn, KnowledgeDefOf.XMT_Knowledge_Acid);
        public float PsychicAwareness => ParentPawn == null ? 0f : KnowledgeUtility.GetEffectiveKnowledge(ParentPawn, KnowledgeDefOf.XMT_Knowledge_Psychic);

        public override float GetStatFactor(StatDef stat)
        {
            if(stat == StatDefOf.RestRateMultiplier)
            {
                float totalExperience = TotalHorrorTrauma();
                if (totalExperience > 0)
                {
                    if (!IsObsessed())
                    {
                        if(totalExperience > 4.0f)
                        {
                            return 0.5f;
                        }
                        else if (totalExperience >= 2.0f)
                        {
                            return 0.75f;
                        }
                        else if (totalExperience >= 1.0f)
                        {
                            return 0.85f;
                        }
                        else if (totalExperience > 0)
                        {
                            return 0.95f;
                        }
                    }
                }
            }
            return base.GetStatFactor(stat);
        }

        public override void CompTickInterval(int delta)
        {
            if (!parent.Spawned)
            {
                return;
            }

            if (parent.IsHashIntervalTick(2000))
            {
                if (parent is Pawn Parent)
                {
                    if(Parent.Swimming)
                    {
                        CleanPheramones(0.12f);
                    }

                    if(parent.MapHeld is Map map)
                    {
                        if (Parent.IsOutside())
                        {
                            WeatherDef weather = map.weatherManager.CurWeatherPerceived;

                            if (weather != null)
                            {

                                if (weather.rainRate > 0)
                                {
                                    CleanPheramones(0.12f * weather.rainRate);
                                }

                            }
                        }
                    }

                }
            }
        }
        public float IdeoReproductionModifier()
        {
            float modifier = 0;
            if (parent is Pawn Parent)
            {
                if (ModsConfig.IdeologyActive)
                {
                    if (Parent.Ideo is Ideo PawnIdeo)
                    {
                        if(PawnIdeo.HasMaxPreceptsForIssue(XenoPreceptDefOf.XMT_Reproduction))
                        {
                            modifier += 0.5f;
                        }
                    }
                }
            }
            return modifier;
        }
        public float IdeoAdultModifier()
        {
            float modifier = 0;
            if (parent is Pawn Parent)
            {
                if (ModsConfig.IdeologyActive)
                {
                    if(Parent.Ideo is Ideo PawnIdeo)
                    {
                        if(PawnIdeo.HasMaxPreceptsForIssue(XenoPreceptDefOf.XMT_Cryptobio))
                        {
                            modifier += 0.5f;
                        }
                    }
                }
            }
            return modifier;
        }
        public float TraitAwarenessModifier()
        {
            float modifier = 0;
            if (parent is Pawn Parent)
            {
                if (Parent?.story?.traits != null)
                {
                    if (Parent.story.traits.HasTrait(HorrorMoodDefOf.XMT_Survivor))
                    {
                        modifier = 1;
                    }
                }
            }
            return modifier;
        }

        public float TraitObsessionModifier()
        {
            float modifier = 0;
            if (parent is Pawn Parent)
            {
                if (Parent.story is Pawn_StoryTracker story)
                {
                    if (story.traits.HasTrait(TraitDefOf.Greedy))
                    {
                        modifier += 0.5f;
                    }
                }
                
                if (ModsConfig.IdeologyActive)
                {
                    if (Parent.Ideo is Ideo PawnIdeo)
                    {
                        foreach(Precept precept in PawnIdeo.PreceptsListForReading)
                        {
                            if(precept.def == XenoPreceptDefOf.XMT_Biomorph_Worship)
                            {
                                modifier += 2;
                            }
                            else if(precept.def == XenoPreceptDefOf.XMT_Biomorph_Study)
                            {
                                modifier += 1;
                            }
                            else if (precept.def == XenoPreceptDefOf.XMT_Biomorph_Abhorrent)
                            {
                                modifier -= 2;
                            }

                            if (precept.def == XenoPreceptDefOf.XMT_Parasite_Reincarnation)
                            {
                                modifier += 2;
                            }
                            else if (precept.def == XenoPreceptDefOf.XMT_Parasite_Revered)
                            {
                                modifier += 1;
                            }
                            else if(precept.def == XenoPreceptDefOf.XMT_Parasite_Abhorrent)
                            {
                                modifier -= 2;
                            }
                        }
                    }
                }
            }
            return modifier;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (parent is Pawn Parent)
            {
                PawnCacheWrapper.Recache(Parent);
                if (!respawningAfterLoad)
                { 
                    if(Parent.Faction == null)
                    {
                        return;
                    }

                    if(!Parent.Faction.IsPlayer)
                    {
                        return;
                    }

                    if(!ResearchUtility.HumanProjectsVisible())
                    {
                        float awareness = TotalHorrorAwareness();
                        if (IsObsessed() || awareness > 0)
                        {
                            ResearchUtility.ProgressCryptobioTech(10 + Mathf.FloorToInt(awareness * 500), Parent);
                        }
                    }
                }
            }

            
        }
        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref headOffset_north, "headOffset_north", Vector3.zero);
            Scribe_Values.Look(ref headOffset_south, "headOffset_south", Vector3.zero);
            Scribe_Values.Look(ref headOffset_east, "headOffset_east", Vector3.zero);
            Scribe_Values.Look(ref headOffset_west, "headOffset_west", Vector3.zero);

            Scribe_Values.Look(ref _loverPheromone, "LoverPheromone", 0);
            Scribe_Values.Look(ref _friendlyPheromone, "FriendlyPheromone", 0);
            Scribe_Values.Look(ref _threatPheromone, "ThreatPheromone", 0);
            Scribe_Values.Look(ref _traumaRelief, "TraumaRelief", 0);
            

            Scribe_Values.Look(ref ovomorphAwareness, "OvomorphAwareness", 0);
            Scribe_Values.Look(ref larvaAwareness, "LarvaAwareness", 0);
            Scribe_Values.Look(ref horrorAwareness, "HorrorAwareness", 0);
            Scribe_Values.Look(ref acidAwareness, "AcidAwareness", 0);
            Scribe_Values.Look(ref psychicAwareness, "psychicAwareness", 0);
            Scribe_Values.Look(ref obsession, "HorrorObsession", 0);
            Scribe_Collections.Look(ref knowledgeRecords, "CryptimorphKnowledge", LookMode.Deep);
            Scribe_Values.Look(ref cryptimorphTrauma, "CryptimorphTrauma", 0f);
            Scribe_Values.Look(ref knowledgeMigrated, "KnowledgeMigrated", false);

            Scribe_Values.Look(ref extractJelly, "extractJelly", false);
            Scribe_Values.Look(ref extractResin, "extractResin", false); 
            Scribe_Values.Look(ref extractAcid, "extractAcid", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                knowledgeRecords ??= new List<PawnKnowledgeRecord>();
                if (!knowledgeMigrated)
                {
                    MigrateLegacyKnowledge();
                }
                if (ParentPawn != null) KnowledgeUtility.Invalidate(ParentPawn);
            }
        }

        private void MigrateLegacyKnowledge()
        {
            MigrateLegacyCategory(KnowledgeDefOf.XMT_Knowledge_Ovomorph, ovomorphAwareness);
            MigrateLegacyCategory(KnowledgeDefOf.XMT_Knowledge_Larva, larvaAwareness);
            MigrateLegacyCategory(KnowledgeDefOf.XMT_Knowledge_Adult, horrorAwareness);
            MigrateLegacyCategory(KnowledgeDefOf.XMT_Knowledge_Acid, acidAwareness);
            MigrateLegacyCategory(KnowledgeDefOf.XMT_Knowledge_Psychic, psychicAwareness);
            cryptimorphTrauma = Mathf.Max(0f, ovomorphAwareness + larvaAwareness + horrorAwareness + acidAwareness + psychicAwareness - _traumaRelief);
            knowledgeMigrated = true;
        }

        private void MigrateLegacyCategory(KnowledgeCategoryDef category, float value)
        {
            if (category == null || value <= 0f) return;
            PawnKnowledgeRecord record = RawKnowledge(category, true);
            record.experienced = Mathf.Max(record.experienced, value);
        }

        public bool ShouldDisplay()
        {
            if (XMTUtility.PlayerXenosOnMap(parent.MapHeld))
            {
                return pheromonesPresent || isAware;
            }
            else if (totalPheromone > 0.1f)
            {
                return true;
            }

            return isAware;
        }

        public Color GetHediffColor()
        {
            bool IsPlayerXenomorph = XMTUtility.PlayerXenosOnMap(parent.MapHeld);

            if (IsPlayerXenomorph)
            {
                if (pheromonesPresent)
                {
                    switch (StrongestPheromone)
                    {
                        case PheromoneType.Lover:
                            return Color.magenta;
                        case PheromoneType.Friend:
                            return Color.green;
                        case PheromoneType.Threat:
                            return Color.red;
                    }
                }
            }
            return Color.white;
        }
        public string GetHediffLabel()
        {
            bool IsPlayerXenomorph = XMTUtility.PlayerXenosOnMap(parent.MapHeld);
            string output = "";

            bool hasSmell = false;
            if (IsPlayerXenomorph)
            {
                if(parent is Pawn Parent)
                {
                    if(XMTUtility.IsInorganic(Parent))
                    {
                        output += "XMT_Info_Inorganic".Translate();
                    }
                }

                if (pheromonesPresent)
                {
                    switch (StrongestPheromone)
                    {
                        case PheromoneType.Lover:
                            output += "XMT_Info_HiveLover".Translate();
                            break;
                        case PheromoneType.Friend:
                            output += "XMT_Info_HiveFriend".Translate();
                            break;
                        case PheromoneType.Threat:
                            output += "XMT_Info_HiveEnemy".Translate();
                            break;
                    }
                    hasSmell = true;
                }
            }
            else
            {
                float severity = totalPheromone;

                if (severity >= 1)
                {
                    hasSmell = true;
                    output += "XMT_Info_Stinky".Translate();
                }
                else if (severity >= 0.75f)
                {
                    hasSmell = true;
                    output += "XMT_Info_Smelly".Translate();
                }
                else if (severity >= 0.25f)
                {
                    hasSmell = true;
                    output += "XMT_Info_Cloying".Translate();
                }
                else if (severity >= 0.1f)
                {
                    hasSmell = true;
                    output += "XMT_Info_Odor".Translate();
                }

            }

            if (hasSmell && isAware)
            {
                output += "XMT_Info_And".Translate();
            }

            if (isAware)
            {
                KnowledgeAssessment assessment = KnowledgeUtility.GetAssessment(ParentPawn);
                if (assessment.obsessionSeverity != ObsessionSeverity.None)
                {
                    output += ("XMT_Obsession_" + assessment.obsessionSeverity).Translate();
                }
                else
                {
                    output += assessment.traumaSeverity == TraumaSeverity.None ? "XMT_Info_Informed".Translate() : ("XMT_Trauma_" + assessment.traumaSeverity).Translate();
                }
            }
            return output;
        }

        public string GetHediffDescription()
        {
            string output = "";
            bool IsPlayerXenomorph = XMTUtility.PlayerXenosOnMap(parent.MapHeld);
            if (IsPlayerXenomorph && pheromonesPresent)
            {
                switch (StrongestPheromone)
                {
                    case PheromoneType.Lover:
                        output += "XMT_Info_Desc_HiveLover".Translate();
                        break;
                    case PheromoneType.Friend:
                        output += "XMT_Info_Desc_HiveFriend".Translate();
                        break;
                    case PheromoneType.Threat:
                        output += "XMT_Info_Desc_HiveEnemy".Translate();
                        break;
                }

                if (DebugSettings.ShowDevGizmos)
                {
                    output += "\n DEV lover smell: " + LoverPheromone;
                    output += "\n DEV friend smell: " + FriendlyPheromone;
                    output += "\n DEV threat smell: " + ThreatPheromone + "\n";
                }
            }
            else
            {
                if (totalPheromone >= 1)
                {
                    output += "XMT_Info_Desc_Stinky".Translate();
                }
                else if (totalPheromone >= 0.75f)
                {
                    output += "XMT_Info_Desc_Smelly".Translate();
                }
                else if (totalPheromone >= 0.25f)
                {
                    output += "XMT_Info_Desc_Cloying".Translate();
                }
                else if (totalPheromone >= 0.1f)
                {
                    output += "XMT_Info_Desc_Odor".Translate();
                }
            }

            if (DebugSettings.ShowDevGizmos)
            {
                output += "\nDEV total pheromone value: " + XenomorphPheromoneValue() + "\n";
            }

            if (isAware)
            {
                KnowledgeAssessment assessment = KnowledgeUtility.GetAssessment(ParentPawn);
                foreach (CategoryKnowledgeAssessment category in assessment.categories.Values.Where(value => value.level != KnowledgeLevel.None).OrderBy(value => value.category.defName))
                {
                    string key = assessment.obsessed && !category.category.obsessedDescriptionKey.NullOrEmpty()
                        ? category.category.obsessedDescriptionKey
                        : category.experienced > 0f ? category.category.experiencedDescriptionKey : category.category.learnedDescriptionKey;
                    if (!key.NullOrEmpty()) output += key.Translate(("XMT_KnowledgeLevel_" + category.level).Translate());
                    if (DebugSettings.ShowDevGizmos) output += "\nDEV " + category.category.defName + ": learned " + category.learned + ", experienced " + category.experienced + ", effective " + category.effective + "\n";
                }
                if (DebugSettings.ShowDevGizmos) output += "\nDEV Trauma: " + assessment.trauma + "; obsession balance: " + assessment.obsessionBalance;
            }
            return output;
        }

        public void TryApplyDisplayHediff()
        {
            if (!parent.Spawned)
            {
                return;
            }

            Pawn Parent = parent as Pawn;
            if (Parent != null)
            {
                if (!XMTUtility.PlayerXenosOnMap(Parent.MapHeld))
                {
                    if (parent.Faction == null)
                    {
                        return;
                    }
                    if (parent.Faction.IsPlayer)
                    {
                        Parent.health.GetOrAddHediff(InternalDefOf.PawnInfoHediff);
                    }
                }
                if (ShouldDisplay()) Parent.health.GetOrAddHediff(InternalDefOf.PawnInfoHediff);
            }
        }

        public void TryRemoveDisplayHediff()
        {
            Pawn Parent = parent as Pawn;
            if (Parent != null)
            {
                DisplayHediff display = Parent.health.hediffSet.GetFirstHediff<DisplayHediff>();
                if (display == null)
                {
                    return;
                }
                
                if (Parent.Faction == null)
                {
                    display.improperRemoval = false;
                    Parent.health.RemoveHediff(display);
                    return;
                }

                if (Parent.Faction.IsPlayer)
                {
                    return;
                }
                display.improperRemoval = false;
                Parent.health.RemoveHediff(display);
            }
        }

        public void WitnessPsychicHorror(float strength, float maxAwareness = 1.0f)
        {
            KnowledgeUtility.ApplyExposure(ParentPawn, KnowledgeDefOf.XMT_Profile_Psychic, strength, KnowledgeAcquisition.PsychicExposure, parent);
        }

        public void WitnessAcidHorror(float strength, float maxAwareness = 1.0f)
        {
            KnowledgeUtility.ApplyExposure(ParentPawn, KnowledgeDefOf.XMT_Profile_Acid, strength, KnowledgeAcquisition.TraumaticExperience, parent);
        }
        public void WitnessOvomorphHorror(float strength, float maxAwareness = 1.0f)
        {
            KnowledgeUtility.ApplyExposure(ParentPawn, KnowledgeDefOf.XMT_Profile_Ovomorph, strength, KnowledgeAcquisition.TraumaticExperience, parent);
        }

        public void WitnessLarvaHorror(float strength, float maxAwareness = 1.0f)
        {
            KnowledgeUtility.ApplyExposure(ParentPawn, KnowledgeDefOf.XMT_Profile_Larva, strength, KnowledgeAcquisition.TraumaticExperience, parent);
        }

        public void WitnessHorror(float strength, float maxAwareness = 1.0f)
        {
            KnowledgeUtility.ApplyExposure(ParentPawn, KnowledgeDefOf.XMT_Profile_Adult, strength, KnowledgeAcquisition.TraumaticExperience, parent);
        }

        public void GainObsession(float strength)
        {
            KnowledgeUtility.GainObsession(ParentPawn, strength);
        }

        public void GainRelief(float strength, float max = 5f)
        {
            KnowledgeUtility.RelieveTrauma(ParentPawn, Mathf.Min(max, strength));
        }

        public float TotalHorrorTrauma()
        {
            return ParentPawn == null ? 0f : KnowledgeUtility.GetTrauma(ParentPawn);
        }
        public float TotalHorrorAwareness()
        {
            return ParentPawn == null ? 0f : KnowledgeUtility.GetTotalEffectiveKnowledge(ParentPawn);
        }

        public bool IsObsessed(out float awareness)
        {
            return KnowledgeUtility.IsObsessed(ParentPawn, out awareness);
        }
        public bool IsObsessed()
        {
            return KnowledgeUtility.IsObsessed(ParentPawn);
        }

        public float XenomorphPheromoneValue()
        {
            float TotalPositivePheromone = LoverPheromone + FriendlyPheromone;
            TotalPositivePheromone -= ThreatPheromone;

            return TotalPositivePheromone;
        }
        public bool IsXenomorphFriendly()
        {
            float TotalPositivePheromone = XenomorphPheromoneValue();

            if (parent.MapHeld != null)
            {
                if (parent.MapHeld.designationManager.DesignationOn(parent) is Designation designation)
                {
                    if (designation.def == XenoWorkDefOf.XMT_Friend)
                    {
                        TotalPositivePheromone += 10;
                    }
                    else if (designation.def == XenoWorkDefOf.XMT_Enemy)
                    {
                        TotalPositivePheromone -= 10;
                    }
                }
            }
            return TotalPositivePheromone > 0;
        }

        public void CleanPheramones(float cleanAmount = 0.25f)
        {
            _loverPheromone = Mathf.Max(0, LoverPheromone - cleanAmount);
            _friendlyPheromone = Mathf.Max(0, FriendlyPheromone - cleanAmount);
            _threatPheromone = Mathf.Max(0, ThreatPheromone - cleanAmount);
        }

        public void ReduceHygiene(float amount)
        {
            Pawn pawn = parent as Pawn;

            if (pawn != null)
            {
                if (pawn.needs != null)
                {
                    Need hygiene = pawn.needs.TryGetNeed(ExternalDefOf.Hygiene);
                    if (hygiene != null)
                    {
                        hygiene.CurLevel = hygiene.CurLevel - (amount * 0.5f);
                    }
                }
            }
        }

        public void ApplyThreatPheromone(Thing victim, float amount = 0.5f, float maxStrength = 1f, float radius = 5)
        {

            _threatPheromone = Mathf.Min(ThreatPheromone + amount, maxStrength);

            ReduceHygiene(amount);
            TryApplyDisplayHediff();

            
            XMTUtility.ThreatResponse(victim, this, radius);

        }
        public void ApplyFriendlyPheromone(Pawn partner, float amount = 0.25f, float maxStrength = 0.25f)
        {

            _friendlyPheromone = Mathf.Min(_friendlyPheromone + amount, maxStrength);

            ReduceHygiene(amount);
            TryApplyDisplayHediff();
        }

        public void ApplyLoverPheromone(Pawn partner, float amount = 0.75f, float maxStrength = 0.75f)
        {

            _loverPheromone = Mathf.Min(LoverPheromone + amount, maxStrength);

            ReduceHygiene(amount);
            TryApplyDisplayHediff();
        }

        public override void Notify_AbandonedAtTile(PlanetTile tile)
        {
            base.Notify_AbandonedAtTile(tile);
            XenoformingUtility.HandleXenoformingImpact(parent);
        }
    }
}
