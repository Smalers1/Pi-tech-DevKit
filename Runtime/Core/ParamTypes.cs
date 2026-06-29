namespace Pitech.XR.Core
{
    // ---------- Param-store type vocabulary (map sec-8) ----------
    // The one typed param store supersedes Stats (StatsConfig/StatEffect). These enums are the
    // shared vocabulary the declaration (ConsoleParameter), the value union (ParamValue), and the
    // store (IParamStore) build on. As of WS B1.2 Step 4 the Local store is the LIVE writer (LabConsole
    // routes effects/quiz/conditions through it; the Stats system is demoted to a UI display mirror).
    // The Networked impl + scope replication remain deferred (Fusion pass). Emit surface freezes 2026-07-07.

    /// <summary>The kind a <see cref="ParamValue"/> carries. Bool/Int/Enum/Float pack into the
    /// numeric slot; String uses the text slot (map sec-8 union).</summary>
    public enum ParamType
    {
        Bool,
        Int,
        Float,
        Enum,
        String
    }

    /// <summary>Per-parameter replication scope (map sec-8). Local = per-client (Definition +
    /// Local-runtime layers only). Networked = replicated (gains the Shared-truth layer); auto-
    /// degrades to Local in a no-Fusion / single-player session.</summary>
    public enum ParamScope
    {
        Local,
        Networked
    }

    /// <summary>A mutation op on a numeric parameter - the StatEffect successor (map sec-8). The
    /// member ORDER mirrors the legacy <c>StatOp</c> {Add, Subtract, Multiply, Divide, Set} EXACTLY,
    /// so a serialized StatOp int migrates to the same ParamOp by value - a rename, not a behaviour
    /// change. Keep this order locked to the migration.</summary>
    public enum ParamOp
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Set
    }
}
