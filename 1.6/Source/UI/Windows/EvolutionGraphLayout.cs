using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Xenomorphtype
{
    internal class EvolutionGraphNode
    {
        public RoyalEvolutionDef Def;
        public int Depth;
        public float Column;
        public Rect Rect;
        public readonly List<EvolutionGraphNode> Parents = new List<EvolutionGraphNode>();
        public readonly List<EvolutionGraphNode> Children = new List<EvolutionGraphNode>();
    }

    internal class EvolutionGraphLayout
    {
        private const int LayoutIterations = 8;
        private const int CrossingReductionIterations = 6;
        private const float bufferGap = 0.35f;
        private const float prerequisiteWeight = 1f;
        private const float sharedPrerequisiteWeight = 2.5f;
        private const float replacesWeight = 8f;

        private readonly float cardWidth;
        private readonly float cardHeight;
        private readonly float horizontalSpacing;
        private readonly float verticalSpacing;

        private readonly List<EvolutionGraphNode> nodes = new List<EvolutionGraphNode>();
        private readonly Dictionary<RoyalEvolutionDef, EvolutionGraphNode> nodesByDef = new Dictionary<RoyalEvolutionDef, EvolutionGraphNode>();
        private readonly Dictionary<EvolutionGraphNode, int> originalOrder = new Dictionary<EvolutionGraphNode, int>();

        public IEnumerable<EvolutionGraphNode> Nodes => nodes;
        public Vector2 Size { get; private set; }

        public EvolutionGraphLayout(float cardWidth, float cardHeight, float horizontalSpacing, float verticalSpacing)
        {
            this.cardWidth = cardWidth;
            this.cardHeight = cardHeight;
            this.horizontalSpacing = horizontalSpacing;
            this.verticalSpacing = verticalSpacing;
            Build();
        }

        public void UpdateRects(Rect rect)
        {
            float stepX = cardWidth + horizontalSpacing;
            float stepY = cardHeight + verticalSpacing;
            float maxX = 0f;
            float maxY = 0f;

            foreach (EvolutionGraphNode node in nodes)
            {
                node.Rect = new Rect(rect.x + node.Column * stepX, rect.y + node.Depth * stepY, cardWidth, cardHeight);
                maxX = Mathf.Max(maxX, node.Rect.xMax - rect.x);
                maxY = Mathf.Max(maxY, node.Rect.yMax - rect.y);
            }

            Size = new Vector2(maxX, maxY);
        }

        private void Build()
        {
            nodes.Clear();
            nodesByDef.Clear();
            originalOrder.Clear();

            foreach (RoyalEvolutionDef def in DefDatabase<RoyalEvolutionDef>.AllDefsListForReading)
            {
                EvolutionGraphNode node = new EvolutionGraphNode { Def = def };
                nodes.Add(node);
                nodesByDef[def] = node;
                originalOrder[node] = nodes.Count - 1;
            }

            foreach (EvolutionGraphNode node in nodes)
            {
                if (node.Def.prerequisites == null)
                {
                    continue;
                }

                foreach (RoyalEvolutionDef prerequisite in node.Def.prerequisites)
                {
                    EvolutionGraphNode parent;
                    if (!nodesByDef.TryGetValue(prerequisite, out parent))
                    {
                        Log.Warning("XMT evolution " + node.Def.defName + " has missing prerequisite " + prerequisite);
                        continue;
                    }

                    node.Parents.Add(parent);
                    parent.Children.Add(node);
                }
            }

            foreach (EvolutionGraphNode node in nodes)
            {
                node.Depth = GetDepth(node, new HashSet<EvolutionGraphNode>());
            }

            RelaxDepths();
            AssignColumns();
        }

        private int GetDepth(EvolutionGraphNode node, HashSet<EvolutionGraphNode> visiting)
        {
            if (node.Parents.Count == 0)
            {
                return 0;
            }

            if (!visiting.Add(node))
            {
                Log.Warning("XMT evolution dependency cycle detected at " + node.Def.defName);
                return 0;
            }

            int depth = 0;
            foreach (EvolutionGraphNode parent in node.Parents)
            {
                depth = Mathf.Max(depth, GetDepth(parent, visiting) + 1);
            }

            visiting.Remove(node);
            return depth;
        }

        private void RelaxDepths()
        {
            if (nodes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                bool changed = false;

                foreach (EvolutionGraphNode node in nodes.OrderByDescending(node => node.Depth))
                {
                    if (node.Children.Count == 0)
                    {
                        continue;
                    }

                    int minimumDepth = 0;
                    if (node.Parents.Count > 0)
                    {
                        minimumDepth = node.Parents.Max(parent => parent.Depth) + 1;
                    }

                    int maximumDepth = node.Children.Min(child => child.Depth) - 1;
                    if (maximumDepth < minimumDepth)
                    {
                        continue;
                    }

                    int relaxedDepth = maximumDepth;

                    if (relaxedDepth > node.Depth)
                    {
                        node.Depth = relaxedDepth;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        private void AssignColumns()
        {
            List<List<EvolutionGraphNode>> components = GetComponents();
            Dictionary<int, float> nextOpenColumnsByDepth = new Dictionary<int, float>();

            foreach (List<EvolutionGraphNode> component in components)
            {
                AssignComponentColumns(component);
                PlaceComponent(component, nextOpenColumnsByDepth);
            }
        }

        private List<List<EvolutionGraphNode>> GetComponents()
        {
            List<List<EvolutionGraphNode>> components = new List<List<EvolutionGraphNode>>();
            HashSet<EvolutionGraphNode> seen = new HashSet<EvolutionGraphNode>();

            foreach (EvolutionGraphNode start in nodes.OrderBy(node => originalOrder[node]))
            {
                if (seen.Contains(start))
                {
                    continue;
                }

                List<EvolutionGraphNode> component = new List<EvolutionGraphNode>();
                Stack<EvolutionGraphNode> open = new Stack<EvolutionGraphNode>();
                open.Push(start);
                seen.Add(start);

                while (open.Count > 0)
                {
                    EvolutionGraphNode node = open.Pop();
                    component.Add(node);

                    foreach (EvolutionGraphNode neighbor in node.Parents.Concat(node.Children))
                    {
                        if (seen.Add(neighbor))
                        {
                            open.Push(neighbor);
                        }
                    }
                }

                components.Add(component);
            }

            return components
                .OrderBy(component => ComponentHasRoot(component) ? 0 : 1)
                .ThenBy(component => component.Min(node => originalOrder[node]))
                .ToList();
        }

        private bool ComponentHasRoot(List<EvolutionGraphNode> component)
        {
            return component.Any(node => node.Parents.Count == 0);
        }

        private void AssignComponentColumns(List<EvolutionGraphNode> component)
        {
            List<int> depths = component
                .Select(node => node.Depth)
                .Distinct()
                .OrderBy(depth => depth)
                .ToList();

            foreach (int depth in depths)
            {
                List<EvolutionGraphNode> layer = GetLayer(component, depth)
                    .OrderBy(node => originalOrder[node])
                    .ToList();
                ApplyLocalColumns(layer);
            }

            for (int i = 0; i < LayoutIterations; i++)
            {
                foreach (int depth in depths.Skip(1))
                {
                    PackLayer(component, depth, useParents: true);
                }

                foreach (int depth in depths.Take(depths.Count - 1).Reverse<int>())
                {
                    PackLayer(component, depth, useParents: false);
                }
            }

            if (depths.Count > 1)
            {
                SnapLayerToGrid(component, depths[0], useParents: false);
            }

            foreach (int depth in depths.Skip(1))
            {
                PackLayer(component, depth, useParents: true);
            }

            ReduceCrossings(component, depths);
            RestoreLockedColumns(component, depths);
            NormalizeComponentColumns(component);
        }

        private void ReduceCrossings(List<EvolutionGraphNode> component, List<int> depths)
        {
            if (depths.Count <= 1)
            {
                return;
            }

            for (int i = 0; i < CrossingReductionIterations; i++)
            {
                foreach (int depth in depths.Skip(1))
                {
                    ReorderLayerByNeighborMedian(component, depth, useParents: true);
                }

                foreach (int depth in depths.Take(depths.Count - 1).Reverse<int>())
                {
                    ReorderLayerByNeighborMedian(component, depth, useParents: false);
                }
            }
        }

        private void ReorderLayerByNeighborMedian(List<EvolutionGraphNode> component, int depth, bool useParents)
        {
            List<EvolutionGraphNode> layer = GetLayer(component, depth);
            if (layer.Count <= 1)
            {
                return;
            }

            List<EvolutionGraphNode> sortedLayer = layer
                .OrderBy(node => NeighborMedianColumn(node, useParents))
                .ThenBy(node => WeightedNeighborColumn(node, useParents))
                .ThenBy(node => node.Column)
                .ThenBy(node => originalOrder[node])
                .ToList();

            float desiredCenter = sortedLayer.Average(node => NeighborMedianColumn(node, useParents));
            float groupStart = desiredCenter - ((sortedLayer.Count - 1) / 2f);
            for (int i = 0; i < sortedLayer.Count; i++)
            {
                sortedLayer[i].Column = groupStart + i;
            }
        }

        private void RestoreLockedColumns(List<EvolutionGraphNode> component, List<int> depths)
        {
            foreach (int depth in depths.Skip(1))
            {
                List<EvolutionGraphNode> layer = GetLayer(component, depth)
                    .OrderBy(node => originalOrder[node])
                    .ToList();

                foreach (EvolutionGraphNode node in layer)
                {
                    EvolutionGraphNode lockedParent = LockedColumnNeighbor(node, useParents: true);
                    if (lockedParent != null)
                    {
                        node.Column = lockedParent.Column;
                    }
                }

                ResolveDepthCollisions(layer);
            }
        }

        private float NeighborMedianColumn(EvolutionGraphNode node, bool useParents)
        {
            EvolutionGraphNode lockedNeighbor = LockedColumnNeighbor(node, useParents);
            if (lockedNeighbor != null)
            {
                return lockedNeighbor.Column;
            }

            List<EvolutionGraphNode> neighbors = useParents ? node.Parents : node.Children;
            if (neighbors.Count == 0)
            {
                return node.Column;
            }

            List<float> columns = neighbors
                .Select(neighbor => neighbor.Column)
                .OrderBy(column => column)
                .ToList();

            int middle = columns.Count / 2;
            if (columns.Count % 2 == 1)
            {
                return columns[middle];
            }

            return (columns[middle - 1] + columns[middle]) / 2f;
        }

        private List<EvolutionGraphNode> GetLayer(List<EvolutionGraphNode> component, int depth)
        {
            return component.Where(node => node.Depth == depth).ToList();
        }

        private void PackLayer(List<EvolutionGraphNode> component, int depth, bool useParents)
        {
            List<EvolutionGraphNode> layer = GetLayer(component, depth);
            if (layer.Count <= 1)
            {
                if (layer.Count == 1)
                {
                    layer[0].Column = DesiredColumn(layer[0], useParents);
                }

                return;
            }

            List<EvolutionGraphNode> sortedLayer = layer
                .OrderBy(node => DesiredColumn(node, useParents))
                .ThenBy(node => originalOrder[node])
                .ToList();

            Dictionary<EvolutionGraphNode, float> desiredColumns = new Dictionary<EvolutionGraphNode, float>();
            foreach (EvolutionGraphNode node in sortedLayer)
            {
                desiredColumns[node] = DesiredColumn(node, useParents);
            }

            List<List<EvolutionGraphNode>> groups = useParents
                ? GetParentBandGroups(sortedLayer)
                : GetDesiredColumnGroups(sortedLayer, desiredColumns);

            float nextOpenColumn = 0f;
            bool hasPlacedGroup = false;
            foreach (List<EvolutionGraphNode> group in groups)
            {
                float desiredCenter;
                if (!useParents || !TryGetParentBandCenter(group, out desiredCenter))
                {
                    desiredCenter = group.Average(node => desiredColumns[node]);
                }

                float groupStart = desiredCenter - ((group.Count - 1) / 2f);
                if (hasPlacedGroup)
                {
                    groupStart = Mathf.Max(groupStart, nextOpenColumn);
                }

                for (int i = 0; i < group.Count; i++)
                {
                    group[i].Column = groupStart + i;
                }

                nextOpenColumn = group[group.Count - 1].Column + 1f;
                hasPlacedGroup = true;
            }
        }

        private List<List<EvolutionGraphNode>> GetParentBandGroups(List<EvolutionGraphNode> sortedLayer)
        {
            List<List<EvolutionGraphNode>> groups = new List<List<EvolutionGraphNode>>();
            List<EvolutionGraphNode> currentGroup = null;
            HashSet<EvolutionGraphNode> currentParents = null;

            foreach (EvolutionGraphNode node in sortedLayer)
            {
                HashSet<EvolutionGraphNode> parents = new HashSet<EvolutionGraphNode>(node.Parents);
                bool sharesCurrentParents = currentParents != null && parents.Any(parent => currentParents.Contains(parent));

                if (currentGroup == null || !sharesCurrentParents)
                {
                    currentGroup = new List<EvolutionGraphNode>();
                    currentParents = new HashSet<EvolutionGraphNode>();
                    groups.Add(currentGroup);
                }

                currentGroup.Add(node);
                foreach (EvolutionGraphNode parent in parents)
                {
                    currentParents.Add(parent);
                }
            }

            return groups;
        }

        private bool TryGetParentBandCenter(List<EvolutionGraphNode> group, out float center)
        {
            List<EvolutionGraphNode> parents = group
                .SelectMany(node => node.Parents)
                .Distinct()
                .ToList();

            if (parents.Count == 0)
            {
                center = 0f;
                return false;
            }

            center = (parents.Min(parent => parent.Column) + parents.Max(parent => parent.Column)) / 2f;
            return true;
        }

        private void SnapLayerToGrid(List<EvolutionGraphNode> component, int depth, bool useParents)
        {
            List<EvolutionGraphNode> layer = GetLayer(component, depth)
                .OrderBy(node => Mathf.RoundToInt(DesiredColumn(node, useParents)))
                .ThenBy(node => DesiredColumn(node, useParents))
                .ThenBy(node => originalOrder[node])
                .ToList();

            float nextOpenColumn = 0f;
            foreach (EvolutionGraphNode node in layer)
            {
                float desiredColumn = Mathf.Round(DesiredColumn(node, useParents));
                node.Column = Mathf.Max(desiredColumn, nextOpenColumn);
                nextOpenColumn = node.Column + 1f;
            }
        }

        private List<List<EvolutionGraphNode>> GetDesiredColumnGroups(List<EvolutionGraphNode> sortedLayer, Dictionary<EvolutionGraphNode, float> desiredColumns)
        {
            List<List<EvolutionGraphNode>> groups = new List<List<EvolutionGraphNode>>();
            List<EvolutionGraphNode> currentGroup = null;
            int currentBucket = 0;
            bool currentGroupLocksSimpleChain = false;

            foreach (EvolutionGraphNode node in sortedLayer)
            {
                int bucket = Mathf.RoundToInt(desiredColumns[node]);
                bool locksSimpleChain = LockedColumnNeighbor(node, useParents: true) != null || LockedColumnNeighbor(node, useParents: false) != null;
                if (currentGroup == null || bucket != currentBucket || locksSimpleChain || currentGroupLocksSimpleChain)
                {
                    currentGroup = new List<EvolutionGraphNode>();
                    groups.Add(currentGroup);
                    currentBucket = bucket;
                    currentGroupLocksSimpleChain = locksSimpleChain;
                }

                currentGroup.Add(node);
            }

            return groups;
        }

        private void ApplyLocalColumns(List<EvolutionGraphNode> layer)
        {
            for (int i = 0; i < layer.Count; i++)
            {
                layer[i].Column = i;
            }
        }

        private float DesiredColumn(EvolutionGraphNode node, bool useParents)
        {
            EvolutionGraphNode lockedNeighbor = LockedColumnNeighbor(node, useParents);
            if (lockedNeighbor != null)
            {
                return lockedNeighbor.Column;
            }

            return WeightedNeighborColumn(node, useParents);
        }

        private EvolutionGraphNode LockedColumnNeighbor(EvolutionGraphNode node, bool useParents)
        {
            if (useParents)
            {
                if (node.Parents.Count == 1 && ParentColumnShouldOwnChild(node.Parents[0], node))
                {
                    return node.Parents[0];
                }
            }
            else if (node.Children.Count == 1 && ParentColumnShouldOwnChild(node, node.Children[0]))
            {
                return node.Children[0];
            }

            return null;
        }

        private bool ParentColumnShouldOwnChild(EvolutionGraphNode parent, EvolutionGraphNode child)
        {
            if (child.Parents.Count != 1)
            {
                return false;
            }

            if (parent.Children.Count == 1)
            {
                return true;
            }

            return parent.Children.Any(sibling => sibling != child && sibling.Parents.Count > 1);
        }

        private float WeightedNeighborColumn(EvolutionGraphNode node, bool useParents)
        {
            List<EvolutionGraphNode> neighbors = useParents ? node.Parents : node.Children;
            if (neighbors.Count == 0)
            {
                return node.Column;
            }

            float weightedTotal = 0f;
            float totalWeight = 0f;

            foreach (EvolutionGraphNode neighbor in neighbors)
            {
                float weight = useParents ? EdgeWeight(neighbor, node) : EdgeWeight(node, neighbor);
                weightedTotal += neighbor.Column * weight;
                totalWeight += weight;
            }

            return totalWeight > 0f ? weightedTotal / totalWeight : node.Column;
        }

        private void NormalizeComponentColumns(List<EvolutionGraphNode> component)
        {
            if (component.Count == 0)
            {
                return;
            }

            ResolveDepthCollisions(component);

            float minColumn = component.Min(node => node.Column);
            foreach (EvolutionGraphNode node in component)
            {
                node.Column -= minColumn;
            }
        }

        private float EdgeWeight(EvolutionGraphNode parent, EvolutionGraphNode child)
        {
            if (IsReplacesEdge(parent, child))
            {
                return replacesWeight;
            }

            if (child.Parents.Count > 1)
            {
                return sharedPrerequisiteWeight;
            }

            return prerequisiteWeight;
        }

        private bool IsReplacesEdge(EvolutionGraphNode parent, EvolutionGraphNode child)
        {
            return child.Def.replaces != null && child.Def.replaces.Contains(parent.Def);
        }

        private void PlaceComponent(List<EvolutionGraphNode> component, Dictionary<int, float> nextOpenColumnsByDepth)
        {
            if (component.Count == 0)
            {
                return;
            }

            float offset = 0f;
            foreach (IGrouping<int, EvolutionGraphNode> depthGroup in component.GroupBy(node => node.Depth))
            {
                float minColumn = depthGroup.Min(node => node.Column);
                float nextOpenColumn;
                if (nextOpenColumnsByDepth.TryGetValue(depthGroup.Key, out nextOpenColumn))
                {
                    offset = Mathf.Max(offset, nextOpenColumn - minColumn);
                }
            }

            foreach (EvolutionGraphNode node in component)
            {
                node.Column += offset;
            }

            foreach (IGrouping<int, EvolutionGraphNode> depthGroup in component.GroupBy(node => node.Depth))
            {
                float nextOpenColumn = depthGroup.Max(node => node.Column) + 1f + bufferGap;
                float currentNextOpenColumn;
                if (!nextOpenColumnsByDepth.TryGetValue(depthGroup.Key, out currentNextOpenColumn) || nextOpenColumn > currentNextOpenColumn)
                {
                    nextOpenColumnsByDepth[depthGroup.Key] = nextOpenColumn;
                }
            }
        }

        private void ResolveDepthCollisions(List<EvolutionGraphNode> targetNodes)
        {
            IEnumerable<IGrouping<int, EvolutionGraphNode>> depthGroups = targetNodes.GroupBy(node => node.Depth);
            foreach (IGrouping<int, EvolutionGraphNode> depthGroup in depthGroups)
            {
                float nextOpenColumn = 0f;
                bool hasPlacedNode = false;
                foreach (EvolutionGraphNode node in depthGroup.OrderBy(node => node.Column))
                {
                    if (hasPlacedNode && node.Column < nextOpenColumn)
                    {
                        node.Column = nextOpenColumn;
                    }

                    nextOpenColumn = node.Column + 1f;
                    hasPlacedNode = true;
                }
            }
        }
    }
}
