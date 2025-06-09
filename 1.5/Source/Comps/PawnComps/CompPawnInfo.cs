using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
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
        protected enum PheromoneType
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
        float LoverPheromone => _loverPheromone;
        float FriendlyPheromone => _friendlyPheromone;
        float ThreatPheromone => _threatPheromone;

        protected PheromoneType strongestPheromone
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

        bool isAware => ovamorphAwareness != 0 || larvaAwareness != 0 || horrorAwareness != 0 || Obsession != 0;

        // Xenomorph Naivety System
        float ovamorphAwareness = 0;
        float larvaAwareness = 0;
        float horrorAwareness = 0;
        float acidAwareness = 0;
        float psychicAwareness = 0;
        float obsession = 0;
        float Obsession => obsession + TraitObsessionModifier();

        public float OvamorphAwareness => ovamorphAwareness + TraitAwarenessModifier() + IdeoReproductionModifier();
        public float LarvaAwareness => larvaAwareness + TraitAwarenessModifier() + IdeoReproductionModifier();
        public float HorrorAwareness => horrorAwareness + TraitAwarenessModifier() + IdeoAdultModifier();
        public float AcidAwareness => acidAwareness + TraitAwarenessModifier() + IdeoAdultModifier();

        public float PsychicAwareness => psychicAwareness;


        public float IdeoReproductionModifier()
        {
            float modifier = 0;
            if (parent is Pawn Parent)
            {
                if (ModsConfig.IdeologyActive)
                {
                    if (Parent.Ideo is Ideo PawnIdeo)
                    {
                        PawnIdeo.HasMaxPreceptsForIssue(XenoPreceptDefOf.XMT_Reproduction);
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
                        PawnIdeo.HasMaxPreceptsForIssue(XenoPreceptDefOf.XMT_Cryptobio);
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

            Scribe_Values.Look(ref ovamorphAwareness, "OvamorphAwareness", 0);
            Scribe_Values.Look(ref larvaAwareness, "LarvaAwareness", 0);
            Scribe_Values.Look(ref horrorAwareness, "HorrorAwareness", 0);
            Scribe_Values.Look(ref acidAwareness, "AcidAwareness", 0);
            Scribe_Values.Look(ref psychicAwareness, "psychicAwareness", 0);
            Scribe_Values.Look(ref obsession, "HorrorObsession", 0);
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
                    switch (strongestPheromone)
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
                if (pheromonesPresent)
                {
                    switch (strongestPheromone)
                    {
                        case PheromoneType.Lover:
                            output += "marked hive lover";
                            break;
                        case PheromoneType.Friend:
                            output += "marked hive friend";
                            break;
                        case PheromoneType.Threat:
                            output += "marked hive enemy";
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
                    output += "drenched in stink";
                }
                else if (severity >= 0.75f)
                {
                    hasSmell = true;
                    output += "overpowering smell";
                }
                else if (severity >= 0.25f)
                {
                    hasSmell = true;
                    output += "cloying scent";
                }
                else if (severity >= 0.1f)
                {
                    hasSmell = true;
                    output += "strange odor";
                }

            }

            if (hasSmell)
            {
                output += " & ";
            }

            if (isAware)
            {
                if (IsObsessed())
                {
                    output += "obsessed";
                }
                else
                {
                    float awareness = TotalHorrorAwareness();

                    if (awareness >= 1)
                    {
                        output += "traumatized";
                    }
                    else if (awareness >= 0.5f)
                    {
                        output += "paranoid";
                    }
                    else if (awareness >= 0.25f)
                    {
                        output += "anxious";
                    }
                    else
                    {
                        output += "concerned";
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
                if (LoverPheromone > 0)
                {
                    output += "Has been marked as a lover of the hive. \n";
                }
                if (FriendlyPheromone > 0)
                {
                    output += "Has been marked as an ally and friend of the hive. \n";
                }
                if (ThreatPheromone > 0)
                {
                    output += "Has been marked as a threat to the hive \n";
                }
            }
            else
            {
                float severity = totalPheromone;

                if (severity >= 1)
                {
                    output += "Covered in a sticky clear goop with an overwhelmingly pungant scent. \n";
                }
                else if (severity >= 0.75f)
                {
                    output += "An overwhelming scent of smokey spices and sharp accents suffuces chokingly. \n";
                }
                else if (severity >= 0.25f)
                {
                    output += "There is a deep sweetly savory smell filling the air. \n";
                }
                else if (severity >= 0.1f)
                {
                    output += "A strange scent of spice lingers. \n";
                }

            }

            if (isAware)
            {
                bool obsessed = IsObsessed();
                if (OvamorphAwareness > 0)
                {
                    output += obsessed ? "Is fascinated by the life cycle of Ovamorphs. \n" : "Has witnessed what comes from Ovamorphs. \n";
                }
                if (LarvaAwareness > 0)
                {
                    output += obsessed ? "Admires the parasitic lifecycle of the facehugger. \n" : "Knows to be wary of skittering spider like things that leap for your face. \n";
                }
                if (HorrorAwareness > 0)
                {
                    output += obsessed ? "Adores the purity of the perfect organism. \n" : "Has seen the brutality and horror in the dark. \n";
                }
                if (AcidAwareness > 0)
                {
                    output += obsessed ? "Is amazed by the strength of acidic blood. \n" : "Has experienced how dangerous acidic blood is. \n";
                }

                if (PsychicAwareness > 0)
                {
                    output += obsessed ? "Hears voices whispering, calling to them. \n" : "Is haunted by strange whispers just outside the audibility. \n";
                }

            }
            return output;
        }

        public void TryApplyDisplayHediff()
        {
            Pawn Parent = parent as Pawn;
            if (parent != null)
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
            if (PsychicAwareness >= maxAwareness)
            {
                return;
            }

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

            if (OvamorphAwareness < maxPsychicBleed)
            {
                WitnessOvamorphHorror(psychicBleed, maxPsychicBleed);
            }

            TryApplyDisplayHediff();

        }

        public void WitnessAcidHorror(float strength, float maxAwareness = 1.0f)
        {
            if (AcidAwareness >= maxAwareness)
            {
                return;
            }

            acidAwareness = Mathf.Min(acidAwareness + strength, maxAwareness);

            if (OvamorphAwareness > 0)
            {
                WitnessOvamorphHorror(strength, maxAwareness);
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
        public void WitnessOvamorphHorror(float strength, float maxAwareness = 1.0f)
        {
            if (OvamorphAwareness >= maxAwareness)
            {
                return;
            }

            ovamorphAwareness = Mathf.Min(ovamorphAwareness + strength, maxAwareness);

            TryApplyDisplayHediff();

        }

        public void WitnessLarvaHorror(float strength, float maxAwareness = 1.0f)
        {
            if (LarvaAwareness >= maxAwareness)
            {
                return;
            }

            larvaAwareness = Mathf.Min(larvaAwareness + strength, maxAwareness);
            if (OvamorphAwareness > 0)
            {
                WitnessOvamorphHorror(strength, maxAwareness);
            }
            TryApplyDisplayHediff();
        }

        public void WitnessHorror(float strength, float maxAwareness = 1.0f)
        {
            if (HorrorAwareness >= maxAwareness)
            {
                return;
            }

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

        public float TotalHorrorExperience()
        {
            return ovamorphAwareness + larvaAwareness + horrorAwareness + acidAwareness + psychicAwareness + TraitAwarenessModifier();
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

        public void ApplyThreatPheromone(Thing victim, float amount = 0.5f, float maxStrength = 1f)
        {

            _threatPheromone = Mathf.Min(ThreatPheromone + amount, maxStrength);

            ReduceHygiene(amount);
            TryApplyDisplayHediff();

            
            XMTUtility.ThreatResponse(victim, this);

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

        public override void Notify_AbandonedAtTile(int tile)
        {
            base.Notify_AbandonedAtTile(tile);
            XenoformingUtility.HandleXenoformingImpact(parent);
        }
    }
}
