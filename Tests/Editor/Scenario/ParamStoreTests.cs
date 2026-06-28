using System.Collections.Generic;
using NUnit.Framework;
using Pitech.XR.Core;

namespace Pitech.XR.Scenario.Editor.Tests
{
    /// <summary>
    /// Coverage for <see cref="LocalParamStore"/> (map sec-8 Local impl): declaration seeds defaults,
    /// typed Set/TryGet round-trips, relative <see cref="ParamOp"/> math (with Divide-by-zero as a
    /// no-op), range clamping on write, the enum bridge, and the change-only
    /// <see cref="IParamStore.ParamChanged"/> semantics (fires on a real change, suppressed on a
    /// no-op write). Pure in-memory dictionaries - no scene/asset deps.
    /// </summary>
    public class ParamStoreTests
    {
        enum Color
        {
            Red = 0,
            Green = 1,
            Blue = 2
        }

        static ConsoleParameter Decl(string id, ParamType type, float defaultNumber = 0f,
            float min = 0f, float max = 0f, string defaultText = "")
            => new ConsoleParameter
            {
                id = id,
                type = type,
                defaultNumber = defaultNumber,
                defaultText = defaultText,
                min = min,
                max = max
            };

        [Test]
        public void Declare_SeedsDefault_ReturnedByTypedGetters()
        {
            var store = new LocalParamStore();
            store.Declare(Decl("hp", ParamType.Float, defaultNumber: 12.5f));
            store.Declare(Decl("count", ParamType.Int, defaultNumber: 3f));
            store.Declare(Decl("on", ParamType.Bool, defaultNumber: 1f));
            store.Declare(Decl("name", ParamType.String, defaultText: "vicky"));

            Assert.AreEqual(12.5f, store.GetFloat("hp"), 0.0001f);
            Assert.AreEqual(3, store.GetInt("count"));
            Assert.IsTrue(store.GetBool("on"));
            Assert.AreEqual("vicky", store.GetString("name"));
        }

        [Test]
        public void Getters_UndeclaredId_ReturnFallback()
        {
            var store = new LocalParamStore();
            Assert.AreEqual(99, store.GetInt("missing", 99));
            Assert.AreEqual("fb", store.GetString("missing", "fb"));
            Assert.IsTrue(store.GetBool("missing", true));
            Assert.AreEqual(1.5f, store.GetFloat("missing", 1.5f), 0.0001f);
        }

        [Test]
        public void SetGet_Bool_RoundTrips()
        {
            var store = new LocalParamStore();
            store.SetBool("flag", true);
            Assert.IsTrue(store.TryGet("flag", out var v));
            Assert.AreEqual(ParamType.Bool, v.Type);
            Assert.IsTrue(store.GetBool("flag"));
        }

        [Test]
        public void SetGet_Int_RoundTrips()
        {
            var store = new LocalParamStore();
            store.SetInt("n", 7);
            Assert.IsTrue(store.TryGet("n", out var v));
            Assert.AreEqual(ParamType.Int, v.Type);
            Assert.AreEqual(7, store.GetInt("n"));
        }

        [Test]
        public void SetGet_Float_RoundTrips()
        {
            var store = new LocalParamStore();
            store.SetFloat("f", 2.25f);
            Assert.IsTrue(store.TryGet("f", out var v));
            Assert.AreEqual(ParamType.Float, v.Type);
            Assert.AreEqual(2.25f, store.GetFloat("f"), 0.0001f);
        }

        [Test]
        public void SetGet_String_RoundTrips()
        {
            var store = new LocalParamStore();
            store.SetString("s", "abc");
            Assert.IsTrue(store.TryGet("s", out var v));
            Assert.AreEqual(ParamType.String, v.Type);
            Assert.AreEqual("abc", store.GetString("s"));
        }

        [Test]
        public void SetGet_Enum_RoundTrips()
        {
            var store = new LocalParamStore();
            store.SetEnum("col", Color.Blue);
            Assert.IsTrue(store.TryGet("col", out var v));
            Assert.AreEqual(ParamType.Enum, v.Type);
            Assert.AreEqual(Color.Blue, store.GetEnum<Color>("col"));
        }

        [Test]
        public void GetEnum_UndeclaredId_ReturnsFallback()
        {
            var store = new LocalParamStore();
            Assert.AreEqual(Color.Green, store.GetEnum("missing", Color.Green));
        }

        [Test]
        public void TryGet_UnsetId_ReturnsFalse()
        {
            var store = new LocalParamStore();
            Assert.IsFalse(store.TryGet("nope", out _));
        }

        [Test]
        public void Apply_Add()
        {
            var store = new LocalParamStore();
            store.SetFloat("v", 10f);
            store.Apply("v", ParamOp.Add, 5f);
            Assert.AreEqual(15f, store.GetFloat("v"), 0.0001f);
        }

        [Test]
        public void Apply_Subtract()
        {
            var store = new LocalParamStore();
            store.SetFloat("v", 10f);
            store.Apply("v", ParamOp.Subtract, 4f);
            Assert.AreEqual(6f, store.GetFloat("v"), 0.0001f);
        }

        [Test]
        public void Apply_Multiply()
        {
            var store = new LocalParamStore();
            store.SetFloat("v", 3f);
            store.Apply("v", ParamOp.Multiply, 4f);
            Assert.AreEqual(12f, store.GetFloat("v"), 0.0001f);
        }

        [Test]
        public void Apply_Divide()
        {
            var store = new LocalParamStore();
            store.SetFloat("v", 12f);
            store.Apply("v", ParamOp.Divide, 4f);
            Assert.AreEqual(3f, store.GetFloat("v"), 0.0001f);
        }

        [Test]
        public void Apply_Set()
        {
            var store = new LocalParamStore();
            store.SetFloat("v", 12f);
            store.Apply("v", ParamOp.Set, 99f);
            Assert.AreEqual(99f, store.GetFloat("v"), 0.0001f);
        }

        [Test]
        public void Apply_DivideByZero_LeavesValueUnchanged()
        {
            var store = new LocalParamStore();
            store.SetFloat("v", 8f);
            store.Apply("v", ParamOp.Divide, 0f);
            Assert.AreEqual(8f, store.GetFloat("v"), 0.0001f);
        }

        [Test]
        public void Apply_ClampsToDeclaredRange_AboveMax()
        {
            var store = new LocalParamStore();
            store.Declare(Decl("hp", ParamType.Float, defaultNumber: 5f, min: 0f, max: 10f));
            // Push above max via a relative op; Set clamps on write to the declaration range.
            store.Apply("hp", ParamOp.Add, 100f);
            Assert.AreEqual(10f, store.GetFloat("hp"), 0.0001f);
        }

        [Test]
        public void Apply_ClampsToDeclaredRange_BelowMin()
        {
            var store = new LocalParamStore();
            store.Declare(Decl("hp", ParamType.Float, defaultNumber: 5f, min: 0f, max: 10f));
            store.Apply("hp", ParamOp.Subtract, 100f);
            Assert.AreEqual(0f, store.GetFloat("hp"), 0.0001f);
        }

        [Test]
        public void Set_ClampsToDeclaredRange()
        {
            var store = new LocalParamStore();
            store.Declare(Decl("hp", ParamType.Int, defaultNumber: 5f, min: 1f, max: 9f));
            store.SetInt("hp", 1000);
            Assert.AreEqual(9, store.GetInt("hp"));
        }

        [Test]
        public void ParamChanged_FiresExactlyOnce_OnRealChange()
        {
            var store = new LocalParamStore();
            store.Declare(Decl("hp", ParamType.Int, defaultNumber: 0f));

            var fired = new List<string>();
            store.ParamChanged += id => fired.Add(id);

            store.SetInt("hp", 5); // 0 -> 5 is a real change

            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual("hp", fired[0]);
        }

        [Test]
        public void ParamChanged_DoesNotFire_OnNoOpWrite()
        {
            var store = new LocalParamStore();
            store.Declare(Decl("hp", ParamType.Int, defaultNumber: 0f));

            var fired = new List<string>();
            store.ParamChanged += id => fired.Add(id);

            store.SetInt("hp", 5); // real change -> 1 event
            store.SetInt("hp", 5); // identical write -> suppressed

            Assert.AreEqual(1, fired.Count, "an unchanged write must not fan out");
        }
    }
}
