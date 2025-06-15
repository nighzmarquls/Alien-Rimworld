
using RimWorld.Planet;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

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
