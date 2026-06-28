using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Pitech.XR.Core;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Coverage for <see cref="LabEventBus"/> (map sec-7 notification plane): publish with no
    /// subscribers is a silent no-op, a published <see cref="LabEvent"/> reaches every subscriber with
    /// its fields intact, a THROWING subscriber is isolated (it logs, the rest still fire), disposing a
    /// subscription unsubscribes it, and subscribing during a publish does not corrupt the in-flight
    /// dispatch (it takes effect only on the next publish). Pure in-process - no scene/asset deps.
    /// </summary>
    public class LabEventBusTests
    {
        static LabEvent SampleEvent()
            => new LabEvent("step.entered", "attempt-1", "lab-instance-7", tick: 42, number: 3.5, text: "guid-x");

        [Test]
        public void Publish_NoSubscribers_DoesNotThrow()
        {
            var bus = new LabEventBus();
            Assert.DoesNotThrow(() =>
            {
                var fact = SampleEvent();
                bus.Publish(in fact);
            });
        }

        [Test]
        public void Publish_DeliversAllFieldsToSubscriber()
        {
            var bus = new LabEventBus();
            LabEvent received = default;
            int hits = 0;

            void Handler(in LabEvent fact)
            {
                received = fact;
                hits++;
            }

            using (bus.Subscribe(Handler))
            {
                var sent = SampleEvent();
                bus.Publish(in sent);
            }

            Assert.AreEqual(1, hits);
            Assert.AreEqual("step.entered", received.Key);
            Assert.AreEqual("attempt-1", received.AttemptId);
            Assert.AreEqual("lab-instance-7", received.LabInstanceId);
            Assert.AreEqual(42, received.Tick);
            Assert.AreEqual(3.5, received.Number, 0.0001);
            Assert.AreEqual("guid-x", received.Text);
        }

        [Test]
        public void Publish_DeliversToMultipleSubscribers()
        {
            var bus = new LabEventBus();
            int a = 0, b = 0, c = 0;

            void HandlerA(in LabEvent fact) => a++;
            void HandlerB(in LabEvent fact) => b++;
            void HandlerC(in LabEvent fact) => c++;

            using (bus.Subscribe(HandlerA))
            using (bus.Subscribe(HandlerB))
            using (bus.Subscribe(HandlerC))
            {
                var fact = SampleEvent();
                bus.Publish(in fact);
            }

            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
            Assert.AreEqual(1, c);
        }

        [Test]
        public void Publish_ThrowingSubscriber_DoesNotBlockOthers()
        {
            var bus = new LabEventBus();
            int before = 0, after = 0;

            void First(in LabEvent fact) => before++;
            void Thrower(in LabEvent fact) => throw new InvalidOperationException("boom");
            void Last(in LabEvent fact) => after++;

            // The bus wraps each subscriber and logs the fault via Debug.LogException - tell the test
            // runner to expect it so the logged exception does not fail the test.
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));

            using (bus.Subscribe(First))
            using (bus.Subscribe(Thrower))
            using (bus.Subscribe(Last))
            {
                var fact = SampleEvent();
                bus.Publish(in fact);
            }

            Assert.AreEqual(1, before, "subscriber before the thrower must still fire");
            Assert.AreEqual(1, after, "subscriber after the thrower must still fire (fault isolation)");
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var bus = new LabEventBus();
            int hits = 0;

            void Handler(in LabEvent fact) => hits++;

            var sub = bus.Subscribe(Handler);
            var fact = SampleEvent();
            bus.Publish(in fact);
            Assert.AreEqual(1, hits);

            sub.Dispose();
            bus.Publish(in fact);
            Assert.AreEqual(1, hits, "a disposed subscription must not receive further publishes");
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var bus = new LabEventBus();
            void Handler(in LabEvent fact) { }
            var sub = bus.Subscribe(Handler);
            sub.Dispose();
            Assert.DoesNotThrow(() => sub.Dispose());
        }

        [Test]
        public void SubscribeDuringPublish_DoesNotCorruptDispatch_AndTakesEffectNextPublish()
        {
            var bus = new LabEventBus();
            var order = new List<string>();
            IDisposable lateSub = null;
            int lateHits = 0;

            void Late(in LabEvent fact) => lateHits++;

            // This subscriber subscribes a NEW handler mid-dispatch. The reused dispatch snapshot must
            // keep the in-flight loop stable; the late handler only joins from the next publish.
            void Joiner(in LabEvent fact)
            {
                order.Add("joiner");
                if (lateSub == null)
                    lateSub = bus.Subscribe(Late);
            }

            using (bus.Subscribe(Joiner))
            {
                var fact = SampleEvent();

                bus.Publish(in fact); // joiner fires, late subscribes mid-flight
                Assert.AreEqual(0, lateHits, "a subscriber added mid-publish must not fire in that same publish");

                bus.Publish(in fact); // now the late subscriber participates
                Assert.AreEqual(1, lateHits, "the mid-publish subscriber fires from the next publish on");
            }

            lateSub?.Dispose();
            Assert.AreEqual(2, order.Count, "joiner fired on both publishes");
        }

        [Test]
        public void Subscribe_NullHandler_Throws()
        {
            var bus = new LabEventBus();
            Assert.Throws<ArgumentNullException>(() => bus.Subscribe(null));
        }
    }
}
