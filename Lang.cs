using System.Collections.Generic;
using System.Linq;

namespace ProzessorAffinityManager
{
    /// <summary>
    /// Language selection. The texts themselves are in Translations.cs.
    /// New language: add an entry to <see cref="Available"/> and a block in Translations.cs.
    /// If a text is missing in a language, the English one is used.
    /// </summary>
    internal static partial class Lang
    {
        // The most spoken languages in the world, plus Polish and Greek. Names are written in each language itself.
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

        /// <summary>Default language on first start.</summary>
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
