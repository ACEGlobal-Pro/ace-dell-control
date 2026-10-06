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
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace AceDellControl
{
    internal static class Program
    {
        internal static MainForm Form;
        private const string MutexName = @"Global\ACE.DellControl";

        /// <summary>
        /// Normal start shows the window. "--restore" (used by the uninstaller) runs a verified
        /// Dell Auto restore without UI and exits 0 only when it is confirmed or not applicable.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            bool restoreMode = HasArg(args, "--restore");
            bool relaunched = HasArg(args, "--elevated");
            string where = InstallLocation.Problem();
            if (where != null)
            {
                if (!restoreMode)
                    MessageBox.Show("ACE Dell Control will not run here because " + where +
                        ".\n\nInstall it with its installer and start it from the Start menu.",
                        "ACE Dell Control", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return 3;
            }
            if (!IsAdministrator())
            {
                if (restoreMode) return 2;
                if (relaunched)
                {
                    MessageBox.Show("Administrator approval is required to run ACE Dell Control.", "ACE Dell Control",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return 2;
                }
                try
                {
                    ProcessStartInfo elevate = new ProcessStartInfo(Application.ExecutablePath, "--elevated");
                    elevate.UseShellExecute = true;
                    elevate.Verb = "runas";
                    Process.Start(elevate);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Administrator approval is required to run ACE Dell Control.\n\n" + ex.Message,
                        "ACE Dell Control", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return 0;
            }
            DataDir.Ensure();
            if (restoreMode) return RestoreForInstaller();

            Mutex mutex = null;
            try
            {
                bool createdNew;
                try { mutex = new Mutex(true, MutexName, out createdNew); }
                catch (UnauthorizedAccessException) { createdNew = false; }
                if (!createdNew)
                {
                    MessageBox.Show("ACE Dell Control is already running (possibly for another user). Use the open window.",
                        "ACE Dell Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                return RunGui();
            }
            finally
            {
                if (mutex != null) { try { mutex.ReleaseMutex(); } catch { } mutex.Dispose(); }
            }
        }

        private static int RunGui()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                ActivityLog.Write("ERROR (unhandled UI exception): " + e.Exception);
                RestoreOutcome o = Safety.RestoreDellAuto("unhandled error", 20000, false);
                if (Form != null && !Form.IsDisposed) Form.ApplyRestoreOutcome(o);
                string detail = o == RestoreOutcome.Confirmed ? "Dell automatic fan control was restored and confirmed."
                    : o == RestoreOutcome.NotNeeded ? "Fan control was not in manual mode."
                    : "Dell automatic fan control could NOT be confirmed. The fan was set to Max as a precaution; please restart the laptop.";
                MessageBox.Show("ACE Dell Control encountered an error. " + detail + "\n\n" + e.Exception.Message,
                    "ACE Dell Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ActivityLog.Write("ERROR (fatal exception): " + e.ExceptionObject);
                Safety.RestoreDellAuto("fatal error", 15000, false);
            };
            Form = new MainForm();
            Application.Run(Form);
            return 0;
        }

        /// <summary>Uninstaller path (M7): gate-checked, verified, bounded; exit 0 = confirmed or not applicable.</summary>
        private static int RestoreForInstaller()
        {
            ModelGate gate = ModelGate.Detect();
            ActivityLog.Write("Restore requested by the installer. " + gate.Describe());
            if (!gate.Passed)
            {
                if (ManualMarker.Exists()) ActivityLog.Write("WARNING: manual-mode marker present on a computer outside the model gate; left in place.");
                return 0;
            }
            Safety.FanAllowed = File.Exists(Paths.Fan);
            Safety.ManualActive = true;
            RestoreOutcome o = Safety.RestoreDellAuto("installer", 30000, true);
            ToolRunner.CleanupDriver(15000);
            return o == RestoreOutcome.Confirmed ? 0 : 1;
        }

        private static bool HasArg(string[] args, string name)
        {
            foreach (string a in args) if (String.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// H4: the app runs elevated and loads tools, a kernel driver and DLLs from its own folder,
    /// so it refuses to run unless that folder is under Program Files and not writable by
    /// non-administrators.
    /// </summary>
    internal static class InstallLocation
    {
        private static readonly FileSystemRights WriteRights =
            FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteAttributes |
            FileSystemRights.WriteExtendedAttributes | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership |
            (FileSystemRights)0x40000000 /* GENERIC_WRITE */ | (FileSystemRights)0x10000000 /* GENERIC_ALL */;

        /// <summary>Null when the location is trusted; otherwise a plain-English reason.</summary>
        internal static string Problem()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (String.IsNullOrEmpty(programFiles)) return "the Program Files folder could not be found";
                string root = Path.GetFullPath(programFiles).TrimEnd('\\') + "\\";
                string baseDir = Path.GetFullPath(Paths.Base).TrimEnd('\\') + "\\";
                if (!baseDir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return "it is not running from " + programFiles;
                foreach (string dir in new string[] { baseDir, Path.Combine(baseDir, "tools") })
                {
                    if (!Directory.Exists(dir)) continue;
                    string why = WritableByUsers(dir);
                    if (why != null) return why;
                }
            }
            catch (Exception ex) { return "its folder permissions could not be checked (" + ex.Message + ")"; }
            return null;
        }

        private static string WritableByUsers(string dir)
        {
            DirectorySecurity security = Directory.GetAccessControl(dir, AccessControlSections.Access);
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType != AccessControlType.Allow) continue;
                SecurityIdentifier sid = rule.IdentityReference as SecurityIdentifier;
                if (sid == null || !IsUnprivileged(sid)) continue;
                if ((rule.FileSystemRights & WriteRights) != 0)
                    return "its folder " + dir + " can be modified by non-administrators";
            }
            return null;
        }

        private static bool IsUnprivileged(SecurityIdentifier sid)
        {
            return sid.IsWellKnown(WellKnownSidType.BuiltinUsersSid) || sid.IsWellKnown(WellKnownSidType.AuthenticatedUserSid) ||
                sid.IsWellKnown(WellKnownSidType.WorldSid) || sid.IsWellKnown(WellKnownSidType.InteractiveSid) ||
                sid.IsWellKnown(WellKnownSidType.BuiltinGuestsSid) || sid.IsWellKnown(WellKnownSidType.AnonymousSid);
        }
    }
}
