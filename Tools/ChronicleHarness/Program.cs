using System;
using System.Linq;
using System.Reflection;

// Runs every [Test] method in every [TestFixture] found in this assembly. Exit code 0 = all passed.
static class Program
{
    static int Main(string[] args)
    {
        var filter = args.Length > 0 ? args[0] : "";
        int passed = 0, failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetCustomAttribute<NUnit.Framework.TestFixtureAttribute>() != null))
        {
            var setup = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.SetUpAttribute>() != null);
            foreach (var method in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null).OrderBy(m => m.Name))
            {
                if (filter.Length > 0 && method.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    var instance = Activator.CreateInstance(type);
                    setup?.Invoke(instance, null);
                    method.Invoke(instance, null);
                    passed++; Console.WriteLine("PASS  " + type.Name + "." + method.Name);
                }
                catch (TargetInvocationException e)
                {
                    failed++; Console.WriteLine("FAIL  " + type.Name + "." + method.Name + "\n      " + e.InnerException?.Message);
                }
            }
        }
        Console.WriteLine("\n" + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
