using System.Globalization;

namespace GHelper
{
    internal static class CrosshairText
    {
        public static string Get(string key)
        {
            return Properties.Strings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
    }
}
