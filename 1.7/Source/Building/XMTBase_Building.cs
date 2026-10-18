using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    public sealed class BuildingOrigin : IExposable
    {
        public int sourceId;
        public string name;
        public ThingDef race;
        public PawnKindDef kind;
        public ThingDef meat;
        public ThingDef blood;
        public Color skin = Color.white;
        public BuildingOrigin() { }
        public BuildingOrigin(Pawn pawn)
        {
            sourceId = pawn.thingIDNumber;
            name = pawn.Name?.ToStringShort;
            race = pawn.def;
            kind = pawn.kindDef;
            meat = pawn.RaceProps.meatDef;
            blood = pawn.RaceProps.BloodDef;
            skin = XMTUtility.GetSkinColorFrom(pawn);
        }
        public string Label => name.NullOrEmpty() ? race?.LabelCap.ToString() ?? "?"
            : "XMT_OriginIdentity".Translate(name, race?.LabelCap.ToString() ?? "?").ToString();
        public string ShortLabel => name.NullOrEmpty() ? race?.LabelCap.ToString() ?? "?" : name;
        public void ExposeData()
        {
            Scribe_Values.Look(ref sourceId, "sourceId");
            Scribe_Values.Look(ref name, "name");
            Scribe_Defs.Look(ref race, "race");
            Scribe_Defs.Look(ref kind, "kind");
            Scribe_Defs.Look(ref meat, "meat");
            Scribe_Defs.Look(ref blood, "blood");
            Scribe_Values.Look(ref skin, "skin", Color.white);
        }
    }

    public abstract class XMTBase_Building : Building
    {
        private List<BuildingOrigin> origins = new List<BuildingOrigin>();
        private DamageInfo? destructionDamage;
        internal bool SuppressTransformationEffects;
        public IReadOnlyList<BuildingOrigin> Origins => origins ??= new List<BuildingOrigin>();
        public void RecordOrigin(Pawn pawn)
        {
            origins ??= new List<BuildingOrigin>();
            if (pawn != null && !origins.Any(origin => origin.sourceId == pawn.thingIDNumber))
                origins.Add(new BuildingOrigin(pawn));
        }
        public void InheritOrigins(XMTBase_Building source)
        {
            if (source == null) return;
            origins ??= new List<BuildingOrigin>();
            foreach (BuildingOrigin origin in source.origins)
                if (!origins.Any(existing => existing.sourceId == origin.sourceId)) origins.Add(origin);
        }
        // Common setup cannot depend on derived hooks remembering a base call.
        internal void InitializeFrom(Pawn pawn, Pawn instigator = null)
        {
            RecordOrigin(pawn);
            TransformedFrom(pawn, instigator);
        }
        internal void InitializeFrom(Thing source, Pawn instigator = null)
        {
            InheritOrigins(source as XMTBase_Building);
            TransformedFrom(source, instigator);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref origins, "mutationOrigins", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) origins ??= new List<BuildingOrigin>();
        }
        public override string LabelNoCount
        {
            get
            {
                if (!XMTUtility.PlayerFactionIsCryptimorph()) return base.LabelNoCount;
                Pawn pawn = GetComp<CompPawnPreservation>()?.Entries.LastOrDefault(entry => entry.Pawn != null)?.Pawn;
                string identity = pawn != null ? new BuildingOrigin(pawn).ShortLabel : Origins.LastOrDefault()?.ShortLabel;
                return identity.NullOrEmpty() ? base.LabelNoCount
                    : "XMT_OriginBuildingLabel".Translate(base.LabelNoCount, identity).ToString();
            }
        }
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (!XMTUtility.PlayerFactionIsCryptimorph()) return text;
            CompPawnPreservation preservation = GetComp<CompPawnPreservation>();
            IEnumerable<string> identities = preservation != null && preservation.Entries.Count > 0
                ? preservation.Entries.Where(entry => entry.Pawn != null).Select(entry => new BuildingOrigin(entry.Pawn).Label)
                : Origins.Select(origin => origin.Label);
            string sources = string.Join(", ", identities);
            return sources.NullOrEmpty() ? text : text + (text.NullOrEmpty() ? "" : "\n") + "XMT_OriginSources".Translate(sources);
        }
        public override void Kill(DamageInfo? dinfo = null, Hediff exactCulprit = null)
        {
            destructionDamage = dinfo;
            try { base.Kill(dinfo, exactCulprit); }
            finally { destructionDamage = null; }
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!SuppressTransformationEffects) GetComp<CompPawnPreservation>()?.DisposeContents(destructionDamage);
            base.Destroy(mode);
        }
        public abstract void TransformedFrom(Pawn pawn, Pawn instigator = null);
        public virtual void TransformedFrom(Thing thing, Pawn instigator = null) { }
        public virtual void TransformingInto(Pawn pawn, Pawn instigator = null) { }
    }
}
