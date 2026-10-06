// ACE Dell Control - fan and thermal control for the Dell Latitude 7400
// Copyright (C) 2026 ACE Global Pro
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License version 3 as published
// by the Free Software Foundation. It is distributed in the hope that it will
// be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details. See the LICENSE file in this repository.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AceDellControl
{
    internal static class Paths
    {
        internal static readonly string Base = AppDomain.CurrentDomain.BaseDirectory;
        internal static readonly string FanDir = Path.Combine(Base, "tools", "DellFanCmd");
        internal static readonly string Fan = Path.Combine(FanDir, "DellFanCmd.exe");
        internal static readonly string Thermal = Path.Combine(Base, "tools", "DellSetThermalSetting", "DellSetThermalSetting.exe");
        internal static readonly string LhmDir = Path.Combine(Base, "tools", "LibreHardwareMonitor");
        internal static readonly string Lhm = Path.Combine(LhmDir, "LibreHardwareMonitorLib.dll");
        // Absolute System32 paths: never let the search order pick a copy from the app folder (H4).
        internal static readonly string PowerCfg = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
        internal static readonly string Sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        internal static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ACE Dell Control");
        internal static readonly string Logs = Path.Combine(Data, "logs");
        internal static readonly string Marker = Path.Combine(Data, "manual-mode.marker");
    }

    /// <summary>%ProgramData%\ACE Dell Control, locked to Administrators and SYSTEM (Users read only).</summary>
    internal static class DataDir
    {
        internal static bool Trusted;
        internal static string Problem = "not initialised";

        internal static bool Ensure()
        {
            try
            {
                foreach (string dir in new string[] { Paths.Data, Paths.Logs })
                {
                    if (Directory.Exists(dir))
                    {
                        if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                        {
                            Problem = dir + " is a link or junction; refusing to use it";
                            return false;
                        }
                    }
                    else Directory.CreateDirectory(dir);
                    Directory.SetAccessControl(dir, AdminOnly());
                    try
                    {
                        DirectorySecurity owner = new DirectorySecurity();
                        owner.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
                        Directory.SetAccessControl(dir, owner);
                    }
                    catch { }
                }
                Trusted = true;
                Problem = null;
                return true;
            }
            catch (Exception ex)
            {
                Problem = ex.Message;
                return false;
            }
        }

        private static DirectorySecurity AdminOnly()
        {
            DirectorySecurity s = new DirectorySecurity();
            s.SetAccessRuleProtection(true, false);
            InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            s.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            s.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            s.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
            return s;
        }
    }

    /// <summary>Plain, dated log file that mirrors the on-screen activity log (M10).</summary>
    internal static class ActivityLog
    {
        private static readonly object Gate = new object();
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        internal static event Action<string> Line;

        internal static string CurrentFile
        {
            get { return Path.Combine(Paths.Logs, "activity-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log"); }
        }

        internal static void Write(string message)
        {
            if (String.IsNullOrWhiteSpace(message)) return;
            string text = message.Trim();
            DateTime now = DateTime.Now;
            if (DataDir.Trusted)
            {
                lock (Gate)
                {
                    try
                    {
                        File.AppendAllText(CurrentFile, now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " +
                            text.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine, Utf8);
                    }
                    catch { }
                }
            }
            Action<string> handler = Line;
            if (handler != null)
            {
                try { handler("[" + now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + text); }
                catch { }
            }
        }
    }

    /// <summary>On-disk record that Dell EC fan control may be disabled (H1/H2).</summary>
    internal static class ManualMarker
    {
        internal static bool Exists()
        {
            try { return File.Exists(Paths.Marker); }
            catch { return false; }
        }

        internal static bool Set(string reason)
        {
            if (!DataDir.Trusted) return false;
            try
            {
                File.WriteAllText(Paths.Marker, DateTime.Now.ToString("o", CultureInfo.InvariantCulture) + " " + reason + Environment.NewLine);
                return true;
            }
            catch { return false; }
        }

        internal static void Clear()
        {
            try { if (File.Exists(Paths.Marker)) File.Delete(Paths.Marker); }
            catch (Exception ex) { ActivityLog.Write("WARNING: manual-mode marker could not be removed: " + ex.Message); }
        }
    }

    internal sealed class CommandResult
    {
        internal int ExitCode;
        internal string Output = "";
        internal string Error = "";
        internal bool Started;
        internal bool TimedOut;
        internal bool Skipped;

        internal string Tail(int max)
        {
            string all = (Output + " " + Error).Trim();
            if (all.Length == 0) return "no output";
            all = System.Text.RegularExpressions.Regex.Replace(all, "\\s+", " ");
            return all.Length > max ? "…" + all.Substring(all.Length - max) : all;
        }

        internal static CommandResult SkippedResult(string why)
        {
            CommandResult r = new CommandResult();
            r.Skipped = true;
            r.ExitCode = -4;
            r.Error = why;
            return r;
        }
    }

    /// <summary>
    /// The single serialized executor for every external tool (C2). One process at a time;
    /// a pending Dell Auto restore makes queued non-restore work skip (restore has priority).
    /// The lock is always acquired and released on a pool thread or synchronously by the
    /// caller, never across an await on the UI thread, so a blocking UI-thread restore
    /// cannot deadlock against an in-flight async command.
    /// </summary>
    internal static class ToolRunner
    {
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);
        private static int restorePending;

        internal static bool Busy { get { return Lock.CurrentCount == 0; } }
        internal static bool RestorePending { get { return Thread.VolatileRead(ref restorePending) > 0; } }
        internal static void BeginRestore() { Interlocked.Increment(ref restorePending); }
        internal static void EndRestore() { Interlocked.Decrement(ref restorePending); }

        internal static Task<CommandResult> RunAsync(string file, string args, int timeout)
        {
            return Task.Run(delegate { return Run(file, args, timeout, Timeout.Infinite, false, false); });
        }

        /// <summary>Runs only if nothing else is running (keep-alive, background RPM).</summary>
        internal static Task<CommandResult> RunIfIdleAsync(string file, string args, int timeout)
        {
            return Task.Run(delegate { return Run(file, args, timeout, 0, false, true); });
        }

        internal static CommandResult Run(string file, string args, int timeout, int lockWaitMs, bool isRestore, bool quiet)
        {
            if (!Lock.Wait(lockWaitMs)) return CommandResult.SkippedResult("Another command is still running.");
            try
            {
                if (RestorePending && !isRestore) return CommandResult.SkippedResult("Skipped: a Dell Auto restore has priority.");
                return Execute(file, args, timeout, quiet);
            }
            finally { Lock.Release(); }
        }

        internal static bool WaitIdle(int ms)
        {
            if (!Lock.Wait(ms)) return false;
            Lock.Release();
            return true;
        }

        /// <summary>Stops and deletes a leftover BZHDELLSMMIO driver service (after a killed DellFanCmd).</summary>
        internal static void CleanupDriver(int lockWaitMs)
        {
            if (!Lock.Wait(lockWaitMs)) return;
            try { CleanupDriverLocked(); }
            finally { Lock.Release(); }
        }

        private static void CleanupDriverLocked()
        {
            Execute(Paths.Sc, "stop BZHDELLSMMIO", 10000, true);
            Execute(Paths.Sc, "delete BZHDELLSMMIO", 10000, true);
            ActivityLog.Write("Driver cleanup: requested stop and removal of the BZHDELLSMMIO service.");
        }

        private static CommandResult Execute(string file, string args, int timeout, bool quiet)
        {
            CommandResult result = new CommandResult();
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.WorkingDirectory = Path.GetDirectoryName(file);
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.Start();
                    result.Started = true;
                    ChildJob.Assign(p);
                    Task<string> outputTask = p.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(timeout))
                    {
                        try { p.Kill(); } catch { }
                        try { p.WaitForExit(3000); } catch { }
                        result.TimedOut = true;
                        result.ExitCode = -2;
                        result.Error = "Command timed out after " + (timeout / 1000) + " s and was stopped.";
                    }
                    else if (!Task.WaitAll(new Task[] { outputTask, errorTask }, 2000))
                    {
                        result.ExitCode = -3;
                        result.Error = "Command finished, but its output could not be read.";
                    }
                    else
                    {
                        result.ExitCode = p.ExitCode;
                        result.Output = outputTask.Result ?? "";
                        result.Error = errorTask.Result ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.Error = ex.Message;
            }
            watch.Stop();
            bool problem = !result.Started || result.TimedOut || result.ExitCode < 0 || result.Error.Trim().Length > 0;
            if (!quiet || problem)
            {
                ActivityLog.Write("Ran " + Path.GetFileName(file) + " " + args + ": exit " + result.ExitCode + " in " +
                    watch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s. Output: " + result.Tail(300));
            }
            if (result.TimedOut && String.Equals(file, Paths.Fan, StringComparison.OrdinalIgnoreCase)) CleanupDriverLocked();
            return result;
        }
    }

    /// <summary>Kill-on-close job object so no tool process outlives the app unsupervised (H1).</summary>
    internal static class ChildJob
    {
        private static readonly object Gate = new object();
        private static IntPtr handle = IntPtr.Zero;
        private static bool failed;

        internal static void Assign(Process p)
        {
            try
            {
                lock (Gate)
                {
                    if (handle == IntPtr.Zero && !failed)
                    {
                        handle = Create();
                        failed = handle == IntPtr.Zero;
                    }
                    if (handle != IntPtr.Zero) AssignProcessToJobObject(handle, p.Handle);
                }
            }
            catch { }
        }

        private static IntPtr Create()
        {
            IntPtr job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) return IntPtr.Zero;
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            int size = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, buffer, false);
                if (!SetInformationJobObject(job, 9, buffer, (uint)size)) // JobObjectExtendedLimitInformation
                {
                    CloseHandle(job);
                    return IntPtr.Zero;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
            return job;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr attributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }

    /// <summary>
    /// DellFanCmd exits 0 even when its driver fails to load (C1), so success is judged from its
    /// output: the driver-load line and the command's own line must each be followed by
    /// " ...Success.", and stderr must report no failure. An SMM-level failure is still
    /// invisible (upstream 32/64-bit sentinel bug); that residual risk is documented.
    /// </summary>
    internal static class FanOutput
    {
        internal const string LoadLine = "Loading SMM I/O driver...";
        internal const string EnableLine = "Attempting to enable EC control of the fan...";
        internal const string DisableLine = "Attempting to disable EC control of the fan...";
        internal const string LevelLine = "Attempting to set the fan level...";
        internal const string RpmLine = "Attempting to query the fan RPM...";

        internal static bool Confirmed(CommandResult r, string commandLine)
        {
            if (r == null || !r.Started || r.Skipped || r.TimedOut || r.ExitCode != 0) return false;
            if (ErrorReported(r.Error)) return false;
            string[] lines = Lines(r.Output);
            return FollowedBySuccess(lines, LoadLine) && FollowedBySuccess(lines, commandLine);
        }

        /// <summary>RPM from the " Result: N" line; -1 when the tool did not demonstrably succeed (M8).</summary>
        internal static int Rpm(CommandResult r)
        {
            if (r == null || !r.Started || r.Skipped || r.TimedOut || ErrorReported(r.Error)) return -1;
            string[] lines = Lines(r.Output);
            if (!FollowedBySuccess(lines, LoadLine)) return -1;
            foreach (string line in lines)
            {
                string t = line.Trim();
                if (!t.StartsWith("Result:", StringComparison.Ordinal)) continue;
                int rpm;
                if (Int32.TryParse(t.Substring(7).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out rpm) &&
                    rpm >= 0 && rpm < 20000 && rpm == r.ExitCode) return rpm;
            }
            return -1;
        }

        private static bool ErrorReported(string error)
        {
            foreach (string line in Lines(error))
            {
                string t = line.Trim();
                if (t.IndexOf("Maybe your system just has one fan", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (t.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("Unable", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool FollowedBySuccess(string[] lines, string marker)
        {
            for (int i = 0; i + 1 < lines.Length; i++)
                if (lines[i].Trim() == marker && lines[i + 1].Trim() == "...Success.") return true;
            return false;
        }

        private static string[] Lines(string text)
        {
            return (text ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    internal enum RestoreOutcome { NotNeeded, Confirmed, Unconfirmed }

    internal static class Safety
    {
        /// <summary>Pessimistic: true from before any EC disable until a confirmed restore.</summary>
        internal static volatile bool ManualActive;
        /// <summary>Model gate passed, install location trusted and DellFanCmd present.</summary>
        internal static volatile bool FanAllowed;
        private static int epoch;

        /// <summary>Changes whenever a restore starts; UI flows abort if it moved under them.</summary>
        internal static int Epoch { get { return Thread.VolatileRead(ref epoch); } }

        internal static bool NeedsRestore { get { return ManualActive || ManualMarker.Exists(); } }

        /// <summary>
        /// Restores Dell automatic fan control and reports "Confirmed" only when DellFanCmd's
        /// output proves it. On failure the fan is set to Max as a precaution and the marker is
        /// kept. Synchronous and bounded by budgetMs; safe to call from any thread.
        /// </summary>
        internal static RestoreOutcome RestoreDellAuto(string trigger, int budgetMs, bool force)
        {
            if (!force && !NeedsRestore) return RestoreOutcome.NotNeeded;
            Interlocked.Increment(ref epoch);
            if (!FanAllowed || !File.Exists(Paths.Fan))
            {
                ActivityLog.Write("Dell Auto restore UNCONFIRMED (" + trigger + "): fan tool unavailable or model gate closed. Restart the laptop.");
                return RestoreOutcome.Unconfirmed;
            }
            Stopwatch watch = Stopwatch.StartNew();
            ToolRunner.BeginRestore();
            try
            {
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    int left = budgetMs - (int)watch.ElapsedMilliseconds;
                    if (left < 3000) break;
                    CommandResult r = ToolRunner.Run(Paths.Fan, "ec-enable", Math.Min(10000, left), left, true, false);
                    if (FanOutput.Confirmed(r, FanOutput.EnableLine))
                    {
                        ManualActive = false;
                        ManualMarker.Clear();
                        ActivityLog.Write("Dell Auto restore CONFIRMED (" + trigger + ", attempt " + attempt + ").");
                        return RestoreOutcome.Confirmed;
                    }
                    ActivityLog.Write("Dell Auto restore attempt " + attempt + " NOT confirmed (" + trigger + ").");
                    if (attempt < 3) Thread.Sleep(Math.Min(1000 * attempt, Math.Max(0, budgetMs - (int)watch.ElapsedMilliseconds - 3000)));
                }
                ManualActive = true;
                ManualMarker.Set("restore unconfirmed (" + trigger + ")");
                int remaining = budgetMs - (int)watch.ElapsedMilliseconds;
                if (remaining >= 3000)
                {
                    CommandResult max = ToolRunner.Run(Paths.Fan, "fan1-level2", Math.Min(10000, remaining), remaining, true, false);
                    ActivityLog.Write(FanOutput.Confirmed(max, FanOutput.LevelLine)
                        ? "Fail-safe: fan set to Max (confirmed by tool output)."
                        : "Fail-safe: fan could NOT be confirmed at Max.");
                }
            }
            finally { ToolRunner.EndRestore(); }
            ActivityLog.Write("Dell Auto restore UNCONFIRMED (" + trigger + "). Restart the laptop to be sure Dell automatic fan control is back.");
            return RestoreOutcome.Unconfirmed;
        }
    }
}
