


using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Xenomorphtype
{

    [StaticConstructorOnStartup]
    public static class PawnCacheWrapper
    {
        public static void ClearAllPawnCaches()
        {
            PawnCache.cache.Clear();
        }
        protected class PawnCache
        {
            public static Dictionary<int, PawnCache> cache = new Dictionary<int, PawnCache>();

            protected Pawn _pawn;

            protected CompPawnInfo _pawnInfo;
            public CompPawnInfo Info => _pawnInfo;

            protected bool _isInorganic;
            public bool IsInorganic => _isInorganic;

            protected CompClimber _climber;
            public CompClimber Climber => _climber;

            protected CompMatureMorph _matureMorph;
            public CompMatureMorph MatureMorph => _matureMorph;

            protected CompPerfectOrganism _perfect;
            public CompPerfectOrganism Perfect => _perfect;

            protected CompAcidBlood _acidBlood;
            public CompAcidBlood AcidBlood => _acidBlood;

            protected PawnMesoSkeletonDrawer _mesoSkeletonDrawer;

            public PawnMesoSkeletonDrawer MesoSkeletonDrawer => _mesoSkeletonDrawer;

            protected CompHoldingPlatformTarget _holdingPlatformTarget;

            public CompHoldingPlatformTarget HoldingPlatformTarget => _holdingPlatformTarget;

            public float XenoSocial = 0;
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
                foreach(ThingComp comp in _pawn.AllComps)
                {
                    if (comp is CompPawnInfo info)
                    {
                        _pawnInfo = info;
                        continue;
                    }
                    if(comp is CompMatureMorph morph)
                    {
                        _matureMorph = morph;
                        continue;
                    }
                    if (comp is CompClimber climber)
                    {
                        _climber = climber;
                        continue;
                    }
                    if (comp is CompPerfectOrganism perfect)
                    {
                        _perfect = perfect;
                        continue;
                    }
                    if (comp is CompAcidBlood acid)
                    {
                        _acidBlood = acid;
                        continue;
                    }
                    if(comp is CompHoldingPlatformTarget target)
                    {
                        _holdingPlatformTarget = target;
                        continue;
                    }
                }

               
                _isInorganic = XMTUtility.CacheIsInorganic(_pawn);

                if (XMTSettings.LogBiohorror)
                {
                    Log.Message(_pawn + " is caching inorganic status  as " + _isInorganic);
                }
            }
            public PawnCache(Pawn pawn)
            {
                _pawn = pawn;
                _mesoSkeletonDrawer = new PawnMesoSkeletonDrawer(_pawn);
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

        public static float AcidBloodValue(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].AcidBloodValue;
            }

            return 0;
        }
        public static void UpdateAcidBloodValue(this Pawn pawn, float value)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                PawnCache.cache[pawn.thingIDNumber].AcidBloodValue = value;
                return;
            }
            PawnCache newCache = new PawnCache(pawn);
            newCache.AcidBloodValue = value;
            PawnCache.cache.Add(pawn.thingIDNumber, newCache);

        }


        public static float XenoSocial(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].XenoSocial;
            }

            return 0;
        }

        public static void UpdateXenoSocial(this Pawn pawn, float value)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                PawnCache.cache[pawn.thingIDNumber].XenoSocial = value;
                return;
            }
            PawnCache newCache = new PawnCache(pawn);
            newCache.XenoSocial = value;
            PawnCache.cache.Add(pawn.thingIDNumber, newCache);

        }

        public static PawnMesoSkeletonDrawer MesoSkeletonDrawer(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].MesoSkeletonDrawer;
            }
            PawnCache newCache = new PawnCache(pawn);
            PawnCache.cache.Add(pawn.thingIDNumber, newCache);

            return newCache.MesoSkeletonDrawer;
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
                PawnCache.cache[pawn.thingIDNumber].CarboSilicate = value;
                return;
            }
            PawnCache newCache = new PawnCache(pawn);
            newCache.CarboSilicate = value;
            PawnCache.cache.Add(pawn.thingIDNumber, newCache);
           
        }

        public static CompHoldingPlatformTarget GetHoldingPlatformTarget(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].HoldingPlatformTarget;
            }

            PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

            return PawnCache.cache[pawn.thingIDNumber].HoldingPlatformTarget;
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

        public static CompClimber GetClimberComp(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].Climber;
            }

            PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

            return PawnCache.cache[pawn.thingIDNumber].Climber;
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

        public static CompPerfectOrganism GetPerfectComp(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].Perfect;
            }

            PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

            return PawnCache.cache[pawn.thingIDNumber].Perfect;
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
