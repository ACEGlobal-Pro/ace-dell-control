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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AceDellControl
{
    internal sealed class MainForm : Form
    {
        private readonly Color Bg = Color.FromArgb(20, 24, 31);
        private readonly Color PanelBg = Color.FromArgb(31, 37, 47);
        private readonly Color TextMain = Color.FromArgb(235, 240, 246);
        private readonly Color TextMuted = Color.FromArgb(164, 176, 191);
        private readonly Color Blue = Color.FromArgb(56, 132, 255);
        private readonly Color Green = Color.FromArgb(46, 196, 126);
        private readonly Color Amber = Color.FromArgb(242, 174, 73);
        private readonly Color Red = Color.FromArgb(235, 87, 87);

        private const int KeepAliveFailureLimit = 3;
        private const int FanTestCapMs = 60000;

        private Label thermalValue;
        private Label fanValue;
        private Label cpuSettingValue;
        private Label cpuLoadValue;
        private Label cpuTempValue;
        private Label ramValue;
        private Label rpmValue;
        private Label statusValue;
        private TextBox logBox;
        private GroupBox thermalGroup;
        private GroupBox fanGroup;
        private readonly List<Button> commandButtons = new List<Button>();
        private bool thermalAllowed;
        private Timer monitorTimer;
        private Timer keepAliveTimer;
        private PerformanceCounterSet counters;
        private HardwareReader hardware;
        private string manualLevel;
        private string manualLabel;
        private int monitorTicks;
        private int keepAliveFailures;
        private bool monitoringBusy;
        private bool keepAliveBusy;
        private bool uiBusy;
        private bool closing;
        private bool closeInProgress;
        private bool allowClose;
        private bool manualBeforeSleep;

        internal MainForm()
        {
            Text = "ACE Dell Control";
            Width = 790;
            Height = 720;
            MinimumSize = new Size(790, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildUi();
            ActivityLog.Line += AppendLogLine;
            FormClosing += OnClosing;
            FormClosed += OnClosed;
            Shown += async delegate { await RunGuardedAsync(InitializeAsync); };
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionEnding += OnSessionEnding;

            monitorTimer = new Timer();
            monitorTimer.Interval = 2000;
            monitorTimer.Tick += async delegate { await UpdateMonitoringAsync(); };
            keepAliveTimer = new Timer();
            keepAliveTimer.Interval = 5000;
            keepAliveTimer.Tick += async delegate { await KeepManualFanAliveAsync(); };
        }

        private void BuildUi()
        {
            Label title = LabelOf("ACE Dell Control", 22, FontStyle.Bold, TextMain);
            title.SetBounds(22, 16, 360, 34);
            Controls.Add(title);
            Label subtitle = LabelOf("Dell laptop preview • thermal, fan, CPU and live health", 9.5f, FontStyle.Regular, TextMuted);
            subtitle.SetBounds(24, 50, 470, 22);
            Controls.Add(subtitle);
            statusValue = Badge("Starting…", Amber);
            statusValue.SetBounds(482, 24, 278, 34);
            Controls.Add(statusValue);

            GroupBox thermal = Group("DELL THERMAL MODE", 18, 82, 744, 116);
            thermalGroup = thermal;
            Controls.Add(thermal);
            thermalValue = StatusLine("Current mode: Checking…");
            thermalValue.SetBounds(18, 25, 700, 24);
            thermal.Controls.Add(thermalValue);
            thermal.Controls.Add(ActionButton("Optimized", 18, 58, delegate { return SetThermalAsync("Optimized"); }));
            thermal.Controls.Add(ActionButton("Cool", 192, 58, delegate { return SetThermalAsync("Cool"); }));
            thermal.Controls.Add(ActionButton("Quiet", 366, 58, delegate { return SetThermalAsync("Quiet"); }));
            thermal.Controls.Add(ActionButton("Ultra Performance", 540, 58, delegate { return SetThermalAsync("UltraPerformance"); }));

            GroupBox fan = Group("FAN CONTROL", 18, 210, 744, 154);
            fanGroup = fan;
            Controls.Add(fan);
            fanValue = StatusLine("Status: Checking…");
            fanValue.SetBounds(18, 24, 450, 24);
            fan.Controls.Add(fanValue);
            rpmValue = StatusLine("Fan: —");
            rpmValue.TextAlign = ContentAlignment.MiddleRight;
            rpmValue.SetBounds(474, 24, 245, 24);
            fan.Controls.Add(rpmValue);
            fan.Controls.Add(ActionButton("Dell Auto", 18, 56, delegate { return SetFanAutoAsync(); }, Green));
            fan.Controls.Add(ActionButton("Medium", 160, 56, delegate { return SetManualFanAsync("fan1-level1", "Medium"); }));
            fan.Controls.Add(ActionButton("Force Max", 302, 56, delegate { return SetManualFanAsync("fan1-level2", "Max"); }, Amber));
            fan.Controls.Add(ActionButton("Read RPM", 444, 56, delegate { return RefreshRpmAsync(true); }));
            fan.Controls.Add(ActionButton("Fan Test", 586, 56, delegate { return FanTestAsync(); }));
            Label safety = LabelOf("Closing the app, sleep and sign-out restore Dell Auto. You are told if the restore cannot be confirmed.", 8.5f, FontStyle.Regular, TextMuted);
            safety.SetBounds(20, 107, 700, 22);
            fan.Controls.Add(safety);

            GroupBox cpu = Group("CPU MAXIMUM PROCESSOR STATE", 18, 376, 744, 104);
            Controls.Add(cpu);
            cpuSettingValue = StatusLine("Plan setting: Checking…");
            cpuSettingValue.SetBounds(18, 25, 425, 24);
            cpu.Controls.Add(cpuSettingValue);
            cpu.Controls.Add(ActionButton("Max state 99%", 446, 21, delegate { return SetTurboAsync(false); }, Green));
            cpu.Controls.Add(ActionButton("Max state 100%", 586, 21, delegate { return SetTurboAsync(true); }, Amber));
            Label cpuHint = LabelOf("Sets the maximum processor state (plugged in and battery) of the current Windows power plan. 99% often, but not always, prevents Turbo.", 8.5f, FontStyle.Regular, TextMuted);
            cpuHint.SetBounds(18, 61, 710, 24);
            cpu.Controls.Add(cpuHint);

            GroupBox monitor = Group("LIVE MONITORING", 18, 492, 744, 86);
            Controls.Add(monitor);
            cpuLoadValue = Metric("CPU load", "—", 18);
            cpuTempValue = Metric("CPU temperature", "—", 194);
            ramValue = Metric("RAM used", "—", 395);
            Label fanMetric = Metric("Fan speed", "—", 566);
            rpmValue.TextChanged += delegate
            {
                string s = rpmValue.Text.Replace("Fan: ", "");
                SetMetricValue(fanMetric, s);
            };
            monitor.Controls.Add(cpuLoadValue);
            monitor.Controls.Add(cpuTempValue);
            monitor.Controls.Add(ramValue);
            monitor.Controls.Add(fanMetric);

            GroupBox log = Group("ACTIVITY LOG", 18, 590, 744, 82);
            Controls.Add(log);
            logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BorderStyle = BorderStyle.None;
            logBox.BackColor = Color.FromArgb(15, 18, 24);
            logBox.ForeColor = Color.FromArgb(194, 207, 224);
            logBox.Font = new Font("Consolas", 8.5f);
            logBox.SetBounds(12, 24, 718, 46);
            log.Controls.Add(logBox);
        }

        // ---- startup ------------------------------------------------------------

        private async Task InitializeAsync()
        {
            Log("ACE Dell Control started with administrator access from " + Paths.Base + " (install location trusted).");
            if (!DataDir.Trusted)
                Log("WARNING: log file and manual-mode marker unavailable (" + DataDir.Problem + "). Manual fan control is blocked.");
            else
                Log("Activity log file: " + ActivityLog.CurrentFile);
            ModelGate gate = await Task.Run(delegate { return ModelGate.Detect(); });
            Log(gate.Describe());
            Safety.FanAllowed = gate.Passed && File.Exists(Paths.Fan);
            thermalAllowed = gate.Passed && File.Exists(Paths.Thermal);
            if (gate.Passed && !File.Exists(Paths.Fan)) Log("ERROR: DellFanCmd.exe is missing; fan controls disabled.");
            if (gate.Passed && !File.Exists(Paths.Thermal)) Log("ERROR: DellSetThermalSetting.exe is missing; thermal controls disabled.");
            if (!gate.Passed)
                Log("Dell-specific controls disabled on unverified model. Monitoring and Windows CPU power controls remain available.");
            thermalGroup.Enabled = thermalAllowed;
            fanGroup.Enabled = Safety.FanAllowed;
            if (!thermalAllowed) thermalValue.Text = "Current mode: Not available on this computer";
            fanValue.Text = Safety.FanAllowed ? "Status: Dell automatic control" : "Status: Fan controls disabled";
            rpmValue.Text = Safety.FanAllowed ? "Fan: press Read RPM" : "Fan: Not available";

            counters = await Task.Run(delegate { return PerformanceCounterSet.Open(Log); });
            hardware = await Task.Run(delegate { return new HardwareReader(Paths.Lhm, Log); });
            if (thermalAllowed) await RefreshThermalAsync();
            await RefreshCpuSettingAsync();
            await UpdateMonitoringAsync();
            monitorTimer.Start();

            if (ManualMarker.Exists() && !Safety.FanAllowed)
                Log("WARNING: a manual-mode marker exists, but fan controls are unavailable here, so it cannot be acted on. Restart the laptop if a fan was left in manual mode.");
            else if (ManualMarker.Exists())
            {
                Log("A previous session left manual fan mode active (marker found). Restoring Dell Auto now.");
                RestoreOutcome o = await Task.Run(delegate { return Safety.RestoreDellAuto("startup recovery", 30000, true); });
                ApplyRestoreOutcome(o);
                MessageBox.Show(this, o == RestoreOutcome.Confirmed
                        ? "ACE Dell Control found that the last session ended in manual fan mode. Dell automatic fan control has now been restored and confirmed."
                        : "ACE Dell Control found that the last session ended in manual fan mode, and could NOT confirm that Dell automatic fan control is back. The fan was set to Max as a precaution. Please restart the laptop.",
                    "Fan safety", MessageBoxButtons.OK, o == RestoreOutcome.Confirmed ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                return;
            }
            SetOverallStatus(Safety.FanAllowed ? "Ready • Dell Auto on exit" : "Monitoring only", Safety.FanAllowed ? Green : Amber);
        }

        // ---- thermal ------------------------------------------------------------

        private async Task SetThermalAsync(string mode)
        {
            if (!thermalAllowed) return;
            SetOverallStatus("Applying thermal mode…", Amber);
            CommandResult r = await ToolRunner.RunAsync(Paths.Thermal, mode, 12000);
            string current = await Task.Run(delegate { return DellThermalReader.GetCurrent(); });
            thermalValue.Text = "Current mode: " + current;
            if (r.Started && r.ExitCode == 0 && current == DisplayThermal(mode))
            {
                Log("Thermal mode set to " + DisplayThermal(mode) + " (confirmed by BIOS read-back).");
                SetOverallStatus("Ready", Green);
            }
            else Fail("Thermal mode not confirmed (BIOS reports " + current + ")", r);
        }

        private async Task RefreshThermalAsync()
        {
            string thermal = await Task.Run(delegate { return DellThermalReader.GetCurrent(); });
            thermalValue.Text = "Current mode: " + thermal;
        }

        // ---- fan ----------------------------------------------------------------

        private async Task SetFanAutoAsync()
        {
            if (!Safety.FanAllowed) return;
            await RestoreFromUiAsync("Dell Auto button", true);
        }

        private async Task RestoreFromUiAsync(string trigger, bool force)
        {
            keepAliveTimer.Stop();
            SetOverallStatus("Restoring Dell fan control…", Amber);
            RestoreOutcome o = await Task.Run(delegate { return Safety.RestoreDellAuto(trigger, 30000, force); });
            ApplyRestoreOutcome(o);
        }

        /// <summary>Brings the UI and keep-alive in line with a restore result. UI thread only.</summary>
        internal void ApplyRestoreOutcome(RestoreOutcome o)
        {
            if (IsDisposed) return;
            if (o == RestoreOutcome.Unconfirmed)
            {
                // EC may still be disabled: hold the fan at Max (fail-safe) until the user retries or restarts.
                manualLevel = "fan1-level2";
                manualLabel = "Max (fail-safe)";
                keepAliveFailures = 0;
                if (!closing) keepAliveTimer.Start();
                fanValue.Text = "Status: restore UNCONFIRMED • fan held at Max";
                SetOverallStatus("Restore UNCONFIRMED — restart the laptop", Red);
                return;
            }
            manualLevel = null;
            manualLabel = null;
            keepAliveTimer.Stop();
            if (Safety.FanAllowed)
            {
                fanValue.Text = o == RestoreOutcome.Confirmed ? "Status: Dell automatic control (restore confirmed)" : "Status: Dell automatic control";
                rpmValue.Text = "Fan: press Read RPM";
            }
            SetOverallStatus(o == RestoreOutcome.Confirmed ? "Ready • Dell Auto confirmed" : "Ready", Green);
        }

        private async Task SetManualFanAsync(string command, string label)
        {
            if (!Safety.FanAllowed) return;
            if (!ManualMarker.Set("manual " + command))
            {
                Fail("Manual fan blocked: the manual-mode marker could not be written", CommandResult.SkippedResult(DataDir.Problem ?? "write failed"));
                return;
            }
            Safety.ManualActive = true; // pessimistic: before ec-disable starts (H1)
            int epoch = Safety.Epoch;
            keepAliveTimer.Stop();
            manualLevel = null;
            SetOverallStatus("Enabling manual fan mode…", Amber);
            CommandResult off = await ToolRunner.RunAsync(Paths.Fan, "ec-disable", 10000);
            if (Superseded(epoch)) return;
            if (!FanOutput.Confirmed(off, FanOutput.DisableLine))
            {
                Log("EC disable not confirmed; restoring Dell Auto.");
                await RestoreFromUiAsync("manual mode failed", true);
                return;
            }
            CommandResult level = await ToolRunner.RunAsync(Paths.Fan, command, 10000);
            if (Superseded(epoch)) return;
            if (!FanOutput.Confirmed(level, FanOutput.LevelLine))
            {
                Log("Manual fan level not confirmed; restoring Dell Auto.");
                await RestoreFromUiAsync("manual level failed", true);
                return;
            }
            manualLevel = command;
            manualLabel = label;
            keepAliveFailures = 0;
            keepAliveTimer.Start();
            fanValue.Text = "Status: Manual " + label;
            Log("Manual fan set to " + label + ". Keep-alive is active; closing the app restores Dell Auto.");
            SetOverallStatus("Manual fan active", Amber);
        }

        private bool Superseded(int epoch)
        {
            if (epoch == Safety.Epoch) return false;
            Log("Fan request cancelled: a Dell Auto restore ran in the meantime.");
            return true;
        }

        private async Task KeepManualFanAliveAsync()
        {
            if (keepAliveBusy || uiBusy || closing || !Safety.ManualActive || String.IsNullOrEmpty(manualLevel)) return;
            keepAliveBusy = true;
            try
            {
                string level = manualLevel;
                int epoch = Safety.Epoch;
                CommandResult r = await ToolRunner.RunIfIdleAsync(Paths.Fan, level, 8000);
                if (r.Skipped || epoch != Safety.Epoch) return;
                if (FanOutput.Confirmed(r, FanOutput.LevelLine)) { keepAliveFailures = 0; return; }
                keepAliveFailures++;
                Log("WARNING: keep-alive (" + level + ") not confirmed, " + keepAliveFailures + " in a row.");
                if (keepAliveFailures >= KeepAliveFailureLimit && !uiBusy && !closing)
                {
                    keepAliveFailures = 0;
                    Log("Keep-alive is failing repeatedly; restoring Dell Auto.");
                    await RunGuardedAsync(delegate { return RestoreFromUiAsync("keep-alive failing", true); });
                }
            }
            finally { keepAliveBusy = false; }
        }

        /// <summary>
        /// ACE-controlled fan test (H3): Medium, then Max, then a verified Dell Auto restore.
        /// Never level 0; hard cap of 60 s; a failed restore leaves the fan at Max and says so.
        /// </summary>
        private async Task FanTestAsync()
        {
            if (!Safety.FanAllowed) return;
            if (!ManualMarker.Set("fan test"))
            {
                Fail("Fan test blocked: the manual-mode marker could not be written", CommandResult.SkippedResult(DataDir.Problem ?? "write failed"));
                return;
            }
            Safety.ManualActive = true;
            int epoch = Safety.Epoch;
            keepAliveTimer.Stop();
            manualLevel = null;
            SetOverallStatus("Running fan test…", Amber);
            fanValue.Text = "Status: Fan test running";
            Log("Fan test started: Medium, then Max, then Dell Auto (about 30 s).");
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(FanTestCapMs - 15000);
            List<string> results = new List<string>();
            bool stepsOk = await FanTestStepsAsync(epoch, deadline, results);
            if (Superseded(epoch)) return;
            await RestoreFromUiAsync("fan test end", true);
            if (results.Count > 0) Log("Fan test readings: " + String.Join(" | ", results.ToArray()));
            if (Safety.ManualActive) { Log("Fan test ended, but Dell Auto could NOT be confirmed. Restart the laptop."); return; }
            if (stepsOk) { Log("Fan test passed; Dell Auto restore confirmed."); SetOverallStatus("Fan test passed • Dell Auto confirmed", Green); }
            else { Log("Fan test did not complete; Dell Auto restore confirmed."); SetOverallStatus("Fan test incomplete • Dell Auto confirmed", Amber); }
        }

        private async Task<bool> FanTestStepsAsync(int epoch, DateTime deadline, List<string> results)
        {
            CommandResult off = await ToolRunner.RunAsync(Paths.Fan, "ec-disable", 10000);
            if (epoch != Safety.Epoch || !FanOutput.Confirmed(off, FanOutput.DisableLine)) return false;
            string[] levels = { "fan1-level1", "fan1-level2" };
            string[] names = { "Medium", "Max" };
            for (int i = 0; i < levels.Length; i++)
            {
                if (DateTime.UtcNow > deadline) { Log("Fan test time cap reached."); return false; }
                CommandResult set = await ToolRunner.RunAsync(Paths.Fan, levels[i], 10000);
                if (epoch != Safety.Epoch || !FanOutput.Confirmed(set, FanOutput.LevelLine)) return false;
                await Task.Delay(7500);
                if (epoch != Safety.Epoch) return false;
                int rpm = FanOutput.Rpm(await ToolRunner.RunAsync(Paths.Fan, "rpm-fan1", 10000));
                results.Add(names[i] + " " + (rpm < 0 ? "unavailable" : rpm.ToString("N0", CultureInfo.InvariantCulture) + " RPM"));
                if (rpm >= 0) rpmValue.Text = "Fan: " + rpm.ToString("N0", CultureInfo.InvariantCulture) + " RPM";
            }
            return true;
        }

        private async Task RefreshRpmAsync(bool writeLog)
        {
            if (!Safety.FanAllowed) { rpmValue.Text = "Fan: Not available"; return; }
            CommandResult r = writeLog
                ? await ToolRunner.RunAsync(Paths.Fan, "rpm-fan1", 10000)
                : await ToolRunner.RunIfIdleAsync(Paths.Fan, "rpm-fan1", 10000);
            if (r.Skipped || IsDisposed) return;
            int rpm = FanOutput.Rpm(r);
            rpmValue.Text = rpm < 0 ? "Fan: unavailable" : "Fan: " + rpm.ToString("N0", CultureInfo.InvariantCulture) + " RPM";
            if (writeLog) Log(rpm < 0 ? "Fan RPM could not be read (tool did not report success)." : "Fan speed read: " + rpm.ToString("N0", CultureInfo.InvariantCulture) + " RPM.");
        }

        // ---- CPU power plan -----------------------------------------------------

        private async Task SetTurboAsync(bool enabled)
        {
            int value = enabled ? 100 : 99;
            SetOverallStatus("Applying maximum processor state…", Amber);
            string args1 = "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX " + value;
            string args2 = "/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX " + value;
            CommandResult a = await ToolRunner.RunAsync(Paths.PowerCfg, args1, 10000);
            CommandResult d = await ToolRunner.RunAsync(Paths.PowerCfg, args2, 10000);
            CommandResult activate = await ToolRunner.RunAsync(Paths.PowerCfg, "/setactive SCHEME_CURRENT", 10000);
            if (a.ExitCode == 0 && d.ExitCode == 0 && activate.ExitCode == 0)
            {
                Log("Maximum processor state set to " + value + "% (plugged in and battery, current plan).");
                await RefreshCpuSettingAsync();
                SetOverallStatus("Ready", Green);
            }
            else Fail("Windows power setting failed", a.ExitCode != 0 ? a : (d.ExitCode != 0 ? d : activate));
        }

        private async Task RefreshCpuSettingAsync()
        {
            CommandResult r = await ToolRunner.RunAsync(Paths.PowerCfg, "/query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX", 10000);
            Match ac = Regex.Match(r.Output ?? "", "AC Power Setting Index:\\s*0x([0-9a-fA-F]+)");
            Match dc = Regex.Match(r.Output ?? "", "DC Power Setting Index:\\s*0x([0-9a-fA-F]+)");
            if (IsDisposed) return;
            if (ac.Success)
            {
                int av = Int32.Parse(ac.Groups[1].Value, NumberStyles.HexNumber);
                int dv = dc.Success ? Int32.Parse(dc.Groups[1].Value, NumberStyles.HexNumber) : av;
                cpuSettingValue.Text = "Plan setting: AC " + av + "% / battery " + dv + "%";
            }
            else cpuSettingValue.Text = "Plan setting: Unable to read";
        }

        // ---- monitoring ---------------------------------------------------------

        private async Task UpdateMonitoringAsync()
        {
            if (monitoringBusy || closing) return;
            monitoringBusy = true;
            try
            {
                float load = counters == null ? -1 : counters.CpuLoad();
                double ram = counters == null ? -1 : counters.RamPercent();
                double? temp = hardware == null ? null : await Task.Run(delegate { return hardware.ReadCpuPackageTemperature(); });
                if (IsDisposed) return;
                SetMetricValue(cpuLoadValue, load < 0 ? "N/A" : load.ToString("0") + "%");
                SetMetricValue(ramValue, ram < 0 ? "N/A" : ram.ToString("0") + "%");
                SetMetricValue(cpuTempValue, temp.HasValue ? temp.Value.ToString("0.0") + " °C" : "N/A");
                monitorTicks++;
                // H5: the SMM driver is only loaded for RPM while manual mode is active, never in Dell Auto.
                if (monitorTicks % 8 == 0 && Safety.ManualActive && !uiBusy && !String.IsNullOrEmpty(manualLevel)) await RefreshRpmAsync(false);
            }
            catch (Exception ex) { Log("Monitoring warning: " + ex.Message); }
            finally { monitoringBusy = false; }
        }

        // ---- power and session events -------------------------------------------

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend)
            {
                keepAliveTimer.Stop();
                if (!Safety.NeedsRestore) return;
                manualBeforeSleep = true;
                ActivityLog.Write("System is going to sleep: restoring Dell Auto first.");
                RunOnUi(ApplyRestoreOutcome, Safety.RestoreDellAuto("suspend", 8000, false));
            }
            else if (e.Mode == PowerModes.Resume)
            {
                if (Safety.NeedsRestore)
                {
                    ActivityLog.Write("Resumed with manual fan mode still recorded: restoring Dell Auto.");
                    RunOnUi(ApplyRestoreOutcome, Safety.RestoreDellAuto("resume", 20000, false));
                }
                else if (manualBeforeSleep)
                    ActivityLog.Write("Resumed: manual fan mode was cleared before sleep; Dell Auto is active. Choose Medium or Max again if wanted.");
                manualBeforeSleep = false;
            }
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            if (!Safety.NeedsRestore) return;
            ActivityLog.Write("Windows session ending (" + e.Reason + "): restoring Dell Auto.");
            Safety.RestoreDellAuto("session ending", 8000, false);
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (allowClose) { closing = true; return; }
            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                // Bounded, never modal: shutdown must not hang behind a dialog (M3). The marker
                // stays if the restore is unconfirmed, so the next start recovers.
                closing = true;
                monitorTimer.Stop();
                keepAliveTimer.Stop();
                if (Safety.NeedsRestore)
                {
                    bool blocked = false;
                    try { blocked = ShutdownBlockReasonCreate(Handle, "Restoring Dell automatic fan control"); } catch { }
                    Safety.RestoreDellAuto("Windows shutdown", 8000, false);
                    try { if (blocked) ShutdownBlockReasonDestroy(Handle); } catch { }
                }
                return;
            }
            if (!Safety.NeedsRestore && !ToolRunner.Busy && !uiBusy) { closing = true; return; }
            e.Cancel = true;
            if (!closeInProgress) BeginSafeClose();
        }

        private async void BeginSafeClose()
        {
            closeInProgress = true;
            closing = true;
            monitorTimer.Stop();
            keepAliveTimer.Stop();
            SetCommandsEnabled(false);
            SetOverallStatus("Restoring Dell Auto before exit…", Amber);
            Log("Closing: waiting for running commands, then restoring Dell automatic fan control.");
            await Task.Run(delegate { ToolRunner.WaitIdle(70000); });
            RestoreOutcome o = await Task.Run(delegate { return Safety.RestoreDellAuto("app closing", 30000, false); });
            closeInProgress = false;
            if (o != RestoreOutcome.Unconfirmed)
            {
                allowClose = true;
                Close();
                return;
            }
            closing = false;
            ApplyRestoreOutcome(o);
            monitorTimer.Start();
            SetCommandsEnabled(true);
            MessageBox.Show(this, "Dell automatic fan control could NOT be confirmed, so the app stays open with the fan held at Max as a precaution.\n\nPress Dell Auto to try again, or restart the laptop.",
                "Fan safety", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnClosed(object sender, FormClosedEventArgs e)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
            ActivityLog.Line -= AppendLogLine;
            monitorTimer.Dispose();
            keepAliveTimer.Dispose();
            if (hardware != null) hardware.Close();
            if (counters != null) counters.Dispose();
            ActivityLog.Write("ACE Dell Control closed." + (Safety.NeedsRestore ? " WARNING: manual-mode marker still present." : ""));
        }

        // ---- helpers ------------------------------------------------------------

        /// <summary>Runs one user action at a time with every command button disabled (C2).</summary>
        private async Task RunGuardedAsync(Func<Task> action)
        {
            if (uiBusy || closing) return;
            uiBusy = true;
            SetCommandsEnabled(false);
            try { await action(); }
            catch (Exception ex) { Log("ERROR: " + ex.Message); SetOverallStatus("Unexpected error", Red); }
            finally
            {
                uiBusy = false;
                if (!IsDisposed && !closing) SetCommandsEnabled(true);
            }
        }

        private void SetCommandsEnabled(bool enabled)
        {
            foreach (Button b in commandButtons) if (!b.IsDisposed) b.Enabled = enabled;
        }

        private void RunOnUi(Action<RestoreOutcome> action, RestoreOutcome o)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(action, o);
            else action(o);
        }

        private void Fail(string message, CommandResult r)
        {
            Log("ERROR: " + message + ". " + r.Tail(220));
            SetOverallStatus(message, Red);
        }

        private void Log(string message)
        {
            ActivityLog.Write(message);
        }

        private void AppendLogLine(string line)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLogLine), line); return; }
            if (!logBox.IsDisposed) logBox.AppendText(line + Environment.NewLine);
        }

        private void SetOverallStatus(string text, Color color)
        {
            if (IsDisposed) return;
            statusValue.Text = text;
            statusValue.BackColor = Color.FromArgb(45, color);
            statusValue.ForeColor = color;
        }

        private GroupBox Group(string text, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = text;
            g.ForeColor = TextMuted;
            g.BackColor = PanelBg;
            g.FlatStyle = FlatStyle.Flat;
            g.SetBounds(x, y, w, h);
            return g;
        }

        private Label LabelOf(string text, float size, FontStyle style, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", size, style);
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            return l;
        }

        private Label StatusLine(string text)
        {
            Label l = LabelOf(text, 10f, FontStyle.Bold, TextMain);
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private Label Badge(string text, Color color)
        {
            Label l = LabelOf(text, 9f, FontStyle.Bold, color);
            l.TextAlign = ContentAlignment.MiddleCenter;
            l.BackColor = Color.FromArgb(45, color);
            return l;
        }

        private Button ActionButton(string text, int x, int y, Func<Task> action, Color? accent = null)
        {
            Button b = new Button();
            b.Text = text;
            b.Name = "btn" + Regex.Replace(text, "[^A-Za-z0-9]", "");
            b.AccessibleName = b.Name;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = accent ?? Blue;
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = Color.FromArgb(38, 45, 58);
            b.ForeColor = accent ?? TextMain;
            b.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            b.SetBounds(x, y, 130, 36);
            b.Click += async delegate { await RunGuardedAsync(action); };
            commandButtons.Add(b);
            return b;
        }

        private Label Metric(string caption, string value, int x)
        {
            Label panel = new Label();
            panel.Name = "metric" + Regex.Replace(caption, "[^A-Za-z0-9]", "");
            panel.AccessibleName = panel.Name;
            panel.Text = caption.ToUpperInvariant() + Environment.NewLine + value;
            panel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            panel.ForeColor = TextMain;
            panel.BackColor = Color.FromArgb(24, 29, 37);
            panel.TextAlign = ContentAlignment.MiddleCenter;
            panel.SetBounds(x, 24, 154, 48);
            return panel;
        }

        private void SetMetricValue(Label metric, string value)
        {
            string caption = metric.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)[0];
            metric.Text = caption + Environment.NewLine + value;
        }

        private static string DisplayThermal(string mode)
        {
            return mode == "UltraPerformance" ? "Ultra Performance" : mode;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool ShutdownBlockReasonCreate(IntPtr hWnd, string reason);

        [DllImport("user32.dll")]
        private static extern bool ShutdownBlockReasonDestroy(IntPtr hWnd);
    }

    /// <summary>Windows CPU and memory counters, opened off the UI thread.</summary>
    internal sealed class PerformanceCounterSet : IDisposable
    {
        private System.Diagnostics.PerformanceCounter cpu;
        private System.Diagnostics.PerformanceCounter memory;
        private double physicalMb;

        internal static PerformanceCounterSet Open(Action<string> log)
        {
            PerformanceCounterSet s = new PerformanceCounterSet();
            try
            {
                s.cpu = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
                s.cpu.NextValue();
                s.memory = new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes");
                using (ManagementObjectSearcher search = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                    foreach (ManagementObject item in search.Get())
                    {
                        s.physicalMb = Convert.ToDouble(item["TotalVisibleMemorySize"], CultureInfo.InvariantCulture) / 1024.0;
                        break;
                    }
            }
            catch (Exception ex) { log("Windows performance counters unavailable: " + ex.Message); }
            return s;
        }

        internal float CpuLoad() { return cpu == null ? -1 : cpu.NextValue(); }

        internal double RamPercent()
        {
            if (memory == null || physicalMb <= 0) return -1;
            float available = memory.NextValue();
            return 100.0 * (physicalMb - available) / physicalMb;
        }

        public void Dispose()
        {
            if (cpu != null) cpu.Dispose();
            if (memory != null) memory.Dispose();
        }
    }
}
