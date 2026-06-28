using NUnit.Framework;
using Pitech.XR.Core;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Pure-value coverage for <see cref="ParamValue"/> (map sec-8 union): each factory tags
    /// <see cref="ParamValue.Type"/> correctly and the matching accessor round-trips the original
    /// payload. No scene/asset deps - the union is an immutable struct.
    /// </summary>
    public class ParamValueTests
    {
        [Test]
        public void Bool_RoundTrips_AndTagsBool()
        {
            var t = ParamValue.Bool(true);
            Assert.AreEqual(ParamType.Bool, t.Type);
            Assert.IsTrue(t.AsBool());

            var f = ParamValue.Bool(false);
            Assert.AreEqual(ParamType.Bool, f.Type);
            Assert.IsFalse(f.AsBool());
        }

        [Test]
        public void Int_RoundTrips_AndTagsInt()
        {
            var v = ParamValue.Int(42);
            Assert.AreEqual(ParamType.Int, v.Type);
            Assert.AreEqual(42, v.AsInt());
        }

        [Test]
        public void Float_RoundTrips_AndTagsFloat()
        {
            var v = ParamValue.Float(3.5f);
            Assert.AreEqual(ParamType.Float, v.Type);
            Assert.AreEqual(3.5f, v.AsFloat(), 0.0001f);
        }

        [Test]
        public void Enum_RoundTrips_AndTagsEnum()
        {
            var v = ParamValue.Enum(2);
            Assert.AreEqual(ParamType.Enum, v.Type);
            Assert.AreEqual(2, v.AsInt());
        }

        [Test]
        public void Str_RoundTrips_AndTagsString()
        {
            var v = ParamValue.Str("hello");
            Assert.AreEqual(ParamType.String, v.Type);
            Assert.AreEqual("hello", v.AsString());
        }

        [Test]
        public void Str_Null_BecomesEmptyString()
        {
            var v = ParamValue.Str(null);
            Assert.AreEqual(ParamType.String, v.Type);
            Assert.AreEqual(string.Empty, v.AsString());
        }
    }
}
