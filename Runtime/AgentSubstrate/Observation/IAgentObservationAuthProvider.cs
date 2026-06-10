namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Resolves the Supabase user JWT for the agent_observation request.
    /// Host project supplies the implementation; the package never stores the JWT
    /// on a MonoBehaviour or in PlayerPrefs.
    /// </summary>
    public interface IAgentObservationAuthProvider
    {
        /// <returns>JWT, or null/empty if no authenticated session.</returns>
        string GetAccessToken();
    }
}
