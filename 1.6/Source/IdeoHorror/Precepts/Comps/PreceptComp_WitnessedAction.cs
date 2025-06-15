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
    internal class PreceptComp_WitnessedAction : PreceptComp_Thought
    {
        public HistoryEventDef              eventDef;
        public bool reportOnHuman         = true;
        public bool reportOnAnimal        = true;
        public bool reportOnSameFaction   = true;
        public bool reportOnOtherFaction  = true;
        public bool reportOnInnocent      = true;
        public bool reportOnGuilty        = true;
        public bool believedByStarbeast   = false;
        public override void Notify_MemberWitnessedAction(HistoryEvent ev, Precept precept, Pawn member)
        {
            if (ev.def != eventDef)
            {
                return;
            }

            Pawn arg = ev.args.GetArg<Pawn>(HistoryEventArgsNames.Doer);

            if (arg == null)
            {
                return;
            }

            if(member == null)
            {
                return;
            }

            if(arg == member)
            {
                return;
            }

            if ((arg.RaceProps.Humanlike && !reportOnHuman) || (!arg.RaceProps.Humanlike && !reportOnAnimal) )
            {
                return;
            }


            if ( (member.Faction == arg.Faction && !reportOnSameFaction) || (member.Faction != arg.Faction && !reportOnOtherFaction))
            {
                return;
            }

            if (arg.guilt != null)
            {
                if ((arg.guilt.IsGuilty && !reportOnGuilty) || (!arg.guilt.IsGuilty && !reportOnInnocent))
                {
                    return;
                }
            }

            if(XMTUtility.IsXenomorph(member) && !believedByStarbeast)
            {
                return;
            }

            if (member.needs != null && member.needs.mood != null )
            {
                Thought_Memory thought_Memory = ThoughtMaker.MakeThought(thought, precept);

                member.needs.mood.thoughts.memories.TryGainMemory(thought_Memory);
            }
        }
    }
}
