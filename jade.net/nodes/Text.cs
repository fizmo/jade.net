namespace jade.net.nodes
{
    internal class Text : Node
    {
        internal Text(string line)
        {
            Value = line;
        }

        internal string Value { get; private set; }

        internal bool IsText { get { return true; } }

        public int Line { get; set; }
    }
}