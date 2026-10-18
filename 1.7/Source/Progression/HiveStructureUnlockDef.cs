using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public enum HiveCreationMode
    {
        ApplyHediff,
        Merge,
        SelfTransform,
        Build
    }

    public class HiveParticipantRules
    {
        public bool requireAdult;
        public bool requireCryptimorph;
        public bool excludeQueens;
        public bool requireDowned;
        public bool excludeHiveFriendly;
        public bool excludeCryptimorph;
        public bool excludeInorganic;
        public bool excludeTransforming;
        public float minBodySize;
        public List<HediffDef> excludedHediffs;
        public GeneDef requiredGene;
        public HediffDef requiredHediff;
        public List<ThingDef> races;
        public bool Allows(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
                return false;
            if (requireAdult && !pawn.DevelopmentalStage.Adult())
                return false;
            if (requireCryptimorph && !XMTUtility.IsXenomorph(pawn))
                return false;
            if (excludeQueens && XMTUtility.IsQueen(pawn))
                return false;
            if (requireDowned && !pawn.Downed)
                return false;
            if (excludeHiveFriendly && XMTUtility.IsXenomorphFriendly(pawn))
                return false;
            if (excludeCryptimorph && XMTUtility.IsXenomorph(pawn))
                return false;
            if (excludeInorganic && XMTUtility.IsInorganic(pawn))
                return false;
            if (excludeTransforming && XMTUtility.IsMorphing(pawn))
                return false;
            if (pawn.BodySize < minBodySize || (!races.NullOrEmpty() && !races.Contains(pawn.def)))
                return false;
            if (requiredGene != null && pawn.genes?.HasActiveGene(requiredGene) != true)
                return false;
            if (requiredHediff != null && !pawn.health.hediffSet.HasHediff(requiredHediff))
                return false;
            return excludedHediffs.NullOrEmpty() || !excludedHediffs.Any(h => pawn.health.hediffSet.HasHediff(h));
        }
    }

    public class HiveStructureUnlockDef : Def
    {
        public HiveCreationMode mode;
        public Type workerClass = typeof(HiveStructureCreationWorker);
        public ThingDef building;
        public ThingFilter sourceBuildings;
        public float minSourceBodySize;
        public HiveParticipantRules actorRules = new HiveParticipantRules
        {
            requireAdult = true,
            requireCryptimorph = true
        };
        public HiveParticipantRules targetRules = new HiveParticipantRules();
        public HediffDef appliedHediff;
        public DesignationDef designation;
        public JobDef job;
        public WorkTypeDef workType;
        public int priority;
        public int workTicks = 350;
        public float bodySizeScore = 10f;
        public float acceptableHostPenalty = 20f;
        public bool automatic;
        public bool allowFeral;
        public bool architect = true;
        public bool floatMenu = true;
        public bool buildingGizmo = true;
        public string iconPath;
        public string confirmationKey;
        public string contributorConfirmationKey;
        public EffecterDef workEffect;
        public ThoughtDef actorMemory;
        public ThoughtDef targetMemory;
        public float nestJoy;
        private HiveStructureCreationWorker worker;
        public HiveStructureCreationWorker Worker => worker ??= (HiveStructureCreationWorker)Activator.CreateInstance(workerClass);
        public Texture2D Icon => !iconPath.NullOrEmpty() ? ContentFinder<Texture2D>.Get(iconPath) : building?.uiIcon;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            sourceBuildings?.ResolveReferences();
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;
            if (workerClass == null || !typeof(HiveStructureCreationWorker).IsAssignableFrom(workerClass))
                yield return "Invalid structure creation worker.";
            if (mode != HiveCreationMode.Build && job == null)
                yield return "Structure creation requires a JobDef.";
            if (mode == HiveCreationMode.ApplyHediff && appliedHediff == null)
                yield return "Host transformation requires a hediff.";
            if (mode != HiveCreationMode.ApplyHediff && building == null)
                yield return "Structure creation requires a building.";
            if (workTicks < 1)
                yield return "Structure workTicks must be positive.";
            if (automatic && designation == null)
                yield return "Automatic host creation requires a designation.";
            if (automatic && (mode == HiveCreationMode.Merge || mode == HiveCreationMode.SelfTransform))
                yield return "Sacrifice tasks cannot be automatic.";
            if (job != null && job.GetModExtension<HiveStructureJobExtension>()?.unlock != this)
                yield return "Job must reference this unlock through HiveStructureJobExtension.";
        }
    }

    public class HiveStructureJobExtension : DefModExtension
    {
        public HiveStructureUnlockDef unlock;
    }
}
