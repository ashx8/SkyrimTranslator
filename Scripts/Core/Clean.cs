//数据清理工具（暂时弃用）
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Godot;

namespace SkyrimModTranslator.Core
{
    public static class Clean
    {
        public static void CleanDupKeys(string filePath)
        {
            if (!File.Exists(filePath))
            {
                GD.PrintErr($"[Clean] 文件不存在: {filePath}");
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath);
                var jsonDoc = JsonDocument.Parse(jsonContent);
                
                var uniqueKeys = new Dictionary<string, JsonElement>();

                foreach (var property in jsonDoc.RootElement.EnumerateObject())
                {
                    if (!uniqueKeys.ContainsKey(property.Name))
                    {
                        uniqueKeys[property.Name] = property.Value;
                    }
                }

                string cleanedJson = JsonSerializer.Serialize(uniqueKeys, Cfg.UnsafeRelaxedOptions);
                File.WriteAllText(filePath, cleanedJson);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Clean] 清理重复键失败: {e.Message}");
            }
        }
    }
}
