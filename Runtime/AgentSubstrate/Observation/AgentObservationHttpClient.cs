using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Production HTTP transport. UnityWebRequest (not System.Net.Http) for IL2CPP
    /// AOT compat on Quest + Android. Owns a bounded drop-oldest queue and drives
    /// the per-envelope retry loop via a coroutine.
    ///
    /// Auth tokens are resolved fresh per envelope from the configured
    /// <see cref="IAgentObservationAuthProvider"/> (set via <see cref="Configure"/>)
    /// so that a JWT refresh during a backoff window is picked up on the next retry
    /// instead of being baked in at enqueue time. Surface travels with the envelope
    /// in the queue so interleaved per-surface sends ship the correct header.
    /// </summary>
    [DisallowMultipleComponent]
    public class AgentObservationHttpClient : MonoBehaviour, IAgentObservationTransport
    {
        [SerializeField] string endpointUrl;
        [SerializeField] int queueCapacity = 32;

        AgentObservationQueue queue;
        IAgentObservationAuthProvider authProvider;
        bool pumping;

        public string EndpointUrl
        {
            get => endpointUrl;
            set => endpointUrl = value;
        }

        public int QueueCapacity
        {
            get => queueCapacity;
            set
            {
                queueCapacity = value;
                queue = null; // lazy-rebuild on next access with the new capacity.
            }
        }

        public AgentObservationQueue Queue
        {
            get
            {
                if (queue == null) queue = new AgentObservationQueue(queueCapacity);
                return queue;
            }
        }

        /// <summary>
        /// Wire the auth provider used to resolve a fresh JWT per envelope. Pass
        /// null explicitly to ship requests without an Authorization header (the
        /// edge function will reply AUTH_REQUIRED, which is a Warning-level retry
        /// per plan §2.6 — useful as the v1 default while the host project's auth
        /// stack is not yet wired).
        /// </summary>
        public void Configure(IAgentObservationAuthProvider authProvider)
        {
            this.authProvider = authProvider;
        }

        public void Send(AgentObservationV1Envelope envelope, string surface)
        {
            if (envelope == null) return;
            if (string.IsNullOrEmpty(endpointUrl))
            {
                Debug.LogWarning("[AgentObservation] EndpointUrl not configured; dropping envelope.");
                return;
            }
            Queue.Enqueue(new AgentObservationOutboundItem(envelope, surface));
            if (!pumping && isActiveAndEnabled)
            {
                StartCoroutine(Pump());
            }
        }

        IEnumerator Pump()
        {
            pumping = true;
            try
            {
                while (Queue.TryDequeue(out var item))
                {
                    yield return SendWithRetry(item);
                }
            }
            finally
            {
                pumping = false;
            }
        }

        IEnumerator SendWithRetry(AgentObservationOutboundItem item)
        {
            var body = AgentObservationEnvelopeWriter.ToJson(item.Envelope);
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            int retry = 0;
            while (true)
            {
                // Resolve auth token FRESH on every attempt so a refresh during
                // a backoff window is picked up on the next try (not the value
                // captured at the initial Send() call site).
                var token = authProvider != null ? authProvider.GetAccessToken() : null;

                using (var req = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST))
                {
                    req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("X-Vicky-Surface", item.Surface ?? string.Empty);
                    if (!string.IsNullOrEmpty(token))
                    {
                        req.SetRequestHeader("Authorization", "Bearer " + token);
                    }

                    yield return req.SendWebRequest();

                    long status = req.responseCode;
                    string responseBody = req.downloadHandler != null ? req.downloadHandler.text : null;

                    var disposition = AgentObservationResponseClassifier.Classify(status, responseBody);
                    AgentObservationResponseClassifier.Apply(disposition);

                    if (!disposition.ShouldRetry)
                    {
                        yield break;
                    }
                }

                retry++;
                if (!TryConsumeRetryBudget(retry))
                {
                    yield break;
                }
                yield return new WaitForSecondsRealtime(AgentObservationRetryPolicy.DelaySeconds(retry));
            }
        }

        /// <summary>
        /// Pure retry-budget gate factored out of <see cref="SendWithRetry"/> so the
        /// drop-after-budget warning is unit-testable without standing up a real
        /// <see cref="UnityWebRequest"/>. Returns true when <paramref name="attempt"/>
        /// is within budget; returns false and emits the drop warning once when the
        /// budget is exhausted.
        /// </summary>
        public static bool TryConsumeRetryBudget(int attempt)
        {
            if (attempt > AgentObservationRetryPolicy.MaxRetries)
            {
                Debug.LogWarning("[AgentObservation] retry budget exhausted (" +
                    AgentObservationRetryPolicy.MaxRetries + " retries); envelope dropped.");
                return false;
            }
            return true;
        }
    }
}
