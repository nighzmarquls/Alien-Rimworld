using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class RoyalEvolutionDef : Def
    {
        public int evoPointCost;

        public HediffDef evolutionHediff;

        public BodyPartDef targetBodyPart;

        public List<RoyalEvolutionDef> replaces;
        public List<RoyalEvolutionDef> prerequisites;

        public bool AvailableForPawn(Pawn pawn)
        {
            CompQueen compQueen = pawn.GetComp<CompQueen>();
            if(compQueen == null)
            {
                return false;
            }

            if (compQueen.AvailableEvoPoints < evoPointCost)
            {
                return false;
            }

            return true;
        }

        public bool IsPrerequisiteOfHeldPermit(Pawn pawn)
        {
            return false;
        }
    }
}
