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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AceDellControl
{
    /// <summary>Exact Dell model allowlist (M5). Only physically validated models belong here.</summary>
    internal sealed class ModelGate
    {
        private static readonly string[] AllowedModels = { "Latitude 7400" };
        internal string Manufacturer = "";
        internal string Model = "";
        internal bool Passed;

        internal static ModelGate Detect()
        {
            ModelGate gate = new ModelGate();
            try
            {
                using (ManagementObjectSearcher search = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                    foreach (ManagementObject item in search.Get())
                    {
                        gate.Manufacturer = (Convert.ToString(item["Manufacturer"]) ?? "").Trim();
                        gate.Model = (Convert.ToString(item["Model"]) ?? "").Trim();
                        break;
                    }
            }
            catch { }
            gate.Passed = String.Equals(gate.Manufacturer, "Dell Inc.", StringComparison.Ordinal) &&
                Array.IndexOf(AllowedModels, gate.Model) >= 0;
            return gate;
        }

        internal string Describe()
        {
            return "Model gate: " + (Passed ? "PASSED" : "CLOSED") + " • detected model: '" + Manufacturer + " / " + Model + "'";
        }
    }

    internal sealed class HardwareReader
    {
        private object computer;
        private PropertyInfo hardwareProperty;
        private readonly Action<string> log;

        // Only these assemblies may be resolved from the sensor folder (H4: strict resolve).
        private static readonly string[] KnownAssemblies =
        {
            "LibreHardwareMonitorLib", "System.Buffers", "System.Memory",
            "System.Numerics.Vectors", "System.Runtime.CompilerServices.Unsafe"
        };

        internal HardwareReader(string libraryPath, Action<string> logger)
        {
            log = logger;
            try
            {
                // Load only from the trusted install folder (Program Main refuses any other location).
                string dir = Path.GetFullPath(Path.GetDirectoryName(libraryPath));
                if (!String.Equals(dir, Path.GetFullPath(Paths.LhmDir), StringComparison.OrdinalIgnoreCase))
                { log("Sensor library path rejected; CPU temperature will show N/A."); return; }
                if (!File.Exists(libraryPath)) { log("LibreHardwareMonitor library not found; CPU temperature will show N/A."); return; }
                AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
                {
                    string name = new AssemblyName(args.Name).Name;
                    if (Array.IndexOf(KnownAssemblies, name) < 0) return null;
                    string candidate = Path.Combine(dir, name + ".dll");
                    return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
                };
                Assembly a = Assembly.LoadFrom(libraryPath);
                Type t = a.GetType("LibreHardwareMonitor.Hardware.Computer", true);
                computer = Activator.CreateInstance(t);
                Set(t, "IsCpuEnabled", true);
                t.GetMethod("Open").Invoke(computer, null);
                hardwareProperty = t.GetProperty("Hardware");
                log("LibreHardwareMonitor sensor library connected.");
            }
            catch (Exception ex) { computer = null; log("LibreHardwareMonitor unavailable: " + RootMessage(ex)); }
        }

        internal double? ReadCpuPackageTemperature()
        {
            if (computer == null || hardwareProperty == null) return null;
            try
            {
                List<double> package = new List<double>();
                List<double> cores = new List<double>();
                foreach (object hw in (IEnumerable)hardwareProperty.GetValue(computer, null)) Visit(hw, package, cores);
                if (package.Count > 0) return Max(package);
                if (cores.Count > 0) return Max(cores);
            }
            catch { }
            return null;
        }

        private void Visit(object hw, List<double> package, List<double> cores)
        {
            Type t = hw.GetType();
            MethodInfo update = t.GetMethod("Update");
            if (update != null) update.Invoke(hw, null);
            PropertyInfo sensorsProp = t.GetProperty("Sensors");
            if (sensorsProp != null)
            {
                foreach (object sensor in (IEnumerable)sensorsProp.GetValue(hw, null))
                {
                    Type st = sensor.GetType();
                    string sensorType = Convert.ToString(st.GetProperty("SensorType").GetValue(sensor, null));
                    if (!String.Equals(sensorType, "Temperature", StringComparison.OrdinalIgnoreCase)) continue;
                    string name = Convert.ToString(st.GetProperty("Name").GetValue(sensor, null));
                    object value = st.GetProperty("Value").GetValue(sensor, null);
                    if (value == null) continue;
                    double v = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    if (name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0) package.Add(v);
                    else if (name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0) cores.Add(v);
                }
            }
            PropertyInfo subProp = t.GetProperty("SubHardware");
            if (subProp != null)
                foreach (object sub in (IEnumerable)subProp.GetValue(hw, null)) Visit(sub, package, cores);
        }

        internal void Close()
        {
            try { if (computer != null) computer.GetType().GetMethod("Close").Invoke(computer, null); } catch { }
        }

        private void Set(Type t, string name, bool value)
        {
            PropertyInfo p = t.GetProperty(name);
            if (p != null) p.SetValue(computer, value, null);
        }

        private static double Max(List<double> values)
        {
            double max = Double.MinValue;
            foreach (double v in values) if (v > max) max = v;
            return max;
        }

        private static string RootMessage(Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            return ex.Message;
        }
    }

    internal static class DellThermalReader
    {
        private const string Scope = "root/wmi";
        private const string Instance = @"ACPI\PNP0C14\0_0";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SmiObject
        {
            public ushort Class;
            public ushort Selector;
            public uint Input1, Input2, Input3, Input4;
            public uint Output1, Output2, Output3, Output4;
        }

        internal static string GetCurrent()
        {
            try
            {
                SmiObject msg = new SmiObject();
                msg.Class = 17;
                msg.Selector = 19;
                byte[] request = ToBytes(msg);
                byte[] buffer = new byte[32768];
                Buffer.BlockCopy(request, 0, buffer, 0, request.Length);
                ManagementBaseObject instance = new ManagementClass(Scope, "BDat", null).CreateInstance();
                instance["Bytes"] = buffer;
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(new ManagementScope(Scope), new SelectQuery("BFn"));
                searcher.Options.EnsureLocatable = true;
                foreach (ManagementObject item in searcher.Get())
                {
                    if (!String.Equals(Convert.ToString(item["InstanceName"]), Instance, StringComparison.OrdinalIgnoreCase)) continue;
                    ManagementBaseObject parameters = item.GetMethodParameters("DoBFn");
                    parameters["Data"] = instance;
                    ManagementBaseObject returned = (ManagementBaseObject)item.InvokeMethod("DoBFn", parameters, null).Properties["Data"].Value;
                    msg = FromBytes((byte[])returned["Bytes"]);
                    switch (msg.Output3)
                    {
                        case 1: return "Optimized";
                        case 2: return "Cool";
                        case 4: return "Quiet";
                        case 8: return "Ultra Performance";
                        default: return "Unknown (" + msg.Output3 + ")";
                    }
                }
            }
            catch { }
            return "Unavailable";
        }

        private static byte[] ToBytes(SmiObject value)
        {
            int size = Marshal.SizeOf(value);
            byte[] bytes = new byte[size];
            IntPtr p = Marshal.AllocHGlobal(size);
            try { Marshal.StructureToPtr(value, p, false); Marshal.Copy(p, bytes, 0, size); }
            finally { Marshal.FreeHGlobal(p); }
            return bytes;
        }

        private static SmiObject FromBytes(byte[] bytes)
        {
            int size = Marshal.SizeOf(typeof(SmiObject));
            IntPtr p = Marshal.AllocHGlobal(size);
            try { Marshal.Copy(bytes, 0, p, size); return (SmiObject)Marshal.PtrToStructure(p, typeof(SmiObject)); }
            finally { Marshal.FreeHGlobal(p); }
        }
    }
}
