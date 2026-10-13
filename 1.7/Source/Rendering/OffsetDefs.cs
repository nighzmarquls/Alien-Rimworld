

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{ 
    public struct RaceOffset
    {
        public string race;
        public Vector3 north;
        public Vector3 east;
        public Vector3 south;
        public Vector3 west;
    }

    public class OffsetListDef : Def
    {
        private List<RaceOffset> raceOffsets;
        private Dictionary<string, RaceOffset> _offsetDictionary = null;
        public override void PostLoad()
        {
            base.PostLoad();
            _offsetDictionary = null;
        }

        public Vector3 OffsetByRace(Rot4 facing, ThingDef Race)
        {
            Vector3 output = Vector3.zero;
            if (_offsetDictionary == null)
            {
                _offsetDictionary = new Dictionary<string, RaceOffset> ();
                foreach (RaceOffset offset in raceOffsets)
                {
                    _offsetDictionary[offset.race] = offset;
                }
            }

            if(_offsetDictionary.ContainsKey(Race.defName))
            {
                switch(facing.AsInt)
                {
                    case 0:
                        output = _offsetDictionary[Race.defName].north;
                        break;
                    case 1:
                        output = _offsetDictionary[Race.defName].east;
                        break;
                    case 2:
                        output = _offsetDictionary[Race.defName].south;
                        break;
                    case 3:
                        output = _offsetDictionary[Race.defName].west;
                        break;
                }
            }

            return output;
        }
        

    }
}
