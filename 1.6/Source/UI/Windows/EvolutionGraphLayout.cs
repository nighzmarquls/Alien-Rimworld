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
        private float bufferGap = 0.05f;
        private readonly float cardWidth;
        private readonly float cardHeight;
        private readonly float horizontalSpacing;
        private readonly float verticalSpacing;

        private readonly List<EvolutionGraphNode> nodes = new List<EvolutionGraphNode>();
        private readonly Dictionary<RoyalEvolutionDef, EvolutionGraphNode> nodesByDef = new Dictionary<RoyalEvolutionDef, EvolutionGraphNode>();

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

        public bool TryGetNode(RoyalEvolutionDef def, out EvolutionGraphNode node)
        {
            return nodesByDef.TryGetValue(def, out node);
        }

        private void Build()
        {
            nodes.Clear();
            nodesByDef.Clear();

            foreach (RoyalEvolutionDef def in DefDatabase<RoyalEvolutionDef>.AllDefsListForReading)
            {
                EvolutionGraphNode node = new EvolutionGraphNode { Def = def };
                nodes.Add(node);
                nodesByDef[def] = node;
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

            AssignColumns();
            ResolveDepthCollisions();
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

        private void AssignColumns()
        {
            float nextColumn = 0f;
            HashSet<EvolutionGraphNode> assigned = new HashSet<EvolutionGraphNode>();
            List<EvolutionGraphNode> roots = nodes.Where(node => node.Parents.Count == 0).ToList();

            foreach (EvolutionGraphNode root in roots)
            {
                AssignColumns(root, assigned, ref nextColumn);
                nextColumn += bufferGap;
            }

            foreach (EvolutionGraphNode node in nodes)
            {
                if (!assigned.Contains(node))
                {
                    AssignColumns(node, assigned, ref nextColumn);
                    nextColumn += bufferGap;
                }
            }
        }

        private float AssignColumns(EvolutionGraphNode node, HashSet<EvolutionGraphNode> assigned, ref float nextColumn)
        {
            if (assigned.Contains(node))
            {
                return node.Column;
            }

            assigned.Add(node);

            List<EvolutionGraphNode> children = node.Children;
            if (children.Count == 0)
            {
                node.Column = nextColumn;
                nextColumn += 1f;
                return node.Column;
            }

            float total = 0f;
            foreach (EvolutionGraphNode child in children)
            {
                total += AssignColumns(child, assigned, ref nextColumn);
            }

            node.Column = total / children.Count;
            return node.Column;
        }

        private void ResolveDepthCollisions()
        {
            IEnumerable<IGrouping<int, EvolutionGraphNode>> depthGroups = nodes.GroupBy(node => node.Depth);
            foreach (IGrouping<int, EvolutionGraphNode> depthGroup in depthGroups)
            {
                float nextOpenColumn = 0f;
                foreach (EvolutionGraphNode node in depthGroup.OrderBy(node => node.Column))
                {
                    if (node.Column < nextOpenColumn)
                    {
                        node.Column = nextOpenColumn;
                    }

                    nextOpenColumn = node.Column + 1f;
                }
            }
        }
    }
}
