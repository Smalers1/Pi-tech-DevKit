using System.Collections.Generic;
using System.Text;

namespace Pitech.XR.Localization
{
    // ---------- Sample Greek + English content (WS B2.5 verification) ----------
    // The DevKit's OWN de-hardcoded keys (the Quiz strings routed through LocalizationServices in B1.5)
    // in English + Greek, so the seam can be exercised end-to-end NOW, before the full StringTable
    // pipeline runs on real labs (which is post-B2 per Stergios). Install one and confirm the Quiz
    // renders in that language:
    //   LocalizationServices.Install(new DictionaryLocalizationLookup(SampleLocalizationStrings.Greek));
    // This is a TEST/DEMO fixture - real lab content is authored through the (post-B2) baked StringTables.
    //
    // ENCODING NOTE: the Greek strings are built from Unicode CODE POINTS via U(...), so this source file
    // stays PURE ASCII. That is deliberate and ENCODING-PROOF: raw Greek string literals render as MOJIBAKE
    // when the Unity/Windows C# compiler reads a no-BOM .cs in the system codepage. Code points are the
    // standard Greek block (U+0391..U+03CC); an ASCII transliteration is on each line.

    /// <summary>English + Greek sample maps for the DevKit's known UI keys (verification only).</summary>
    public static class SampleLocalizationStrings
    {
        /// <summary>English source map (matches the authored fallbacks - effectively a passthrough).</summary>
        public static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "quiz.feedback.correct", "Correct (+{0})" },
            { "quiz.feedback.wrong", "Wrong" },
            { "quiz.results.passed", "Passed" },
            { "quiz.results.failed", "Failed" },
            { "quiz.results.answered", "Answered: {0}" },
            { "quiz.results.correct", "Correct: {0}" },
            { "quiz.results.wrong", "Wrong: {0}" },
        };

        /// <summary>Greek translations of the DevKit's known UI keys (human-reviewed medical Greek; {0}
        /// placeholders preserved). Built from code points (pure ASCII) so they are encoding-proof.</summary>
        public static readonly Dictionary<string, string> Greek = new Dictionary<string, string>
        {
            { "quiz.feedback.correct", U(0x03A3, 0x03C9, 0x03C3, 0x03C4, 0x03CC) + " (+{0})" },                            // "Sosto (+{0})"
            { "quiz.feedback.wrong",   U(0x039B, 0x03AC, 0x03B8, 0x03BF, 0x03C2) },                                        // "Lathos"
            { "quiz.results.passed",   U(0x0395, 0x03C0, 0x03B9, 0x03C4, 0x03C5, 0x03C7, 0x03AF, 0x03B1) },                // "Epitychia"
            { "quiz.results.failed",   U(0x0391, 0x03C0, 0x03BF, 0x03C4, 0x03C5, 0x03C7, 0x03AF, 0x03B1) },                // "Apotychia"
            { "quiz.results.answered", U(0x0391, 0x03C0, 0x03B1, 0x03BD, 0x03C4, 0x03AE, 0x03B8, 0x03B7, 0x03BA, 0x03B1, 0x03BD) + ": {0}" }, // "Apantithikan: {0}"
            { "quiz.results.correct",  U(0x03A3, 0x03C9, 0x03C3, 0x03C4, 0x03AC) + ": {0}" },                              // "Sosta: {0}"
            { "quiz.results.wrong",    U(0x039B, 0x03AC, 0x03B8, 0x03B7) + ": {0}" },                                      // "Lathi: {0}"
        };

        /// <summary>Compose a string from Unicode code points - keeps this file pure ASCII (encoding-proof).</summary>
        static string U(params int[] codePoints)
        {
            var sb = new StringBuilder(codePoints.Length);
            for (int i = 0; i < codePoints.Length; i++) sb.Append((char)codePoints[i]);
            return sb.ToString();
        }
    }
}
