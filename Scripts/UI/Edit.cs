//翻译编辑窗口
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

using TransEntry = SkyrimModTranslator.Core.Data.TransEntry;

namespace SkyrimModTranslator.UI
{
    public partial class Edit : Window
    {
        private Label _lblRecType;
        private Label _lblFormId;
        private Label _lblFieldType;
        private TextEdit _editOrig;
        private TextEdit _editTrans;
        private Button _btnSave;
        private Label _lblStat;
        private Button _btnBookPrev;
        private CheckBox _chkWrap;
        private bool _isDistMode = false;
        private VBoxContainer _vboxWordRef;

        private SkyrimModTranslator.Core.Data.Item _it;
        private string _origText;

        [Signal]
        public delegate void TextSavedEventHandler(string trans, string ori);

        [Signal]
        public delegate void ChangedNotifyEventHandler();

        public Edit(SkyrimModTranslator.Core.Data.Item it)
        {
            Visible = false;
            _it = it;
            _isDistMode = (it.Type == "GLOSSARY");
        }

        public void LoadData(SkyrimModTranslator.Core.Data.Item it) { _it = it; RefreshUI(); }

        public override void _Ready()
        {
            Title = L.T("WIN_EDIT");
            MinSize = new Vector2I(800, 600);
            Size = new Vector2I(800, 600);
            
            Transient = true;

            CloseRequested += OnCloseRequested;

            SetupUI();
            SetupConnections();
            LoadEntryData();

            LateInitialize();
        }

        private void OnCloseRequested()
        {
            Pos.SaveWindowState(this);
            QueueFree();
        }

        private void LateInitialize()
        {
            CallDeferred(MethodName.UpdateWindowSizeFix);
        }

        private void UpdateWindowSizeFix()
        {
            Size += new Vector2I(1, 1);
            Size -= new Vector2I(1, 1);
            Visible = true;
            Pos.RestoreWindowState(this);
        }

        private void SetupUI()
        {
            SkyrimModTranslator.Core.Theme.ApplyStdBg(this);

            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            marginContainer.AddThemeConstantOverride("margin_left", 30);
            marginContainer.AddThemeConstantOverride("margin_right", 30);
            marginContainer.AddThemeConstantOverride("margin_top", 15);
            marginContainer.AddThemeConstantOverride("margin_bottom", 15);
            AddChild(marginContainer);

            VBoxContainer mainVBox = new VBoxContainer();
            mainVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVBox.AddThemeConstantOverride("separation", 10);
            marginContainer.AddChild(mainVBox);

            var infoHBox = new HBoxContainer();
            infoHBox.AddThemeConstantOverride("separation", 15);
            mainVBox.AddChild(infoHBox);

            _lblRecType = new Label();
            _lblFormId = new Label();
            _lblFieldType = new Label();
            infoHBox.AddChild(_lblRecType);
            infoHBox.AddChild(_lblFormId);
            infoHBox.AddChild(_lblFieldType);

            _lblRecType.Visible = !_isDistMode;
            _lblFormId.Visible = !_isDistMode;
            _lblFieldType.Visible = !_isDistMode;

            var contentHBox = new HBoxContainer();
            contentHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            contentHBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            contentHBox.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(contentHBox);

            var refVBox = new VBoxContainer();
            refVBox.CustomMinimumSize = new Vector2(150, 0);
            refVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            refVBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
            contentHBox.AddChild(refVBox);

            var refHeader = new HBoxContainer { CustomMinimumSize = new Vector2(0, 35) };
            refVBox.AddChild(refHeader);

            var refLabel = new Label { Text = L.T("LABEL_REF_SIDEBAR"), VerticalAlignment = VerticalAlignment.Center };
            refHeader.AddChild(refLabel);

            var refScroll = new ScrollContainer();
            refScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            refVBox.AddChild(refScroll);

            _vboxWordRef = new VBoxContainer();
            _vboxWordRef.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            refScroll.AddChild(_vboxWordRef);

            var splitContainer = new HSplitContainer();
            splitContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            splitContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            contentHBox.AddChild(splitContainer);

            var leftVBox = new VBoxContainer();
            leftVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            splitContainer.AddChild(leftVBox);

            var originalHeader = new HBoxContainer { CustomMinimumSize = new Vector2(0, 35) };
            leftVBox.AddChild(originalHeader);

            var originalLabel = new Label { Text = L.T("LABEL_ORIGINAL"), VerticalAlignment = VerticalAlignment.Center };
            originalHeader.AddChild(originalLabel);

            _chkWrap = new CheckBox { Text = L.T("CHK_WRAP"), ButtonPressed = true, FocusMode = Control.FocusModeEnum.None };
            _chkWrap.Toggled += OnCheckWrapToggled;
            originalHeader.AddChild(_chkWrap);

            _editOrig = new TextEdit {
                Editable = true,
                WrapMode = TextEdit.LineWrappingMode.Boundary,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 350)
            };
            if (!_isDistMode)
            {
                _editOrig.TextChanged += OnOriginalTextChanged;
            }
            leftVBox.AddChild(_editOrig);

            var rightVBox = new VBoxContainer();
            rightVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            splitContainer.AddChild(rightVBox);

            var translatedHeader = new HBoxContainer { CustomMinimumSize = new Vector2(0, 35) };
            rightVBox.AddChild(translatedHeader);

            var translatedLabel = new Label { Text = L.T("LABEL_TRANSLATED"), VerticalAlignment = VerticalAlignment.Center };
            translatedHeader.AddChild(translatedLabel);

            _editTrans = new TextEdit {
                WrapMode = TextEdit.LineWrappingMode.Boundary,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 350)
            };
            rightVBox.AddChild(_editTrans);

            var bottomHBox = new HBoxContainer();
            bottomHBox.AddThemeConstantOverride("separation", 15);
            mainVBox.AddChild(bottomHBox);

            _btnBookPrev = new Button { Text = "📔 " + L.T("BTN_PREVIEW_BOOK"), Visible = false, CustomMinimumSize = new Vector2(100, 35) };
            _btnBookPrev.Pressed += OnBookPreviewPressed;
            bottomHBox.AddChild(_btnBookPrev);

            var horizontalSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bottomHBox.AddChild(horizontalSpacer);

            _btnSave = new Button { Text = L.T("BTN_SAVE_CLOSE"), CustomMinimumSize = new Vector2(200, 45) };
            bottomHBox.AddChild(_btnSave);

            _lblStat = new Label { Visible = false };
        }

        private void OnCheckWrapToggled(bool toggled)
        {
            var mode = toggled ? TextEdit.LineWrappingMode.Boundary : TextEdit.LineWrappingMode.None;
            _editOrig.WrapMode = mode;
            _editTrans.WrapMode = mode;
        }

        private void SetupConnections()
        {
            _btnSave.Pressed += OnSavePressed;
        }

        private void LoadEntryData()
        {
            if (_it == null)
                return;

            _lblRecType.Text = string.Format("{0}: {1}", L.T("LABEL_TYPE"), _it.Type);
            _lblFormId.Text = string.Format("{0}: {1:X8}", L.T("LABEL_FORMID"), _it.FormID);
            _lblFieldType.Text = string.Format("{0}: {1} (索引: {2})", L.T("LABEL_FIELD_TYPE"), _it.FType, _it.FIdx);

            _btnBookPrev.Visible = (_it.Type == "BOOK" && _it.FType == "DESC");

            _origText = _it.Ori;
            _editOrig.Text = _origText;
            _editTrans.Text = _it.IsDictApplied ? "" : (_it.Trans ?? "");

            UpdateWordReference(_origText);
        }

        private void RefreshUI()
        {
            LoadEntryData();
        }
        
        //更新单词引用
        private void UpdateWordReference(string text)
        {
            foreach (var child in _vboxWordRef.GetChildren())
            {
                child.QueueFree();
            }

            var contentHBox = _vboxWordRef.GetParent().GetParent().GetParent() as HBoxContainer;
            var refVBox = _vboxWordRef.GetParent().GetParent() as VBoxContainer;
            var splitContainer = contentHBox.GetChild(1) as HSplitContainer;

            int foundWords = 0;

            if (!string.IsNullOrEmpty(text) && text.Length < 10000)
            {
                var words = Regex.Matches(text, @"\b[A-Za-z']{3,}\b")
                                 .Cast<Match>()
                                 .Select(m => m.Value.ToLower())
                                 .Distinct()
                                 .Take(50);

                foreach (var word in words)
                {
                    string trans = FindWordTranslation(word);
                    if (!string.IsNullOrEmpty(trans))
                    {
                        var btn = new Button { 
                            Text = $"{word} → {trans}", 
                            Alignment = HorizontalAlignment.Left,
                            Flat = true,
                            FocusMode = Control.FocusModeEnum.None
                        };
                        _vboxWordRef.AddChild(btn);
                        foundWords++;
                        
                        if (foundWords >= 30)
                        {
                            break;
                        }
                    }
                }
            }

            if (foundWords == 0)
            {
                refVBox.Visible = false;
                splitContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            }
            else
            {
                refVBox.Visible = true;
                splitContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            }
        }

        private Dictionary<string, string> _wordTranslationCache;

        private string FindWordTranslation(string word)
        {
            if (_wordTranslationCache != null && _wordTranslationCache.TryGetValue(word, out string cachedTrans))
            {
                return cachedTrans;
            }

            string translation = string.Empty;

            if (_wordTranslationCache == null)
            {
                _wordTranslationCache = new Dictionary<string, string>();
            }
            _wordTranslationCache[word] = translation;

            return translation;
        }

        private void SaveCurrentTranslation()
        {
            if (_it == null) return;

            string finalTrans = _editTrans.Text.Trim();
            string finalOri = _editOrig.Text.Trim();
            
            if (!string.IsNullOrEmpty(finalTrans))
            {
                _it.IsDictApplied = false;
            }
            
            if (_isDistMode)
            {
                if (!string.IsNullOrEmpty(finalOri))
                {
                    _it.Ori = finalOri;
                }
            }
            
            _it.Trans = finalTrans;
            
            EmitSignal(SignalName.ChangedNotify);
        }
        
        //保存翻译
        private void OnSavePressed()
        {
            if (_it == null) return;

            string input = _editTrans.Text; 
            string original = _editOrig.Text;
            
            string normO = _it.Ori.Replace("\r\n", "\n").Trim();
            string normT = input.Replace("\r\n", "\n").Trim();

            if (string.IsNullOrEmpty(normT) || normT == normO)
            {
                if (MainSingleton.Instance != null)
                {
                    MainSingleton.Instance.AutoFillOrClear(_it);
                }
            }
            else
            {
                _it.Trans = input;
                _it.IsDictApplied = false;
                
                if (MainSingleton.Instance != null)
                {
                    int sameCount = MainSingleton.Instance.GetSameOriginalCount(_it.Ori);
                    if (sameCount > 1)
                    {
                        ConfirmationDialog dialog = new ConfirmationDialog();
                        dialog.Title = L.T("DIALOG_UNIFY_TRANSLATION_TITLE");
                        dialog.AddChild(new Label { Text = string.Format(L.T("DIALOG_UNIFY_TRANSLATION_TEXT"), sameCount - 1) });
                        dialog.OkButtonText = L.T("BTN_APPLY");
                        dialog.CancelButtonText = L.T("BTN_CANCEL");
                        GetTree().Root.AddChild(dialog);
                        
                        dialog.SetMeta("input", input);
                        dialog.SetMeta("original", original);
                        dialog.Confirmed += OnConfirmDialogConfirmed;
                        dialog.Canceled += OnConfirmDialogCanceled;
                        
                        dialog.PopupCentered();
                        return;
                    }
                }
            }

            if (_isDistMode)
            {
                EmitSignal(SignalName.TextSaved, input, original);
            }
            else
            {
                EmitSignal(SignalName.ChangedNotify);
            }
            EmitSignal(SignalName.CloseRequested);
        }
        
        //更新预览。检测是否需要预览书籍
        public void UpdatePreviewConnectivity(bool canPreview)
        {
            if (canPreview)
            {
                _btnBookPrev.Visible = true;
            }
            else
            {
                _btnBookPrev.Visible = false;
            }
        }
        
        private void OnOriginalTextChanged()
        {
            if (_editOrig.Text != _origText) {
                _editOrig.Text = _origText;
            }
        }
        
        private void OnConfirmDialogConfirmed()
        {
            var dialog = (ConfirmationDialog)GetTree().Root.GetChild(GetTree().Root.GetChildCount() - 1);
            if (dialog != null)
            {
                string input = (string)dialog.GetMeta("input");
                string original = (string)dialog.GetMeta("original");
                
                MainSingleton.Instance.ApplyTranslationToSameOriginal(_it.Ori, input);
                
                if (_isDistMode)
                {
                    EmitSignal(SignalName.TextSaved, input, original);
                }
                else
                {
                    EmitSignal(SignalName.ChangedNotify);
                }
                dialog.QueueFree();
                EmitSignal(SignalName.CloseRequested);
            }
        }
        
        private void OnConfirmDialogCanceled()
        {
            var dialog = (ConfirmationDialog)GetTree().Root.GetChild(GetTree().Root.GetChildCount() - 1);
            if (dialog != null)
            {
                string input = (string)dialog.GetMeta("input");
                string original = (string)dialog.GetMeta("original");
                
                if (_isDistMode)
                {
                    EmitSignal(SignalName.TextSaved, input, original);
                }
                else
                {
                    EmitSignal(SignalName.ChangedNotify);
                }
                dialog.QueueFree();
                EmitSignal(SignalName.CloseRequested);
            }
        }
        
        public void ManualSave()
        {
            if (_it == null || string.IsNullOrWhiteSpace(_editTrans.Text)) return;
            
            _it.Trans = _editTrans.Text.Trim();
            _it.IsDictApplied = false;
            
            EmitSignal(SignalName.ChangedNotify);
        }

        public void UpdateEntry(SkyrimModTranslator.Core.Data.Item entry)
        {
            if (_it != null)
            {
                _it.Trans = _editTrans.Text.Trim();
            }

            _it = entry;

            if (_it != null)
            {
                LoadEntryData();
                Title = string.Format(L.T("WIN_EDIT_ID"), _it.FIdx);
            }
        }

        private Book _bookWin = null;
        private void OnBookPreviewPressed()
        {
            if (_bookWin != null && IsInstanceValid(_bookWin))
            {
                _bookWin.GrabFocus();
                return;
            }

            string content = string.IsNullOrEmpty(_editTrans.Text)
                ? _it.Ori
                : _editTrans.Text;

            _bookWin = new Book();
            GetTree().Root.AddChild(_bookWin);

            _bookWin.Initialize(content);

            _bookWin.CloseRequested += OnBookWinCloseRequested;

            _bookWin.Show();
        }

        private void OnBookWinCloseRequested()
        {
            _bookWin = null;
        }

        private void UpdateStatus(string message)
        {
            _lblStat.Text = message;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey k && k.Pressed && k.Keycode == Key.Enter)
            {
                if (_editTrans.HasFocus())
                {
                    _editTrans.InsertTextAtCaret("\n");
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public void RefreshLocalization()
        {
            Title = L.T("WIN_EDIT");
            _chkWrap.Text = L.T("CHK_WRAP");
            _btnSave.Text = L.T("BTN_SAVE_CLOSE");
            _btnBookPrev.Text = L.T("BTN_PREVIEW_BOOK");
        }

        public void FocusTranslatedTextEdit()
        {
            _editTrans.GrabFocus();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
        }


    }
}
