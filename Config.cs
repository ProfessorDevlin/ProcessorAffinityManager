using System;
using System.Collections.Generic;
using System.IO;

namespace ProzessorAffinityManager
{
    internal class AffinityRule
    {
        public string ProcessName;
        public long Mask;
    }

    /// <summary>
    /// Einstellungen und Regeln, gespeichert unter %AppData%\Prozessor Affinity Manager\config.txt
    /// </summary>
    internal class Config
    {
        public bool StartInTray;
        public string Language = Lang.Default;
        public List<AffinityRule> Rules = new List<AffinityRule>();

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Prozessor Affinity Manager");

        private static string FilePath => Path.Combine(Dir, "config.txt");

        public static Config Load()
        {
            var config = new Config();
            if (!File.Exists(FilePath))
                return config;

            foreach (string line in File.ReadAllLines(FilePath))
            {
                int eq = line.IndexOf('=');
                if (eq < 0)
                    continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (key == "StartInTray")
                {
                    config.StartInTray = value == "1";
                }
                else if (key == "Language")
                {
                    if (Lang.IsAvailable(value))
                        config.Language = value;
                }
                else if (key == "Rule")
                {
                    // Format: Prozessname|Bitmaske
                    int bar = value.LastIndexOf('|');
                    if (bar > 0 && long.TryParse(value.Substring(bar + 1), out long mask) && mask != 0)
                        config.Rules.Add(new AffinityRule { ProcessName = value.Substring(0, bar), Mask = mask });
                }
            }
            return config;
        }

        public void Save()
        {
            var lines = new List<string>
            {
                "StartInTray=" + (StartInTray ? "1" : "0"),
                "Language=" + Language,
            };
            foreach (var rule in Rules)
                lines.Add("Rule=" + rule.ProcessName + "|" + rule.Mask);

            Directory.CreateDirectory(Dir);
            File.WriteAllLines(FilePath, lines);
        }
    }
}
