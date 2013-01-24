using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace jade.net.test
{
    [TestFixture]
    public class TestCasesRunner
    {
        [Test, TestCaseSource(typeof(TestCases), "Cases")]
        public void RunCase(string test)
        {
            var path = test + ".jade";
            var str = File.ReadAllText(path, Encoding.UTF8);
            var html = File.ReadAllText(test + ".html", Encoding.UTF8).Trim();
            var fn = Jade.Compile(str, new { Filename = path, Pretty = true });
            var actual = fn(new {title = "Jade"});
            Assert.That(actual, Is.EqualTo(html));
        }
    }

    public class TestCases : IEnumerable<String>
    {
        private readonly IEnumerable<string> _cases = Directory.EnumerateFiles("cases")
                    .Where(file => file.IndexOf(".jade") != -1)
                    .Select(file => file.Replace(".jade", ""));

        public IEnumerable<string> Cases
        {
            get { return _cases; }
        }

        public IEnumerator<string> GetEnumerator()
        {
            return _cases.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _cases.GetEnumerator();
        }
    }
}