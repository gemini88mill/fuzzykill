using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace kill;

/// <summary>
/// A process that carries the username of the account running it.
/// Inherits from System.Diagnostics.Process, so it has all Process members, plus User/Domain info.
/// Note: Construct from an existing process or PID to populate the user information.
/// </summary>
public class ProcessWithUser : Process
{
    private static readonly HashSet<string> SystemOwnerSids = new(StringComparer.OrdinalIgnoreCase)
    {
        // LocalSystem, LocalService, NetworkService
        "S-1-5-18", "S-1-5-19", "S-1-5-20"
    };

    private readonly Process _inner;

    /// <summary>
    /// The user name (without domain) that owns the process, if available.
    /// </summary>
    public string? User { get; }

    /// <summary>
    /// The domain (or machine name) for the user that owns the process, if available.
    /// </summary>
    public string? Domain { get; }

    /// <summary>
    /// The Windows Security Identifier (SID) of the account that owns the process, if available.
    /// </summary>
    public string? OwnerSid { get; }

    /// <summary>
    /// True if the process is owned by a well-known system account (LocalSystem, LocalService, NetworkService).
    /// </summary>
    public bool IsSystemProcess { get; }

    /// <summary>
    /// Convenience property combining Domain and User (e.g., DOMAIN\User) when available.
    /// </summary>
    public string? UserDisplay => User is null
        ? null
        : (string.IsNullOrEmpty(Domain) ? User : $"{Domain}\\{User}");

    /// <summary>
    /// Creates an empty instance. User information is not populated.
    /// </summary>
    public ProcessWithUser()
    {
        _inner = new Process();
    }

    /// <summary>
    /// Creates a ProcessWithUser based on an existing Process.
    /// Attempts to resolve the owning user via WMI.
    /// </summary>
    public ProcessWithUser(Process p)
    {
        _inner = p;
        int pid;
        try { pid = p.Id; }
        catch { pid = -1; }
        if (pid > 0)
        {
            (Domain, User) = TryGetOwner(pid);
            OwnerSid = TryGetOwnerSid(pid);
            IsSystemProcess = OwnerSid is not null && SystemOwnerSids.Contains(OwnerSid);
        }
    }
 
     /// <summary>
     /// Creates a ProcessWithUser from a process id.
     /// Attempts to resolve the owning user via WMI.
     /// </summary>
     public ProcessWithUser(int processId)
     {
         try { _inner = Process.GetProcessById(processId); }
         catch { _inner = new Process(); }
         if (processId > 0)
         {
             (Domain, User) = TryGetOwner(processId);
             OwnerSid = TryGetOwnerSid(processId);
             IsSystemProcess = OwnerSid is not null && SystemOwnerSids.Contains(OwnerSid);
         }
     }

    /// <summary>
    /// Tries to get the owner (Domain, User) for a process id using WMI (Win32_Process.GetOwner).
    /// Returns (null, null) if it cannot be determined (access denied or process exited).
    /// </summary>
    public static (string? Domain, string? User) TryGetOwner(int pid)
    {
        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            using var proc = new ManagementObject($"win32_process.handle='{pid}'");
            using var outParams = proc.InvokeMethod("GetOwner", null, null);
            if (outParams is ManagementBaseObject mbo)
            {
                var user = mbo["User"]?.ToString();
                var domain = mbo["Domain"]?.ToString();
                if (!string.IsNullOrEmpty(user) || !string.IsNullOrEmpty(domain))
                {
                    return (domain, user);
                }
            }
#pragma warning restore CA1416
        }
        catch
        {
            // Swallow exceptions: access denied, process exited, WMI not available, etc.
        }
        return (null, null);
    }

    /// <summary>
    /// Tries to get the owner SID for a process id using WMI (Win32_Process.GetOwnerSid).
    /// Returns null if it cannot be determined (access denied or process exited).
    /// </summary>
    public static string? TryGetOwnerSid(int pid)
    {
        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            using var proc = new ManagementObject($"win32_process.handle='{pid}'");
            using var outParams = proc.InvokeMethod("GetOwnerSid", null, null);
            if (outParams is ManagementBaseObject mbo)
            {
                var sid = mbo["Sid"]?.ToString();
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    return sid;
                }
            }
#pragma warning restore CA1416
        }
        catch
        {
            // Swallow exceptions: access denied, process exited, WMI not available, etc.
        }
        return null;
    }

    // Expose the wrapped process for advanced scenarios
    public Process Inner => _inner;

    // Hide selected Process properties to delegate to the wrapped instance
    public new int Id
    {
        get { try { return _inner.Id; } catch { return -1; } }
    }

    public new string ProcessName
    {
        get { return SafeGet(() => _inner.ProcessName) ?? string.Empty; }
    }

    public new string MainWindowTitle
    {
        get { return SafeGet(() => _inner.MainWindowTitle) ?? string.Empty; }
    }

    private static T? SafeGet<T>(Func<T> getter)
    {
        try { return getter(); }
        catch { return default; }
    }
}
