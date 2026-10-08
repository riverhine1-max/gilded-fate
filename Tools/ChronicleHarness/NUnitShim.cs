using System;

// A minimal stand-in for the slice of NUnit the Chronicle tests use, so the same test source runs under Unity's Test Runner
// (real NUnit) and under this standalone harness (no NuGet, no Unity). Only compiled by the harness project.
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class SetUpAttribute : Attribute { }

    public sealed class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public static class Assert
    {
        static string M(string m) => string.IsNullOrEmpty(m) ? "" : " - " + m;
        public static void IsTrue(bool v, string m = null) { if (!v) throw new AssertionException("Expected true" + M(m)); }
        public static void IsFalse(bool v, string m = null) { if (v) throw new AssertionException("Expected false" + M(m)); }
        public static void IsNull(object o, string m = null) { if (o != null) throw new AssertionException("Expected null" + M(m)); }
        public static void IsNotNull(object o, string m = null) { if (o == null) throw new AssertionException("Expected not null" + M(m)); }
        public static void AreEqual(object expected, object actual, string m = null) { if (!Equals(expected, actual)) throw new AssertionException("Expected <" + expected + "> but was <" + actual + ">" + M(m)); }
        public static void AreNotEqual(object a, object b, string m = null) { if (Equals(a, b)) throw new AssertionException("Expected values to differ, both <" + a + ">" + M(m)); }
        public static void Greater(double a, double b, string m = null) { if (!(a > b)) throw new AssertionException(a + " is not greater than " + b + M(m)); }
        public static void GreaterOrEqual(double a, double b, string m = null) { if (!(a >= b)) throw new AssertionException(a + " is not >= " + b + M(m)); }
        public static void Less(double a, double b, string m = null) { if (!(a < b)) throw new AssertionException(a + " is not less than " + b + M(m)); }
        public static void LessOrEqual(double a, double b, string m = null) { if (!(a <= b)) throw new AssertionException(a + " is not <= " + b + M(m)); }
        public static void Fail(string m = null) { throw new AssertionException("Fail" + M(m)); }
    }
}
