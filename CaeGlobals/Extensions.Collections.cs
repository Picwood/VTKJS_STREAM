// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

using System;
using System.Collections.Generic;

namespace CaeGlobals
{
    public static partial class ExtensionMethods
    {
        // Dictionary
        public static bool ContainsValidKey<T>(this IDictionary<string, T> dictionary, string key)
        {
            T value;
            if (dictionary.TryGetValue(key, out value))
            {
                if (value is NamedClass nc) return nc.Valid;
                else return true;  // act as ordinary ContainsKey
            }
            else return false;
        }
        public static bool ContainsValidActiveKey<T>(this IDictionary<string, T> dictionary, string key)
        {
            T value;
            if (dictionary.TryGetValue(key, out value))
            {
                if (value is NamedClass nc) return nc.Valid && nc.Active;
                else return true;  // act as ordinary ContainsKey
            }
            else return false;
        }
        public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dic, Dictionary<TKey, TValue> dicToAdd)
        {
            foreach (var item in dicToAdd) dic.Add(item.Key, item.Value);
        }
        public static void AddUniqueItemsFromRange<TKey, TValue>(this Dictionary<TKey, TValue> dic, Dictionary<TKey, TValue> dicToAdd)
        {
            foreach (var item in dicToAdd)
            {
                if (!dic.ContainsKey(item.Key)) dic.Add(item.Key, item.Value);
            }
                
        }
        public static string GetNextNumberedKey<T>(this IDictionary<string, T> dictionary, string key,
                                                   string postFix = "", string separator = "-")
        {
            int n = 0;
            string newKey;
            while (true)
            {
                n++;
                newKey = key + separator + n + postFix;
                if (!dictionary.ContainsKey(newKey)) break;
            }
            return newKey;
        }
        public static string GetNextNumberedKey(this HashSet<string> hashSet, string key,
                                                string postFix = "", string separator = "-")
        {
            int n = 0;
            string newKey;
            while (true)
            {
                n++;
                newKey = key + separator + n + postFix;
                if (!hashSet.Contains(newKey)) break;
            }
            return newKey;
        }
        public static string GetNextNumberedKey(this List<string> list, string key, string postFix = "")
        {
            return new HashSet<string>(list).GetNextNumberedKey(key, postFix);
        }
        public static string GetNextNumberedKey(this string[] array, string key, string postFix = "")
        {
            return new HashSet<string>(array).GetNextNumberedKey(key, postFix);
        }
        public static int FindFreeIntervalOfKeys<T>(this Dictionary<int, T> dic, int numOfKeys, int maxKey)
        {
            int count = 0;
            int firstId = 1;                    // start at 1
            //
            for (int i = 1; i <= maxKey; i++)   // start at 1
            {
                if (dic.ContainsKey(i))
                {
                    count = 0;
                    firstId = i + 1;
                }
                else
                {
                    if (++count == numOfKeys) break;
                }
            }
            //
            return firstId;
        }
    }
}
