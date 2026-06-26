using System;
using System.Collections.Generic;
using Pitech.XR.Core;

namespace Pitech.XR.Networking
{
    /// <summary>
    /// The single-player <see cref="IScenarioFlowStore"/> impl (map sec-7 / sec-10.1): an in-memory
    /// append-only guid path, always the driver - a transparent passthrough so single-player / AR is
    /// trace-identical to today. Always compiled (no Fusion). The Networked twin
    /// (<c>FusionScenarioPath</c>, <c>[Networked, Capacity(256)]</c>) is authored under
    /// <c>#if PITECH_HAS_FUSION</c> in this same module. INERT in Phase B.1 - the runner writes/reads
    /// it in WS B1.7 / Phase B.2.
    ///
    /// Internal to the package (the seam is [InternalsVisibleTo]-internal at launch, map sec-7).
    /// </summary>
    internal sealed class LocalScenarioPath : IScenarioFlowStore
    {
        readonly List<string> _path = new List<string>();

        public bool IsDriver => true;   // single-player: always the driver

        public int Count => _path.Count;

        public string Last => _path.Count > 0 ? _path[_path.Count - 1] : string.Empty;

        public string GetEntered(int index) => _path[index];

        public void AppendEntered(string stepGuid)
        {
            _path.Add(stepGuid);
            Changed?.Invoke();
        }

        public event Action Changed;
    }
}
