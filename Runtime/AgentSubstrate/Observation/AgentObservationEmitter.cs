using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Scene-attached MonoBehaviour that turns IAgentStateSource snapshots into
    /// AgentObservationV1Envelope payloads and forwards them to the injected HTTP
    /// client. Fail-closed on consent: any null/absent IConsentGate, or any false
    /// IsConsentGranted, results in a Verbose-level silent drop (no network call).
    ///
    /// Plan §2.4, §2.7, §2.8.
    /// </summary>
    [DisallowMultipleComponent]
    public class AgentObservationEmitter : MonoBehaviour
    {
        [SerializeField] string endpointUrl;
        [SerializeField] List<string> enabledSurfaces = new List<string>
        {
            AgentObservationSurfaceV1.Ar,
            AgentObservationSurfaceV1.Vr,
        };
        [SerializeField] string surface = AgentObservationSurfaceV1.Ar;
        [SerializeField] int queueCapacity = 32;
        [SerializeField, Tooltip("Verbose logging for fail-closed consent drops. Off by default to avoid log spam during the PIT-NEW-A in-flight window.")]
        bool verboseConsentDrops;

        IConsentGate consentGate;
        IAgentStateSource stateSource;
        IAgentObservationTransport transport;

        public string Surface
        {
            get => surface;
            set => surface = value;
        }

        public string EndpointUrl
        {
            get => endpointUrl;
            set => endpointUrl = value;
        }

        public int QueueCapacity => queueCapacity;
        public IReadOnlyList<string> EnabledSurfaces => enabledSurfaces;

        public void Configure(
            IConsentGate consentGate,
            IAgentStateSource stateSource,
            IAgentObservationTransport transport)
        {
            // Configure may run AFTER OnEnable when the emitter is added via
            // AddComponent (Unity fires OnEnable before AddComponent returns).
            // Unhook from any prior state source first, then re-subscribe to the
            // new one if the component is currently enabled. Safe to call
            // multiple times. The auth provider is owned by the transport, not
            // the emitter, so a JWT refresh during a backoff window is honored
            // on the next retry rather than baked in at enqueue time.
            if (this.stateSource != null)
            {
                this.stateSource.ObservationReady -= OnObservationReady;
            }

            this.consentGate = consentGate;
            this.stateSource = stateSource;
            this.transport = transport;

            if (this.stateSource != null && isActiveAndEnabled)
            {
                this.stateSource.ObservationReady += OnObservationReady;
            }
        }

        void OnEnable()
        {
            if (stateSource != null) stateSource.ObservationReady += OnObservationReady;
        }

        void OnDisable()
        {
            if (stateSource != null) stateSource.ObservationReady -= OnObservationReady;
        }

        void OnObservationReady(AgentStateSnapshot snapshot)
        {
            Emit(snapshot);
        }

        /// <summary>
        /// Build an envelope from <paramref name="snapshot"/> and hand it to the
        /// transport. Returns the constructed envelope (useful for tests). Returns
        /// null when the emission was dropped (consent denied, snapshot null,
        /// surface not enabled, etc.).
        /// </summary>
        public AgentObservationV1Envelope Emit(AgentStateSnapshot snapshot)
        {
            if (snapshot == null) return null;

            if (!enabledSurfaces.Contains(surface))
            {
                return null;
            }

            var gate = consentGate ?? new DenyAllConsentGate();
            if (!gate.IsConsentGranted())
            {
                if (verboseConsentDrops)
                {
                    Debug.Log("[AgentObservation] Consent not granted; observation dropped (fail-closed).");
                }
                return null;
            }

            var envelope = BuildEnvelope(snapshot);

            if (transport != null)
            {
                transport.Send(envelope, surface);
            }
            return envelope;
        }

        public AgentObservationV1Envelope BuildEnvelope(AgentStateSnapshot snapshot)
        {
            var obs = new AgentObservationV1
            {
                version = "v1",
                observationId = Guid.NewGuid().ToString("D"),
                kind = snapshot.Kind,
                observedAt = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                surface = surface,
                labId = snapshot.LabId,
                labVersionId = snapshot.LabVersionId,
                attemptId = snapshot.AttemptId,
                sessionId = snapshot.SessionId,
                semanticState = snapshot.SemanticState ?? new AgentObservationSemanticStateV1
                {
                    summary = string.Empty,
                },
                renderedState = snapshot.RenderedState,
                engine = new AgentObservationEngineV1
                {
                    name = "unity",
                    version = Application.unityVersion,
                },
            };

            var envelope = new AgentObservationV1Envelope();
            envelope.observations.Add(obs);
            return envelope;
        }
    }
}
