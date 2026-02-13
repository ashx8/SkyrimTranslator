//配置管理与窗口持久化
using System;
using Godot;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace SkyrimModTranslator.Core
{
    public static class Pos
    {
        private static readonly string SettingsPath = ProjectSettings.GlobalizePath("user://settings.cfg");

        //保存窗口位置和大小
        public static void SaveWindowState(Window win)
        {
            string key = win.GetType().Name;
            string posKey = $"win_pos_{key}";
            string sizeKey = $"win_size_{key}";
            
            SaveSetting(posKey, $"{win.Position.X}|{win.Position.Y}");
            SaveSetting(sizeKey, $"{win.Size.X}|{win.Size.Y}");
        }

        //恢复窗口位置和大小
        public static void RestoreWindowState(Window win)
        {
            string key = win.GetType().Name;
            string posKey = $"win_pos_{key}";
            string sizeKey = $"win_size_{key}";
            
            //恢复位置
            string posValue = GetSetting(posKey, "");
            if (!string.IsNullOrEmpty(posValue))
            {
                try
                {
                    string[] posParts = posValue.Split('|');
                    if (posParts.Length >= 2 && int.TryParse(posParts[0], out int x) && int.TryParse(posParts[1], out int y))
                    {
                        Vector2I savedPos = new Vector2I(x, y);
                        win.InitialPosition = Window.WindowInitialPosition.Absolute;
                        win.Position = savedPos;
                        win.CallDeferred(Window.MethodName.SetPosition, savedPos);
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[Pos] 恢复位置失败: {e.Message}");
                }
            }
            
            //恢复大小
            string sizeValue = GetSetting(sizeKey, "");
            if (!string.IsNullOrEmpty(sizeValue))
            {
                try
                {
                    string[] sizeParts = sizeValue.Split('|');
                    if (sizeParts.Length >= 2 && int.TryParse(sizeParts[0], out int width) && int.TryParse(sizeParts[1], out int height))
                    {
                        Vector2I savedSize = new Vector2I(width, height);
                        win.Size = savedSize;
                        win.CallDeferred(Window.MethodName.SetSize, savedSize);
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[Pos] 恢复大小失败: {e.Message}");
                }
            }
        }

        //保存设置
        public static void SaveSetting(string key, string value)
        {
            var settings = LoadSettings();
            settings[key] = value;
            SaveSettings(settings);
        }

        //获取设置
        public static string GetSetting(string key, string defaultVal)
        {
            var settings = LoadSettings();
            if (settings.TryGetValue(key, out string value))
            {
                return value;
            }
            return defaultVal;
        }

        //加载所有设置
        private static Dictionary<string, string> LoadSettings()
        {
            var settings = new Dictionary<string, string>();
            
            if (File.Exists(SettingsPath))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(SettingsPath))
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length >= 2)
                        {
                            settings[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[Pos] 加载设置失败: {e.Message}");
                }
            }
            
            return settings;
        }

        //保存所有设置
        private static void SaveSettings(Dictionary<string, string> settings)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var kvp in settings)
                {
                    sb.AppendLine($"{kvp.Key}={kvp.Value}");
                }
                File.WriteAllText(SettingsPath, sb.ToString());
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Pos] 保存设置失败: {e.Message}");
            }
        }


    }
}
