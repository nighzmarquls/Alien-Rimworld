using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Xenomorphtype
{
    public class Designator_HiveUnlockCommand : Designator
    {
        public HiveStructureUnlockDef Unlock { get; }

        public Designator_HiveUnlockCommand(HiveStructureUnlockDef unlock)
        {
            Unlock = unlock;
            defaultLabel = unlock.LabelCap;
            defaultDesc = unlock.description;
            icon = unlock.Icon;
        }

        public override bool Visible => HiveStructureUtility.Unlocked(Unlock);

        public override AcceptanceReport CanDesignateCell(IntVec3 cell) => false;
        public override void ProcessInput(Event ev) => HiveStructureUtility.SelectDesignator(Unlock);
    }

    public class Designator_HiveHost : Designator
    {
        private readonly HiveStructureUnlockDef unlock;
        public override bool Visible => HiveStructureUtility.Unlocked(unlock);
        protected override DesignationDef Designation => unlock.designation;

        public Designator_HiveHost(HiveStructureUnlockDef unlock)
        {
            this.unlock = unlock;
            defaultLabel = unlock.LabelCap;
            defaultDesc = unlock.description;
            icon = unlock.Icon;
            soundSucceeded = SoundDefOf.Designate_Hunt;
        }

        public override AcceptanceReport CanDesignateThing(Thing thing) => HiveStructureUtility.Unlocked(unlock) && unlock.Worker.TargetAllowed(unlock, thing) && Map.designationManager.DesignationOn(thing, Designation) == null;
        public override AcceptanceReport CanDesignateCell(IntVec3 cell) => cell.InBounds(Map) && !cell.Fogged(Map) && cell.GetThingList(Map).Any(t => CanDesignateThing(t).Accepted);
        public override void DesignateSingleCell(IntVec3 cell)
        {
            foreach (Thing target in cell.GetThingList(Map).Where(t => CanDesignateThing(t).Accepted).ToList())
                DesignateThing(target);
        }

        public override void DesignateThing(Thing thing) => Map.designationManager.AddDesignation(new Designation(thing, Designation));
    }

    public class Designator_HiveStructure : Designator_Build
    {
        private readonly HiveStructureUnlockDef unlock;
        public Designator_HiveStructure(HiveStructureUnlockDef unlock) : base(unlock.building)
        {
            this.unlock = unlock;
            defaultLabel = unlock.LabelCap;
            defaultDesc = unlock.description;
            if (unlock.Icon != null)
                icon = unlock.Icon;
        }

        public override bool Visible => HiveStructureUtility.Unlocked(unlock);

        public override AcceptanceReport CanDesignateCell(IntVec3 cell) => HiveStructureSite.CanPlace(unlock, cell, placingRot, Map);
        public override void DesignateSingleCell(IntVec3 cell)
        {
            if (unlock.mode == HiveCreationMode.Build)
            {
                base.DesignateSingleCell(cell);
                return;
            }

            Map map = Map;
            Rot4 rotation = placingRot;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(unlock.confirmationKey.Translate(), () =>
            {
                var parameters = new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = false,
                    canTargetItems = false,
                    validator = t => t.Thing is Pawn p && p.Map == map && p.Faction == Faction.OfPlayer && unlock.Worker.ActorAllowed(unlock, p)
                };
                Find.Targeter.BeginTargeting(parameters, target =>
                {
                    Pawn actor = target.Pawn;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(unlock.contributorConfirmationKey.Translate(actor.LabelShortCap, unlock.building?.label ?? unlock.label), () =>
                    {
                        if (!HiveStructureSite.CanPlace(unlock, cell, rotation, map).Accepted || actor.Map != map || actor.Faction != Faction.OfPlayer || !unlock.Worker.ActorAllowed(unlock, actor))
                            return;
                        var site = (HiveStructureSite)ThingMaker.MakeThing(XenoBuildingDefOf.XMT_HiveStructureSite);
                        site.unlock = unlock;
                        site.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(site, cell, map, rotation);
                        Job job = HiveStructureUtility.MakeJob(unlock, site);
                        job.playerForced = true;
                        actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }));
                });
            }));
        }
    }
}
