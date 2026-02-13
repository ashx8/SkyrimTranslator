//窗口位置及其大小
using Godot;
using SkyrimModTranslator.Core;

namespace SkyrimModTranslator.Common
{
    public static class WinPersist
    {
        public static void Load(Window win, string key, Vector2I defaultSize)
        {
            string data = Pos.GetSetting($"win_{key}", "");
            if (!string.IsNullOrEmpty(data))
            {
                string[] p = data.Split('|');
                if (p.Length == 4 &&
                    int.TryParse(p[0], out int x) &&
                    int.TryParse(p[1], out int y) &&
                    int.TryParse(p[2], out int w) &&
                    int.TryParse(p[3], out int h))
                {
                    win.InitialPosition = Window.WindowInitialPosition.Absolute;
                    win.Position = new Vector2I(x, y);
                    win.Size = new Vector2I(w, h);
                    return;
                }
            }
            win.Size = defaultSize;
            win.InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen;
        }

        public static void Save(Window win, string key)
        {
            Pos.SaveSetting($"win_{key}", $"{win.Position.X}|{win.Position.Y}|{win.Size.X}|{win.Size.Y}");
        }
    }
}
