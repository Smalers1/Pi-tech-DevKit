namespace Pitech.XR.Core
{
    /// <summary>
    /// Minimal, stable control surface over a scenario runner. Behaviour-neutral seam (WS A8):
    /// implementers forward to existing members; no new behaviour.
    ///
    /// Trajectory (Petros, 2026-06-10) - do NOT widen now, keep exactly these three members: Phase D
    /// extracts the runner behind this seam; Phase E adds <c>IScenarioFlowStore</c> beneath it; Phase H
    /// defines the gated flow-control vocabulary (<c>advance_step</c> / <c>branch_to</c> /
    /// <c>pause_scenario</c>) that routes through LabConsole onto this seam toward VickyMode.Director.
    /// Design choices here must never assume the caller is only ContentDelivery.
    /// </summary>
    public interface ISceneRunnerControl
    {
        /// <summary>Current step index while running; -1 when idle or finished. Forwards SceneManager.StepIndex.</summary>
        int CurrentStepIndex { get; }

        /// <summary>Whether the runner auto-starts from Start(). Forwards SceneManager.autoStart.</summary>
        bool AutoStart { get; set; }

        /// <summary>Restart the scenario from the beginning. Forwards SceneManager.Restart().</summary>
        void Restart();
    }
}
