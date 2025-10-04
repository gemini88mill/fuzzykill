using System;
using System.Threading.Tasks;
using Spectre.Console;

namespace kill;

public static class Logger
{
    private static void WriteColored(string message, string? colorTag)
    {
        var safe = Markup.Escape(message);
        var content = colorTag is null ? safe : $"[{colorTag}]{safe}[/]";
        AnsiConsole.Write(new Markup(content + System.Environment.NewLine));
    }

    public static void Info(string message)
    {
        WriteColored(message, null);
    }

    public static void Success(string message)
    {
        WriteColored(message, "green");
    }

    public static void Warning(string message)
    {
        WriteColored(message, "yellow");
    }

    public static void Error(string message)
    {
        WriteColored(message, "red");
    }

    /// <summary>
    /// Writes a single process entry in a human-friendly, colored format.
    /// Format: PID (gray, right-aligned) Name (white) [optional Title dim] [optional Domain\User cyan]
    /// </summary>
    public static void WriteProcess(ProcessWithUser? p)
    {
        if (p is null)
        {
            Warning("<null process>");
            return;
        }

        var pid = p.Id > 0 ? p.Id.ToString() : "?";
        var name = p.ProcessName ?? string.Empty;
        var title = p.MainWindowTitle ?? string.Empty;
        var user = p.UserDisplay ?? CombineUser(p.Domain, p.User) ?? string.Empty;

        // Escape user-provided/process strings for Spectre.Console markup.
        var mPid = Markup.Escape(pid);
        var mName = Markup.Escape(name);
        var mTitle = Markup.Escape(title);
        var mUser = Markup.Escape(user);

        var line = $"[grey]{mPid,6}[/] [white]{mName}[/]";
        if (!string.IsNullOrWhiteSpace(mTitle))
        {
            line += $" [dim]{mTitle}[/]";
        }
        if (!string.IsNullOrWhiteSpace(mUser))
        {
            line += $" [cyan]{mUser}[/]";
        }
        if (p.IsSystemProcess)
        {
            line += " [bold red]SYSTEM[/]";
        }

        AnsiConsole.Write(new Markup(line + Environment.NewLine));
    }

    /// <summary>
    /// Displays processes using Spectre.Console Grid in a human-friendly way.
    /// Columns: PID, Name, Title, User. Applies styling and escapes content safely.
    /// </summary>
    public static void DisplayProcessGrid(IEnumerable<ProcessWithUser>? processes)
    {
        if (processes is null)
        {
            Info("No processes.");
            return;
        }

        var list = processes.ToList();
        if (list.Count == 0)
        {
            Info("No processes.");
            return;
        }

        var grid = new Grid();
        grid.AddColumn(new GridColumn().RightAligned()); // PID
        grid.AddColumn(new GridColumn()); // Name
        grid.AddColumn(new GridColumn()); // Title
        grid.AddColumn(new GridColumn()); // User

        // Header row
        grid.AddRow(
            new Markup("[bold grey]PID[/]"),
            new Markup("[bold]Name[/]"),
            new Markup("[bold dim]Title[/]"),
            new Markup("[bold cyan]User[/]"));

        foreach (var p in list)
        {
            var pid = p.Id > 0 ? p.Id.ToString() : "?";
            var name = p.ProcessName;
            var title = p.MainWindowTitle;
            var user = p.UserDisplay ?? CombineUser(p.Domain, p.User) ?? string.Empty;

            var mPid = Markup.Escape(pid);
            var mName = Markup.Escape(name);
            var mTitle = Markup.Escape(title);
            var mUser = Markup.Escape(user);

            var pidCell = new Markup($"[grey]{mPid}[/]");
            var nameCell = new Markup(mName + (p.IsSystemProcess ? " [bold red]SYSTEM[/]" : string.Empty));
            var titleCell = new Markup(string.IsNullOrWhiteSpace(mTitle) ? string.Empty : $"[dim]{mTitle}[/]");
            var userCell = new Markup(string.IsNullOrWhiteSpace(mUser) ? string.Empty : $"[cyan]{mUser}[/]");

            grid.AddRow(pidCell, nameCell, titleCell, userCell);
        }

        AnsiConsole.Write(grid);
    }

    public static IReadOnlyList<ProcessWithUser> SelectProcesses(IEnumerable<ProcessWithUser>? processes)
    {
        var empty = new List<ProcessWithUser>();
        if (processes is null)
        {
            Info("No processes to select.");
            return empty;
        }
        var list = processes.Where(p => p is not null).ToList();
        if (list.Count == 0)
        {
            Info("No processes to select.");
            return empty;
        }

        // Group by process name (case-insensitive)
        var groups = list
            .GroupBy(p => p.ProcessName ?? string.Empty, StringComparer.InvariantCultureIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.InvariantCultureIgnoreCase)
            .ToList();

        // Build choices: include a group entry if group has more than 1 item, plus individual entries
        var choices = new List<ChoiceItem>();
        foreach (var g in groups)
        {
            var name = g.Key ?? string.Empty;
            var members = g.OrderBy(p => p.Id).ToList();

            if (members.Count > 1)
            {
                var mName = Markup.Escape(name);
                var label = $"[bold]All[/] [white]{mName}[/] [grey]({members.Count})[/]";
                choices.Add(ChoiceItem.ForGroup(label, name, members));
            }

            foreach (var p in members)
            {
                var pid = p.Id > 0 ? p.Id.ToString() : "?";
                var title = p.MainWindowTitle ?? string.Empty;
                var user = p.UserDisplay ?? CombineUser(p.Domain, p.User) ?? string.Empty;
                var mPid = Markup.Escape(pid);
                var mName = Markup.Escape(p.ProcessName ?? string.Empty);
                var mTitle = Markup.Escape(title);
                var mUser = Markup.Escape(user);
                var label = $"[grey]{mPid,6}[/] [white]{mName}[/]" +
                            (string.IsNullOrWhiteSpace(mTitle) ? string.Empty : $" [dim]{mTitle}[/]") +
                            (string.IsNullOrWhiteSpace(mUser) ? string.Empty : $" [cyan]{mUser}[/]") +
                            (p.IsSystemProcess ? " [bold red]SYSTEM[/]" : string.Empty);
                choices.Add(ChoiceItem.ForProcess(label, p));
            }
        }

        var prompt = new MultiSelectionPrompt<ChoiceItem>()
            .Title("[bold]Select processes to kill[/] [grey](Space to toggle, Enter to confirm)[/]")
            .PageSize(15)
            .NotRequired()
            .UseConverter(ci => ci.Label)
            .InstructionsText("[grey](Press [yellow]<space>[/] to toggle a selection, [green]<enter>[/] to accept)[/]");

        prompt.AddChoices(choices);

        var selected = AnsiConsole.Prompt(prompt);
        if (selected is null || selected.Count == 0)
            return empty;

        // Expand selection into unique list of processes
        var byPid = new HashSet<int>();
        var unique = new List<ProcessWithUser>();
        var refSet = new HashSet<ProcessWithUser>(ReferenceEqualityComparer<ProcessWithUser>.Instance);

        foreach (var item in selected)
        {
            if (item.IsGroup)
            {
                foreach (var p in item.GroupMembers!)
                {
                    AddUnique(p);
                }
            }
            else if (item.Process is not null)
            {
                AddUnique(item.Process);
            }
        }

        return unique;

        void AddUnique(ProcessWithUser p)
        {
            if (p.Id > 0)
            {
                if (byPid.Add(p.Id)) unique.Add(p);
            }
            else
            {
                if (refSet.Add(p)) unique.Add(p);
            }
        }
    }

    private static string? CombineUser(string? domain, string? user)
    {
        if (string.IsNullOrEmpty(user) && string.IsNullOrEmpty(domain)) return null;
        if (string.IsNullOrEmpty(user)) return domain;
        if (string.IsNullOrEmpty(domain)) return user;
        return $"{domain}\\{user}";
    }

    // Supporting types
    private sealed class ChoiceItem
    {
        public string Label { get; }
        public bool IsGroup { get; }
        public string? GroupKey { get; }
        public List<ProcessWithUser>? GroupMembers { get; }
        public ProcessWithUser? Process { get; }

        private ChoiceItem(string label, bool isGroup, string? groupKey, List<ProcessWithUser>? groupMembers, ProcessWithUser? process)
        {
            Label = label;
            IsGroup = isGroup;
            GroupKey = groupKey;
            GroupMembers = groupMembers;
            Process = process;
        }

        public static ChoiceItem ForGroup(string label, string groupKey, List<ProcessWithUser> members)
            => new ChoiceItem(label, true, groupKey, members, null);

        public static ChoiceItem ForProcess(string label, ProcessWithUser process)
            => new ChoiceItem(label, false, null, null, process);
    }

    // Reference equality comparer for fallback uniqueness when PID is unknown
    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    // Shows a status spinner while running a synchronous operation.
    public static void ShowStatus(string message, Action operation, string? successMessage = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var safe = Markup.Escape(message ?? "Working...");
        try
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .Start(safe, _ => { operation(); });
            if (!string.IsNullOrWhiteSpace(successMessage))
                Success(successMessage!);
        }
        catch (Exception ex)
        {
            Error($"Operation failed: {ex.Message}");
            throw;
        }
    }

    // Async overload for operations returning a Task.
    public static async Task ShowStatus(string message, Func<Task> operation, string? successMessage = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var safe = Markup.Escape(message ?? "Working...");
        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync(safe, async _ => { await operation(); });
            if (!string.IsNullOrWhiteSpace(successMessage))
                Success(successMessage!);
        }
        catch (Exception ex)
        {
            Error($"Operation failed: {ex.Message}");
            throw;
        }
    }
}

