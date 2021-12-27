using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCProjectBot
{
    public static class Extensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue) where TKey : notnull
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            if (dictionary.TryGetValue(key, out TValue existingValue))
                return existingValue;
            else
            {
                dictionary.Add(key, defaultValue);
                return dictionary[key];
            }    
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

        }
    }
}
