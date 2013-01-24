using System.Linq;
using jade.net.utils;

namespace jade.net.nodes
{
    internal class Tag : Attributes
    {
        internal bool Buffer { get; set; }

        internal Tag(string name, Block block = null)
        {            
            Name = name;
            Block = block ?? new Block();
        }

        internal override Node Clone()
        {
            return new Tag(Name, (Block) Block.Clone()) {Line = Line, Attrs = Attrs, TextOnly = TextOnly};
        }

        internal bool IsInline
        {
            get { return InlineTags.Tags.Contains(Name); }
        }

        internal bool CanInline()
        {
            var nodes = Block.Nodes;

            var isInline = YCombinator.Y<Node, bool>(self =>
                node =>
                {
                    var block = node as Block;
                    if (block != null) return block.Nodes.All(self);
                    var text = node as Text;
                    var tag = node as Tag;
                    return text != null || (tag != null && tag.IsInline);
                });

            // Empty tag
            if (!nodes.Any()) return true;

            // Text-only or inline-only tag
            if (1 == nodes.Count) return isInline(nodes.First());

            // Multi-line inline-only tag
            if (Block.Nodes.All(isInline))
            {
                for (int i = 1, len = nodes.Count; i < len; ++i)
                {
                    if (nodes[i - 1] as Text != null && nodes[i] as Text != null)
                        return false;
                }
                return true;
            }

            // Mixed tag
            return false;
        }
    }
}