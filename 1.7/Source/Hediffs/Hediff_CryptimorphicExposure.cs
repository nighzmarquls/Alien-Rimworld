using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    public class ContaminationDeposit : IExposable
    {
        public string key;
        public float potency;
        public float volume;
        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref potency, "potency");
            Scribe_Values.Look(ref volume, "volume");
        }
    }

    public class Hediff_CryptimorphicExposure : Hediff_Tissue
    {
        public float persistentInfluence;
        public bool discovered;
        public List<ContaminationDeposit> deposits = new List<ContaminationDeposit>();
        public override bool Visible => discovered || DebugSettings.godMode;
        public override bool ShouldRemove => persistentInfluence <= 0f && (deposits == null || deposits.All(d => d.volume <= 0f));
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref persistentInfluence, "persistentInfluence");
            Scribe_Values.Look(ref discovered, "discovered");
            Scribe_Collections.Look(ref deposits, "deposits", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) deposits ??= new List<ContaminationDeposit>();
        }
        public float Influence => persistentInfluence + (deposits?.Sum(d => Mathf.Max(0f, d.volume) * d.potency) ?? 0f) /
            CryptimorphicInfluenceDef.Current.normalDepositMl * CryptimorphicInfluenceDef.Current.normalDepositInfluence;
    }
}
