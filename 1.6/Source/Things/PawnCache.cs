


using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{

    [StaticConstructorOnStartup]
    public static class PawnCacheWrapper
    {
        protected class PawnCache
        {
            public static Dictionary<int, PawnCache> cache = new Dictionary<int, PawnCache>();

            protected Pawn _pawn;

            protected CompPawnInfo _pawnInfo;
            public CompPawnInfo Info => _pawnInfo;

            protected bool _isInorganic;
            public bool IsInorganic => _isInorganic;

            protected CompMatureMorph _matureMorph;
            public CompMatureMorph MatureMorph => _matureMorph;

            protected CompAcidBlood _acidBlood;
            public CompAcidBlood AcidBlood => _acidBlood;

            public float CarboSilicate = 0;
            public float MesoSkeletalValue = 0;

            public float _acidBloodValue = 0;

            public float AcidBloodValue
            {
                get
                {
                    if(AcidBlood != null)
                    {
                        return 1;
                    }

                    return _acidBloodValue;
                }
                
                set
                {
                    if (AcidBlood != null)
                    {
                        return;
                    }
                    _acidBloodValue = value;
                }
            }

            public void Recache()
            {
                _pawnInfo = _pawn.GetComp<CompPawnInfo>();
                _matureMorph = _pawn.GetComp<CompMatureMorph>();
                _acidBlood = _pawn.GetComp<CompAcidBlood>();

                _isInorganic = XMTUtility.CacheIsInorganic(_pawn);
            }
            public PawnCache(Pawn pawn)
            {
                _pawn = pawn;
                Recache();
            }
        }
        public static void Cleanup(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if(PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                PawnCache.cache.Remove(pawn.thingIDNumber);
            }
        }

        public static void Recache(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                PawnCache.cache[pawn.thingIDNumber].Recache();
            }
        }

        public static float MesoSkeletonization(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].MesoSkeletalValue;
            }

            return 0;
        }
        public static void UpdateMesoSkeletonization(this Pawn pawn, float value)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                PawnCache.cache[pawn.thingIDNumber].MesoSkeletalValue = value;
                return;
            }
            PawnCache newCache = new PawnCache(pawn);
            newCache.MesoSkeletalValue = value;
            PawnCache.cache.Add(pawn.thingIDNumber, newCache);

        }

        public static float CarboSilicate(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].CarboSilicate;
            }

            return 0;
        }
        public static void UpdateCarboSilicate(this Pawn pawn, float value)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                Log.Message(pawn + " updating carbosilicate to " + value);
                PawnCache.cache[pawn.thingIDNumber].CarboSilicate = value;
                return;
            }
            Log.Message(pawn + " updating new carbosilicate to " + value);
            PawnCache newCache = new PawnCache(pawn);
            newCache.CarboSilicate = value;
            PawnCache.cache.Add(pawn.thingIDNumber, newCache);
           
        }

        public static CompAcidBlood GetAcidBloodComp(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].AcidBlood;
            }

            PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

            return PawnCache.cache[pawn.thingIDNumber].AcidBlood;
        }
        public static CompMatureMorph GetMorphComp(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].MatureMorph;
            }

            PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

            return PawnCache.cache[pawn.thingIDNumber].MatureMorph;
        }
        public static bool IsInorganic(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].IsInorganic;
            }

            PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

            return PawnCache.cache[pawn.thingIDNumber].IsInorganic;
        }
        public static bool IsAcidImmune(this Pawn pawn)
        {
            if (!PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));
            }

            if (PawnCache.cache[pawn.thingIDNumber].AcidBlood != null)
            {
                return true;
            }

            return PawnCache.cache[pawn.thingIDNumber].MesoSkeletalValue >= 1;
        }
        public static CompPawnInfo Info(this Pawn pawn)
        {
           if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
           {
                return PawnCache.cache[pawn.thingIDNumber].Info;
           }

           PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

           return PawnCache.cache[pawn.thingIDNumber].Info;
        }
    }
}
