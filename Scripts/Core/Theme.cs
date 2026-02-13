//主题与背景管理
using Godot;
using System.IO;

namespace SkyrimModTranslator.Core
{
    public static class Theme
    {
        public static string CurrentBackgroundPath {
            get => Pos.GetSetting("bg_path", "res://icon.svg");
            set => Pos.SaveSetting("bg_path", value);
        }

        public static float BackgroundOpacity {
            get {
                string opacityStr = Pos.GetSetting("bg_opacity", "0.4");
                if (float.TryParse(opacityStr, out float opacity)) {
                    return Mathf.Clamp(opacity, 0.0f, 1.0f);
                }
                return 0.4f;
            }
            set => Pos.SaveSetting("bg_opacity", Mathf.Clamp(value, 0.0f, 1.0f).ToString());
        }

        public static void ApplyStdBg(Window window)
        {
            var bg = new TextureRect {
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                Modulate = new Color(1, 1, 1, BackgroundOpacity)
            };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            
            string bgPath = CurrentBackgroundPath;
            string fullPath = ProjectSettings.GlobalizePath(bgPath);
            
            if (File.Exists(fullPath)) {
                bg.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(fullPath));
            }
            
            window.AddChild(bg);
            
            var filter = new ColorRect {
                Color = new Color(0.15f, 0.15f, 0.18f, 0.6f)
            };
            filter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            window.AddChild(filter);
        }

        public static ImageTexture LoadBackgroundTexture(string path)
        {
            string fullPath = ProjectSettings.GlobalizePath(path);
            if (File.Exists(fullPath)) {
                try {
                    return ImageTexture.CreateFromImage(Image.LoadFromFile(fullPath));
                }
                catch (System.Exception ex) {
                    GD.PrintErr($"加载背景图片失败: {ex.Message}");
                }
            }
            return null;
        }

        public static void SaveBackgroundSettings(string path, float opacity)
        {
            CurrentBackgroundPath = path;
            BackgroundOpacity = opacity;
        }
    }
}
