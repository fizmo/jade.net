using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using jade.net.nodes;

namespace jade.net
{
    internal class Parser
    {
        private string _input;
        private readonly Lexer _lexer;
        private readonly string _filename;
        private IDictionary<string, Block> _blocks;
        private IDictionary<string, Node> _mixins;
        private readonly IDictionary<string, object> _options;
        private List<Parser> _contexts;
        private int? _spaces;

        private Parser Extending { get; set; }       

        internal static HashSet<string> TextOnly { get; private set; }

        static Parser()
        {
            TextOnly = new HashSet<string>{"script", "style"};
        }

        public Parser(string str, string filename, IDictionary<string, object> options)
        {
            _input = str;
            _lexer = new Lexer(str, options);
            _filename = filename;
            _blocks = new Dictionary<string, Block>();
            _mixins = new Dictionary<string, Node>();
            _options = options;
            _contexts = new List<Parser> {this};
        }

        /// <summary>
        /// Push `parser` onto the context stack. 
        /// </summary>
        /// <param name="parser"></param>
        private void Context(Parser parser)
        {
            Debug.Assert(parser != null);
            _contexts.Push(parser);
        }


        /// <summary>
        /// Pop and return a parser.
        /// </summary>
        /// <returns></returns>
        private Parser Context()
        {
            return _contexts.Pop();
        }

        /// <summary>
        /// Return the next token object.
        /// </summary>
        /// <returns></returns>
        private Lexer.Token Advance()
        {
            return _lexer.Advance();
        }

        /// <summary>
        /// Skip `n` tokens.
        /// </summary>
        /// <param name="n"></param>
        private void Skip(int n)
        {
            while (n-- > 0) Advance();
        }

        /// <summary>
        /// Single token lookahead.
        /// </summary>
        /// <returns></returns>
        private Lexer.Token Peek()
        {
            return _lexer.Lookahead(1);
        }

        /// <summary>
        /// Return lexer lineNumber.
        /// </summary>
        /// <returns></returns>
        private int Line
        {
            get { return _lexer.LineNumber; }
        }

        /// <summary>
        /// `n` token lookahead.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        private Lexer.Token Lookahead(int n)
        {
            return _lexer.Lookahead(n);
        }

        /// <summary>
        /// Parse input returning a string of C# for evaluation.
        /// </summary>
        private Block Parse()
        {
            var block = new Block();
            Parser parser;
            block.Line = Line;

            while ("eos" != Peek().Type)
            {
                if ("newline" == Peek().Type)
                {
                    Advance();
                }
                else
                {
                    block.Push(ParseExpr());
                }
            }

            if ((parser = Extending) != null)
            {
                Context(parser);
                var ast = parser.Parse();
                Context();
                // hoist mixins
                foreach (var name in _mixins.Keys)
                    ast.Unshift(_mixins[name]);
                return ast;
            }

            return block;
        }

        /// <summary>
        /// Expect the given type, or throw an exception.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private Lexer.Token Expect(string type)
        {
            Debug.Assert(Peek().Type == type);
            if (Peek().Type == type)
            {
                return Advance();
            }
            throw new Exception(string.Format("expected {0}, but got {1}", type, Peek().Type));
        }

        /// <summary>
        /// Accept the given `type`.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private Lexer.Token Accept(string type)
        {
            return Peek().Type == type ? Advance() : null;
        }

        /// <summary>
        ///   tag
        /// | doctype
        /// | mixin
        /// | include
        /// | filter
        /// | comment
        /// | text
        /// | each
        /// | code
        /// | yield
        /// | id
        /// | class
        /// | interpolation
        /// </summary>
        /// <returns></returns>
        private Node ParseExpr()
        {
            switch (Peek().Type)
            {
                case "tag":
                    return ParseTag();
                case "mixin":
                    return ParseMixin();
                case "block":
                    return ParseBlock();
                case "case":
                    return ParseCase();
                case "when":
                    return ParseWhen();
                case "default":
                    return ParseDefault();
                case "extends":
                    return ParseExtends();
                case "include":
                    return ParseInclude();
                case "doctype":
                    return ParseDoctype();
                case "filter":
                    return ParseFilter();
                case "comment":
                    return ParseComment();
                case "text":
                    return ParseText();
                case "each":
                    return ParseEach();
                case "code":
                    return ParseCode();
                case "call":
                    return ParseCall();
                case "interpolation":
                    return ParseInterpolation();
                case "yield":
                    Advance();
                    return new Block {Yield = true};
                case "id":
                case "class":
                    var tok = Advance();
                    _lexer.Defer(_lexer.Tok("tag", "div"));
                    _lexer.Defer(tok);
                    return ParseExpr();
                default:
                    throw new Exception(string.Format("Unexpected token \"{0}\"", Peek().Type));
            }
        }

        /// <summary>
        /// Text.
        /// </summary>
        /// <returns></returns>
        private Text ParseText()
        {
            var tok = Expect("text");
            var node = new Text(tok.Value.ToString()) {Line = Line};
            return node;
        }

        /// <summary>
        ///   ':' expr
        /// | block
        /// </summary>
        /// <returns></returns>
        private Block ParseBlockExpansion()
        {
            if (":" == Peek().Type)
            {
                Advance();
                return new Block(ParseExpr());
            }
            return Block();
        }

        /// <summary>
        /// case
        /// </summary>
        /// <returns></returns>
        private Case ParseCase()
        {
            var val = Expect("case").Value;
            var node = new Case(val) {Line = Line, Block = Block()};
            return node;
        }

        /// <summary>
        /// when
        /// </summary>
        /// <returns></returns>
        private Case.When ParseWhen()
        {
            var val = Expect("when").Value;
            return new Case.When(val, ParseBlockExpansion());
        }

        /// <summary>
        /// default
        /// </summary>
        /// <returns></returns>
        private Case.When ParseDefault()
        {
            Expect("default");
            return new Case.When("default", ParseBlockExpansion());
        }

        /// <summary>
        /// code
        /// </summary>
        /// <returns></returns>
        private Code ParseCode()
        {
            var tok = Expect("code");
            var node = new Code(tok.Value, tok.Buffer, tok.Escape) {Line = Line};
            var i = 1;
            while (Lookahead(i) != null && "newline" == Lookahead(i).Type) ++i;
            var block = "indent" == Lookahead(i).Type;
            if (block)
            {
                Skip(i - 1);
                node.Block = Block();
            }
            return node;
        }

        /// <summary>
        /// comment
        /// </summary>
        /// <returns></returns>
        private Node ParseComment()
        {
            var tok = Expect("comment");
            Node node;

            if ("indent" == Peek().Type)
            {
                node = new BlockComment(tok.Value, Block(), tok.Buffer);
            }
            else
            {
                node = new Comment(tok.Value, tok.Buffer);
            }

            node.Line = Line;
            return node;
        }

        /// <summary>
        /// doctype
        /// </summary>
        /// <returns></returns>
        private Doctype ParseDoctype()
        {
            var tok = Expect("Doctype");
            var node = new Doctype(tok.Value) {Line = Line};
            return node;
        }

        /// <summary>
        /// filter attrs? text-block
        /// </summary>
        /// <returns></returns>
        private Filter ParseFilter()
        {
            var tok = Expect("filter");
            var attrs = Accept("attrs");

            _lexer.Pipeless = true;
            var block = ParseTextBlock();
            _lexer.Pipeless = false;

            var node = new Filter((string) tok.Value, block, attrs != null ? attrs.Attrs : null) {Line = Line};
            return node;
        }

        /// <summary>
        /// each block
        /// </summary>
        /// <returns></returns>
        private Each ParseEach()
        {
            var tok = Expect("each");
            var node = new Each(tok.Code, tok.Value, tok.Key) {Line = Line, Block = Block()};
            if (Peek().Type == "code" && (string) Peek().Value == "else")
            {
                Advance();
                node.Alternative = Block();
            }
            return node;
        }

        /// <summary>
        /// 'extends' name
        /// </summary>
        /// <returns></returns>
        private Node ParseExtends()
        {
            if (_filename == null)
                throw new Exception("the \"filename\" option is required to extend templates");

            var path = ((string) Expect("extends").Value).Trim();
            var dir = Path.GetDirectoryName(_filename);

            path = Path.Combine(dir, path + ".jade");
            var str = File.ReadAllText(path, Encoding.UTF8);
            var parser = new Parser(str, path, _options) {_blocks = _blocks, _contexts = _contexts};

            Extending = parser;

            // TODO: null node
            return new Literal("");
        }


        /// <summary>
        /// 'block' name block
        /// </summary>
        /// <returns></returns>
        private Block ParseBlock()
        {
            var blockTok = Expect("block");
            var mode = blockTok.Mode;
            var name = ((string) blockTok.Value).Trim();

            var block = "indent" == Peek().Type ? Block() : new Block(new Literal(""));

            var prev = _blocks[name];

            if (prev != null)
            {
                switch (prev.Mode)
                {
                    case "append":
                        block.Nodes = block.Nodes.Concat(prev.Nodes).ToList();
                        prev = block;
                        break;
                    case "prepend":
                        block.Nodes = prev.Nodes.Concat(block.Nodes).ToList();
                        prev = block;
                        break;
                }
            }

            block.Mode = mode;
            return _blocks[name] = prev ?? block;
        }

        /// <summary>
        /// include block?
        /// </summary>
        /// <returns></returns>
        private Node ParseInclude()
        {
            var path = ((string) Expect("include").Value).Trim();
            var dir = Path.GetDirectoryName(path) ?? "";

            if (_filename == null)
                throw new Exception("the \"filename\" option is required to use includes");

            // no extension
            if (Path.GetExtension(path) == null)
            {
                path += ".jade";
            }

            path = Path.Combine(dir, path);
            var str = File.ReadAllText(path, Encoding.UTF8);

            // non-jade
            if (".jade" != path.Substring(path.Length-6))
            {
                return new Literal(str);
            }

            var parser = new Parser(str, path, _options)
                             {_blocks = new Dictionary<string, Block>(_blocks), _mixins = _mixins};

            Context(parser);
            var ast = parser.Parse();
            Context();
            ast.Filename = path;

            if ("indent" == Peek().Type)
            {
                ast.IncludeBlock().Push(Block());
            }

            return ast;
        }

        /// <summary>
        /// call ident block
        /// </summary>
        /// <returns></returns>
        private Mixin ParseCall()
        {
            var tok = Expect("call");
            var name = tok.Value.ToString();
            var args = tok.Args;
            var mixin = new Mixin(name, args, new Block(), true);

            Tag(mixin);
            if (mixin.Block.IsEmpty) mixin.Block = null;
            return mixin;
        }

        /// <summary>
        /// mixin block
        /// </summary>
        /// <returns></returns>
        private Mixin ParseMixin()
        {
            var tok = Expect("mixin");
            var name = tok.Value.ToString();
            var args = tok.Args;

            if ("indent" == Peek().Type)
            {
                var mixin = new Mixin(name, args, Block(), false);
                _mixins[name] = mixin;
                return mixin;
            }
            return new Mixin(name, args, null, true);
        }

        /// <summary>
        /// indent (text | newline)* outdent
        /// </summary>
        /// <returns></returns>
        private Block ParseTextBlock()
        {
            var block = new Block {Line = Line};
            var spaces = (int) Expect("indent").Value;
            if (null == _spaces) _spaces = spaces;
            var indent = new string(' ', spaces - _spaces.Value + 1);
            while ("outdent" != Peek().Type)
            {
                switch (Peek().Type)
                {
                    case "newline":
                        Advance();
                        break;
                    case "indent":
                        ParseTextBlock().Nodes.ForEach(block.Push);
                        break;
                    default:
                        var text = new Text(indent + Advance().Value) {Line = Line};
                        block.Push(text);
                        break;
                }   
            }

            if (spaces == _spaces.Value) _spaces = null;
            Expect("outdent");
            return block;
        }

        /// <summary>
        /// indent expr* outdent
        /// </summary>
        /// <returns></returns>
        private Block Block()
        {
            var block = new Block {Line = Line};
            Expect("indent");
            while ("outdent" != Peek().Type)
            {
                if ("newline" == Peek().Type)
                {
                    Advance();
                }
                else
                {
                    block.Push(ParseExpr());
                }
            }
            Expect("outdent");
            return block;
        }

        /// <summary>
        /// interpolation (attrs | class | id)* (text | code | ':')? newline* block?
        /// </summary>
        /// <returns></returns>
        private Tag ParseInterpolation()
        {
            var tok = Advance();
            var tag = new Tag(tok.Value.ToString()) {Buffer = true};
            return Tag(tag);
        }

        /// <summary>
        /// tag (attrs | class | id)* (text | code | ':')? newline* block?
        /// </summary>
        /// <returns></returns>
        private Tag ParseTag()
        {
            // ast-filter look-ahead
            var i = 2;
            if ("attrs" == Lookahead(i).Type) ++i;

            var tok = Advance();
            var tag = new Tag(tok.Value.ToString()) {SelfClosing = tok.SelfClosing};

            return Tag(tag);
        }

        private static readonly Regex TypeRegex = new Regex("^['\"]|['\"]$");

        /// <summary>
        /// parse tag
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        /// <returns></returns>
        private T Tag<T>(T tag) where T : Attributes
        {
            var dot = false;

            tag.Line = Line;

            // (attrs | class | id)*
            var done = false;
            while (!done)
            {
                switch (Peek().Type)
                {
                    case "id":
                    case "class":
                        {
                            var tok = Advance();
                            tag.SetAttribute(tok.Type, "'" + tok.Value + "'");
                            continue;
                        }
                    case "attrs":
                        {
                            var tok = Advance();
                            var obj = tok.Attrs;
                            var escaped = tok.Escaped;
                            var names = obj.Keys;

                            if (tok.SelfClosing) tag.SelfClosing = true;

                            foreach (var name in names)
                            {
                                var val = obj[name];
                                tag.SetAttribute(name, val.ToString(), escaped[name]);
                            }
                            continue;
                        }
                    default:
                        done = true;
                        break;
                }
            }

            // check immediate '.'
            if ("." == (string) Peek().Value)
            {
                dot = tag.TextOnly = true;
                Advance();
            }

            // (text | code | ':')?
            switch (Peek().Type)
            {
                case "text":
                    tag.Block.Push(ParseText());
                    break;
                case "code":
                    tag.Code = ParseCode();
                    break;
                case ":":
                    Advance();
                    tag.Block = new Block();
                    tag.Block.Push(ParseExpr());
                    break;
            }

            // newline*
            while ("newline" == Peek().Type) Advance();

            tag.TextOnly = tag.TextOnly || TextOnly.Contains(tag.Name);

            // script special case
            if ("script" == tag.Name)
            {
                var type = tag.GetAttribute("type");
                if (!dot && type != null && "text/javascript" == TypeRegex.Replace(type, ""))
                {
                    tag.TextOnly = false;
                }
            }

            // block?
            if ("indent" == Peek().Type)
            {
                if (tag.TextOnly)
                {
                    _lexer.Pipeless = true;
                    tag.Block = ParseTextBlock();
                    _lexer.Pipeless = false;
                }
                else
                {
                    var block = Block();
                    if (tag.Block != null)
                    {
                        foreach (var node in block.Nodes)
                        {
                            tag.Block.Push(node);
                        }
                    }
                    else
                    {
                        tag.Block = block;
                    }
                }                
            }

            return tag;
        }
    }
}