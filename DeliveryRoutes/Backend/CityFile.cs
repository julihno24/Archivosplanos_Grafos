using System.Globalization;

namespace Backend;

public static class CityFile
{
    public static Graph<string> Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"City file not found: {path}", path);
        }

        var graph = new Graph<string>(directed: true);
        var seenStreets = new HashSet<(string A, string B)>();
        string[] lines = File.ReadAllLines(path);

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNumber = i + 1;
            string raw = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('#'))
            {
                continue;
            }

            string[] parts = raw.Split(',');
            if (parts.Length != 4)
            {
                throw new FormatException($"Line {lineNumber}: expected 4 comma-separated fields (from,to,distance_in_meters,direction).");
            }

            string from = parts[0].Trim();
            string to = parts[1].Trim();
            string distRaw = parts[2].Trim();
            string directionRaw = parts[3].Trim();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                throw new FormatException($"Line {lineNumber}: locations 'from' and 'to' cannot be empty.");
            }

            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                throw new FormatException($"Line {lineNumber}: self-loop street '{from} -> {to}' is not allowed.");
            }

            if (!double.TryParse(distRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out double distance) ||
                double.IsNaN(distance) || double.IsInfinity(distance) || distance <= 0)
            {
                throw new FormatException($"Line {lineNumber}: distance '{distRaw}' must be a positive number.");
            }

            string direction = directionRaw.ToLowerInvariant();
            if (direction != "twoway" && direction != "oneway")
            {
                throw new FormatException($"Line {lineNumber}: invalid direction '{directionRaw}'. Expected TwoWay or OneWay.");
            }

            // Detect duplicate streets regardless of endpoint order.
            var streetKey = string.CompareOrdinal(from, to) <= 0 ? (from, to) : (to, from);
            if (!seenStreets.Add(streetKey))
            {
                throw new FormatException($"Line {lineNumber}: duplicate street between '{from}' and '{to}'.");
            }

            graph.AddVertex(from);
            graph.AddVertex(to);
            graph.AddEdge(from, to, distance);

            if (direction == "twoway")
            {
                graph.AddEdge(to, from, distance);
            }
        }

        if (!graph.Vertices.Any())
        {
            throw new InvalidDataException("City file contains no locations.");
        }

        return graph;
    }
}