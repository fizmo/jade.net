namespace jade.net.nodes
{
    internal class Text : Node
    {
        internal Text(string line)
        {
            Value = line;
        }

        internal string Value { get; private set; }
    }
}