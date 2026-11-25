using System.Reflection;

namespace MechCore.Core
{
    public static class AppInfo
    {
        public static string Name => "Flexterion";

        // Читает версию из <Version> в .csproj в формате "v1.0"
        public static string Version
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version != null ? $"v{version.Major}.{version.Minor}" : "v1.0";
            }
        }

        // Автоматически формирует "© 2026 Flexterion", "© 2027 Flexterion" и т.д.
        public static string Copyright => $"© {DateTime.Now.Year} {Name}";

    }
}
