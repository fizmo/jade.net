using System;

namespace jade.net.nodes
{
    internal class Node
    {
        internal virtual Node Clone()
        {
            return this;
        }

        internal int Line { get; set; }

        internal Block Block { get; set; }
    }
}