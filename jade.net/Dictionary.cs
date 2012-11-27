using System;
using System.Collections.Generic;

namespace jade.net
{
    public static class Dictionary
    {
         public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
         {
             return dictionary.GetValueOrDefault(key, () => defaultValue);
         }

         public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> defaultValueProvider)
         {
             TValue value;
             return dictionary.TryGetValue(key, out value) ? value : defaultValueProvider();
         }

    }
}