//本地化与文本处理
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SkyrimModTranslator.Common
{
    public static class L
    {
        private static JsonElement _root;
        public static string Current { get; private set; } = "中文";
        public static event Action OnLanguageChanged;
        public static bool IsLoaded { get; private set; } = false;

        public static void Load(string langCode)
        {
            if (string.IsNullOrEmpty(langCode))
            {
                langCode = "中文";
            }
            string path = ProjectSettings.GlobalizePath("res://Localization/" + langCode + ".json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    JsonElement newRoot = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
                    _root = newRoot;
                    Current = langCode;
                    IsLoaded = true;
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[L] 加载语言文件失败: {e.Message}");
                    IsLoaded = false;
                }
            }
            else
            {
                GD.PrintErr($"[L] 语言文件不存在: {path}");
                IsLoaded = false;
            }
            OnLanguageChanged?.Invoke();
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "";
            }
            try
            {
                if (_root.TryGetProperty(key, out JsonElement value))
                {
                    return value.GetString();
                }
                return key;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[L] 获取文本失败: {e.Message}");
                return key;
            }
        }
        
        //解析字符串列表字典（分类以及本地化文本）
        private static Dictionary<string, List<string>> ParseStringListDict(string propertyName)
        {
            Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();
            if (_root.TryGetProperty(propertyName, out JsonElement element))
            {
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    List<string> list = new List<string>();
                    foreach (JsonElement item in prop.Value.EnumerateArray())
                    {
                        string str = item.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            list.Add(str);
                        }
                    }
                    dict[prop.Name] = list;
                }
            }
            return dict;
        }
    
        public static Dictionary<string, List<string>> GetCats()
        {
            return ParseStringListDict("CATEGORIES");
        }

        public static Dictionary<string, List<string>> GetFieldMap()
        {
            return ParseStringListDict("FIELD_MAPPING");
        }

        public static void SaveCats(Dictionary<string, List<string>> categories)
        {
            string path = ProjectSettings.GlobalizePath("res://Localization/" + Current + ".json");
            if (File.Exists(path))
            {
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        StringEscapeHandling = StringEscapeHandling.Default
                    };

                    string json = File.ReadAllText(path);
                    var allData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    allData["CATEGORIES"] = categories;

                    string updatedJson = JsonConvert.SerializeObject(allData, settings);
                    File.WriteAllText(path, updatedJson);

                    Load(Current);
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[L] 分类保存失败: {e.Message}");
                }
            }
        }
    }
}
