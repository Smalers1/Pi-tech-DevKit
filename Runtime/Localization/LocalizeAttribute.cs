using System;

namespace Pitech.XR.Localization
{
    /// <summary>
    /// Marks a serialized string field/property (data-asset text such as a QuizAsset prompt or
    /// answer) or a code-literal holder as localizable, so the editor keying scan (WS B1.5 Step 3,
    /// the "extend keying beyond scene TMP_Text" half) can collect it into the manifest the same way
    /// the scene scanner collects TMP_Text. At runtime the holder resolves through
    /// <see cref="LocalizationServices.Resolve(string, string)"/> using its key.
    ///
    /// Pass an explicit <see cref="Key"/> when the string needs a stable, human-meaningful key
    /// (e.g. "quiz.wrong"); otherwise leave it null and the scan derives one from the declaring
    /// type + member, mirroring the scene scanner's path-derived keys.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class LocalizeAttribute : Attribute
    {
        /// <summary>Optional explicit key; null = let the keying scan derive one.</summary>
        public string Key { get; }

        public LocalizeAttribute() { }

        public LocalizeAttribute(string key) { Key = key; }
    }
}
