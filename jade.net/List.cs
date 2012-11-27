using System.Collections.Generic;
using System.Linq;

namespace jade.net
{
    public static class List
    {
        public static TSource Shift<TSource>(this IList<TSource> source)
        {
            var result = source.First();
            source.RemoveAt(0);
            return result;
        }

        public static void Unshift<TSource>(this IList<TSource> source, TSource value)
        {
            source.Insert(0, value);
        }

        public static void Push<TSource>(this IList<TSource> source, TSource value)
        {
            source.Add(value);
        }

        public static TSource Pop<TSource>(this IList<TSource> source)
        {
            var result = source.Last();
            source.RemoveAt(source.Count - 1);
            return result;
        }
    }
}