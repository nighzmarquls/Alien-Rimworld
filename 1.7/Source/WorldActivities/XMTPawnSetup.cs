using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Xenomorphtype
{
    public abstract class XMT_PawnSetupComp : HediffComp
    {
        private bool finished;
        public override bool CompShouldRemove => finished;
        public override void CompExposeData() { Scribe_Values.Look(ref finished, "XMT_setupFinished"); }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if (!PawnGenerator.IsBeingGenerated(Pawn)) Finish();
        }
        internal void Finish()
        {
            if (finished) return;
            Assign();
            finished = true;
        }
        protected abstract void Assign();
    }

    public sealed class XMT_CompProperties_AssignAge : HediffCompProperties
    {
        public FloatRange biologicalAge;
        public bool matchChronologicalAge = true;
        public XMT_CompProperties_AssignAge() { compClass = typeof(XMT_CompAssignAge); }
    }
    public sealed class XMT_CompAssignAge : XMT_PawnSetupComp
    {
        protected override void Assign()
        {
            var configuration = (XMT_CompProperties_AssignAge)props;
            Pawn.ageTracker.AgeBiologicalTicks = (long)(configuration.biologicalAge.RandomInRange * GenDate.TicksPerYear);
            if (configuration.matchChronologicalAge) Pawn.ageTracker.AgeChronologicalTicks = Pawn.ageTracker.AgeBiologicalTicks;
        }
    }
    public sealed class XMT_CompProperties_AddGenes : HediffCompProperties
    {
        public List<GeneDef> genes = new List<GeneDef>();
        public XMT_CompProperties_AddGenes() { compClass = typeof(XMT_CompAddGenes); }
    }
    public sealed class XMT_CompAddGenes : XMT_PawnSetupComp
    {
        protected override void Assign()
        {
            if (Pawn.genes == null) return;
            foreach (GeneDef gene in ((XMT_CompProperties_AddGenes)props).genes)
                if (gene != null && !Pawn.genes.HasActiveGene(gene)) Pawn.genes.AddGene(gene, true);
        }
    }

    public class XMT_TraitAssignment { public TraitDef trait; public int degree; }
    public sealed class XMT_CompProperties_AssignTraits : HediffCompProperties
    {
        public List<XMT_TraitAssignment> traits = new List<XMT_TraitAssignment>();
        public XMT_CompProperties_AssignTraits() { compClass = typeof(XMT_CompAssignTraits); }
    }
    public sealed class XMT_CompAssignTraits : XMT_PawnSetupComp
    {
        protected override void Assign()
        {
            if (Pawn.story?.traits == null) return;
            foreach (XMT_TraitAssignment entry in ((XMT_CompProperties_AssignTraits)props).traits)
            {
                if (entry.trait == null || Pawn.story.traits.HasTrait(entry.trait)) continue;
                foreach (Trait conflict in Pawn.story.traits.allTraits.Where(t => t.def.ConflictsWith(entry.trait)).ToList())
                    Pawn.story.traits.RemoveTrait(conflict);
                Pawn.story.traits.GainTrait(new Trait(entry.trait, entry.degree, true));
            }
            KnowledgeUtility.Invalidate(Pawn);
        }
    }

    public sealed class XMT_CompProperties_AssignLoadout : HediffCompProperties
    {
        public List<ThingDef> weapons = new List<ThingDef>();
        public List<ThingDefCountClass> inventory = new List<ThingDefCountClass>();
        public XMT_CompProperties_AssignLoadout() { compClass = typeof(XMT_CompAssignLoadout); }
    }
    public sealed class XMT_CompAssignLoadout : XMT_PawnSetupComp
    {
        protected override void Assign()
        {
            XMT_CompProperties_AssignLoadout configuration = (XMT_CompProperties_AssignLoadout)props;
            if (Pawn.equipment != null && !Pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                ThingDef weapon = configuration.weapons.Where(d => d != null && d.IsWeapon)
                    .OrderByDescending(SustainedDamage).FirstOrDefault();
                if (weapon != null)
                {
                    foreach (ThingWithComps previous in Pawn.equipment.AllEquipmentListForReading.ToList())
                    {
                        Pawn.equipment.Remove(previous);
                        if (Pawn.inventory?.innerContainer.TryAdd(previous, canMergeWithExistingStacks: false) != true) previous.Destroy();
                    }
                    Pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(weapon, GenStuff.DefaultStuffFor(weapon)));
                }
            }
            if (Pawn.inventory == null) return;
            foreach (ThingDefCountClass entry in configuration.inventory)
            {
                if (entry.thingDef == null) continue;
                int remaining = entry.count;
                while (remaining > 0)
                {
                    Thing item = ThingMaker.MakeThing(entry.thingDef, GenStuff.DefaultStuffFor(entry.thingDef));
                    item.stackCount = System.Math.Min(remaining, item.def.stackLimit);
                    remaining -= item.stackCount;
                    if (!Pawn.inventory.innerContainer.TryAdd(item)) item.Destroy();
                }
            }
        }
        // Compare configured candidates once during setup; include beam and projectile weapons.
        private static float SustainedDamage(ThingDef weapon) => weapon.Verbs?.Max(verb =>
        {
            float damage = verb.defaultProjectile?.projectile?.GetDamageAmount(weapon, null) ?? verb.beamDamageDef?.defaultDamage ?? 0f;
            int shots = System.Math.Max(1, verb.burstShotCount);
            float seconds = verb.warmupTime + weapon.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown)
                + (shots - 1) * verb.ticksBetweenBurstShots / 60f;
            return damage * shots / UnityEngine.Mathf.Max(0.1f, seconds);
        }) ?? 0f;
    }

    public sealed class XMT_CompProperties_AssignIdeology : HediffCompProperties
    {
        public List<PreceptDef> precepts = new List<PreceptDef>();
        public List<MemeDef> memes = new List<MemeDef>();
        public XMT_CompProperties_AssignIdeology() { compClass = typeof(XMT_CompAssignIdeology); }
    }
    public sealed class XMT_CompAssignIdeology : XMT_PawnSetupComp
    {
        protected override void Assign()
        {
            if (!ModsConfig.IdeologyActive || Pawn.ideo == null) return;
            XMT_CompProperties_AssignIdeology configuration = (XMT_CompProperties_AssignIdeology)props;
            List<PreceptDef> required = configuration.precepts.Where(p => p != null).ToList();
            List<PreceptDef> disallowed = DefDatabase<PreceptDef>.AllDefsListForReading
                .Where(p => required.Any(r => r.issue != null && r.issue == p.issue && r != p)).ToList();
            Ideo ideology = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(FactionDefOf.PlayerColony,
                disallowedPrecepts: disallowed, forcedMemes: configuration.memes.Count > 0 ? configuration.memes : null));
            foreach (PreceptDef definition in required)
            {
                foreach (Precept conflict in ideology.PreceptsListForReading.Where(p => p.def.issue == definition.issue && p.def != definition).ToList())
                    ideology.RemovePrecept(conflict);
                if (!ideology.HasPrecept(definition)) ideology.AddPrecept(PreceptMaker.MakePrecept(definition), true);
            }
            Find.IdeoManager.Add(ideology);
            Pawn.ideo.SetIdeo(ideology);
            KnowledgeUtility.Invalidate(Pawn);
        }
    }

    public sealed class XMT_CompProperties_RegisterCompanion : HediffCompProperties
    {
        public XMT_CompanionDef companion;
        public XMT_CompProperties_RegisterCompanion() { compClass = typeof(XMT_CompRegisterCompanion); }
    }
    public sealed class XMT_CompRegisterCompanion : XMT_PawnSetupComp
    {
        protected override void Assign() => Current.Game.GetComponent<GameComponent_XMTWorldState>()
            .RegisterCompanion(Pawn, ((XMT_CompProperties_RegisterCompanion)props).companion, 3);
    }

    internal static class XMT_PawnSetupUtility
    {
        private static Dictionary<BackstoryDef, List<XMT_BackstoryCompanionDef>> byBackstory;
        private static readonly HashSet<Pawn> finishing = new HashSet<Pawn>();
        internal static void Finish(Pawn pawn)
        {
            if (pawn?.health == null || !finishing.Add(pawn)) return;
            try
            {
                foreach (HediffWithComps hediff in pawn.health.hediffSet.hediffs.OfType<HediffWithComps>().ToList())
                {
                    foreach (XMT_PawnSetupComp setup in hediff.comps.OfType<XMT_PawnSetupComp>()) setup.Finish();
                    foreach (HediffComp_ForceWearApparel apparel in hediff.comps.OfType<HediffComp_ForceWearApparel>()) apparel.FinishSetup();
                    if (hediff.comps.Count > 0 && hediff.comps.All(c => c.CompShouldRemove)) pawn.health.RemoveHediff(hediff);
                }
                if (XMT_CompanionGenerationScope.Source != null || pawn.story == null || Current.Game == null) return;
                byBackstory ??= DefDatabase<XMT_BackstoryCompanionDef>.AllDefsListForReading.Where(d => d.backstory != null)
                    .GroupBy(d => d.backstory).ToDictionary(g => g.Key, g => g.ToList());
                // Higher-priority associations are registered first; no companion generation occurs here.
                foreach (var slot in new[] { (pawn.story.Adulthood, 2), (pawn.story.Childhood, 1) })
                    if (slot.Item1 != null && byBackstory.TryGetValue(slot.Item1, out List<XMT_BackstoryCompanionDef> associations))
                        foreach (XMT_BackstoryCompanionDef association in associations)
                            Current.Game.GetComponent<GameComponent_XMTWorldState>().RegisterCompanion(pawn, association.companion, slot.Item2);
            }
            finally { finishing.Remove(pawn); }
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    internal static class XMT_PawnGenerationSetupPatch
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(Pawn __result) { XMT_PawnSetupUtility.Finish(__result); }
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class XMT_PawnSpawnSetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn __instance) { XMT_PawnSetupUtility.Finish(__instance); }
    }
}
