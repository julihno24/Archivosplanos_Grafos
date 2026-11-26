namespace Backend;

public sealed class RoutePlanner
{
    private readonly Graph<string> _city;
    private readonly IReadOnlyList<Package> _packages;
    private readonly string _start;
    private readonly Dictionary<string, ShortestPaths> _dijkstraByOrigin = new();
    private readonly Dictionary<int, Package> _packageById;

    public RoutePlanner(Graph<string> city, IReadOnlyList<Package> packages, string start)
    {
        _city = city ?? throw new ArgumentNullException(nameof(city));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _start = start ?? throw new ArgumentNullException(nameof(start));

        if (!_city.ContainsVertex(_start))
        {
            throw new InvalidDataException($"Start location '{_start}' does not exist in city map.");
        }

        _packageById = _packages.ToDictionary(p => p.Id);

        var relevantLocations = new HashSet<string> { _start };
        foreach (Package pkg in _packages)
        {
            relevantLocations.Add(pkg.Pickup);
            relevantLocations.Add(pkg.Delivery);
        }

        foreach (string loc in relevantLocations)
        {
            _dijkstraByOrigin[loc] = new ShortestPaths(_city, loc);
        }

        ValidateReachability(relevantLocations);
    }

    private void ValidateReachability(HashSet<string> relevantLocations)
    {
        ShortestPaths fromStart = _dijkstraByOrigin[_start];
        foreach (string loc in relevantLocations)
        {
            if (double.IsPositiveInfinity(fromStart.DistanceTo(loc)))
            {
                throw new InvalidDataException($"Location '{loc}' is not reachable from start '{_start}'.");
            }
        }

        foreach (string origin in relevantLocations)
        {
            ShortestPaths sp = _dijkstraByOrigin[origin];
            foreach (string target in relevantLocations)
            {
                if (double.IsPositiveInfinity(sp.DistanceTo(target)))
                {
                    throw new InvalidDataException($"Location '{target}' cannot be reached from '{origin}'.");
                }
            }
        }
    }

    public double LegDistance(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return 0.0;
        }

        return _dijkstraByOrigin[from].DistanceTo(to);
    }

    public List<string> PathBetween(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return new List<string> { from };
        }

        return _dijkstraByOrigin[from].ReconstructPath(to);
    }

    public double TotalDistance(List<Stop> route)
    {
        if (route is null || route.Count == 0)
        {
            return 0.0;
        }

        double total = 0.0;
        string current = _start;
        foreach (Stop stop in route)
        {
            double leg = LegDistance(current, stop.Location);
            if (double.IsPositiveInfinity(leg))
            {
                return double.PositiveInfinity;
            }
            total += leg;
            current = stop.Location;
        }

        return total;
    }

    public bool IsValid(List<Stop> route)
    {
        if (route is null || route.Count != _packages.Count * 2)
        {
            return false;
        }

        var pickedUp = new HashSet<int>();
        var delivered = new HashSet<int>();
        string current = _start;

        foreach (Stop stop in route)
        {
            if (double.IsPositiveInfinity(LegDistance(current, stop.Location)))
            {
                return false;
            }

            if (!_packageById.TryGetValue(stop.PackageId, out Package pkg))
            {
                return false;
            }

            if (stop.Kind == StopKind.Pickup)
            {
                if (!string.Equals(stop.Location, pkg.Pickup, StringComparison.Ordinal) ||
                    !pickedUp.Add(stop.PackageId))
                {
                    return false;
                }
            }
            else
            {
                if (!string.Equals(stop.Location, pkg.Delivery, StringComparison.Ordinal) ||
                    !pickedUp.Contains(stop.PackageId) ||
                    !delivered.Add(stop.PackageId))
                {
                    return false;
                }
            }

            current = stop.Location;
        }

        return pickedUp.Count == _packages.Count && delivered.Count == _packages.Count;
    }

    public List<Stop> Plan()
    {
        List<Stop> best = ImproveRoute(BuildNearestNeighborRoute());
        double bestCost = TotalDistance(best);

        for (int seed = 1; seed <= 80; seed++)
        {
            var rng = new Random(seed * 97 + 13);
            List<Stop> candidate = BuildCheapestPairInsertionRoute(rng);
            candidate = ImproveRoute(candidate);
            double cost = TotalDistance(candidate);

            if (IsValid(candidate) && cost < bestCost)
            {
                best = candidate;
                bestCost = cost;
            }

            List<Stop> randomValid = BuildRandomValidRoute(rng);
            randomValid = ImproveRoute(randomValid);
            double randomCost = TotalDistance(randomValid);

            if (IsValid(randomValid) && randomCost < bestCost)
            {
                best = randomValid;
                bestCost = randomCost;
            }
        }

        return ImproveRoute(best);
    }

    private List<Stop> BuildRandomValidRoute(Random rng)
    {
        var route = new List<Stop>(_packages.Count * 2);
        var availablePickups = _packages.Select(p => p.Id).OrderBy(_ => rng.Next()).ToList();
        var pendingDeliveries = new List<int>();

        while (availablePickups.Count > 0 || pendingDeliveries.Count > 0)
        {
            bool canPickup = availablePickups.Count > 0;
            bool canDeliver = pendingDeliveries.Count > 0;

            if (canPickup && (!canDeliver || rng.NextDouble() < 0.55))
            {
                int idx = rng.Next(availablePickups.Count);
                int pkgId = availablePickups[idx];
                availablePickups.RemoveAt(idx);
                pendingDeliveries.Add(pkgId);
                route.Add(new Stop(_packageById[pkgId].Pickup, StopKind.Pickup, pkgId));
            }
            else if (canDeliver)
            {
                int idx = rng.Next(pendingDeliveries.Count);
                int pkgId = pendingDeliveries[idx];
                pendingDeliveries.RemoveAt(idx);
                route.Add(new Stop(_packageById[pkgId].Delivery, StopKind.Delivery, pkgId));
            }
        }

        return route;
    }

    private List<Stop> BuildNearestNeighborRoute()
    {
        var route = new List<Stop>(_packages.Count * 2);
        var remainingPickups = new HashSet<int>(_packages.Select(p => p.Id));
        var pendingDeliveries = new HashSet<int>();
        string current = _start;

        while (remainingPickups.Count > 0 || pendingDeliveries.Count > 0)
        {
            Stop? bestStop = null;
            double bestDist = double.PositiveInfinity;

            foreach (int id in remainingPickups)
            {
                string loc = _packageById[id].Pickup;
                double d = LegDistance(current, loc);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestStop = new Stop(loc, StopKind.Pickup, id);
                }
            }

            foreach (int id in pendingDeliveries)
            {
                string loc = _packageById[id].Delivery;
                double d = LegDistance(current, loc);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestStop = new Stop(loc, StopKind.Delivery, id);
                }
            }

            if (bestStop is null)
            {
                break;
            }

            Stop chosen = bestStop.Value;
            route.Add(chosen);
            current = chosen.Location;

            if (chosen.Kind == StopKind.Pickup)
            {
                remainingPickups.Remove(chosen.PackageId);
                pendingDeliveries.Add(chosen.PackageId);
            }
            else
            {
                pendingDeliveries.Remove(chosen.PackageId);
            }
        }

        return route;
    }

    private List<Stop> BuildCheapestPairInsertionRoute(Random rng)
    {
        var remaining = _packages.OrderBy(_ => rng.Next()).ToList();
        var route = new List<Stop>(_packages.Count * 2);

        while (remaining.Count > 0)
        {
            int bestPkgIndex = -1;
            int bestP = -1;
            int bestD = -1;
            double bestCost = double.PositiveInfinity;

            for (int r = 0; r < remaining.Count; r++)
            {
                Package pkg = remaining[r];
                var pStop = new Stop(pkg.Pickup, StopKind.Pickup, pkg.Id);
                var dStop = new Stop(pkg.Delivery, StopKind.Delivery, pkg.Id);

                for (int i = 0; i <= route.Count; i++)
                {
                    for (int j = i + 1; j <= route.Count + 1; j++)
                    {
                        List<Stop> trial = BuildWithPairInserted(route, i, pStop, j, dStop);
                        double cost = TotalDistance(trial);
                        if (cost < bestCost && IsValid(trial))
                        {
                            bestCost = cost;
                            bestPkgIndex = r;
                            bestP = i;
                            bestD = j;
                        }
                    }
                }
            }

            if (bestPkgIndex < 0)
            {
                break;
            }

            Package chosenPkg = remaining[bestPkgIndex];
            route = BuildWithPairInserted(
                route,
                bestP,
                new Stop(chosenPkg.Pickup, StopKind.Pickup, chosenPkg.Id),
                bestD,
                new Stop(chosenPkg.Delivery, StopKind.Delivery, chosenPkg.Id));
            remaining.RemoveAt(bestPkgIndex);
        }

        return route;
    }

    private static List<Stop> BuildWithPairInserted(
        List<Stop> baseRoute,
        int i,
        Stop pStop,
        int j,
        Stop dStop)
    {
        var result = new List<Stop>(baseRoute.Count + 2);
        result.AddRange(baseRoute);
        result.Insert(i, pStop);
        result.Insert(j, dStop);
        return result;
    }

    private List<Stop> ImproveRoute(List<Stop> initial)
    {
        var current = new List<Stop>(initial);
        double bestCost = TotalDistance(current);
        bool improved = true;

        while (improved)
        {
            improved = false;

            // 1. Reubicar una parada individual.
            for (int fromIdx = 0; fromIdx < current.Count; fromIdx++)
            {
                Stop moving = current[fromIdx];
                var baseList = new List<Stop>(current);
                baseList.RemoveAt(fromIdx);

                for (int toIdx = 0; toIdx <= baseList.Count; toIdx++)
                {
                    var trial = new List<Stop>(baseList);
                    trial.Insert(toIdx, moving);

                    double cost = TotalDistance(trial);
                    if (cost + 1e-6 < bestCost && IsValid(trial))
                    {
                        bestCost = cost;
                        current = trial;
                        improved = true;
                    }
                }
            }

            // 2. Intercambiar dos paradas individuales.
            for (int a = 0; a < current.Count - 1; a++)
            {
                for (int b = a + 1; b < current.Count; b++)
                {
                    var trial = new List<Stop>(current);
                    (trial[a], trial[b]) = (trial[b], trial[a]);

                    double cost = TotalDistance(trial);
                    if (cost + 1e-6 < bestCost && IsValid(trial))
                    {
                        bestCost = cost;
                        current = trial;
                        improved = true;
                    }
                }
            }

            // 3. Reinsertar un par completo (Pickup, Delivery).
            foreach (Package pkg in _packages)
            {
                int pIdx = current.FindIndex(s => s.PackageId == pkg.Id && s.Kind == StopKind.Pickup);
                int dIdx = current.FindIndex(s => s.PackageId == pkg.Id && s.Kind == StopKind.Delivery);
                if (pIdx < 0 || dIdx < 0)
                {
                    continue;
                }

                Stop pStop = current[pIdx];
                Stop dStop = current[dIdx];

                var withoutPair = new List<Stop>(current.Count - 2);
                for (int k = 0; k < current.Count; k++)
                {
                    if (k != pIdx && k != dIdx)
                    {
                        withoutPair.Add(current[k]);
                    }
                }

                for (int i = 0; i <= withoutPair.Count; i++)
                {
                    for (int j = i + 1; j <= withoutPair.Count + 1; j++)
                    {
                        List<Stop> trial = BuildWithPairInserted(withoutPair, i, pStop, j, dStop);
                        double cost = TotalDistance(trial);
                        if (cost + 1e-6 < bestCost && IsValid(trial))
                        {
                            bestCost = cost;
                            current = trial;
                            improved = true;
                        }
                    }
                }
            }
        }

        return current;
    }
}