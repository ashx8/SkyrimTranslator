//词典的分片和存储格式
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
            
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.Replace("\n", "\\n").Trim();
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
        
        //把已有的词典文件的换行符都修一遍
        public static void FixExistingDicts()
        {
            string basePath = SkyrimModTranslator.Core.Dict.DictStorage.GetRealBasePath();
            if (!Directory.Exists(basePath)) return;
            
            string[] categories = { "UserDict", "ModDict", "OtherDict" };
            
            foreach (string category in categories)
            {
                string catPath = Path.Combine(basePath, category);
                if (!Directory.Exists(catPath)) continue;
                
                FixCategoryDicts(catPath, category);
            }
        }
        
        private static void FixCategoryDicts(string catPath, string category)
        {
            if (category == "UserDict")
            {
                FixShardFiles(catPath, category, "");
            }
            else
            {
                foreach (string subDir in Directory.GetDirectories(catPath))
                {
                    string subName = Path.GetFileName(subDir);
                    FixShardFiles(subDir, category, subName);
                }
            }
        }
        
        private static void FixShardFiles(string dirPath, string category, string subName)
        {
            foreach (string file in Directory.GetFiles(dirPath, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var glossary = System.Text.Json.JsonSerializer.Deserialize<TranslationDist>(json);
                    
                    if (glossary?.Entries != null)
                    {
                        bool changed = false;
                        
                        foreach (var entry in glossary.Entries)
                        {
                            string newOriginal = NormalizeNewlines(entry.OriginalText);
                            string newTrans = NormalizeNewlines(entry.TranslatedText);
                            
                            if (newOriginal != entry.OriginalText || newTrans != entry.TranslatedText)
                            {
                                entry.OriginalText = newOriginal;
                                entry.TranslatedText = newTrans;
                                changed = true;
                            }
                        }
                        
                        if (changed)
                        {
                            var options = new System.Text.Json.JsonSerializerOptions
                            {
                                WriteIndented = true,
                                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            };
                            File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(glossary, options));
                            System.Console.WriteLine($"[DictFmt] 修复: {Path.GetFileName(file)}");
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine($"[DictFmt] 修复失败 {file}: {e.Message}");
                }
            }
        }
        
        private static string NormalizeNewlines(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.Replace("\n", "\\n");
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
