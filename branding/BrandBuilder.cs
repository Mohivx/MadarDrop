using System;
internal static class BrandBuilder
{
    [STAThread] public static int Main(string[] args) { TangerineWindows.Brand.WriteAssets(args[0]); return 0; }
}
