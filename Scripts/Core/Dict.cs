// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//词典存储
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.Core.Dict
{
    public static class DictStorage
    {
        private static string GetBasePath()
        {
            try
            {
                string langName = L.Current;
                string exePath = OS.GetExecutablePath();
                string exeDir = Path.GetDirectoryName(exePath);
                string baseDir = Path.Combine(exeDir, "Dist", langName);
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                return baseDir;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 获取基础路径失败失败: {e.Message}");
                return Directory.GetCurrentDirectory();
            }
        }
        
        private static string GetTargetDir(string category, string modName)
        {
            string basePath = GetBasePath();
            if (category == "UserDict")
                return Path.Combine(basePath, category);
            return Path.Combine(basePath, category, modName);
        }

        public static void Save(string category, string modName, Dictionary<string, string> data)
        {
            try
            {
                string dir = GetTargetDir(category, modName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                List<DistEntry> entries = new List<DistEntry>();
                foreach (var kv in data)
                {
                    entries.Add(new DistEntry {
                        OriginalText = kv.Key,
                        TranslatedText = kv.Value
                    });
                }

                var grouped = entries.GroupBy(e => DictFmt.GetShard(e.OriginalText));

                foreach (var group in grouped)
                {
                    string shardName = group.Key;
                    string filePath = Path.Combine(dir, $"{shardName}.json");

                    var dict = new Dictionary<string, string>();
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var json = File.ReadAllText(filePath);
                            var existing = System.Text.Json.JsonSerializer.Deserialize<TranslationDist>(json);
                            if (existing?.Entries != null)
                            {
                                foreach (var e in existing.Entries) dict[e.OriginalText] = e.TranslatedText;
                            }
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"[Dict] 加载现有数据失败: {e.Message}");
                        }
                    }

                    foreach (var entry in group)
                    {
                        string key = DictFmt.ToStorage(entry.OriginalText);
                        string val = DictFmt.ToStorage(entry.TranslatedText);
                        dict[key] = val;
                    }

                    var finalObj = new TranslationDist {
                        Entries = new List<DistEntry>()
                    };
                    foreach (var kv in dict)
                    {
                        finalObj.Entries.Add(new DistEntry {
                            OriginalText = kv.Key,
                            TranslatedText = kv.Value
                        });
                    }

                    finalObj.Entries.Sort((a, b) => string.Compare(a.OriginalText, b.OriginalText));

                    var options = new System.Text.Json.JsonSerializerOptions {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(finalObj, options));
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 保存失败: {e.Message}");
            }
        }

        public static Dictionary<string, string> Load(string category, string modName, char? shard = null)
        {
            try
            {
                string dir = GetTargetDir(category, modName);
                if (!Directory.Exists(dir)) return new Dictionary<string, string>();

                var result = new Dictionary<string, string>();

                if (shard.HasValue)
                {
                    string path = Path.Combine(dir, $"{shard.Value}.json");
                    if (File.Exists(path)) LoadFile(path, result);
                }
                else
                {
                    foreach (string file in Directory.GetFiles(dir, "*.json"))
                    {
                        LoadFile(file, result);
                    }
                }
                return result;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 加载失败: {e.Message}");
                return new Dictionary<string, string>();
            }
        }

        public static Dictionary<string, string> LoadByShard(string category, string modName, string shardName)
        {
            try
            {
                string dir = GetTargetDir(category, modName);
                if (!Directory.Exists(dir)) return new Dictionary<string, string>();

                var result = new Dictionary<string, string>();
                string path = Path.Combine(dir, $"{shardName}.json");
                if (File.Exists(path)) LoadFile(path, result);
                return result;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 加载按分片失败失败: {e.Message}");
                return new Dictionary<string, string>();
            }
        }

        private static void LoadFile(string path, Dictionary<string, string> dict)
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(path);
                string content;
                
                try
                {
                    content = System.Text.Encoding.UTF8.GetString(fileBytes);
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[Dict] UTF-8 解码失败失败: {e.Message}");
                    //兼容Edit插件的词典文件
                    var gbEncoding = System.Text.Encoding.GetEncoding(936);
                    content = gbEncoding.GetString(fileBytes);
                }
                
                var glossary = System.Text.Json.JsonSerializer.Deserialize<TranslationDist>(content);
                if (glossary?.Entries != null)
                {
                    foreach (var entry in glossary.Entries)
                    {
                        dict[entry.OriginalText] = DictFmt.ToUI(entry.TranslatedText);
                    }
                }
                else
                {
                    var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                    if (data != null)
                    {
                        foreach (var kv in data)
                        {
                            dict[DictFmt.ToStorage(kv.Key)] = DictFmt.ToUI(kv.Value);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 加载文件失败失败: {e.Message}");
            }
        }

        public static Dictionary<string, string> LoadAllInSub(string category)
        {
            try
            {
                var result = new Dictionary<string, string>();
                string categoryDir = Path.Combine(GetBasePath(), category);
                if (!Directory.Exists(categoryDir)) return result;

                foreach (string modSubDir in Directory.GetDirectories(categoryDir))
                {
                    var modData = Load(category, Path.GetFileName(modSubDir));
                    foreach (var kv in modData) 
                        if (!result.ContainsKey(kv.Key)) result[kv.Key] = kv.Value;
                }
                return result;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 加载所有子目录失败失败失败: {e.Message}");
                return new Dictionary<string, string>();
            }
        }

        public static void Delete(string category, string modName, string key)
        {
            try
            {
                string dir = GetTargetDir(category, modName);
                if (!Directory.Exists(dir)) return;

                string processedKey = DictFmt.ToStorage(key);
                string shardName = DictFmt.GetShard(key);
                string filePath = Path.Combine(dir, $"{shardName}.json");
                
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = File.ReadAllText(filePath);
                        var glossary = System.Text.Json.JsonSerializer.Deserialize<TranslationDist>(json);
                        if (glossary?.Entries != null)
                        {
                            DistEntry entryToRemove = null;
                            for (int i = 0; i < glossary.Entries.Count; i++)
                            {
                                if (glossary.Entries[i].OriginalText == processedKey)
                                {
                                    entryToRemove = glossary.Entries[i];
                                    break;
                                }
                            }
                            if (entryToRemove != null)
                            {
                                glossary.Entries.Remove(entryToRemove);
                                var options = new System.Text.Json.JsonSerializerOptions {
                                    WriteIndented = true,
                                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                                };
                                File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(glossary, options));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[Dict] 删除条目失败失败: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 删除失败失败: {e.Message}");
            }
        }

        public static List<string> GetModList(string category)
        {
            try
            {
                string dir = Path.Combine(GetBasePath(), category);
                if (!Directory.Exists(dir)) return new List<string>();
                return Directory.GetDirectories(dir).Select(d => Path.GetFileName(d)).ToList();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 获取模组列表失败失败失败: {e.Message}");
                return new List<string>();
            }
        }

        public static string GetRealBasePath()
        {
            return GetBasePath();
        }

        public static string GetCurrentLangPath() => GetBasePath();

        public static void ClearAll(string category, string subCategory, bool deleteDirectory = false)
        {
            try
            {
                string dir = GetTargetDir(category, subCategory);
                if (Directory.Exists(dir))
                {
                    try
                    {
                        foreach (string file in Directory.GetFiles(dir, "*.json"))
                        {
                            File.Delete(file);
                        }
                        if (deleteDirectory)
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[Dict] 清除文件失败失败失败: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 清除所有失败失败失败: {e.Message}");
            }
        }
        
        public static void ClearAll(string category, string subCategory)
        {
            ClearAll(category, subCategory, false);
        }

        public static List<string> GetExistShards(string category, string subCategory)
        {
            try
            {
                string path = GetTargetDir(category, subCategory);
                if (!Directory.Exists(path)) return new List<string> { "a" };
                return Directory.GetFiles(path, "*.json")
                                .Select(Path.GetFileNameWithoutExtension)
                                .OrderBy(f => f)
                                .ToList();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 获取现有分片失败失败失败: {e.Message}");
                return new List<string> { "a" };
            }
        }

        public static string QuickSearch(string word)
        {
            try
            {
                var userDict = Load("UserDict", "");
                foreach (var kvp in userDict)
                {
                    if (kvp.Key.Equals(word, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Value;
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Dict] 快速搜索失败失败失败: {e.Message}");
                return null;
            }
        }

        public static string ProcessedQuickSearch(string word)
        {
            return QuickSearch(word);
        }
    }
}
