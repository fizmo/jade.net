using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using jade.net.nodes;

namespace jade.net
{
    internal class Parser
    {
        private string _input;
        private readonly Lexer _lexer;
        private string _filename;
        private IDictionary<string, object> _blocks;
        private readonly IDictionary<string, Node> _mixins;
        private IDictionary<string, object> _options;
        private readonly List<Parser> _contexts;

        private Parser Extending { get; set; }       

        public static List<string> TextOnly { get; private set; }

        static Parser()
        {
            TextOnly = new List<string>{"script", "style"};
        }

        public Parser(string str, string filename, IDictionary<string, object> options)
        {
            _input = str;
            _lexer = new Lexer(str, options);
            _filename = filename;
            _blocks = new Dictionary<string, object>();
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
                case "case":
                    return ParseCase();
                case "when":
                    return ParseWhen();
                case "default":
                    return ParseDefault();
                case "doctype":
                    return ParseDoctype();
                case "text":
                    return ParseText();
                case "code":
                    return ParseCode();
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




        private void ParseExtends()
        {
            if (_filename == null)
                throw new Exception("the \"filename\" option is required to extend templates");

            var path = ((string) Expect("extends").Value).Trim();
            var dir = "";//Path.Dirname(_filename);

        }


        private Block ParseTextBlock()
        {
            return null;
        }

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
    }
}