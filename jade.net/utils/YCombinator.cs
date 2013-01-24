using System;

namespace jade.net.utils
{
    internal static class YCombinator
    {
        delegate Func<TA, TR> Recursive<TA, TR>(Recursive<TA, TR> r);

        internal static Func<TA, TR> Y<TA, TR>(Func<Func<TA, TR>, Func<TA, TR>> f)
        {
            Recursive<TA, TR> rec = r => a => f(r(r))(a);
            return rec(rec);
        }
    }
}