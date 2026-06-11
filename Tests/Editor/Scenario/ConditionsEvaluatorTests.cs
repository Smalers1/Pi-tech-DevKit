using NUnit.Framework;
using Pitech.XR.Scenario;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Proof-of-behaviour lock for <see cref="ConditionsEvaluator.EvalCompare"/> on UNMODIFIED code
    /// (WS A3 Step 2). This is the live comparison used by every ConditionsStep outcome and every
    /// GroupStep MultiCondition branch (SceneManager.cs:1042 / :2226). The runner unification deferred
    /// to Phase D must reproduce this table byte-for-byte; these cases are the contract it inherits.
    /// </summary>
    public class ConditionsEvaluatorTests
    {
        // --- Ordered comparisons -------------------------------------------------------------

        [TestCase(1f, 2f, true)]
        [TestCase(2f, 2f, false)]
        [TestCase(3f, 2f, false)]
        public void Less(float value, float compare, bool expected)
            => Assert.AreEqual(expected, ConditionsEvaluator.EvalCompare(value, CompareOp.Less, compare));

        [TestCase(1f, 2f, true)]
        [TestCase(2f, 2f, true)]
        [TestCase(3f, 2f, false)]
        public void LessOrEqual(float value, float compare, bool expected)
            => Assert.AreEqual(expected, ConditionsEvaluator.EvalCompare(value, CompareOp.LessOrEqual, compare));

        [TestCase(3f, 2f, true)]
        [TestCase(2f, 2f, false)]
        [TestCase(1f, 2f, false)]
        public void Greater(float value, float compare, bool expected)
            => Assert.AreEqual(expected, ConditionsEvaluator.EvalCompare(value, CompareOp.Greater, compare));

        [TestCase(3f, 2f, true)]
        [TestCase(2f, 2f, true)]
        [TestCase(1f, 2f, false)]
        public void GreaterOrEqual(float value, float compare, bool expected)
            => Assert.AreEqual(expected, ConditionsEvaluator.EvalCompare(value, CompareOp.GreaterOrEqual, compare));

        // --- Equality (Mathf.Approximately, NOT operator ==) ---------------------------------

        [Test]
        public void Equal_TreatsApproximatelyEqualAsEqual()
        {
            Assert.IsTrue(ConditionsEvaluator.EvalCompare(1f, CompareOp.Equal, 1f));
            // Within Mathf.Approximately tolerance.
            Assert.IsTrue(ConditionsEvaluator.EvalCompare(1f, CompareOp.Equal, 1f + 1e-7f));
        }

        [Test]
        public void Equal_RejectsClearlyDifferentValues()
            => Assert.IsFalse(ConditionsEvaluator.EvalCompare(1f, CompareOp.Equal, 1.1f));

        [Test]
        public void NotEqual_IsTheExactNegationOfEqual()
        {
            Assert.IsFalse(ConditionsEvaluator.EvalCompare(1f, CompareOp.NotEqual, 1f));
            Assert.IsTrue(ConditionsEvaluator.EvalCompare(1f, CompareOp.NotEqual, 1.1f));
        }

        // --- Bool encodings (compareValue is ignored; value is compared against 0.5) ---------
        // IsTrue  => value > 0.5f ; IsFalse => value < 0.5f. Exactly 0.5 is NEITHER (both false).

        [TestCase(1f, true)]
        [TestCase(0.51f, true)]
        [TestCase(0.5f, false)]
        [TestCase(0.49f, false)]
        [TestCase(0f, false)]
        public void IsTrue(float value, bool expected)
            => Assert.AreEqual(expected, ConditionsEvaluator.EvalCompare(value, CompareOp.IsTrue, 0f));

        [TestCase(0f, true)]
        [TestCase(0.49f, true)]
        [TestCase(0.5f, false)]
        [TestCase(0.51f, false)]
        [TestCase(1f, false)]
        public void IsFalse(float value, bool expected)
            => Assert.AreEqual(expected, ConditionsEvaluator.EvalCompare(value, CompareOp.IsFalse, 0f));

        [Test]
        public void ExactlyHalf_IsNeitherTrueNorFalse()
        {
            // Documents the boundary explicitly: 0.5 satisfies neither bool encoding.
            Assert.IsFalse(ConditionsEvaluator.EvalCompare(0.5f, CompareOp.IsTrue, 0f));
            Assert.IsFalse(ConditionsEvaluator.EvalCompare(0.5f, CompareOp.IsFalse, 0f));
        }

        [Test]
        public void BoolEncodings_IgnoreCompareValue()
        {
            // compareValue must not influence IsTrue/IsFalse.
            Assert.IsTrue(ConditionsEvaluator.EvalCompare(1f, CompareOp.IsTrue, 999f));
            Assert.IsTrue(ConditionsEvaluator.EvalCompare(0f, CompareOp.IsFalse, -999f));
        }
    }
}
