using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CSharp;
using jade.net.nodes;
using jade.net.utils;

namespace jade.net
{
    internal class Compiler
    {
        private readonly IDictionary<string, object> _options;
        private readonly Node _node;
        private bool _hasCompiledDoctype;
        private bool _hasCompiledTag;
        private bool _pp;
        private bool _debug;
        private int _indents;
        private int _parentIndents;
        // private IList<string> _buf;
        private CodeEntryPointMethod _buf;
        private int _lastBufferedIdx;
        private string _lastBuffered;
        private string _doctype;
        private bool _terse;
        private bool _xml;
 
        internal Compiler(Node node, IDictionary<string, object> options = null)
        {
            _options = options ?? new Dictionary<string, object>();
            _node = node;
            _hasCompiledDoctype = false;
            _hasCompiledTag = false;
            _pp = (bool) _options.GetValueOrDefault("pretty", false);
            _debug = _options.ContainsKey("compileDebug") && (bool) _options["compileDebug"];
            _indents = 0;
            _parentIndents = 0;
            if (_options.ContainsKey("doctype")) SetDoctype((string) _options["doctype"]);
                
        }

        /// <summary>
        /// Compile the parse tree to CSharp.
        /// </summary>
        /// <returns></returns>
        private string Compile()
        {
            // this.buf = ['var interp'];
            _buf = new CodeEntryPointMethod();
            if (_pp)
                // _buf.Push("/* var __indent = []; */");
                _buf.Statements.Add(new CodeVariableDeclarationStatement("string[]", "__indent"));
            _lastBufferedIdx = 1;
            Visit(_node);

            var gen = new CSharpCodeProvider();
            var writer = new StringWriter();
            gen.GenerateCodeFromMember(_buf, writer, null);
            return writer.ToString();
        }

        /// <summary>
        /// Sets the default doctype `name`. Sets
        /// </summary>
        /// <param name="name"></param>
        private void SetDoctype(string name)
        {
            name = (name != null ? name.ToLower(CultureInfo.InvariantCulture) : null) ?? "default";
            _doctype = Doctypes.Types.GetValueOrDefault(name, () => string.Format("<!DOCTYPE {0}>", name));
            _terse = _doctype.ToLower(CultureInfo.InvariantCulture) == "<!doctype html>";
            _xml = 0 == _doctype.IndexOf("<?xml", StringComparison.InvariantCulture);
        }

        private void Buffer(string str, bool esc)
        {
            if (esc) str = Utils.Escape(str);

            if (_lastBufferedIdx == _buf.Statements.Count)
            {
                _lastBuffered += str;
                _buf.Statements[_lastBufferedIdx - 1] = new CodeSnippetStatement(string.Format(@"buf.Add(""{0}"");", _lastBuffered));
            }
            else
            {
                _buf.Statements.Add(new CodeSnippetStatement(string.Format(@"buf.Add({0});", str)));
                _lastBuffered = str;
                _lastBufferedIdx = _buf.Statements.Count;
            }
        }

        private void Visit(Node node)
        {
            
        }
    }
}