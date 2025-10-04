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

var details = new Option<bool>("--details", "-d")
{
    Description = "Show details about the matched processes"
};

var interactive = new Option<bool>("--interactive", "-i")
{
    Description = "Select processes interactively"
};


var root = new RootCommand("Fuzzy process killer CLI");
root.Add(query);
root.Add(force);
root.Add(useRegex);
root.Add(details);
root.Add(interactive);


root.SetAction(parseResult =>
{
    var processManager = new ProcessManager();
    var queryRes = parseResult.GetValue(query);
    if (queryRes == null) return 0;

    IEnumerable<ProcessWithUser> processWithUsers = [];
    Logger.ShowStatus("Getting processes...", () => { processWithUsers = processManager.Discover(queryRes).ToList(); });
    // var results = processManager.Discover(queryRes);

    var selection = Logger.SelectProcesses(processWithUsers);
    
    Console.WriteLine($"Selected {selection.Count} processes.");
    foreach (var item in selection)
    {
        Console.WriteLine(item.ProcessName);
    }

    return 0;
});

var result = root.Parse(args);

return await result.InvokeAsync();