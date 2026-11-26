namespace Backend;

public sealed class Graph<T> where T : notnull
{
    private readonly Dictionary<T, List<Edge>> _adjacency = new();
    private readonly bool _directed;

    public Graph(bool directed = true)
    {
        _directed = directed;
    }

    public IEnumerable<T> Vertices => _adjacency.Keys;

    public bool ContainsVertex(T vertex) => _adjacency.ContainsKey(vertex);

    public void AddVertex(T vertex)
    {
        if (!_adjacency.ContainsKey(vertex))
        {
            _adjacency[vertex] = new List<Edge>();
        }
    }

    public void AddEdge(T from, string to, double weight)
    {
        AddVertex(from);
        if (to is T toVertex)
        {
            AddVertex(toVertex);
        }

        _adjacency[from].Add(new Edge(to, weight));

        if (!_directed && from is string fromStr && to is T toAsT)
        {
            AddVertex(toAsT);
            _adjacency[toAsT].Add(new Edge(fromStr, weight));
        }
    }

    public IReadOnlyList<Edge> GetNeighbors(T vertex)
    {
        return _adjacency.TryGetValue(vertex, out List<Edge>? edges)
            ? edges
            : Array.Empty<Edge>();
    }
}