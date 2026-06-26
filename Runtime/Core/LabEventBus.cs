using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pitech.XR.Core
{
    /// <summary>
    /// Default in-process implementation of <see cref="ILabEventBus"/> (map sec-7). Synchronous,
    /// fire-and-forget, allocation-free on the publish path. Each subscriber is wrapped so a throwing
    /// subscriber cannot break the runner or the other subscribers. NOT thread-safe by design: a lab
    /// bus is driven from the Unity main thread only. One instance per lab attempt (owned by
    /// <see cref="LabRuntimeContext"/>) - never a global singleton.
    /// </summary>
    public sealed class LabEventBus : ILabEventBus
    {
        readonly List<LabFactHandler> _handlers = new List<LabFactHandler>();

        // Reused dispatch snapshot so a subscriber can (un)subscribe during a publish without the
        // list shifting under the loop, and without allocating per publish. Grows only when the
        // subscriber count exceeds the previous high-water mark.
        LabFactHandler[] _dispatch = Array.Empty<LabFactHandler>();

        public void Publish(in LabEvent fact)
        {
            int count = _handlers.Count;
            if (count == 0) return;

            if (_dispatch.Length < count) _dispatch = new LabFactHandler[count];
            _handlers.CopyTo(_dispatch);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    _dispatch[i](in fact);
                }
                catch (Exception e)
                {
                    // Isolate the fault: one bad subscriber must not break the runner or the rest.
                    Debug.LogException(e);
                }
            }

            Array.Clear(_dispatch, 0, count); // don't pin delegates between publishes
        }

        public IDisposable Subscribe(LabFactHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler);
            return new Subscription(this, handler);
        }

        void Unsubscribe(LabFactHandler handler)
        {
            _handlers.Remove(handler);
        }

        sealed class Subscription : IDisposable
        {
            LabEventBus _bus;
            LabFactHandler _handler;

            public Subscription(LabEventBus bus, LabFactHandler handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_bus == null) return; // idempotent
                _bus.Unsubscribe(_handler);
                _bus = null;
                _handler = null;
            }
        }
    }
}
