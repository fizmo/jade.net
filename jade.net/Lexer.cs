using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using jade.net.utils;

namespace jade.net
{
    internal class Lexer
    {
        internal class Token
        {
            public string Type { get; set; }
            public int LineNumber { get; set; }
            public object Value { get; set; }
            public bool Buffer { get; set; }
            public string Mode { get; set; }
            public string Args { get; set; }
            public string Key { get; set; }
            public string Code { get; set; }
            public bool Escape { get; set; }
            public Dictionary<string, object> Attrs { get; set; }
            public Dictionary<string, bool> Escaped { get; set; }
            public bool SelfClosing { get; set; }
        }

        internal int LineNumber { get; private set; }
        private string _input;
        internal bool Pipeless { get; set; }
        private readonly List<Token> _deferredTokens;
        private readonly List<Token> _stash;
        private readonly List<int> _indentStack;
        private readonly bool _colons;
        private Regex _indentRe;
        private int _lastIndents;

        private static readonly Regex InputRegex = new Regex(@"\r\n|\r");
        
        internal Lexer(string str, IDictionary<string, object> options)
        {
            _input = InputRegex.Replace(str, "\n");
            _colons = (bool) options.GetValueOrDefault("colons", false);
            _deferredTokens = new List<Token>();
            _lastIndents = 0;
            LineNumber = 1;
            _stash = new List<Token>();
            _indentStack = new List<int>();
            _indentRe = null;
            Pipeless = false;
        }


        /// <summary>
        /// Construct a token with the give `type` and `val`.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="val"></param>
        /// <returns>Token</returns>
        internal Token Tok(string type, object val = null)
        {
            return new Token {Type = type, LineNumber = LineNumber, Value = val};
        }

        /// <summary>
        /// Consume the given `len` of input.
        /// </summary>
        /// <param name="len"></param>
        private void Consume(int len)
        {
            _input = _input.Substring(len);
        }

        /// <summary>
        /// Scan for `type` with the given `regexp`.
        /// </summary>
        /// <param name="regexp"></param>
        /// <param name="type"></param>
        /// <returns>Nullable&lt;Token&gt;</returns>
        private Token Scan(Regex regexp, string type)
        {
            var captures = regexp.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                return Tok(type, captures[1].Value);
            }

            return null;
        }

        /// <summary>
        /// Defer the given `tok`.
        /// </summary>
        /// <param name="tok"></param>
        internal void Defer(Token tok)
        {
            _deferredTokens.Push(tok);
        }

        /// <summary>
        /// Lookahead `n` tokens.
        /// </summary>
        /// <param name="n"></param>
        /// <returns>Token</returns>
        internal Token Lookahead(int n)
        {
            var fetch = n - _stash.Count;
            while (fetch-- > 0)
            {
                _stash.Push(Next());
            }
            return _stash[--n];
        }

        /// <summary>
        /// Return the indexOf `start` / `end` delimiters.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        private int IndexOfDelimiters(char start, char end)
        {
            var str = _input;
            int nstart = 0,
                nend = 0,
                pos = 0;

            for (int i = 0, len = str.Length; i < len; ++i)
            {
                if (start == str[i])
                {
                    ++nstart;
                }
                else if (end == str[i])
                {
                    if (++nend == nstart)
                    {
                        pos = i;
                        break;
                    }
                }
            }
            return pos;
        }

        private Token Stashed()
        {
            return _stash.Count > 0 ? _stash.Shift() : null;
        }

        private Token Deferred()
        {
            return _deferredTokens.Count > 0 ? _deferredTokens.Shift() : null;
        }

        private Token EOS()
        {
            if (_input.Length > 0) return null;
            if (_indentStack.Count > 0)
            {
                _indentStack.Shift();
                return Tok("outdent");
            }
            return Tok("eos");
        }


        private static readonly Regex BlankRegex = new Regex(@"^\n *\n");

        /// <summary>
        /// Blank line.
        /// </summary>
        /// <returns></returns>                
        private Token Blank()
        {
            
            var captures = BlankRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length - 1);
                ++LineNumber;
                return Pipeless ? Tok("text", "") : Next();
            }
            return null;
        }

        private static readonly Regex CommentRegex = new Regex(@"/^ *\/\/(-)?([^\n]*)");
        
        /// <summary>
        /// Comment.
        /// </summary>
        /// <returns></returns>
        private Token Comment()
        {
            var captures = CommentRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length - 1);
                var tok = Tok("comment", captures[2].Value);
                tok.Buffer = "-" != captures[1].Value;
                return tok;
            }
            return null;
        }

        private static readonly Regex InterpolationRegex = new Regex(@"^#\{(.*?)\}");

        /// <summary>
        /// Interpolated tag.
        /// </summary>
        /// <returns></returns>
        private Token Interpolation()
        {
            var captures = InterpolationRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                return Tok("interpolation", captures[1].Value);
            }
            return null;
        }

        private static readonly Regex TagRegex = new Regex(@"^(\w[-:\w]*)(\/?)");

        /// <summary>
        /// Tag.
        /// </summary>
        /// <returns></returns>
        private Token Tag()
        {
            var captures = TagRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                Token tok;
                var name = captures[1].Value;
                if (':' == name[name.Length - 1])
                {
                    name = name.Substring(0, name.Length - 1);
                    tok = Tok("tag", name);
                    Defer(Tok(":"));
                    while (' ' == _input[0]) _input = _input.Substring(1);
                }
                else
                {
                    tok = Tok("tag", name);
                }
                return tok;
            }
            return null;
        }

        private static readonly Regex FilterRegex = new Regex(@"/^:(\w+)/");

        /// <summary>
        /// Filter.
        /// </summary>
        /// <returns></returns>
        private Token Filter()
        {
            return Scan(FilterRegex, "filter");
        }


        private static readonly Regex DoctypeRegex = new Regex(@"^(?:!!!|doctype) *([^\n]+)?");

        /// <summary>
        /// Doctype.
        /// </summary>
        /// <returns></returns>
        private Token Doctype()
        {
            return Scan(DoctypeRegex, "doctype");
        }

        private static readonly Regex IdRegex = new Regex(@"^#([\w-]+)");

        /// <summary>
        /// Id.
        /// </summary>
        /// <returns></returns>
        private Token Id()
        {
            return Scan(IdRegex, "id");
        }

        private static readonly Regex ClassNameRegex = new Regex(@"^\.([\w-]+)");

        /// <summary>
        /// Class.
        /// </summary>
        /// <returns></returns>
        private Token ClassName()
        {
            return Scan(ClassNameRegex, "class");
        }

        private static readonly Regex TextRegex = new Regex(@"^(?:\| ?| ?)?([^\n]+)");

        /// <summary>
        /// Text.
        /// </summary>
        /// <returns></returns>
        private Token Text()
        {
            return Scan(TextRegex, "text");
        }

        private static readonly Regex ExtendsRegex = new Regex(@"^extends? +([^\n]+)");
 
        /// <summary>
        /// Extends.
        /// </summary>
        /// <returns></returns>
        private Token Extends()
        {
            return Scan(ExtendsRegex, "extends");
        }

        private static readonly Regex BlockPrependRegex = new Regex(@"^prepend +([^\n]+)");

        /// <summary>
        /// Block prepend.
        /// </summary>
        /// <returns></returns>
        private Token Prepend()
        {
            var captures = BlockPrependRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                const string mode = "prepend";
                var name = captures[1].Value;
                var tok = Tok("block", name);
                tok.Mode = mode;
                return tok;
            }

            return null;
        }

        private static readonly Regex BlockAppendRegex = new Regex(@"^append +([^\n]+)");

        /// <summary>
        /// Block append.
        /// </summary>
        /// <returns></returns>
        private Token Append()
        {
            var captures = BlockAppendRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                const string mode = "append";
                var name = captures[1].Value;
                var tok = Tok("block", name);
                tok.Mode = mode;
                return tok;
            }

            return null;
        }


        private static readonly Regex BlockRegex = new Regex(@"^block\b *(?:(prepend|append) +)?([^\n]*)");

        /// <summary>
        /// Block.
        /// </summary>
        /// <returns></returns>
        private Token Block()
        {
            var captures = BlockRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                var mode = captures[1].Length > 0 ? captures[1].Value : "replace";
                var name = captures[2].Value;
                var tok = Tok("block", name);
                tok.Mode = mode;
                return tok;
            }

            return null;
        }

        private static readonly Regex YieldRegex = new Regex(@"^yield *");

        /// <summary>
        /// Yield.
        /// </summary>
        /// <returns></returns>
        private Token Yield()
        {
            return Scan(YieldRegex, "yield");
        }

        private static readonly Regex IncludeRegex = new Regex(@"^include +([^\n]+)");

        /// <summary>
        /// Include.
        /// </summary>
        /// <returns></returns>
        private Token Include()
        {
            return Scan(IncludeRegex, "include");
        }

        private static readonly Regex CaseRegex = new Regex(@"^case +([^\n]+)");

        /// <summary>
        /// Case.
        /// </summary>
        /// <returns></returns>
        private Token Case()
        {
            return Scan(CaseRegex, "case");
        }

        private static readonly Regex WhenRegex = new Regex(@"^when +([^:\n]+)");

        /// <summary>
        /// When.
        /// </summary>
        /// <returns></returns>
        private Token When()
        {
            return Scan(WhenRegex, "when");
        }

        private static readonly Regex DefaultRegex = new Regex(@"^default *");

        /// <summary>
        /// Default.
        /// </summary>
        /// <returns></returns>
        private Token Default()
        {
            return Scan(DefaultRegex, "default");
        }

        private static readonly Regex AssignmentRegex = new Regex(@"^(\w+) += *([^;\n]+)( *;? *)");

        /// <summary>
        /// Assignment.
        /// </summary>
        /// <returns></returns>
        private Token Assignment()
        {
            var captures = AssignmentRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                var name = captures[1].Value;
                var val = captures[2].Value;
                // TODO: Need to figure out C# code here. Or, use the token to send back name/value and do codegen later (better?)
                return Tok("code", string.Format("var {0} = ({1});", name, val));
            }
            return null;
        }

        private static readonly Regex CallRegex = new Regex(@"^\+([-\w]+)");
        private static readonly Regex ArgsRegex = new Regex(@"^ *\((.*?)\)");
        private static readonly Regex AttributesRegex = new Regex(@"^ *[-\w]+ *=");

        private Token Call()
        {
            var captures = CallRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                var tok = Tok("call", captures[1].Value);

                // Check for args (not attributes)
                captures = ArgsRegex.Match(_input).Captures;
                if (captures.Count > 0)
                {
                    if (!AttributesRegex.IsMatch(captures[1].Value))
                    {
                        Consume(captures[0].Length);
                        tok.Args = captures[1].Value;
                    }                        
                }
            }
            return null;
        }

        private static readonly Regex MixinRegex = new Regex(@"mixin +([-\w]+)(?: *\((.*)\))?");

        /// <summary>
        /// Mixin.
        /// </summary>
        /// <returns></returns>
        private Token Mixin()
        {
            var captures = MixinRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                var tok = Tok("mixin", captures[1].Value);
                tok.Args = captures[2].Value;
                return tok;
            }
            return null;
        }

        private static readonly Regex ConditionalRegex = new Regex(@"^(if|unless|else if|else)\b([^\n]*)");

        /// <summary>
        /// Conditional.
        /// </summary>
        /// <returns></returns>
        private Token Conditional()
        {
            var captures = ConditionalRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                var type = captures[1].Value;
                var code = captures[2].Value;

                switch (type)
                {
                    case "if":
                        code = string.Format("if ({0})", code);
                        break;
                    case "unless":
                        code = string.Format("if (!({0}))", code);
                        break;
                    case "else if":
                        code = string.Format("else if ({0})", code);
                        break;
                    case "else":
                        code = "else";
                        break;
                }

                return Tok("code", code);
            }
            return null;
        }

        private static readonly Regex WhileRegex = new Regex(@"^while +([^\n]+)");

        /// <summary>
        /// While.
        /// </summary>
        /// <returns></returns>
        private Token While()
        {
            var captures = WhileRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                return Tok("code", string.Format("while ({0})", captures[1].Value));
            }
            return null;
        }

        private static readonly Regex EachRegex = new Regex(@"^(?:- *)?(?:each|for) +(\w+)(?: *, *(\w+))? * in *([^\n]+)");

        /// <summary>
        /// Each.
        /// </summary>
        /// <returns></returns>
        private Token Each()
        {
            var captures = EachRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                var tok = Tok("each", captures[1].Value);
                // TODO: should "$index" be different?
                tok.Key = captures[2].Length > 0 ? captures[2].Value : "$index";
                tok.Code = captures[3].Value;
            }
            return null;
        }

        private static readonly Regex CodeRegex = new Regex(@"^(!?=|-)([^\n]+)");

        /// <summary>
        /// Code.
        /// </summary>
        /// <returns></returns>
        private Token Code()
        {
            var captures = CodeRegex.Match(_input).Captures;
            if (captures.Count > 0)
            {
                Consume(captures[0].Length);
                var flags = captures[1].Value;
                var tok = Tok("code", captures[2].Value);
                tok.Escape = flags.Length > 0 && flags[0] == '=';
                tok.Buffer = (flags.Length > 0 && flags[0] == '=') || (flags.Length > 1 && flags[1] == '=');
                return tok;
            }
            return null;
        }

        private static readonly Regex InterpolateRegex = new Regex(@"(\\)?#\{([^}]+)\}");
        private static readonly Regex KeyQuoteRegex = new Regex(@"^['""]|['""]$");

        /// <summary>
        /// Attributes.
        /// </summary>
        /// <returns></returns>
        private Token Attrs()
        {
            if ('(' == _input[0])
            {
                var index = IndexOfDelimiters('(', ')');
                var str = _input.Substring(1, index - 1);
                var tok = Tok("attrs");
                var len = str.Length;
                var colons = _colons;
                var states = new List<string> {"key"};
                var escapedAttr = false;
                var key = "";
                var val = "";
                char quote;
                char p;

                Func<string> state = states.Last;
                Func<string, string> interpolate = attr =>
                    InterpolateRegex.Replace(attr, match =>
                    {
                        if (match.Captures[1].Length > 0)
                            return match.Value;
                        return string.Format( "{0} + ({1}) + {0}", quote, match.Captures[2].Value);
                    });

                Consume(index + 1);
                tok.Attrs = new Dictionary<string, object>();
                tok.Escaped = new Dictionary<string, bool>();

                Action<char> parse = c =>
                    {
                        var real = c;
                        // Comment from upstream: TODO: remove when people fix ":"
                        if (colons && ':' == c) c = '=';
                        switch (c)
                        {
                            case ',':
                            case '\n':
                                switch (state())
                                {
                                    case "expr":
                                    case "array":
                                    case "string":
                                    case "object":
                                        val += c;
                                        break;
                                    default:
                                        states.Push("key");
                                        val = val.Trim();
                                        key = key.Trim();
                                        if (string.IsNullOrEmpty(key)) return;
                                        key = KeyQuoteRegex.Replace(key, "").Replace("!", "");
                                        tok.Escaped[key] = escapedAttr;
                                        // TODO: Figure out how the "true" case is used.
                                        tok.Attrs[key] = "" == val ? (object)true : interpolate(val);
                                        key = val = "";
                                        break;
                                }
                                break;
                            case '=':
                                switch (state())
                                {
                                    case "key char":
                                        key += real;
                                        break;
                                    case "val":
                                    case "expr":
                                    case "array":
                                    case "string":
                                    case "object":
                                        val += real;
                                        break;
                                    default:
                                        escapedAttr = '!' != p;
                                        states.Push("val");
                                        break;
                                }
                                break;
                            case '(':
                                if ("val" == state() || "expr" == state()) states.Push("expr");
                                val += c;
                                break;
                            case ')':
                                if ("expr" == state() || "val" == state()) states.Pop();
                                val += c;
                                break;
                            case '{':
                                if ("val" == state()) states.Push("object");
                                val += c;
                                break;
                            case '}':
                                if ("object" == state()) states.Pop();
                                val += c;
                                break;
                            case '[':
                                if ("val" == state()) states.Push("array");
                                val += c;
                                break;
                            case ']':
                                if ("array" == state()) states.Pop();
                                val += c;
                                break;
                            case '"':
                            case '\'':
                                switch (state())
                                {
                                    case "key":
                                        states.Push("key char");
                                        break;
                                    case "key char":
                                        states.Pop();
                                        break;
                                    case "string":
                                        if (c == quote) states.Pop();
                                        val += c;
                                        break;
                                    default:
                                        states.Push("string");
                                        val += c;
                                        quote = c;
                                        break;
                                }
                                break;
                            // case '': break;
                            default:
                                switch (state())
                                {
                                    case "key":
                                    case "key char":
                                        key += c;
                                        break;
                                    default:
                                        val += c;
                                        break;
                                }
                                break;
                        }
                        p = c;
                    };

                for (var i = 0; i < len; ++i)
                {
                    parse(str[i]);
                }

                parse(',');

                if ('/' == _input[0])
                {
                    Consume(1);
                    tok.SelfClosing = true;
                }

                return tok;
            }
            return null;
        }

        private static readonly Regex TabsRegex = new Regex(@"^\n(\t*) *");
        private static readonly Regex SpacesRegex = new Regex(@"^\n( *)");

        /// <summary>
        /// Indent | Outdent | NewLine.
        /// </summary>
        /// <returns></returns>
        private Token Indent()
        {
            CaptureCollection captures;

            if (_indentRe != null)
            {
                captures = _indentRe.Match(_input).Captures;
            }
            else
            {
                // tabs
                var re = TabsRegex;
                captures = re.Match(_input).Captures;

                // spaces
                if (captures.Count > 0 && captures[1].Length <= 0)
                {
                    re = SpacesRegex;
                    captures = re.Match(_input).Captures;
                }

                // established
                if (captures.Count > 0 && captures[1].Length > 0) _indentRe = re;
            }

            if (captures.Count > 0)
            {
                var indents = captures[1].Length;

                ++LineNumber;
                Consume(indents + 1);

                if (' ' == _input[0] || '\t' == _input[0])
                {
                    throw new Exception("Invalid indentation, you can use tabs or spaces but not both");
                }

                // blank line
                if ('\n' == _input[0]) return Tok("newline");

                // outdent
                if (_indentStack.Count > 0 && indents < _indentStack[0])
                {
                    while (_indentStack.Count > 0 && _indentStack[0] > indents)
                    {
                        _stash.Push(Tok("outdent"));
                        _indentStack.Shift();
                    }
                    return _stash.Pop();
                }

                // indent
                if (indents > 0 && indents != _indentStack[0])
                {
                    _indentStack.Unshift(indents);
                    return Tok("indent", indents);
                }

                // newline
                return Tok("newline");
            }

            return null;
        }

        /// <summary>
        /// Pipe-less text consumed only when
        /// pipeless is true;
        /// </summary>
        /// <returns></returns>
        private Token PipelessText()
        {
            if (Pipeless)
            {
                if ('\n' == _input[0]) return null;
                var i = _input.IndexOf('\n');
                if (-1 == i) i = _input.Length;
                var str = _input.Substring(0, i);
                Consume(str.Length);
                return Tok("text", str);
            }

            return null;
        }

        private static readonly Regex ColonRegex = new Regex(@"^: *");

        /// <summary>
        /// ':'
        /// </summary>
        /// <returns></returns>
        private Token Colon()
        {
            return Scan(ColonRegex, ":");
        }

        /// <summary>
        /// Return the next token object, or those
        /// previously stashed by lookahead.
        /// </summary>
        /// <returns>Token</returns>
        internal Token Advance()
        {
            return Stashed() ?? Next();
        }

        public Token Next()
        {
            return Blank()
                   ?? EOS()
                   ?? PipelessText()
                   ?? Yield()
                   ?? Doctype()
                   ?? Interpolation()
                   ?? Case()
                   ?? When()
                   ?? Default()
                   ?? Extends()
                   ?? Append()
                   ?? Prepend()
                   ?? Block()
                   ?? Include()
                   ?? Mixin()
                   ?? Call()
                   ?? Conditional()
                   ?? Each()
                   ?? While()
                   ?? Assignment()
                   ?? Tag()
                   ?? Filter()
                   ?? Code()
                   ?? Id()
                   ?? ClassName()
                   ?? Attrs()
                   ?? Indent()
                   ?? Comment()
                   // ?? Colon()
                   ?? Text();
        }
    }
}
