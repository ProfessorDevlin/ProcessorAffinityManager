using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace ProzessorAffinityManager
{
    /// <summary>
    /// Autostart über die Windows-Aufgabenplanung. Ein normaler Autostart-Eintrag (Registry "Run")
    /// funktioniert nicht, weil Windows Programme mit Admin-Rechten dort nicht startet.
    /// </summary>
    internal static class Autostart
    {
        private const string TaskName = "Prozessor Affinity Manager";

        public static bool IsEnabled()
        {
            return RunSchtasks($"/Query /TN \"{TaskName}\"", out _) == 0;
        }

        public static void Enable()
        {
            string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
            string exe = SecurityElement.Escape(Application.ExecutablePath);

            // Start 60 Sekunden nach der Anmeldung, mit höchsten Rechten, ohne Laufzeitbegrenzung
            // und auch im Akkubetrieb.
            string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Startet Prozessor Affinity Manager 60 Sekunden nach der Anmeldung.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{user}</UserId>
      <Delay>PT60S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{user}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>5</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{exe}</Command>
    </Exec>
  </Actions>
</Task>";

            string xmlFile = Path.Combine(Path.GetTempPath(), "ProzessorAffinityManager_Task.xml");
            File.WriteAllText(xmlFile, xml, Encoding.Unicode);
            try
            {
                if (RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlFile}\" /F", out string output) != 0)
                    throw new InvalidOperationException(output.Trim());
            }
            finally
            {
                File.Delete(xmlFile);
            }
        }

        public static void Disable()
        {
            if (RunSchtasks($"/Delete /TN \"{TaskName}\" /F", out string output) != 0)
                throw new InvalidOperationException(output.Trim());
        }

        private static int RunSchtasks(string arguments, out string output)
        {
            var psi = new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var p = Process.Start(psi))
            {
                output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode;
            }
        }
    }
}
