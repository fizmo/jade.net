using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace jade.net.nodes
{
    internal class Block : Node
    {
        public List<Node> Nodes { get; internal set; }

        internal Block()
        {
            Nodes = new List<Node>();
        }

        internal Block(Node node)
            : this()
        {
            Debug.Assert(node != null);
            Nodes.Push(node);
        }

        /// <summary>
        /// Block flag.
        /// </summary>
        private bool IsBlock
        {
            get { return true; }
        }

        /// <summary>
        /// Replace the nodes in `other` with the nodes
        /// in `this` block.
        /// </summary>
        /// <param name="other"></param>
        private void Replace(Block other)
        {
            other.Nodes = Nodes;
        }

        /// <summary>
        /// Pust the given `node`
        /// </summary>
        /// <param name="node"></param>
        internal void Push(Node node)
        {
            Nodes.Push(node);
        }

        /// <summary>
        /// Check if this block is empty.
        /// </summary>
        internal bool IsEmpty
        {
            get { return !Nodes.Any(); }
        }

        /// <summary>
        /// Unshift the given `node`.
        /// </summary>
        /// <param name="node"></param>
        internal void Unshift(Node node)
        {
            Nodes.Unshift(node);
        }

        internal Func<Block> IncludeBlock
        {
            get
            {
                return () =>
                           {
                               var ret = this;
                               foreach (var node in Nodes.OfType<Block>())
                               {
                                   if (node.Yield) return node;
                                   if (node.TextOnly) continue;
                                   if (node.IncludeBlock != null) ret = node.IncludeBlock();
                                   else if (node.Block != null && !node.Block.IsEmpty)
                                       ret = node.Block.IncludeBlock();
                                   if (ret.Yield) return ret;
                               }
                               return ret;
                           };
            }
        }

        internal override Node Clone()
        {
            var clone = new Block();
            clone.Nodes.AddRange(from node in Nodes select node.Clone());
            return clone;
        }

        internal string Filename { get; set; }

        internal string Mode { get; set; }

        internal bool TextOnly { get; set; }

        internal bool Yield { get; set; }
    }
}