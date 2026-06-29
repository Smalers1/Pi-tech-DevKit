using System;

namespace Pitech.XR.Core
{
    /// <summary>
    /// The one typed parameter store (map sec-8) - successor to the Stats system
    /// (StatsConfig/StatsRuntime/StatEffect). Holds <c>{ id -> ParamValue }</c> over declared
    /// <see cref="ConsoleParameter"/>s; writes are clamped to the declaration's range; relative ops
    /// (<see cref="ParamOp"/>) are the StatEffect successor. Two impls: a Local <c>Dictionary</c> and
    /// a Networked Fusion-backed one (map sec-10); a Networked param auto-degrades to Local with no
    /// Fusion. <see cref="ILabStateStore"/> is the narrow bool-view over this. LIVE as of WS B1.2 Step 4
    /// (the Local impl is LabConsole's runtime source of truth, superseding Stats).
    /// </summary>
    public interface IParamStore
    {
        /// <summary>Register a parameter declaration and seed its default value.</summary>
        void Declare(ConsoleParameter declaration);

        /// <summary>True if a parameter with this id has been declared.</summary>
        bool IsDeclared(string id);

        /// <summary>Read the raw value. Returns false (and a default value) if the id is unset.</summary>
        bool TryGet(string id, out ParamValue value);

        /// <summary>Write a value, clamped to the declaration's range (if declared).</summary>
        void Set(string id, in ParamValue value);

        /// <summary>Apply a relative op to a numeric parameter, clamped to range. On the networked
        /// side relative ops must apply authority-only + sequenced (map sec-8) - the Networked impl's
        /// concern; the Local impl applies directly.</summary>
        void Apply(string id, ParamOp op, float operand);

        // ---- typed accessors (sugar over the union) ----
        bool GetBool(string id, bool fallback = false);
        int GetInt(string id, int fallback = 0);
        float GetFloat(string id, float fallback = 0f);
        string GetString(string id, string fallback = "");
        T GetEnum<T>(string id, T fallback = default) where T : struct, Enum;

        void SetBool(string id, bool value);
        void SetInt(string id, int value);
        void SetFloat(string id, float value);
        void SetString(string id, string value);
        void SetEnum<T>(string id, T value) where T : struct, Enum;

        /// <summary>Raised with the parameter id whenever its value changes.</summary>
        event Action<string> ParamChanged;
    }
}
