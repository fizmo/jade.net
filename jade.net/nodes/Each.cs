namespace jade.net.nodes
{
    internal class Each : Node
    {
        private string _code;
        private object _value;
        private string _key;

        public Each(string code, object value, string key)
        {
            _code = code;
            _value = value;
            _key = key;
        }

        internal Block Alternative { get; set; }
    }
}