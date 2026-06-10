using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.AgentSubstrate.Observation
{
    /// <summary>
    /// Bounded drop-oldest ring buffer for envelope+surface pairs awaiting
    /// transmission. Drop-oldest (not drop-newest) preserves recency; the
    /// assumption is that the newest observation is the most actionable one.
    /// Logs a single Warning per overflow event with the running drop count.
    /// </summary>
    public sealed class AgentObservationQueue
    {
        readonly Queue<AgentObservationOutboundItem> buffer;
        readonly int capacity;
        int totalDropped;

        public AgentObservationQueue(int capacity)
        {
            this.capacity = capacity <= 0 ? 1 : capacity;
            this.buffer = new Queue<AgentObservationOutboundItem>(this.capacity);
        }

        public int Count => buffer.Count;
        public int Capacity => capacity;
        public int TotalDropped => totalDropped;

        public void Enqueue(AgentObservationOutboundItem item)
        {
            if (item.Envelope == null) return;
            if (buffer.Count >= capacity)
            {
                buffer.Dequeue();
                totalDropped++;
                Debug.LogWarning(
                    "[AgentObservation] queue overflow; dropped oldest envelope (running total " +
                    totalDropped.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").");
            }
            buffer.Enqueue(item);
        }

        public bool TryDequeue(out AgentObservationOutboundItem item)
        {
            if (buffer.Count == 0)
            {
                item = default;
                return false;
            }
            item = buffer.Dequeue();
            return true;
        }
    }
}
