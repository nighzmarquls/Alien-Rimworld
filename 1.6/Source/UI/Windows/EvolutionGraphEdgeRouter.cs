using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Xenomorphtype
{
    internal class EvolutionGraphEdgeRoute
    {
        public EvolutionGraphNode Parent;
        public EvolutionGraphNode Child;
        public readonly List<Vector2> Points = new List<Vector2>();
    }

    internal class EvolutionGraphEdgeRouter
    {
        private const float endpointSpacing = 7f;
        private const float preferredLaneSpacing = 18f;
        private const float lanePadding = 8f;
        private const float minimumLaneSpacing = 6f;
        private const float horizontalLaneOverlapPadding = 8f;
        private const int minimumChildMergeEdgeCount = 2;

        private class Edge
        {
            public EvolutionGraphNode Parent;
            public EvolutionGraphNode Child;
            public int Order;
            public Vector2 Start;
            public Vector2 End;
            public float LaneY;
        }

        private class LaneReservation
        {
            public readonly List<Edge> Edges = new List<Edge>();
            public int LaneIndex;
            public int PreferredLaneIndex;
            public float SpanStart;
            public float SpanEnd;
            public float AnchorX;
            public int Order;
        }

        public List<EvolutionGraphEdgeRoute> BuildRoutes(IEnumerable<EvolutionGraphNode> nodes)
        {
            List<Edge> edges = BuildEdges(nodes);
            AssignEndpoints(edges);
            AssignLanes(edges);
            return edges.Select(BuildRoute).ToList();
        }

        private List<Edge> BuildEdges(IEnumerable<EvolutionGraphNode> nodes)
        {
            List<Edge> edges = new List<Edge>();
            foreach (EvolutionGraphNode child in nodes)
            {
                foreach (EvolutionGraphNode parent in child.Parents)
                {
                    edges.Add(new Edge
                    {
                        Parent = parent,
                        Child = child,
                        Order = edges.Count
                    });
                }
            }

            return edges;
        }

        private void AssignEndpoints(List<Edge> edges)
        {
            foreach (IGrouping<EvolutionGraphNode, Edge> parentGroup in edges.GroupBy(edge => edge.Parent))
            {
                List<Edge> orderedEdges = parentGroup
                    .OrderBy(edge => edge.Child.Rect.center.x)
                    .ThenBy(edge => edge.Child.Rect.center.y)
                    .ThenBy(edge => edge.Order)
                    .ToList();

                for (int i = 0; i < orderedEdges.Count; i++)
                {
                    Edge edge = orderedEdges[i];
                    edge.Start = new Vector2(edge.Parent.Rect.center.x + CenteredOffset(i, orderedEdges.Count, endpointSpacing), edge.Parent.Rect.yMax);
                }
            }

            foreach (IGrouping<EvolutionGraphNode, Edge> childGroup in edges.GroupBy(edge => edge.Child))
            {
                List<Edge> orderedEdges = childGroup
                    .OrderBy(edge => edge.Parent.Rect.center.x)
                    .ThenBy(edge => edge.Parent.Rect.center.y)
                    .ThenBy(edge => edge.Order)
                    .ToList();

                for (int i = 0; i < orderedEdges.Count; i++)
                {
                    Edge edge = orderedEdges[i];
                    edge.End = new Vector2(edge.Child.Rect.center.x + CenteredOffset(i, orderedEdges.Count, endpointSpacing), edge.Child.Rect.yMin);
                }
            }
        }

        private void AssignLanes(List<Edge> edges)
        {
            foreach (IGrouping<int, Edge> group in edges.GroupBy(LaneGroupKey))
            {
                List<Edge> orderedEdges = group
                    .OrderBy(edge => Mathf.Min(edge.Start.x, edge.End.x))
                    .ThenBy(edge => Mathf.Max(edge.Start.x, edge.End.x))
                    .ThenBy(edge => edge.Start.x)
                    .ThenBy(edge => edge.End.x)
                    .ThenBy(edge => edge.Order)
                    .ToList();

                List<LaneReservation> reservations = BuildLaneReservations(orderedEdges);
                AssignPreferredLaneIndices(reservations);
                int laneCount = AssignLaneIndices(reservations);
                float top = orderedEdges.Min(edge => edge.Start.y);
                float bottom = orderedEdges.Max(edge => edge.End.y);
                float laneSpacing = GetLaneSpacing(laneCount, bottom - top);
                float laneStart = GetLaneStart(top, bottom, laneCount, laneSpacing);

                foreach (LaneReservation reservation in reservations)
                {
                    float laneY = laneStart + (reservation.LaneIndex * laneSpacing);
                    foreach (Edge edge in reservation.Edges)
                    {
                        edge.LaneY = laneY;
                    }
                }
            }
        }

        private List<LaneReservation> BuildLaneReservations(List<Edge> edges)
        {
            List<LaneReservation> reservations = new List<LaneReservation>();
            HashSet<Edge> reservedEdges = new HashSet<Edge>();

            foreach (IGrouping<EvolutionGraphNode, Edge> childGroup in edges.GroupBy(edge => edge.Child))
            {
                List<Edge> incomingEdges = childGroup
                    .OrderBy(edge => edge.Start.x)
                    .ThenBy(edge => edge.Order)
                    .ToList();

                if (incomingEdges.Count < minimumChildMergeEdgeCount)
                {
                    continue;
                }

                float mergeX = childGroup.Key.Rect.center.x;
                foreach (Edge edge in incomingEdges)
                {
                    edge.End = new Vector2(mergeX, edge.Child.Rect.yMin);
                    reservedEdges.Add(edge);
                }

                LaneReservation reservation = new LaneReservation
                {
                    SpanStart = incomingEdges.Min(edge => Mathf.Min(edge.Start.x, edge.End.x)),
                    SpanEnd = incomingEdges.Max(edge => Mathf.Max(edge.Start.x, edge.End.x)),
                    AnchorX = incomingEdges.Average(edge => edge.End.x),
                    Order = incomingEdges.Min(edge => edge.Order)
                };
                reservation.Edges.AddRange(incomingEdges);
                reservations.Add(reservation);
            }

            foreach (Edge edge in edges)
            {
                if (reservedEdges.Contains(edge))
                {
                    continue;
                }

                LaneReservation reservation = new LaneReservation
                {
                    SpanStart = SpanStart(edge),
                    SpanEnd = SpanEnd(edge),
                    AnchorX = edge.End.x,
                    Order = edge.Order
                };
                reservation.Edges.Add(edge);
                reservations.Add(reservation);
            }

            return reservations
                .OrderBy(reservation => reservation.SpanStart)
                .ThenBy(reservation => reservation.SpanEnd)
                .ThenBy(reservation => reservation.Order)
                .ToList();
        }

        private void AssignPreferredLaneIndices(List<LaneReservation> reservations)
        {
            foreach (LaneReservation reservation in reservations)
            {
                reservation.PreferredLaneIndex = reservations.Count(otherReservation =>
                    otherReservation != reservation &&
                    ReservationsOverlap(otherReservation, reservation) &&
                    LanePreferenceSortsBefore(otherReservation, reservation));
            }
        }

        private int AssignLaneIndices(List<LaneReservation> reservations)
        {
            List<LaneReservation> activeReservations = new List<LaneReservation>();
            int laneCount = 0;

            foreach (LaneReservation reservation in reservations)
            {
                activeReservations.RemoveAll(activeReservation => activeReservation.SpanEnd + horizontalLaneOverlapPadding < reservation.SpanStart);

                HashSet<int> occupiedLanes = new HashSet<int>(activeReservations.Select(activeReservation => activeReservation.LaneIndex));
                int laneIndex = BestAvailableLaneIndex(occupiedLanes, reservation.PreferredLaneIndex);

                reservation.LaneIndex = laneIndex;
                laneCount = Mathf.Max(laneCount, laneIndex + 1);
                activeReservations.Add(reservation);
            }

            return Mathf.Max(1, laneCount);
        }

        private int BestAvailableLaneIndex(HashSet<int> occupiedLanes, int preferredLaneIndex)
        {
            int maximumCandidate = occupiedLanes.Count + 1;
            int bestLaneIndex = 0;
            float bestScore = float.MaxValue;

            for (int laneIndex = 0; laneIndex <= maximumCandidate; laneIndex++)
            {
                if (occupiedLanes.Contains(laneIndex))
                {
                    continue;
                }

                float score = Mathf.Abs(laneIndex - preferredLaneIndex);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestLaneIndex = laneIndex;
                }
            }

            return bestLaneIndex;
        }

        private bool ReservationsOverlap(LaneReservation a, LaneReservation b)
        {
            return a.SpanStart <= b.SpanEnd + horizontalLaneOverlapPadding &&
                b.SpanStart <= a.SpanEnd + horizontalLaneOverlapPadding;
        }

        private bool LanePreferenceSortsBefore(LaneReservation a, LaneReservation b)
        {
            bool sharedParentPreference;
            if (TryGetSharedParentLanePreference(a, b, out sharedParentPreference))
            {
                return sharedParentPreference;
            }

            if (!Mathf.Approximately(a.AnchorX, b.AnchorX))
            {
                return a.AnchorX < b.AnchorX;
            }

            if (!Mathf.Approximately(a.SpanStart, b.SpanStart))
            {
                return a.SpanStart < b.SpanStart;
            }

            if (!Mathf.Approximately(a.SpanEnd, b.SpanEnd))
            {
                return a.SpanEnd < b.SpanEnd;
            }

            return a.Order < b.Order;
        }

        private bool TryGetSharedParentLanePreference(LaneReservation a, LaneReservation b, out bool aSortsBeforeB)
        {
            List<EvolutionGraphNode> sharedParents = a.Edges
                .Select(edge => edge.Parent)
                .Intersect(b.Edges.Select(edge => edge.Parent))
                .ToList();

            if (sharedParents.Count == 0)
            {
                aSortsBeforeB = false;
                return false;
            }

            float aDistance = SharedParentHorizontalDistance(a, sharedParents);
            float bDistance = SharedParentHorizontalDistance(b, sharedParents);
            if (!Mathf.Approximately(aDistance, bDistance))
            {
                aSortsBeforeB = aDistance > bDistance;
                return true;
            }

            float aStart = SharedParentStartX(a, sharedParents);
            float bStart = SharedParentStartX(b, sharedParents);
            if (!Mathf.Approximately(aStart, bStart))
            {
                aSortsBeforeB = aStart < bStart;
                return true;
            }

            aSortsBeforeB = false;
            return false;
        }

        private float SharedParentHorizontalDistance(LaneReservation reservation, List<EvolutionGraphNode> sharedParents)
        {
            return reservation.Edges
                .Where(edge => sharedParents.Contains(edge.Parent))
                .Average(edge => Mathf.Abs(edge.End.x - edge.Start.x));
        }

        private float SharedParentStartX(LaneReservation reservation, List<EvolutionGraphNode> sharedParents)
        {
            return reservation.Edges
                .Where(edge => sharedParents.Contains(edge.Parent))
                .Average(edge => edge.Start.x);
        }

        private int LaneGroupKey(Edge edge)
        {
            int startY = Mathf.RoundToInt(edge.Start.y);
            int endY = Mathf.RoundToInt(edge.End.y);
            return (startY * 397) ^ endY;
        }

        private float GetLaneSpacing(int laneCount, float verticalDistance)
        {
            if (laneCount <= 1)
            {
                return 0f;
            }

            float available = Mathf.Max(0f, verticalDistance - (lanePadding * 2f));
            float fullBandSpacing = available / (laneCount - 1);
            if (fullBandSpacing < minimumLaneSpacing)
            {
                return fullBandSpacing;
            }

            return Mathf.Min(preferredLaneSpacing, fullBandSpacing);
        }

        private float GetLaneStart(float top, float bottom, int laneCount, float laneSpacing)
        {
            float center = top + ((bottom - top) / 2f);
            float fullLaneHeight = (laneCount - 1) * laneSpacing;
            float paddedStart = top + lanePadding;
            float centeredStart = center - (fullLaneHeight / 2f);
            return Mathf.Max(paddedStart, centeredStart);
        }

        private float SpanStart(Edge edge)
        {
            return Mathf.Min(edge.Start.x, edge.End.x);
        }

        private float SpanEnd(Edge edge)
        {
            return Mathf.Max(edge.Start.x, edge.End.x);
        }

        private EvolutionGraphEdgeRoute BuildRoute(Edge edge)
        {
            EvolutionGraphEdgeRoute route = new EvolutionGraphEdgeRoute
            {
                Parent = edge.Parent,
                Child = edge.Child
            };

            route.Points.Add(edge.Start);
            route.Points.Add(new Vector2(edge.Start.x, edge.LaneY));
            route.Points.Add(new Vector2(edge.End.x, edge.LaneY));
            route.Points.Add(edge.End);
            RemoveRedundantPoints(route.Points);
            return route;
        }

        private void RemoveRedundantPoints(List<Vector2> points)
        {
            for (int i = points.Count - 2; i > 0; i--)
            {
                if (Approximately(points[i - 1], points[i]) || Approximately(points[i], points[i + 1]))
                {
                    points.RemoveAt(i);
                }
            }
        }

        private bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
        }

        private float CenteredOffset(int index, int count, float spacing)
        {
            if (count <= 1)
            {
                return 0f;
            }

            return (index - ((count - 1) / 2f)) * spacing;
        }
    }
}
