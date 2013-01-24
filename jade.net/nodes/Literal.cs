using System.Text.RegularExpressions;

namespace jade.net.nodes
{
    internal class Literal : Node
    {
        private string _str;

        private static readonly Regex EscapeRegex = new Regex(@"\\");
        private static readonly Regex NewlineRegex = new Regex(@"\n|\r\n");
        private static readonly Regex QuoteRegex = new Regex(@"'");

        internal Literal(string str)
        {
            var escaped = EscapeRegex.Replace(str, "\\\\");
            var newlined = NewlineRegex.Replace(escaped, "\\n");
            _str = QuoteRegex.Replace(newlined, "\\'");
        }
    }
}