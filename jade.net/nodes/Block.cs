using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace jade.net.nodes
{
    internal class Block : Node
    {
        private List<Node> _nodes;

        internal Block()
        {
            _nodes = new List<Node>();
        }

        internal Block(Node node)
            : this()
        {
            Debug.Assert(node != null);
            _nodes.Push(node);
        }

        internal override bool Yield { get; set; }

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
            other._nodes = _nodes;
        }

        /// <summary>
        /// Pust the given `node`
        /// </summary>
        /// <param name="node"></param>
        internal void Push(Node node)
        {
            _nodes.Push(node);
        }

        /// <summary>
        /// Check if this block is empty.
        /// </summary>
        private bool IsEmpty
        {
            get { return 0 == _nodes.Count; }
        }

        /// <summary>
        /// Unshift the given `node`.
        /// </summary>
        /// <param name="node"></param>
        internal void Unshift(Node node)
        {
            _nodes.Unshift(node);
        }

        internal override Func<Node> IncludeBlock
        {
            get
            {
                return () =>
                           {
                               Node ret = this;
                               foreach (var node in _nodes)
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
            clone._nodes.AddRange(from node in _nodes select node.Clone());
            return clone;
        }
    }
}