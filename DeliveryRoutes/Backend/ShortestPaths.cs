namespace Backend;

public sealed class ShortestPaths
{
    private readonly Dictionary<string, double> _distances = new();
    private readonly Dictionary<string, string?> _previous = new();

    public ShortestPaths(Graph<string> graph, string source)
    {
        if (!graph.ContainsVertex(source))
        {
            throw new ArgumentException($"Source location '{source}' does not exist in graph.", nameof(source));
        }

        foreach (string v in graph.Vertices)
        {
            _distances[v] = double.PositiveInfinity;
            _previous[v] = null;
        }

        _distances[source] = 0.0;
        var queue = new PriorityQueue<string, double>();
        queue.Enqueue(source, 0.0);

        while (queue.TryDequeue(out string? current, out double currentDist))
        {
            if (current is null || currentDist > _distances[current])
            {
                continue;
            }

            foreach (Edge edge in graph.GetNeighbors(current))
            {
                double nextDist = currentDist + edge.Weight;
                if (nextDist < _distances[edge.To])
                {
                    _distances[edge.To] = nextDist;
                    _previous[edge.To] = current;
                    queue.Enqueue(edge.To, nextDist);
                }
            }
        }
    }

    public double DistanceTo(string target)
    {
        return _distances.TryGetValue(target, out double d) ? d : double.PositiveInfinity;
    }

    public List<string> ReconstructPath(string target)
    {
        var path = new List<string>();
        if (!_distances.TryGetValue(target, out double dist) || double.IsPositiveInfinity(dist))
        {
            return path;
        }

        string? step = target;
        while (step is not null)
        {
            path.Add(step);
            step = _previous.TryGetValue(step, out string? prev) ? prev : null;
        }

        path.Reverse();
        return path;
    }
}