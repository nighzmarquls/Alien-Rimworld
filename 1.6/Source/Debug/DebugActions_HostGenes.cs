using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Xenomorphtype
{
    public static class DebugActions_HostGenes
    {
        private const string Category = "Alien | Rimworld";

        [DebugAction(Category, "Spawn gene ovamorph by host source", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnGeneOvomorphByHostSource()
        {
            List<HostGeneSource> sources = AllHostGeneSources().OrderBy(source => source.MenuLabel).ToList();

            if (sources.NullOrEmpty())
            {
                Messages.Message("No host gene sources found.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (HostGeneSource source in sources)
            {
                HostGeneSource selectedSource = source;
                options.Add(new FloatMenuOption(selectedSource.MenuLabel, delegate
                {
                    BeginSpawnTargeter(new List<SpawnRequest>
                    {
                        new SpawnRequest(selectedSource.MenuLabel, selectedSource.Genes)
                    });
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction(Category, "Spawn all custom host gene examples", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnAllCustomHostGeneExamples()
        {
            Dictionary<GeneDef, HostGeneSource> examplesByGene = new Dictionary<GeneDef, HostGeneSource>();

            foreach (HostGeneSource source in AllHostGeneSources())
            {
                foreach (GeneDef gene in source.Genes)
                {
                    if (gene == null || !IsCustomHostGene(gene) || examplesByGene.ContainsKey(gene))
                    {
                        continue;
                    }

                    examplesByGene.Add(gene, source);
                }
            }

            List<SpawnRequest> requests = examplesByGene
                .OrderBy(pair => pair.Key.defName)
                .Select(pair => new SpawnRequest("Example: " + pair.Key.defName + " from " + pair.Value.SourceDef.defName, new List<GeneDef> { pair.Key }))
                .ToList();

            BeginSpawnTargeter(requests);
        }

        [DebugAction(Category, "Spawn all host source gene ovamorphs", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnAllHostSourceGeneOvomorphs()
        {
            List<SpawnRequest> requests = AllHostGeneSources()
                .OrderBy(source => source.MenuLabel)
                .Select(source => new SpawnRequest(source.MenuLabel, source.Genes))
                .ToList();

            BeginSpawnTargeter(requests);
        }

        private static void BeginSpawnTargeter(List<SpawnRequest> requests)
        {
            if (requests.NullOrEmpty())
            {
                Messages.Message("No host gene ovamorph examples found.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            TargetingParameters targetingParameters = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = target => target.Cell.InBounds(Find.CurrentMap)
            };

            Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo target)
            {
                Map map = Find.CurrentMap;
                if (map == null)
                {
                    Messages.Message("No current map found.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                int spawned = SpawnRequestsAround(target.Cell, map, requests);
                Messages.Message("Spawned " + spawned + " gene ovamorph example(s).", MessageTypeDefOf.TaskCompletion, false);
            });
        }

        private static int SpawnRequestsAround(IntVec3 center, Map map, List<SpawnRequest> requests)
        {
            int spawned = 0;
            IEnumerator<IntVec3> cellEnumerator = SpawnCellsAround(center, map).GetEnumerator();

            foreach (SpawnRequest request in requests)
            {
                if (!cellEnumerator.MoveNext())
                {
                    Log.Warning("[Xenomorphtype] Ran out of valid cells while spawning host gene ovamorph examples.");
                    break;
                }

                GeneOvomorph ovamorph = GenSpawn.Spawn(XenoBuildingDefOf.XMT_GeneOvomorph, cellEnumerator.Current, map, WipeMode.VanishOrMoveAside) as GeneOvomorph;
                if (ovamorph == null)
                {
                    continue;
                }

                AssignGenes(ovamorph, request);
                spawned++;
            }

            return spawned;
        }

        private static IEnumerable<IntVec3> SpawnCellsAround(IntVec3 center, Map map)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 20f, true))
            {
                if (cell.InBounds(map) && !cell.Fogged(map))
                {
                    yield return cell;
                }
            }
        }

        private static void AssignGenes(GeneOvomorph ovamorph, SpawnRequest request)
        {
            CompHiveGeneHolder geneHolder = ovamorph.GetComp<CompHiveGeneHolder>();
            if (geneHolder == null)
            {
                Log.Warning("[Xenomorphtype] Spawned gene ovamorph missing CompHiveGeneHolder.");
                return;
            }

            geneHolder.templateName = request.TemplateName;
            geneHolder.genes = new GeneSet();
            BioUtility.ExtractGenesToGeneset(ref geneHolder.genes, request.Genes);
        }

        private static IEnumerable<HostGeneSource> AllHostGeneSources()
        {
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (thingDef.modExtensions.NullOrEmpty())
                {
                    continue;
                }

                List<GeneDef> genes = new List<GeneDef>();

                foreach (AnimalHostGenes hostGenes in thingDef.modExtensions.OfType<AnimalHostGenes>())
                {
                    if (hostGenes.genes.NullOrEmpty())
                    {
                        continue;
                    }

                    foreach (GeneDef gene in hostGenes.genes)
                    {
                        if (gene != null && !genes.Contains(gene))
                        {
                            genes.Add(gene);
                        }
                    }
                }

                if (!genes.NullOrEmpty())
                {
                    yield return new HostGeneSource(thingDef, genes);
                }
            }
        }

        private static bool IsCustomHostGene(GeneDef gene)
        {
            return gene.defName.StartsWith("XMT_");
        }

        private class HostGeneSource
        {
            public HostGeneSource(ThingDef sourceDef, List<GeneDef> genes)
            {
                SourceDef = sourceDef;
                Genes = genes;
            }

            public ThingDef SourceDef { get; private set; }

            public List<GeneDef> Genes { get; private set; }

            public string MenuLabel
            {
                get
                {
                    string label = SourceDef.label.NullOrEmpty() ? SourceDef.defName : SourceDef.label;
                    return SourceDef.defName + " / " + label;
                }
            }
        }

        private class SpawnRequest
        {
            public SpawnRequest(string templateName, List<GeneDef> genes)
            {
                TemplateName = templateName;
                Genes = genes;
            }

            public string TemplateName { get; private set; }

            public List<GeneDef> Genes { get; private set; }
        }
    }
}
