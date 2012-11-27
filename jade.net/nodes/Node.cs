using System;

namespace jade.net.nodes
{
    internal class Node
    {
        internal virtual Node Clone()
        {
            return this;
        }

        internal virtual bool Yield
        {
            get { return false; }
            set { throw new NotImplementedException(); }
        }

        internal virtual bool TextOnly
        {
            get { return false; }
        }

        internal virtual Func<Node> IncludeBlock { get { return null; } }

        internal int Line { get; set; }

        internal Block Block { get; set; }
    }
}