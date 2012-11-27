namespace jade.net.nodes
{
    internal class BlockComment : Node
    {
        private object _value;
        private Block _block;
        private bool _buffer;

        internal BlockComment(object value, Block block, bool buffer)
        {
            _value = value;
            _buffer = buffer;
        }
    }
}