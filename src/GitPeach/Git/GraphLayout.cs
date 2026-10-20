using GitPeach.Models;

namespace GitPeach.Git;

/// <summary>
/// Assigns lanes to a topologically ordered commit list (newest first). Can be fed pages
/// incrementally: call <see cref="Append"/> with each new batch; state carries over.
/// </summary>
public sealed class GraphLayout
{
    public const int ColorCount = 8;

    // Each active lane holds the hash of the commit it is waiting for (null = free slot).
    private readonly List<string?> _lanes = new();
    private readonly List<int> _laneColors = new();
    private int _nextColor;

    public List<GraphRow> Append(IReadOnlyList<Commit> commits)
    {
        var rows = new List<GraphRow>(commits.Count);
        foreach (var c in commits) rows.Add(Layout(c));
        return rows;
    }

    private GraphRow Layout(Commit c)
    {
        var segs = new List<GraphSegment>();

        // Lanes that were waiting for this commit.
        var incoming = new List<int>();
        for (int i = 0; i < _lanes.Count; i++)
            if (_lanes[i] == c.Hash) incoming.Add(i);

        int nodeLane;
        int nodeColor;
        if (incoming.Count > 0)
        {
            nodeLane = incoming[0];
            nodeColor = _laneColors[nodeLane];
        }
        else
        {
            nodeLane = FirstFree();
            nodeColor = NewColor();
            Set(nodeLane, c.Hash, nodeColor);
        }

        // Top half: every pass-through lane continues straight; waiting lanes converge on the node.
        for (int i = 0; i < _lanes.Count; i++)
        {
            if (_lanes[i] is null) continue;
            if (_lanes[i] == c.Hash)
                segs.Add(new GraphSegment(i, nodeLane, _laneColors[i], true));
            else
                segs.Add(new GraphSegment(i, i, _laneColors[i], true));
        }

        // Free all lanes that converged here.
        foreach (var i in incoming) _lanes[i] = null;

        // Bottom half: parents.
        bool isMerge = c.Parents.Length > 1;
        for (int p = 0; p < c.Parents.Length; p++)
        {
            var parent = c.Parents[p];
            int existing = _lanes.IndexOf(parent);
            if (existing >= 0)
            {
                segs.Add(new GraphSegment(nodeLane, existing, _laneColors[existing], false));
            }
            else if (p == 0)
            {
                Set(nodeLane, parent, nodeColor);
                segs.Add(new GraphSegment(nodeLane, nodeLane, nodeColor, false));
            }
            else
            {
                int lane = FirstFree();
                int color = NewColor();
                Set(lane, parent, color);
                segs.Add(new GraphSegment(nodeLane, lane, color, false));
            }
        }

        // Pass-through lanes on the bottom half.
        for (int i = 0; i < _lanes.Count; i++)
        {
            if (_lanes[i] is null || i == nodeLane) continue;
            bool isParentLane = Array.IndexOf(c.Parents, _lanes[i]) >= 0
                                && segs.Any(s => !s.Top && s.To == i && s.From == nodeLane);
            if (!isParentLane) segs.Add(new GraphSegment(i, i, _laneColors[i], false));
        }

        // Trim trailing free lanes.
        while (_lanes.Count > 0 && _lanes[^1] is null)
        {
            _lanes.RemoveAt(_lanes.Count - 1);
            _laneColors.RemoveAt(_laneColors.Count - 1);
        }

        int width = segs.Count == 0 ? nodeLane + 1 : Math.Max(nodeLane, segs.Max(s => Math.Max(s.From, s.To))) + 1;
        return new GraphRow
        {
            NodeLane = nodeLane,
            NodeColor = nodeColor,
            IsMerge = isMerge,
            Segments = segs,
            Width = width,
        };
    }

    private int FirstFree()
    {
        for (int i = 0; i < _lanes.Count; i++)
            if (_lanes[i] is null) return i;
        _lanes.Add(null);
        _laneColors.Add(0);
        return _lanes.Count - 1;
    }

    private void Set(int lane, string hash, int color)
    {
        while (_lanes.Count <= lane) { _lanes.Add(null); _laneColors.Add(0); }
        _lanes[lane] = hash;
        _laneColors[lane] = color;
    }

    private int NewColor() => _nextColor++ % ColorCount;
}
