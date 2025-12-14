using RimWorld;
using RimWorld.Planet;
using System;
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

        bool isAware => OvomorphAwareness != 0 || larvaAwareness != 0 || horrorAwareness != 0;

        // Xenomorph Naivety System
        float ovomorphAwareness = 0;
        float larvaAwareness = 0;
        float horrorAwareness = 0;
        float acidAwareness = 0;
        float psychicAwareness = 0;
        float obsession = 0;
        float Obsession => obsession + TraitObsessionModifier();

        public float OvomorphAwareness => ovomorphAwareness + TraitAwarenessModifier() + IdeoReproductionModifier();
        public float LarvaAwareness => larvaAwareness + TraitAwarenessModifier() + IdeoReproductionModifier();
        public float HorrorAwareness => horrorAwareness + TraitAwarenessModifier() + IdeoAdultModifier();
        public float AcidAwareness => acidAwareness + TraitAwarenessModifier() + IdeoAdultModifier();

        public float PsychicAwareness => psychicAwareness;

        public override float GetStatFactor(StatDef stat)
        {
            if(stat == StatDefOf.RestRateMultiplier)
            {
                float totalExperience = TotalHorrorExperience();
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

                    if (_traumaRelief > TotalHorrorExperience())
                    {
                        //Log.Message(Parent + " still has " + _traumaRelief + " relief");
                        _traumaRelief += 0.001f;
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
                    foreach(BackstoryHorror horror in XenoStoryDefOf.XMT_ObsessedBackstories.backstories)
                    {
                        if(story.AllBackstories.Contains(horror.backstory))
                        {
                            modifier += horror.obsession;
                        }
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

            Scribe_Values.Look(ref extractJelly, "extractJelly", false);
            Scribe_Values.Look(ref extractResin, "extractResin", false);
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
                    output += "";
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
                if (IsObsessed())
                {
                    output += "XMT_Obsessed".Translate();
                }
                else
                {
                    float awareness = TotalHorrorAwareness();

                    if (awareness >= 1)
                    {
                        output += "XMT_Info_Traumatized".Translate();
                    }
                    else if (awareness >= 0.5f)
                    {
                        output += "XMT_Info_Paranoid".Translate();
                    }
                    else if (awareness >= 0.25f)
                    {
                        output += "XMT_Info_Anxious".Translate();
                    }
                    else
                    {
                        output += "XMT_Info_Concern".Translate();
                    }
                }
            }
            return output;
        }

        public string GetHediffDescription()
        {
            string output = "";
            bool IsPlayerXenomorph = XMTUtility.PlayerXenosOnMap(parent.MapHeld);
            if (IsPlayerXenomorph)
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
                output += "\nDEV total pheromone value: " + totalPheromone + "\n";
            }

            if (isAware)
            {
                bool obsessed = IsObsessed();
                if (OvomorphAwareness > 0)
                {
                    output += obsessed ? "XMT_Info_Ovomorph_Obsessed".Translate() : "XMT_Info_Ovomorph".Translate();
                    if (DebugSettings.ShowDevGizmos)
                    {
                        output += "\nDEV Ovomorph Awareness: " + OvomorphAwareness + "\n";
                    }
                }
                if (LarvaAwareness > 0)
                {
                    output += obsessed ? "XMT_Info_Larva_Obsessed".Translate() : "XMT_Info_Larva".Translate();
                    if (DebugSettings.ShowDevGizmos)
                    {
                        output += "\nDEV Larva Awareness: " + LarvaAwareness + "\n";
                    }
                }
                if (HorrorAwareness > 0)
                {
                    output += obsessed ? "XMT_Info_Horror_Obsessed".Translate() : "XMT_Info_Horror".Translate();
                    if (DebugSettings.ShowDevGizmos)
                    {
                        output += "\nDEV Cryptimorph Awareness: " + HorrorAwareness + "\n";
                    }
                }
                if (AcidAwareness > 0)
                {
                    output += obsessed ? "XMT_Info_Acid_Obsessed".Translate() : "XMT_Info_Acid".Translate();
                    if (DebugSettings.ShowDevGizmos)
                    {
                        output += "\nDEV Acid Awareness: " + AcidAwareness + "\n";
                    }
                }

                if (PsychicAwareness > 0)
                {
                    output += obsessed ? "XMT_Info_Psychic_Obsessed".Translate() : "XMT_Info_Psychic".Translate();
                    if (DebugSettings.ShowDevGizmos)
                    {
                        output += "\nDEV Psychic Awareness: " + PsychicAwareness +"\n";
                    }
                }

                if(DebugSettings.ShowDevGizmos)
                {
                    output += "\nDEV Total Awareness: " + TotalHorrorAwareness();
                }
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
                Parent.health.GetOrAddHediff(InternalDefOf.PawnInfoHediff);
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
                    Parent.health.RemoveHediff(display);
                    return;
                }

                if (Parent.Faction.IsPlayer)
                {
                    return;
                }

                Parent.health.RemoveHediff(display);
            }
        }

        public void WitnessPsychicHorror(float strength, float maxAwareness = 1.0f)
        {
            _traumaRelief -= strength;
            if (PsychicAwareness >= maxAwareness)
            {
                return;
            }
            _traumaRelief = 0;
            psychicAwareness = Mathf.Min(psychicAwareness + strength, maxAwareness);

            if (!Rand.Chance(psychicAwareness))
            {
                TryApplyDisplayHediff();
                return;
            }

            float maxPsychicBleed = maxAwareness / 2;
            float psychicBleed = strength / 2;

            if (AcidAwareness < maxPsychicBleed)
            {
                WitnessAcidHorror(psychicBleed, maxPsychicBleed);
            }

            if (HorrorAwareness < maxPsychicBleed)
            {
                WitnessHorror(psychicBleed, maxPsychicBleed);
            }

            if (LarvaAwareness < maxPsychicBleed)
            {
                WitnessLarvaHorror(psychicBleed, maxPsychicBleed);
            }

            if (OvomorphAwareness < maxPsychicBleed)
            {
                WitnessOvomorphHorror(psychicBleed, maxPsychicBleed);
            }

            TryApplyDisplayHediff();

        }

        public void WitnessAcidHorror(float strength, float maxAwareness = 1.0f)
        {
            if (AcidAwareness >= maxAwareness)
            {
                return;
            }
            _traumaRelief = 0;
            acidAwareness = Mathf.Min(acidAwareness + strength, maxAwareness);

            if (OvomorphAwareness > 0)
            {
                WitnessOvomorphHorror(strength, maxAwareness);
            }
            if (LarvaAwareness > 0)
            {
                WitnessLarvaHorror(strength, maxAwareness);
            }
            if (HorrorAwareness > 0)
            {
                WitnessHorror(strength, maxAwareness);
            }

            TryApplyDisplayHediff();

        }
        public void WitnessOvomorphHorror(float strength, float maxAwareness = 1.0f)
        {
            _traumaRelief -= strength;
            if (OvomorphAwareness >= maxAwareness)
            {
                return;
            }
            _traumaRelief = 0;
            ovomorphAwareness = Mathf.Min(OvomorphAwareness + strength, maxAwareness);

            TryApplyDisplayHediff();

        }

        public void WitnessLarvaHorror(float strength, float maxAwareness = 1.0f)
        {
            _traumaRelief -= strength;
            if (LarvaAwareness >= maxAwareness)
            {
                return;
            }
            _traumaRelief = 0;
            larvaAwareness = Mathf.Min(larvaAwareness + strength, maxAwareness);
            if (OvomorphAwareness > 0)
            {
                WitnessOvomorphHorror(strength, maxAwareness);
            }
            TryApplyDisplayHediff();
        }

        public void WitnessHorror(float strength, float maxAwareness = 1.0f)
        {
            
            if (HorrorAwareness >= maxAwareness)
            {
                return;
            }
            _traumaRelief = 0;
            horrorAwareness = Mathf.Min(horrorAwareness + strength, maxAwareness);

            if (LarvaAwareness > 0)
            {
                WitnessLarvaHorror(strength, maxAwareness);
            }
            TryApplyDisplayHediff();
        }

        public void GainObsession(float strength)
        {
            if (XMTUtility.IsXenomorph(parent))
            {
                return;
            }
            obsession += strength;
            TryApplyDisplayHediff();
        }

        public void GainRelief(float strength, float max = 5f)
        {
            _traumaRelief = Mathf.Min(max, _traumaRelief + strength);

        }

        public float TotalHorrorExperience()
        {
            return OvomorphAwareness + larvaAwareness + horrorAwareness + acidAwareness + psychicAwareness + TraitAwarenessModifier() - _traumaRelief;
        }
        public float TotalHorrorAwareness()
        {
            return TotalHorrorExperience() + IdeoReproductionModifier() + IdeoAdultModifier();
        }

        public bool IsObsessed(out float awareness)
        {
            awareness = TotalHorrorAwareness();
            return Obsession > awareness;
        }
        public bool IsObsessed()
        {
            return Obsession > TotalHorrorAwareness();
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
