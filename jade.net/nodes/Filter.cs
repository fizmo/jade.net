namespace jade.net.nodes
{
    internal class Filter : Node
    {
        private string _name;
        private Block _block;
        private object _attrs;

        internal Filter(string name, Block block, object attrs)
        {
            _name = name;
            _block = block;
            _attrs = attrs;
        }
    }
}