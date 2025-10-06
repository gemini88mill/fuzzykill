using System.CommandLine;
using Spectre.Console;
using kill;

var query = new Argument<string>("query")
{
    Description = "Process name or command line",
};

var force = new Option<bool>("--force", "-f")
{
    Description = "will kill the process without asking for confirmation"
};

var useRegex = new Option<bool>("--regex", "-r")
{
    Description = "use regex to match processes"
};



var root = new RootCommand("Fuzzy process killer CLI");
root.Add(query);
root.Add(force);
root.Add(useRegex);


root.SetAction(parseResult =>
{
    var processManager = new ProcessManager();
    var queryRes = parseResult.GetValue(query);
    if (queryRes == null) return 0;

    IEnumerable<ProcessWithUser> processWithUsers = [];
    Logger.ShowStatus("Getting processes...", () => { processWithUsers = processManager.Discover(queryRes).ToList(); });

    var isForce = parseResult.GetValue(force);
    IReadOnlyList<ProcessWithUser> selection;
    if (isForce)
    {
        // Bypass interactive selection when --force is supplied
        selection = processWithUsers.ToList();
    }
    else
    {
        selection = Logger.SelectProcesses(processWithUsers);
    }

    Console.WriteLine($"Selected {selection.Count} processes.");
    foreach (var item in selection)
    {
        Console.WriteLine(item.ProcessName);
    }

    return 0;
});

var result = root.Parse(args);

return await result.InvokeAsync();