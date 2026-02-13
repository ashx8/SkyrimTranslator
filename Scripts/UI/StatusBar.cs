//状态栏（翻译进度显示）
using Godot;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    public partial class StatusBar : HBoxContainer
    {
        private ProgressBar _prog;
        private Label _lblProgressText;

        public StatusBar()
        {
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            CustomMinimumSize = new Vector2(0, 40);
            AddThemeConstantOverride("separation", 10);

            _prog = new ProgressBar {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                ShowPercentage = false,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(100, 40)
            };

            var sb = new StyleBoxFlat 
            {
                BgColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            _prog.AddThemeStyleboxOverride("fill", sb);
            AddChild(_prog);

            _lblProgressText = new Label {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Text = string.Format(L.T("STATUS_PROGRESS_FORMAT"), 0, 0, 0.0f)
            };
            _lblProgressText.AddThemeFontSizeOverride("font_size", 16);
            _prog.AddChild(_lblProgressText);
            _lblProgressText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        }

        public void Update(string projectName, int total, int trans)
        {
            _prog.MaxValue = total;
            _prog.Value = trans;
            
            float perc = total > 0 ? (float)trans / total * 100f : 0f;
            _lblProgressText.Text = string.Format(L.T("STATUS_PROGRESS_FORMAT"), total, trans, perc);
        }

        public void Clear() => Update("", 0, 0);
    }
}
