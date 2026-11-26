using System.Globalization;
using System.Text;

namespace Backend;

public static class RoutePrinter
{
    public static string Format(string start, List<Stop> route, RoutePlanner planner)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Start location: {start}");
        sb.AppendLine($"Total stops: {route.Count}");
        sb.AppendLine();

        string current = start;
        double totalMeters = 0.0;
        int stopNumber = 1;

        int i = 0;
        while (i < route.Count)
        {
            string targetLocation = route[i].Location;
            var actionsAtStop = new List<Stop>();

            while (i < route.Count && string.Equals(route[i].Location, targetLocation, StringComparison.Ordinal))
            {
                actionsAtStop.Add(route[i]);
                i++;
            }

            double legMeters = planner.LegDistance(current, targetLocation);
            totalMeters += legMeters;
            List<string> path = planner.PathBetween(current, targetLocation);

            sb.AppendLine($"{stopNumber}. {current} -> {targetLocation} ({legMeters.ToString("0.##", CultureInfo.InvariantCulture)} m)");

            foreach (Stop act in actionsAtStop)
            {
                string verb = act.Kind == StopKind.Pickup ? "Pickup" : "Delivery";
                sb.AppendLine($"   - {verb} package #{act.PackageId} at {act.Location}");
            }

            sb.AppendLine($"   Path: {string.Join(" -> ", path)}");
            sb.AppendLine();

            current = targetLocation;
            stopNumber++;
        }

        double totalKm = totalMeters / 1000.0;
        sb.AppendLine($"Total distance: {totalKm.ToString("0.00", CultureInfo.InvariantCulture)} km ({totalMeters.ToString("0.##", CultureInfo.InvariantCulture)} m)");
        return sb.ToString();
    }
}