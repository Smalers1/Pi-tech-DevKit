using NUnit.Framework;
using Pitech.XR.Core;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Coverage for <see cref="ConsoleParameter"/> (map sec-8 declaration): <see cref="ConsoleParameter.Clamp"/>
    /// clamps ONLY when a real range is declared (max &gt; min) AND the value is Float or Int, and
    /// <see cref="ConsoleParameter.DefaultValue"/> produces a <see cref="ParamValue"/> of the declared
    /// type. Plain authored data - no scene/asset deps.
    /// </summary>
    public class ConsoleParameterTests
    {
        static ConsoleParameter Param(ParamType type, float min, float max)
            => new ConsoleParameter { id = "p", type = type, min = min, max = max };

        [Test]
        public void Clamp_FloatWithRealRange_ClampsAboveMax()
        {
            var p = Param(ParamType.Float, 0f, 10f);
            var clamped = p.Clamp(ParamValue.Float(99f));
            Assert.AreEqual(ParamType.Float, clamped.Type);
            Assert.AreEqual(10f, clamped.AsFloat(), 0.0001f);
        }

        [Test]
        public void Clamp_FloatWithRealRange_ClampsBelowMin()
        {
            var p = Param(ParamType.Float, 0f, 10f);
            var clamped = p.Clamp(ParamValue.Float(-5f));
            Assert.AreEqual(0f, clamped.AsFloat(), 0.0001f);
        }

        [Test]
        public void Clamp_FloatWithRealRange_WithinRange_Unchanged()
        {
            var p = Param(ParamType.Float, 0f, 10f);
            var clamped = p.Clamp(ParamValue.Float(4f));
            Assert.AreEqual(4f, clamped.AsFloat(), 0.0001f);
        }

        [Test]
        public void Clamp_IntWithRealRange_Clamps()
        {
            var p = Param(ParamType.Int, 1f, 5f);
            var clamped = p.Clamp(ParamValue.Int(9));
            Assert.AreEqual(ParamType.Int, clamped.Type);
            Assert.AreEqual(5, clamped.AsInt());
        }

        [Test]
        public void Clamp_NoRealRange_MaxEqualsMin_DoesNotClamp()
        {
            var p = Param(ParamType.Float, 5f, 5f);
            var v = p.Clamp(ParamValue.Float(99f));
            Assert.AreEqual(99f, v.AsFloat(), 0.0001f);
        }

        [Test]
        public void Clamp_NoRealRange_MaxBelowMin_DoesNotClamp()
        {
            var p = Param(ParamType.Float, 10f, 0f);
            var v = p.Clamp(ParamValue.Float(99f));
            Assert.AreEqual(99f, v.AsFloat(), 0.0001f);
        }

        [Test]
        public void Clamp_BoolValue_NotClampedEvenWithRange()
        {
            // A real range is declared, but Bool is not a numeric-clamp type - value passes through.
            var p = Param(ParamType.Bool, 0f, 1f);
            var v = p.Clamp(new ParamValue(ParamType.Bool, 99f, null));
            Assert.AreEqual(ParamType.Bool, v.Type);
            Assert.AreEqual(99f, v.Number, 0.0001f);
        }

        [Test]
        public void Clamp_StringValue_NotClamped()
        {
            var p = Param(ParamType.String, 0f, 10f);
            var v = p.Clamp(ParamValue.Str("xyz"));
            Assert.AreEqual(ParamType.String, v.Type);
            Assert.AreEqual("xyz", v.AsString());
        }

        [Test]
        public void Clamp_EnumValue_NotClamped()
        {
            var p = Param(ParamType.Enum, 0f, 2f);
            var v = p.Clamp(new ParamValue(ParamType.Enum, 99f, null));
            Assert.AreEqual(ParamType.Enum, v.Type);
            Assert.AreEqual(99f, v.Number, 0.0001f);
        }

        [Test]
        public void DefaultValue_Float_ReturnsFloatTyped()
        {
            var p = new ConsoleParameter { id = "p", type = ParamType.Float, defaultNumber = 2.5f };
            var d = p.DefaultValue();
            Assert.AreEqual(ParamType.Float, d.Type);
            Assert.AreEqual(2.5f, d.AsFloat(), 0.0001f);
        }

        [Test]
        public void DefaultValue_Int_ReturnsIntTyped()
        {
            var p = new ConsoleParameter { id = "p", type = ParamType.Int, defaultNumber = 7f };
            var d = p.DefaultValue();
            Assert.AreEqual(ParamType.Int, d.Type);
            Assert.AreEqual(7, d.AsInt());
        }

        [Test]
        public void DefaultValue_String_ReturnsStringTyped()
        {
            var p = new ConsoleParameter { id = "p", type = ParamType.String, defaultText = "init" };
            var d = p.DefaultValue();
            Assert.AreEqual(ParamType.String, d.Type);
            Assert.AreEqual("init", d.AsString());
        }

        [Test]
        public void DefaultValue_Float_ClampedToDeclaredRange()
        {
            var p = new ConsoleParameter
            {
                id = "p",
                type = ParamType.Float,
                defaultNumber = 99f,
                min = 0f,
                max = 10f
            };
            var d = p.DefaultValue();
            Assert.AreEqual(10f, d.AsFloat(), 0.0001f);
        }
    }
}
