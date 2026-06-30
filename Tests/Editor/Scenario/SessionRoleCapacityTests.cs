using NUnit.Framework;
using UnityEngine;
using Pitech.XR.Analytics;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// EditMode coverage for the P7 local capacity guard: the per-lab role capacities (pushed from the
    /// rubric by LabAnalytics at Start) forbid a role whose max is 0 and leave others selectable. Locks the
    /// LOCAL single-peer guard (SessionRoleSelector.IsSelectable); cross-peer headcount is B2.4 (MP). Pure
    /// component logic - no Awake/Start lifecycle needed.
    /// </summary>
    [TestFixture]
    public sealed class SessionRoleCapacityTests
    {
        GameObject _go;
        SessionRoleSelector _sel;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("roleSel");
            _sel = _go.AddComponent<SessionRoleSelector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Default_AllRolesSelectable()
        {
            Assert.IsTrue(_sel.IsSelectable(SessionRole.Professor));
            Assert.IsTrue(_sel.IsSelectable(SessionRole.Participant));
            Assert.IsTrue(_sel.IsSelectable(SessionRole.Spectator));
        }

        [Test]
        public void SetCapacities_MaxZero_ForbidsOnlyThatRole()
        {
            _sel.SetCapacities(new SessionRoleCapacities { maxSpectators = 0 });
            Assert.IsFalse(_sel.IsSelectable(SessionRole.Spectator), "max 0 must forbid the role");
            Assert.IsTrue(_sel.IsSelectable(SessionRole.Participant), "other roles unaffected");
            Assert.IsTrue(_sel.IsSelectable(SessionRole.Professor), "other roles unaffected");
        }
    }
}
