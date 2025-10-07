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
    var isRegex = parseResult.GetValue(useRegex);
    Logger.ShowStatus("Getting processes...", () =>
    {
        processWithUsers = processManager.Discover(queryRes, isRegex).ToList();
        Console.WriteLine();
    });

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

    return processManager.Kill(selection);
});

var result = root.Parse(args);

return await result.InvokeAsync();