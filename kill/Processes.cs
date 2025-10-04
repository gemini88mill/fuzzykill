using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace kill;

public class ProcessManager
{
    private readonly List<Process> _processes;

    public ProcessManager()
    {
        _processes = [];

        Process[] list;
        try
        {
            list = Process.GetProcesses();
        }
        catch
        {
            // In rare cases, Process.GetProcesses can throw. Leave the list empty.
            return;
        }

        foreach (var p in list)
        {
            try
            {
                _ = p.Id; // touch to force ObjectDisposed in some cases
            }
            catch
            {
                // Ignore processes we cannot access anymore
                continue;
            }

            _processes.Add(p);
        }
    }

    /// <summary>
    /// Returns the snapshot of processes captured at construction time.
    /// </summary>
    public IEnumerable<Process> GetProcesses() => _processes;

    /// <summary>
    /// Finds processes using fuzzy matching (like fzf) against the process name and command line.
    /// - If useRegex=false: performs case-insensitive fuzzy match with ranking; only returns results with score >= 3 * query length; results are ordered by best score.
    /// - If useRegex=true: interprets query as a .NET regular expression and returns matches (unordered).
    /// Returns distinct processes; ignores processes that exit during enumeration.
    /// </summary>
    public IEnumerable<ProcessWithUser> Discover(string query, bool useRegex = false)
    {
        if (string.IsNullOrWhiteSpace(query)) yield break;

        Regex? rx = null;
        var q = query.Trim();
        if (useRegex)
        {
            try
            {
                rx = new Regex(q, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                // Invalid regex: nothing will match
                yield break;
            }
        }

        var minScore = 3 * q.Length;
        var results = new List<(Process p, int score, int nameLen)>();

        foreach (var p in GetProcesses())
        {
            var name = SafeGet(() => p.ProcessName) ?? string.Empty;
            var cmd = GetCommandLineSafe(p) ?? string.Empty;

            if (useRegex)
            {
                if (rx!.IsMatch(name) || (cmd.Length > 0 && rx.IsMatch(cmd)))
                {
                    results.Add((p, 1, name.Length));
                }
                continue;
            }

            var s1 = FuzzyScore(name, q);
            var s2 = cmd.Length == 0 ? 0 : FuzzyScore(cmd, q);
            var best = Math.Max(s1, s2);
            if (best >= minScore)
            {
                results.Add((p, best, name.Length));
            }
        }

        foreach (var item in results
                     .OrderByDescending(x => x.score)
                     .ThenBy(x => x.nameLen)
                     .ThenBy(x => x.p.Id))
        {
            yield return new ProcessWithUser(item.p);
        }
    }

    private static int FuzzyScore(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrWhiteSpace(needle)) return 0;
        var h = haystack.AsSpan();
        var n = needle.AsSpan().Trim();
        var j = 0;
        var score = 0;
        var consec = 0;
        var lastMatchIndex = -2;
        for (int i = 0; i < h.Length && j < n.Length; i++)
        {
            var hc = char.ToLowerInvariant(h[i]);
            char nc = char.ToLowerInvariant(n[j]);
            if (hc == nc)
            {
                // base match
                score += 1;
                // consecutive bonus
                if (i == lastMatchIndex + 1) { consec++; score += Math.Min(3, consec); }
                else { consec = 0; }
                // word boundary / start bonus
                if (i == 0 || IsWordBoundary(h, i)) score += 3;
                lastMatchIndex = i;
                j++;
            }
        }
        return j == n.Length ? score : 0;
    }

    private static bool IsWordBoundary(ReadOnlySpan<char> s, int index)
    {
        if (index <= 0) return true;
        char prev = s[index - 1];
        char curr = s[index];
        if (!char.IsLetterOrDigit(prev) && char.IsLetterOrDigit(curr)) return true;
        if (char.IsLower(prev) && char.IsUpper(curr)) return true;
        return prev == ' ' || prev == '-' || prev == '_' || prev == '.' || prev == '\\' || prev == '/' || prev == ':';
    }

    private static string? GetCommandLineSafe(Process p)
    {
        try
        {
            // Some system processes or protected ones will throw here; we swallow and return null
            return GetCommandLine(p.Id);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Uses WMI to retrieve the command line for a process id. Returns null if not available.
    /// </summary>
    private static string? GetCommandLine(int pid)
    {
        try
        {
#pragma warning disable CA1416
            using var searcher = new ManagementObjectSearcher(
                "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + pid);
#pragma warning restore CA1416
            using var objects = searcher.Get();
            foreach (var o in objects)
            {
                var obj = (ManagementObject)o;
                return obj["CommandLine"]?.ToString();
            }
        }
        catch
        {
            // Access denied or process exited
        }
        return null;
    }

    private static T? SafeGet<T>(Func<T> getter)
    {
        try { return getter(); }
        catch { return default; }
    }
}