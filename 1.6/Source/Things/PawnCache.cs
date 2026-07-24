


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

            protected bool _isHorror;
            public bool IsHorror => _isHorror;

            protected int _brainMutationCount;
            public bool HasBrainMutation => _brainMutationCount > 0;

            protected CompClimber _climber;
            public CompClimber Climber => _climber;

            protected CompCrawler _crawler;
            public CompCrawler Crawler => _crawler;

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
            public float InorganicSubversion = 0;
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
                    if(comp is CompCrawler crawler)
                    {
                        _crawler = crawler;
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
                _isHorror = _pawn.kindDef?.HasModExtension<XMT_HorrorPawnExtension>() == true ||
                    _pawn.def.HasModExtension<XMT_HorrorPawnExtension>();
                _brainMutationCount = CountBrainMutations(_pawn);

                if (XMTSettings.LogBiohorror)
                {
                    if (_isInorganic)
                    {
                        Log.Message("[XMT][Biohorror] " + _pawn + " is caching inorganic status  as " + _isInorganic);
                    }
                    if (_brainMutationCount > 0)
                    {
                        Log.Message(_pawn + " brainMutation count is " + _brainMutationCount);
                    }
                    if (_isHorror)
                    {
                        Log.Message(_pawn + " is caching horror status as " + _isHorror);
                    }
                }
            }

            private static int CountBrainMutations(Pawn pawn)
            {
                if (pawn?.health?.hediffSet?.hediffs == null || XenoGeneDefOf.XMT_InfluencesSet?.influences == null)
                {
                    return 0;
                }

                int count = 0;
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff?.Part == null || !IsBrainPart(hediff.Part.def))
                    {
                        continue;
                    }

                    foreach (InfluenceHealth influence in XenoGeneDefOf.XMT_InfluencesSet.influences)
                    {
                        if (influence != null && influence.hediff == hediff.def && influence.influence > 0f)
                        {
                            count++;
                            break;
                        }
                    }
                }

                return count;
            }

            private static bool IsBrainPart(BodyPartDef partDef)
            {
                return partDef == ExternalDefOf.Brain || partDef == InternalDefOf.StarbeastBrain;
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

        public static float InorganicSubversionLoad(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].InorganicSubversion;
            }

            return 0;
        }

        public static void UpdateInorganicSubversionLoad(this Pawn pawn, float value)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                PawnCache.cache[pawn.thingIDNumber].InorganicSubversion = value;
                return;
            }
            PawnCache newCache = new PawnCache(pawn);
            newCache.InorganicSubversion = value;
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

        public static CompCrawler GetCrawlerComp(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].Crawler;
            }

            PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

            return PawnCache.cache[pawn.thingIDNumber].Crawler;
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

        public static bool IsHorror(this Pawn pawn)
        {
            if (PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                return PawnCache.cache[pawn.thingIDNumber].IsHorror;
            }

            PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));

            return PawnCache.cache[pawn.thingIDNumber].IsHorror;
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

        public static bool HasBrainMutation(this Pawn pawn)
        {
            if (!PawnCache.cache.ContainsKey(pawn.thingIDNumber))
            {
                PawnCache.cache.Add(pawn.thingIDNumber, new PawnCache(pawn));
            }

            return PawnCache.cache[pawn.thingIDNumber].HasBrainMutation;
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
