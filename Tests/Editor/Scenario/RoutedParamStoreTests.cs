using NUnit.Framework;
using Pitech.XR.Core;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for the WS B2.4 Step 4 declared-param router (<see cref="RoutedParamStore"/>) - the
    /// part of B2.4 that IS verifiable headless (the Fusion data plane is not). Two
    /// <see cref="LocalParamStore"/>s stand in for the local + networked halves - the router only depends on
    /// <see cref="IParamStore"/>, so this isolates the SCOPE ROUTING + ParamChanged aggregation without
    /// Fusion: Networked-scope ids land in the networked store, Local-scope + undeclared ids stay local,
    /// neither leaks into the other, relative ops route, and Dispose detaches the forwarders.
    /// </summary>
    [TestFixture]
    public sealed class RoutedParamStoreTests
    {
        const float Eps = 1e-4f;

        static ConsoleParameter Param(string id, ParamScope scope, float def = 0f)
            => new ConsoleParameter { id = id, type = ParamType.Float, defaultNumber = def, scope = scope };

        [Test]
        public void NetworkedScopeId_RoutesToNetworkedStoreOnly()
        {
            var local = new LocalParamStore();
            var net = new LocalParamStore();
            var routed = new RoutedParamStore(local, net);

            routed.Declare(Param("hr", ParamScope.Networked));
            routed.SetFloat("hr", 42f);

            Assert.That(net.TryGet("hr", out var nv), Is.True, "Networked-scope id should live in the networked store");
            Assert.That(nv.AsFloat(), Is.EqualTo(42f).Within(Eps));
            Assert.That(local.TryGet("hr", out _), Is.False, "Networked-scope id must NOT touch the local store");
        }

        [Test]
        public void LocalScopeId_RoutesToLocalStoreOnly()
        {
            var local = new LocalParamStore();
            var net = new LocalParamStore();
            var routed = new RoutedParamStore(local, net);

            routed.Declare(Param("score", ParamScope.Local));
            routed.SetFloat("score", 7f);

            Assert.That(local.TryGet("score", out var lv), Is.True);
            Assert.That(lv.AsFloat(), Is.EqualTo(7f).Within(Eps));
            Assert.That(net.TryGet("score", out _), Is.False, "Local-scope id must NOT replicate");
        }

        [Test]
        public void UndeclaredId_FallsBackToLocal()
        {
            var local = new LocalParamStore();
            var net = new LocalParamStore();
            var routed = new RoutedParamStore(local, net);

            routed.SetFloat("adhoc", 3f);   // never declared

            Assert.That(local.TryGet("adhoc", out _), Is.True, "Undeclared ids route to the local store");
            Assert.That(net.TryGet("adhoc", out _), Is.False);
        }

        [Test]
        public void Apply_RoutesByScope()
        {
            var local = new LocalParamStore();
            var net = new LocalParamStore();
            var routed = new RoutedParamStore(local, net);
            routed.Declare(Param("hr", ParamScope.Networked, def: 10f));

            routed.Apply("hr", ParamOp.Add, 5f);

            Assert.That(net.GetFloat("hr"), Is.EqualTo(15f).Within(Eps), "relative op must apply in the networked store");
            Assert.That(local.TryGet("hr", out _), Is.False, "the local store must stay untouched");
        }

        [Test]
        public void ParamChanged_AggregatesBothStores()
        {
            var local = new LocalParamStore();
            var net = new LocalParamStore();
            var routed = new RoutedParamStore(local, net);
            routed.Declare(Param("hr", ParamScope.Networked));
            routed.Declare(Param("score", ParamScope.Local));

            var changed = new System.Collections.Generic.List<string>();
            routed.ParamChanged += changed.Add;

            routed.SetFloat("hr", 1f);      // networked half
            routed.SetFloat("score", 2f);   // local half

            Assert.That(changed, Does.Contain("hr"), "a networked change must fan out through the router");
            Assert.That(changed, Does.Contain("score"), "a local change must fan out through the router");
        }

        [Test]
        public void Dispose_DetachesForwarders()
        {
            var local = new LocalParamStore();
            var net = new LocalParamStore();
            var routed = new RoutedParamStore(local, net);
            routed.Declare(Param("score", ParamScope.Local));

            int count = 0;
            routed.ParamChanged += _ => count++;
            routed.Dispose();

            routed.SetFloat("score", 9f);   // still hits local, but the router's forwarder is detached
            Assert.That(count, Is.EqualTo(0), "after Dispose the router must not fan out store changes");
        }
    }
}
