using System;
using System.Threading;
using System.Windows.Forms;

namespace ProzessorAffinityManager
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, @"Local\ProzessorAffinityManager", out bool firstInstance))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ProzessorAffinityManager.Show"))
            {
                // Already running (e.g. in the tray)? Then open the existing window instead of starting a second time.
                if (!firstInstance)
                {
                    showEvent.Set();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var config = Config.Load();
                Lang.Set(config.Language);
                var form = new MainForm(config, config.StartInTray);

                ThreadPool.RegisterWaitForSingleObject(showEvent,
                    (state, timedOut) => form.BeginInvoke(new Action(form.ShowFromTray)),
                    null, Timeout.Infinite, false);

                Application.Run(form);
            }
        }
    }
}
