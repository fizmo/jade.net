namespace jade.net.nodes
{
    internal class Case : Node
    {
        private object _expr;
        private Block _block;

        internal Case(object expr)
        {
            _expr = expr;
        }

        internal Case(object expr, Block block)
        {
            _expr = expr;
            _block = block;
        }

        internal class When : Node
        {
            private object _expr;
            private Block _block;
            private bool _debug;

            internal When(object expr, Block block)
            {
                _expr = expr;
                _block = block;
                _debug = false;
            }
        }
    }
}