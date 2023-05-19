using System.Diagnostics;
using System.Reflection;

namespace NetAmermaid
{
    internal static class AssemblyInfo
    {
        internal static readonly string Location;
        internal static readonly string? Version;

        static AssemblyInfo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Location = assembly.Location;
            var version = assembly.GetName().Version?.ToString();
            Version = version == null ? null : version.Remove(version.LastIndexOf('.'));
        }

        internal static string? GetProductVersion()
        {
            try { return FileVersionInfo.GetVersionInfo(Location).ProductVersion ?? Version; }
            catch { return Version; }
        }
    }
}