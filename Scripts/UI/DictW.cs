//书籍预览窗口
using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    public partial class Book : Window
    {
        private RichTextLabel _contentLabel;
        private Button _prevButton;
        private Button _nextButton;
        private Label _pageInfo;
        private List<string> _pages = new List<string>();
        private int _currentPage = 0;
        private string _pendingContent;

        public Book()
        {
            Visible = false;
        }

        public override void _Ready()
        {
            Title = L.T("WIN_BOOK_PREVIEW");
            MinSize = new Vector2I(450, 600);
            Size = new Vector2I(500, 700);
            CloseRequested += OnCloseRequested;

            SetupUI();
            SetupConnections();

            if (!string.IsNullOrEmpty(_pendingContent))
            {
                UpdateContent(_pendingContent);
            }

            Visible = true;
            Pos.RestoreWindowState(this);
        }

        private void OnCloseRequested()
        {
            Pos.SaveWindowState(this);
            QueueFree();
        }
        
        //设置书籍内容
        private void SetupUI()
        {
            SkyrimModTranslator.Core.Theme.ApplyStdBg(this);

            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            marginContainer.AddThemeConstantOverride("margin_top", 25);
            marginContainer.AddThemeConstantOverride("margin_bottom", 25);
            marginContainer.AddThemeConstantOverride("margin_left", 30);
            marginContainer.AddThemeConstantOverride("margin_right", 30);
            AddChild(marginContainer);

            var mainVBox = new VBoxContainer();
            mainVBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            mainVBox.AddThemeConstantOverride("separation", 18);
            marginContainer.AddChild(mainVBox);

            _contentLabel = new RichTextLabel
            {
                BbcodeEnabled = true,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Text = L.T("MSG_LOADING_CONTENT"),
                ThemeTypeVariation = "Book"
            };
            mainVBox.AddChild(_contentLabel);

            var buttonHBox = new HBoxContainer();
            buttonHBox.AddThemeConstantOverride("separation", 15);
            buttonHBox.AddChild(new HBoxContainer());

            _prevButton = new Button { Text = L.T("BTN_PREV_PAGE") };
            buttonHBox.AddChild(_prevButton);

            _pageInfo = new Label { Text = L.T("MSG_PAGE_INFO").Replace("{current}", "1").Replace("{total}", "1") };
            buttonHBox.AddChild(_pageInfo);

            _nextButton = new Button { Text = L.T("BTN_NEXT_PAGE") };
            buttonHBox.AddChild(_nextButton);

            buttonHBox.AddChild(new HBoxContainer());
            mainVBox.AddChild(buttonHBox);
        }

        private void SetupConnections()
        {
            _prevButton.Pressed += OnPrevButtonPressed;
            _nextButton.Pressed += OnNextButtonPressed;
        }

        private void OnPrevButtonPressed()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                UpdatePage();
            }
        }

        private void OnNextButtonPressed()
        {
            if (_currentPage < _pages.Count - 1)
            {
                _currentPage++;
                UpdatePage();
            }
        }

        public void Initialize(string content)
        {
            _pendingContent = content;
            if (IsInsideTree() && _contentLabel != null)
            {
                UpdateContent(content);
            }
        }
        
        //解析书籍内容
        private void ParseContent(string input)
        {
            string content = input.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] splits = Regex.Split(content, @"\[pagebreak\]|\n---+");

            foreach (var pageContent in splits)
            {
                string trimmed = pageContent.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                string renderedPage = ParseSkyrimHtmlToBBCode(trimmed);
                if (!string.IsNullOrWhiteSpace(renderedPage))
                    _pages.Add(renderedPage);
            }

            if (_pages.Count == 0)
                _pages.Add(L.T("MSG_EMPTY_PAGE"));
        }

        //解析HTML为BBCode格式（修复大量闭合标签，可能还有遗漏？只测试了部分模组）
        private string ParseSkyrimHtmlToBBCode(string input)
        {
            string output = input;

            output = output.Replace("</i>", "");
            output = output.Replace("</b>", "");
            output = output.Replace("</font>", "");

            output = Regex.Replace(output, @"<font\s+size=['""](\d+)['""]>", "[font_size=$1]", RegexOptions.IgnoreCase);

            output = Regex.Replace(output, @"<font\s+face=[""'](.*?)[""']>", (m) => {
                string f = m.Groups[1].Value.ToLower();
                return f.Contains("handwritten") ? "[i][color=tan]" : "[b]";
            }, RegexOptions.IgnoreCase);

            output = Regex.Replace(output, @"</p>|</div>", "", RegexOptions.IgnoreCase);
            output = Regex.Replace(output, @"<p\s+align=[""']center[""']>", "\n[center]", RegexOptions.IgnoreCase);
            output = Regex.Replace(output, @"<p\s+align=[""']left[""']>", "\n[left]", RegexOptions.IgnoreCase);
            output = Regex.Replace(output, @"<p>", "\n", RegexOptions.IgnoreCase);

            string imgPattern = @"<img\s+.*?src=[""'](.*?)[""'].*?>";
            output = Regex.Replace(output, imgPattern, (m) => {
                string imgName = System.IO.Path.GetFileName(m.Groups[1].Value);
                return $"\n[center][color=yellow]🖼️ [{L.T("IMAGE")}: {imgName}][/color][/center]\n";
            }, RegexOptions.IgnoreCase);

            output = output.Replace("<br>", "\n");
            output = Regex.Replace(output, @"<b>(.*?)</b>", "[b]$1[/b]", RegexOptions.IgnoreCase);

            int fontStartCount = Regex.Matches(output, @"\[font_size=\d+\]").Count;
            int fontEndCount = Regex.Matches(output, @"\[/font_size\]").Count;
            while (fontEndCount > fontStartCount)
            {
                output = output.Replace("[/font_size]", "");
                fontEndCount--;
            }

            int colorStartCount = Regex.Matches(output, @"\[color=\w+\]").Count;
            int colorEndCount = Regex.Matches(output, @"\[/color\]").Count;
            while (colorEndCount > colorStartCount)
            {
                output = output.Replace("[/color]", "");
                colorEndCount--;
            }

            int iStartCount = Regex.Matches(output, @"\[i\]").Count;
            int iEndCount = Regex.Matches(output, @"\[/i\]").Count;
            while (iEndCount > iStartCount)
            {
                output = output.Replace("[/i]", "");
                iEndCount--;
            }

            int bStartCount = Regex.Matches(output, @"\[b\]").Count;
            int bEndCount = Regex.Matches(output, @"\[/b\]").Count;
            while (bEndCount > bStartCount)
            {
                output = output.Replace("[/b]", "");
                bEndCount--;
            }

            output = output.TrimStart('\n');

            return output;
        }

        public void UpdateContent(string newContent)
        {
            _pages.Clear();
            ParseContent(newContent);
            _currentPage = 0;
            UpdatePage();
        }

        private void UpdatePage()
        {
            if (_pages.Count == 0 || _contentLabel == null) return;

            _contentLabel.Text = _pages[_currentPage];
            _pageInfo.Text = L.T("MSG_PAGE_INFO").Replace("{current}", (_currentPage + 1).ToString()).Replace("{total}", _pages.Count.ToString());
            _prevButton.Disabled = _currentPage <= 0;
            _nextButton.Disabled = _currentPage >= _pages.Count - 1;

            _contentLabel.ForceUpdateTransform();
        }
    }
}
