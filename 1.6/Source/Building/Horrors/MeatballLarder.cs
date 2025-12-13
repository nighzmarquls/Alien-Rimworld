
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;


namespace Xenomorphtype
{
    public class MeatballLarder : XMTBase_Building
    {
        CompMeatBall meatBall;
        static private Texture2D jellyTexture => ContentFinder<Texture2D>.Get("UI/Abilities/JellyWell");
        public float bodySize => meatBall.TotalBodySize();
        internal float harvestWork => meatBall.harvestWork;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic.drawSize = meatBall.DisplaySize;
            Graphic.color = meatBall.progenitorSkinColor;
            base.DrawAt(drawLoc, flip);
            Graphic.drawSize = def.graphic.drawSize;
            Graphic.color = def.graphic.color;
        }

        public bool CanBePruned()
        {
            return meatBall.CanBePruned();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {

            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!XMTUtility.HasQueenWithEvolution(RoyalEvolutionDefOf.Evo_JellyWellSerum))
            {
                yield break;
            }

            TargetingParameters GeneTargetParameters = new TargetingParameters();

            GeneTargetParameters.validator = delegate (TargetInfo target)
            {
                if (target.Thing == this)
                {
                    return false;
                }

                if (target.Thing is Pawn targetPawn)
                {
                    if (targetPawn.Dead || targetPawn.Downed)
                    {
                        return false;
                    }

                    if (XMTUtility.IsQueen(targetPawn))
                    {
                        return false;
                    }

                    return XMTUtility.IsXenomorph(targetPawn);
                }

                return false;

            };

            Command_Action Jelly_Action = new Command_Action();
            Jelly_Action.defaultLabel = "Create Jelly Well";
            Jelly_Action.defaultDesc = "Target will devour the larder and become a Jelly Well.";
            Jelly_Action.icon = jellyTexture;
            Jelly_Action.action = delegate
            {
                Find.Targeter.BeginTargeting(GeneTargetParameters, delegate (LocalTargetInfo target)
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("The target will merge to become a Jelly Well, this is lethal and irreversible.\n" +
                        "Are you sure?",
                    delegate {
                        if (target.Thing is Pawn targetPawn)
                        {
                            Job job = JobMaker.MakeJob(XenoWorkDefOf.XMT_MergeIntoJellyWell, this);
                            targetPawn.jobs.StartJob(job, JobCondition.InterruptForced);
                        }
                    }));
                });

            };

            if (bodySize < 4)
            {
                Jelly_Action.Disabled = true;
                Jelly_Action.disabledReason = "Larder is not large enough.";
            }


            yield return Jelly_Action;
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (meatBall == null)
            {
                meatBall = this.GetComp<CompMeatBall>();
            }
            XMTHiveUtility.AddLarder(this, map);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            XMTHiveUtility.RemoveLarder(this, this.Map);
            base.DeSpawn(mode);
        }
        
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (meatBall != null && Spawned)
            {
                int stackTotal = HitPoints;
                meatBall.DropMeat(stackTotal);
            }

            base.Destroy(mode);
        }

        public override void TransformedFrom(Pawn pawn, Pawn instigator)
        {
            meatBall.SetProgenitor(pawn); 
        }
    }
}
