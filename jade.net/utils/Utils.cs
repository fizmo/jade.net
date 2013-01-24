using System.Text.RegularExpressions;

namespace jade.net.utils
{
    internal static class Utils
    {
        private readonly static Regex EscapeRegex = new Regex("'");
        internal static string Escape(string str)
        {
            return EscapeRegex.Replace(str, "\\'");
        }
    }
}