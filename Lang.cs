using System.Collections.Generic;
using System.Linq;

namespace ProzessorAffinityManager
{
    /// <summary>
    /// Sprachauswahl. Die Texte selbst stehen in Translations.cs.
    /// Neue Sprache: Eintrag in <see cref="Available"/> ergänzen und in Translations.cs einen Block anlegen.
    /// Fehlt ein Text in einer Sprache, wird der englische verwendet.
    /// </summary>
    internal static partial class Lang
    {
        // Die meistgesprochenen Sprachen der Welt, dazu Polnisch und Griechisch. Namen in der jeweiligen Sprache.
        public static readonly (string Code, string Name)[] Available =
        {
            ("en", "English"),
            ("zh", "中文 (简体)"),
            ("hi", "हिन्दी"),
            ("es", "Español"),
            ("ar", "العربية"),
            ("fr", "Français"),
            ("bn", "বাংলা"),
            ("pt", "Português"),
            ("ru", "Русский"),
            ("id", "Bahasa Indonesia"),
            ("ur", "اردو"),
            ("de", "Deutsch"),
            ("ja", "日本語"),
            ("mr", "मराठी"),
            ("vi", "Tiếng Việt"),
            ("te", "తెలుగు"),
            ("tr", "Türkçe"),
            ("ta", "தமிழ்"),
            ("ko", "한국어"),
            ("it", "Italiano"),
            ("pl", "Polski"),
            ("el", "Ελληνικά"),
        };

        /// <summary>Standardsprache beim ersten Start.</summary>
        public const string Default = "en";

        public static string Current { get; private set; } = Default;

        public static bool IsAvailable(string code) => Available.Any(a => a.Code == code);

        public static void Set(string code)
        {
            Current = IsAvailable(code) ? code : Default;
        }

        public static string T(string key)
        {
            if (Texts.TryGetValue(Current, out var texts) && texts.TryGetValue(key, out var text))
                return text;
            if (Texts[Default].TryGetValue(key, out var fallback))
                return fallback;
            return key;
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);
    }
}
