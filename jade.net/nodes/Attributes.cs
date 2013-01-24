using System.Collections.Generic;
using jade.net.utils;

namespace jade.net.nodes
{
    internal class Attributes : Node
    {
        internal IDictionary<string, string> Attrs { get; set; }
        internal bool SelfClosing { get; set; }
        internal Code Code { get; set; }
        internal bool TextOnly { get; set; }
        internal string Name { get; set; }

        internal Attributes()
        {
            Attrs = new Dictionary<string, string>();
        }

        internal Attributes SetAttribute(string name, string val, bool escaped = false)
        {
            Attrs[name] = val;
            return this;
        }

        internal void RemoveAttribute(string name)
        {
            Attrs.Remove(name);
        }

        internal string GetAttribute(string name)
        {
            return Attrs.GetValueOrDefault(name, (string) null);
        }
    }
}