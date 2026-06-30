using NUnit.Framework;
using UnityEngine;
using Pitech.XR.Core;
using Pitech.XR.Networking;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for <see cref="LocalLabStateStore"/> (map sec-10.2): the bool-view sugar over
    /// a <see cref="LocalParamStore"/> - unknown-default reads, change-only <c>StateChanged</c> fan-out,
    /// and <c>Toggle</c>. Creates a single host GameObject and destroys it in teardown.
    /// </summary>
    [TestFixture]
    public sealed class LocalLabStateStoreTests
    {
        GameObject _host;
        LocalLabStateStore _store;
        int _changedCount;
        string _lastChangedId;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("LocalLabStateStoreTestHost");
            _store = _host.AddComponent<LocalLabStateStore>();
            _store.Initialize(new LocalParamStore());
            _changedCount = 0;
            _lastChangedId = null;
            _store.StateChanged += OnStateChanged;
        }

        [TearDown]
        public void TearDown()
        {
            if (_store != null) _store.StateChanged -= OnStateChanged;
            if (_host != null) Object.DestroyImmediate(_host);
            _host = null;
            _store = null;
        }

        void OnStateChanged(string id)
        {
            _changedCount++;
            _lastChangedId = id;
        }

        [Test]
        public void GetState_UnknownId_IsFalse()
        {
            Assert.That(_store.GetState("unknown"), Is.False);
        }

        [Test]
        public void SetState_True_MakesGetStateTrue_AndFiresOnce()
        {
            _store.SetState("door", true);
            Assert.That(_store.GetState("door"), Is.True);
            Assert.That(_changedCount, Is.EqualTo(1));
            Assert.That(_lastChangedId, Is.EqualTo("door"));
        }

        [Test]
        public void SetState_SameValue_DoesNotFireAgain()
        {
            _store.SetState("door", true);
            Assert.That(_changedCount, Is.EqualTo(1));

            // Re-writing the same value is a no-op (change-only): no second fan-out.
            _store.SetState("door", true);
            Assert.That(_changedCount, Is.EqualTo(1));
            Assert.That(_store.GetState("door"), Is.True);
        }

        [Test]
        public void Toggle_FlipsTheBool()
        {
            Assert.That(_store.GetState("valve"), Is.False);

            _store.Toggle("valve");
            Assert.That(_store.GetState("valve"), Is.True);

            _store.Toggle("valve");
            Assert.That(_store.GetState("valve"), Is.False);
        }

        // --- #1 unification: the bool-view and the runner share ONE store ---

        [Test]
        public void SharedStore_StateWriteIsVisibleToTheBackingStore()
        {
            // The store LabConsole hands the runner as Params is the SAME store backing the bool-view.
            var shared = new LocalParamStore();
            _store.Initialize(shared);

            _store.SetState("WaterFlowing", true);   // a trigger (writer) sets the state

            // A ConditionsStep / effect reads via LabConsole.Params == this store. Before the fix it read a
            // DIFFERENT store and saw false; now it must see the trigger's write.
            Assert.That(shared.GetBool("WaterFlowing"), Is.True);
        }

        [Test]
        public void SharedStore_StoreWriteSurfacesViaGetState_AndForwardsStateChanged()
        {
            var shared = new LocalParamStore();
            _store.Initialize(shared);

            shared.SetBool("WaterFlowing", true);   // an effect writes the param directly

            Assert.That(_store.GetState("WaterFlowing"), Is.True);   // the bool-view reflects it
            Assert.That(_changedCount, Is.EqualTo(1));               // StateChanged forwarded the store change
            Assert.That(_lastChangedId, Is.EqualTo("WaterFlowing"));
        }
    }
}
