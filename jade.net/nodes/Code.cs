namespace jade.net.nodes
{
    internal class Code : Node
    {
        private object _value;
        private bool _buffer;
        private bool _escape;

        internal Code(object value, bool buffer, bool escape)
        {
            _value = value;
            _buffer = buffer;
            _escape = escape;
        }
    }
}