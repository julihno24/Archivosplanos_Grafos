namespace Backend;

public static class PackageFile
{
    public static List<Package> Load(string path, Graph<string> city)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Packages file not found: {path}", path);
        }

        var pickups = new Dictionary<int, (string Location, int Line)>();
        var deliveries = new Dictionary<int, (string Location, int Line)>();
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
            if (parts.Length != 3)
            {
                throw new FormatException($"Line {lineNumber}: expected 3 comma-separated fields (location,action,package).");
            }

            string location = parts[0].Trim();
            string actionRaw = parts[1].Trim();
            string packageRaw = parts[2].Trim();

            if (!city.ContainsVertex(location))
            {
                throw new InvalidDataException($"Line {lineNumber}: location '{location}' does not exist in city map.");
            }

            string action = actionRaw.ToLowerInvariant();
            StopKind kind;
            if (action == "pickup")
            {
                kind = StopKind.Pickup;
            }
            else if (action == "delivery")
            {
                kind = StopKind.Delivery;
            }
            else
            {
                throw new FormatException($"Line {lineNumber}: invalid action '{actionRaw}'. Expected Pickup or Delivery.");
            }

            if (!int.TryParse(packageRaw, out int packageId) || packageId <= 0)
            {
                throw new FormatException($"Line {lineNumber}: package id '{packageRaw}' must be a positive integer.");
            }

            if (kind == StopKind.Pickup)
            {
                if (!pickups.TryAdd(packageId, (location, lineNumber)))
                {
                    throw new InvalidDataException($"Line {lineNumber}: duplicate Pickup for package {packageId}.");
                }
            }
            else
            {
                if (!deliveries.TryAdd(packageId, (location, lineNumber)))
                {
                    throw new InvalidDataException($"Line {lineNumber}: duplicate Delivery for package {packageId}.");
                }
            }
        }

        foreach (int id in pickups.Keys)
        {
            if (!deliveries.ContainsKey(id))
            {
                throw new InvalidDataException($"Package {id} has a Pickup but is missing a Delivery.");
            }
        }

        foreach (int id in deliveries.Keys)
        {
            if (!pickups.ContainsKey(id))
            {
                throw new InvalidDataException($"Package {id} has a Delivery but is missing a Pickup.");
            }
        }

        if (pickups.Count == 0)
        {
            throw new InvalidDataException("Packages file contains no valid packages.");
        }

        var packages = new List<Package>(pickups.Count);
        foreach (int id in pickups.Keys.OrderBy(x => x))
        {
            packages.Add(new Package(id, pickups[id].Location, deliveries[id].Location));
        }

        return packages;
    }
}