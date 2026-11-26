using Backend;

try
{
    string cityPath;
    string packagesPath;
    string startLocation;

    if (args.Length >= 3)
    {
        cityPath = args[0];
        packagesPath = args[1];
        startLocation = args[2].Trim();
    }
    else
    {
        Console.Write("City file path: ");
        cityPath = (Console.ReadLine() ?? string.Empty).Trim();

        Console.Write("Packages file path: ");
        packagesPath = (Console.ReadLine() ?? string.Empty).Trim();

        Console.Write("Start location: ");
        startLocation = (Console.ReadLine() ?? string.Empty).Trim();
    }

    if (string.IsNullOrWhiteSpace(cityPath) ||
        string.IsNullOrWhiteSpace(packagesPath) ||
        string.IsNullOrWhiteSpace(startLocation))
    {
        Console.Error.WriteLine("Error: city file, packages file, and start location are required.");
        return 1;
    }

    Graph<string> city = CityFile.Load(cityPath);
    List<Package> packages = PackageFile.Load(packagesPath, city);

    var planner = new RoutePlanner(city, packages, startLocation);
    List<Stop> route = planner.Plan();

    if (!planner.IsValid(route))
    {
        Console.Error.WriteLine("Error: planned route failed validation.");
        return 1;
    }

    Console.WriteLine(RoutePrinter.Format(startLocation, route, planner));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}