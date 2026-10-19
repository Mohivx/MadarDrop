// Test-only legacy application identity; never shipped in an application payload.
using System.Reflection;
[assembly: AssemblyVersion("0.1.0.0")]
internal static class LegacyFixture { public static void Main() { } }
