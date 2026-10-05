using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ThirdPerson
{
    internal static class DpiTests
    {
        [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wp, IntPtr lp);
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        private static T Field<T>(MainForm form, string name) {
            return (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        }
        private static void Check(bool condition, string label, StringBuilder log) {
            if (!condition) throw new Exception("DPI test: " + label);
            log.AppendLine("PASS: " + label);
        }
        public static void Run(MainForm form, string report)
        {
            StringBuilder log = new StringBuilder();
            bool perMonitorV2 = AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), new IntPtr(-4));
            Check(perMonitorV2, "actual process uses PerMonitorV2 (no bitmap DPI virtualization)", log);
            Check(form.Icon != null, "application window icon loaded", log);
            int originalDpi = form.DeviceDpi;
            log.AppendLine("Host initial DPI: " + originalDpi);
            Check(form.AutoScaleMode == AutoScaleMode.Dpi, "DPI scaling enabled", log);
            foreach (int dpi in new int[] { 96,120,144,192,240,288,192,96 }) {
                int previous = form.DeviceDpi;
                Rect bounds = new Rect { Left=-10000, Top=-10000,
                    Right=-10000+(int)Math.Round(form.Width * dpi/(double)previous),
                    Bottom=-10000+(int)Math.Round(form.Height * dpi/(double)previous) };
                IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Rect)));
                try {
                    Marshal.StructureToPtr(bounds,memory,false);
                    SendMessage(form.Handle,0x02E0,new IntPtr((dpi<<16)|dpi),memory);
                } finally { Marshal.FreeHGlobal(memory); }
                Application.DoEvents();
                Check(form.DeviceDpi == dpi,"DPI change handled: " + dpi,log);
                form.ClientSize = new Size((int)Math.Round(1120 * dpi/96.0), (int)Math.Round(790 * dpi/96.0));
                form.PerformLayout(); Application.DoEvents();
                foreach (string name in new string[] { "paste","convert","cancel","copy","save","clear","demo","applyVariant","keepOriginal","undoDecision" }) {
                    Button button = Field<Button>(form,name);
                    Check(button.Right <= button.Parent.ClientSize.Width && button.Bottom <= button.Parent.ClientSize.Height,
                        dpi + " button fits: " + name,log);
                    Size text = TextRenderer.MeasureText(button.Text,button.Font);
                    Check(text.Width <= button.ClientSize.Width && text.Height <= button.ClientSize.Height,
                        dpi + " button text fits: " + name,log);
                }
                RichTextBox input=Field<RichTextBox>(form,"input"), output=Field<RichTextBox>(form,"output");
                Check(input.ClientSize.Height > dpi && output.ClientSize.Height > dpi, dpi + " editors retain usable height",log);
                ListView issues=Field<ListView>(form,"issues");
                Button action=Field<Button>(form,"applyVariant");
                Check(issues.Bottom <= action.Parent.Top, dpi + " review list does not overlap actions",log);
                Check(issues.Columns[0].Width >= (int)Math.Round(85*dpi/96.0)-2, dpi + " review columns scaled",log);
            }
            log.AppendLine("DPI changes were simulated with WM_DPICHANGED; system display settings were not changed.");
            log.AppendLine("This is not a visual test on a physical 4K multi-monitor configuration.");
            File.WriteAllText(report,log.ToString(),new UTF8Encoding(true));
        }
    }
}
