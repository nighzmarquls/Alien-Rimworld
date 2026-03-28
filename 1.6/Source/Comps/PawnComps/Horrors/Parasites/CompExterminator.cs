using RimWorld;

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;


namespace Xenomorphtype
{
    public class CompExterminator : CompHostHunter
    {
        ThingDef targetRaceDef = null;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref targetRaceDef, "targetRaceDef");
        }
        static private Texture2D targetingTexture => ContentFinder<Texture2D>.Get("UI/Abilities/XMT_SelectVermin");
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (Parent.Downed)
            {
                yield break;
            }

            TargetingParameters TargetPawnParameters = TargetingParameters.ForPawns();

            TargetPawnParameters.validator = delegate (TargetInfo target)
            {
                if (target.Cell.GetEdifice(target.Map) != null)
                {
                    return false;
                }

                if (!target.HasThing)
                {
                    return false;
                }

                if (target.Thing is Pawn testPawn)
                {
                    if (XMTUtility.IsInorganic(testPawn) || XMTUtility.IsXenomorph(testPawn))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                return target.Map.reachability.CanReach(parent.Position, target.Cell, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Deadly);
            };

            Command_Action TargetPawn_Action = new Command_Action();
            TargetPawn_Action.defaultLabel = "XMT_Vermin_Target".Translate();
            TargetPawn_Action.defaultDesc = "XMT_Vermin_Target_Description".Translate();
            TargetPawn_Action.icon = targetingTexture;
            TargetPawn_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(TargetPawnParameters, delegate (LocalTargetInfo target)
                {
                    if(target.Pawn is Pawn targetedPawn)
                    {
                        targetRaceDef = targetedPawn.def;
                    }
                });

            };
            yield return TargetPawn_Action;
        }

        public override Pawn GetPreyTarget()
        {
            foreach (Pawn pawn in parent.Map.mapPawns.AllPawnsSpawned)
            {
                if(pawn.Downed)
                {
                    continue;
                }

                if(pawn.def == targetRaceDef)
                {
                    return pawn;
                }
            }
            return null;
        }
        public override bool TryResist(Pawn target)
        {
            bool resisted = base.TryResist(target);
            if (resisted)
            {
                return resisted;
            }

            if (target.apparel != null)
            {
                XMTUtility.DamageApparelByBodyPart(target, BodyPartGroupDefOf.FullHead, 160f);
                return target.apparel.BodyPartGroupIsCovered(BodyPartGroupDefOf.FullHead);

            }
            return false;
        }

        public override List<BodyPartRecord> GetTargetBodyParts(Pawn target)
        {
            return (from x in target.health.hediffSet.GetNotMissingParts()
                   where
                    XMTUtility.IsPartHead(x)
                   select x).ToList();
        }
        public override void PreattachTarget(Pawn target)
        {
            base.PreattachTarget(target);
        }
    }

}
