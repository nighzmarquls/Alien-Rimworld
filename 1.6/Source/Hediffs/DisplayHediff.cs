using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public class DisplayHediff : Hediff
    {
        CompPawnInfo pawnInfo => pawn.GetComp<CompPawnInfo>();

        public override Color LabelColor => GetColor();
        public override bool Visible => ShoulldDisplay();
        public override string Label => GetLabel();
        public override string Description => GetDescription();
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
        } 

        protected Color GetColor()
        {
            if (pawnInfo == null)
            {
                return Color.white;
            }
            return pawnInfo.GetHediffColor();
        }
        protected bool ShoulldDisplay()
        {
            if (pawnInfo == null)
            {
                return false;
            }

            return pawnInfo.ShouldDisplay();
            
        }
        protected string GetLabel()
        {
            if(pawnInfo == null)
            {
                return "";
            }
            return pawnInfo.GetHediffLabel();
        }

        protected string GetDescription()
        {
            if (pawnInfo == null)
            {
                return "";
            }
            return pawnInfo.GetHediffDescription();
        }
    
        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}
