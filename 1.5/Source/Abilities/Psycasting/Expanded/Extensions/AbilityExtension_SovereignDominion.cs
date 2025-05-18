using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using VFECore.Abilities;
using Ability = VFECore.Abilities.Ability;

namespace Xenomorphtype
{
    public class AbilityExtension_SovereignDominion : AbilityExtension_AbilityMod
    {
        
        public override void Cast(GlobalTargetInfo[] targets, Ability ability)
        {
            base.Cast(targets, ability);
            Pawn caster = ability.pawn;
            bool xenomorphCaster = XMTUtility.IsXenomorph(caster);
            foreach (GlobalTargetInfo target in targets)
            {
                
                if (target.Thing == caster)
                {
                    continue;
                }

                if (target.Thing is Pawn subject)
                {
                    bool xenomorphTarget = XMTUtility.IsXenomorph(subject);
                    

                    HivecastUtility.EnactDominion(subject, caster, xenomorphTarget, xenomorphCaster);
                   
                }
            }
        }

    }
}
