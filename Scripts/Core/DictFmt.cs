// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//词典分片和存储格式
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SkyrimModTranslator.Core.Dict
{
    public static class DictFmt
    {
        public static string ToStorage(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n").Trim();
        }

        public static string ToUI(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\n", "\n");
        }

        public static string GetShard(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "#";
            string clean = text.TrimStart();
            if (clean.Length == 0) return "#";
            
            char first = char.ToUpper(clean[0]);
            if (first >= 'A' && first <= 'Z') return first.ToString();
            if (first == '[') return "[";
            return "#";
        }

        public static bool IsValidFileNameChar(char c)
        {
            return !Path.GetInvalidFileNameChars().Contains(c);
        }
    }

    public class DistEntry
    {
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
    }

    public class TranslationDist
    {
        public List<DistEntry> Entries { get; set; } = new List<DistEntry>();
    }
}
