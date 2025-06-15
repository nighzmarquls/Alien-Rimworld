using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class HediffComp_RemoveOnCommandByXenomorph : HediffComp
    {
        DesignationManager designationManager => parent.pawn.Map.designationManager;
        HediffCompProperties_RemoveOnCommandByXenomorph Props => props as HediffCompProperties_RemoveOnCommandByXenomorph;
        private Texture2D releaseTexture => ContentFinder<Texture2D>.Get(Props.releaseTexturePath);
        private Texture2D cancelTexture => ContentFinder<Texture2D>.Get(Props.cancelTexturePath);
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            Log.Message("gizmo started in " + parent + " on " + parent.pawn);
            if (XMTUtility.NoQueenPresent())
            {
                yield break;
            }

            Log.Message("queen present in " + parent + " on " + parent.pawn);
            Pawn Queen = XMTUtility.GetQueen();

            if(Queen.Faction != null && !Queen.Faction.IsPlayer)
            {
                yield break;
            }
            Log.Message("queen is player faction " + parent + " on " + parent.pawn);

            Designation designation = designationManager.DesignationOn(parent.pawn);

            
            if (designation != null && designation.def == Props.releaseDesignation)
            {
                Log.Message("designation present " + parent + " on " + parent.pawn);
                Command_Action Cancel_Action = new Command_Action();
                Cancel_Action.defaultLabel = "Cancel Release";
                Cancel_Action.defaultDesc = "Cancel Release from " + parent.Label;
                Cancel_Action.icon = cancelTexture;
                Cancel_Action.action = delegate
                {
                    if(parent.pawn.Map.reservationManager.TryGetReserver(parent.pawn,Queen.Faction, out Pawn reserver))
                    {
                        parent.pawn.Map.reservationManager.ReleaseAllClaimedBy(reserver);
                    }
                    designationManager.RemoveAllDesignationsOn(parent.pawn);
                };

                yield return Cancel_Action;
                yield break;
            }

            Log.Message("setting up release action gizmo in " + parent + " on " + parent.pawn);
            Command_Action Release_Action = new Command_Action();
            Release_Action.defaultLabel = "Release";
            Release_Action.defaultDesc = "Release from " + parent.Label;
            Release_Action.icon = releaseTexture;
            Release_Action.action = delegate
            {
                designationManager.RemoveAllDesignationsOn(parent.pawn);
                designationManager.AddDesignation(new Designation(parent.pawn, Props.releaseDesignation));
            };

            Log.Message("posting release action on " + parent + " on " + parent.pawn);
            yield return Release_Action; 
            
        }
    }

    public class HediffCompProperties_RemoveOnCommandByXenomorph : HediffCompProperties
    {

        public string releaseTexturePath = "UI/Designators/Break";
        public string cancelTexturePath = "UI/Designators/Cancel";
        public DesignationDef releaseDesignation;
        public HediffCompProperties_RemoveOnCommandByXenomorph()
        {
            this.compClass = typeof(HediffComp_RemoveOnCommandByXenomorph);
        }
    }
}
