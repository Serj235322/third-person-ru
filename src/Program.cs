using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Третье лицо")]
[assembly: AssemblyDescription("Локальное преобразование русскоязычного текста по правилам")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace ThirdPerson
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--self-test") return Tests.Run(args[1]);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--ui-test")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                int code = 0;
                MainForm form = new MainForm();
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-10000, -10000);
                form.Shown += delegate {
                    form.BeginInvoke(new MethodInvoker(delegate {
                        try { form.RunUITests(Path.GetFullPath(args[1])); }
                        catch (Exception ex) { File.WriteAllText(args[1], ex.ToString()); code = 1; }
                        finally { form.EndUITest(); }
                    }));
                };
                Application.Run(form); return code;
            }
            if (args.Length == 2 && args[0] == "--render-sample")
            {
                try {
                    using (MainForm form = new MainForm()) form.RenderSample(Path.GetFullPath(args[1]));
                    return 0;
                } catch (Exception ex) { File.WriteAllText(args[1] + ".error.txt", ex.ToString()); return 1; }
            }
            Application.Run(new MainForm()); return 0;
        }
    }
}
