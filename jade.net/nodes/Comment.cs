namespace jade.net.nodes
{
    internal class Comment : Node
    {
        private object _value;
        private bool _buffer;

        internal Comment(object value, bool buffer)
        {
            _value = value;
            _buffer = buffer;
        }
    }
}