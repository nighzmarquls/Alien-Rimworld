using System;
using System.Collections.Generic;
using System.Linq;
using AlienRace;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public abstract class XMT_CompanionSource
    {
        public PawnKindDef pawnKind;
        public virtual IEnumerable<string> ConfigErrors() { yield break; }
        public Pawn Create(Pawn owner, XMT_CompanionDef definition)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(pawnKind ?? PawnKindDefOf.Colonist, owner != null ? owner.Faction : Faction.OfPlayer);
            request.ForceGenerateNewPawn = true;
            request.CanGeneratePawnRelations = false;
            if (this is XMT_CompanionSource_Bio named && named.bio != null)
                request.FixedGender = named.bio.gender == GenderPossibility.Female ? Gender.Female : Gender.Male;
            request.FixedBiologicalAge = definition.biologicalAge.RandomInRange;
            request.FixedChronologicalAge = definition.yearsAfterOwnerBirth >= 0f && owner != null
                ? Math.Max(0f, owner.ageTracker.AgeChronologicalYearsFloat - definition.yearsAfterOwnerBirth)
                : definition.chronologicalAge.max > 0 ? definition.chronologicalAge.RandomInRange : request.FixedBiologicalAge;
            using (XMT_CompanionGenerationScope.Begin(this))
            {
                Pawn pawn = PawnGenerator.GeneratePawn(request);
                if (pawn == null) return null;
                Finish(pawn);
                // Generation enforces chronological >= biological; synthetic apparent age is deliberately independent.
                pawn.ageTracker.AgeBiologicalTicks = (long)(request.FixedBiologicalAge.Value * GenDate.TicksPerYear);
                pawn.ageTracker.AgeChronologicalTicks = (long)(request.FixedChronologicalAge.Value * GenDate.TicksPerYear);
                foreach (HediffDef hediff in definition.setupHediffs) pawn.health.AddHediff(hediff);
                XMT_PawnSetupUtility.Finish(pawn);
                return pawn;
            }
        }
        protected virtual void Finish(Pawn pawn) { }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo))]
    internal static class XMT_ExactCompanionStoryPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Prefix(Pawn pawn)
        {
            if (XMT_CompanionGenerationScope.Source is not XMT_CompanionSource_Bio source || source.bio == null) return true;
            XMT_CompanionGenerationScope.SelectBio(source.bio);
            pawn.story.Childhood = source.bio.childhood;
            pawn.story.Adulthood = source.bio.adulthood;
            pawn.Name = new NameTriple(source.bio.name.First, source.bio.name.Nick, source.bio.name.Last);
            return false;
        }
    }

    public class XMT_CompanionSource_Generic : XMT_CompanionSource { }

    public sealed class XMT_CompanionSource_Bio : XMT_CompanionSource
    {
        public PawnBioDef bio;
        public bool Matches(Pawn pawn) => bio != null && pawn?.Name is NameTriple name
            && name.First == bio.name.First && name.Last == bio.name.Last
            && pawn.story?.Childhood == bio.childhood && pawn.story?.Adulthood == bio.adulthood;
        public override IEnumerable<string> ConfigErrors()
        {
            if (bio == null) yield return "exact bio is required";
        }
        protected override void Finish(Pawn pawn)
        {
            pawn.Name = new NameTriple(bio.name.First, bio.name.Nick, bio.name.Last);
        }
    }

    internal sealed class XMT_CompanionGenerationScope : IDisposable
    {
        [ThreadStatic] internal static XMT_CompanionSource Source;
        private static readonly System.Reflection.FieldInfo harBio = AccessTools.Field(typeof(AlienRace.HarmonyPatches), "bioReference");
        private readonly XMT_CompanionSource previous;
        private readonly object previousBio;
        private XMT_CompanionGenerationScope(XMT_CompanionSource source)
        {
            previous = Source; Source = source;
            previousBio = harBio.GetValue(null);
            harBio.SetValue(null, (source as XMT_CompanionSource_Bio)?.bio);
        }
        internal static IDisposable Begin(XMT_CompanionSource source) => new XMT_CompanionGenerationScope(source);
        internal static void SelectBio(PawnBioDef bio) => harBio.SetValue(null, bio);
        public void Dispose() { Source = previous; harBio.SetValue(null, previousBio); }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "TryGiveSolidBioTo")]
    internal static class XMT_GenericCompanionBioPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (XMT_CompanionGenerationScope.Source is not XMT_CompanionSource_Generic) return true;
            __result = false;
            return false;
        }
    }

    public class XMT_CompanionRelationWorker
    {
        public virtual void Assign(Pawn owner, Pawn companion, XMT_CompanionDef definition)
        {
            bool ownerHadRelation = definition.ownerToCompanion?.Worker.InRelation(owner, companion) == true;
            bool companionHadRelation = definition.companionToOwner?.Worker.InRelation(companion, owner) == true;
            AssignOne(owner, companion, definition.ownerToCompanion);
            AssignOne(companion, owner, definition.companionToOwner);
            if (!ownerHadRelation) ApplyKnownLoss(owner, companion, definition.ownerToCompanion);
            if (!companionHadRelation) ApplyKnownLoss(companion, owner, definition.companionToOwner);
        }
        public virtual void NotifySeparated(Pawn owner, Pawn companion, XMT_CompanionDef definition) => ApplyKnownLoss(companion, owner, definition.companionToOwner);
        public virtual void NotifyReunited(Pawn owner, Pawn companion, XMT_CompanionDef definition)
        {
            if (definition.companionToOwner?.lostThought != null) companion?.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDefWhereOtherPawnIs(definition.companionToOwner.lostThought, owner);
            if (definition.companionToOwner?.lostThoughtFemale != null) companion?.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDefWhereOtherPawnIs(definition.companionToOwner.lostThoughtFemale, owner);
        }
        private static void ApplyKnownLoss(Pawn pawn, Pawn other, PawnRelationDef relation)
        {
            if (pawn == null || pawn.Dead || other == null || relation == null || pawn.needs?.mood == null) return;
            bool female = other.gender == Gender.Female;
            ThoughtDef thought = other.Dead ? female ? relation.diedThoughtFemale ?? relation.diedThought : relation.diedThought
                : other.IsPrisoner || other.IsSlave || XMTContainmentUtility.IsHeld(other)
                    ? female ? relation.lostThoughtFemale ?? relation.lostThought : relation.lostThought : null;
            if (thought != null && !pawn.needs.mood.thoughts.memories.Memories.Any(m => m.def == thought && m.otherPawn == other))
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought, other);
        }
        private static void AssignOne(Pawn pawn, Pawn other, PawnRelationDef relation)
        {
            if (relation == null || pawn.relations == null || relation.Worker.InRelation(pawn, other)) return;
            if (relation.implied && relation.Worker is not XMT_PawnRelationWorker)
            {
                PawnGenerationRequest request = new PawnGenerationRequest(pawn.kindDef, pawn.Faction);
                relation.Worker.CreateRelation(pawn, other, ref request);
            }
            else pawn.relations.AddDirectRelation(relation, other);
        }
    }

    // Adoptive/social links are explicit; never manufacture biological ancestry to display them.
    public class XMT_PawnRelationWorker : PawnRelationWorker
    {
        public override bool InRelation(Pawn me, Pawn other) => me?.relations?.DirectRelationExists(def, other) == true;
    }
}
