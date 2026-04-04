//批量导入窗口，用来一次性导入多个文件
using Godot;
using System;
using System.IO;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    public partial class BatchImport : Window
    {
        [Signal] public delegate void ContentSubmittedEventHandler(string content, bool trimSpaces);
        private TextEdit _textEdit;
        private Label _importTipLabel;
        private CheckBox _chkTrimSpaces;

        public BatchImport()
        {
            Visible = false;
        }

        public override void _Ready()
        {
            Title = L.T("WIN_BATCH_IMPORT");
            Size = new Vector2I(800, 600);
            CloseRequested += OnCloseRequested;

            SkyrimModTranslator.Core.Theme.ApplyStdBg(this);

            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            marginContainer.AddThemeConstantOverride("margin_top", 25);
            marginContainer.AddThemeConstantOverride("margin_bottom", 25);
            marginContainer.AddThemeConstantOverride("margin_left", 30);
            marginContainer.AddThemeConstantOverride("margin_right", 30);
            AddChild(marginContainer);

            var innerVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            innerVBox.AddThemeConstantOverride("separation", 18);
            marginContainer.AddChild(innerVBox);

            _importTipLabel = new Label { Text = L.T("IMPORT_TIP") };
            innerVBox.AddChild(_importTipLabel);

            _textEdit = new TextEdit { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            _textEdit.PlaceholderText = L.T("IMPORT_PLACEHOLDER");
            
            innerVBox.AddChild(_textEdit);

            var bottomHBox = new HBoxContainer();
            bottomHBox.AddThemeConstantOverride("separation", 15);
            innerVBox.AddChild(bottomHBox);

            var btnSubmit = new Button { Text = L.T("BTN_SUBMIT"), CustomMinimumSize = new Vector2(100, 40) };
            btnSubmit.Pressed += OnSubmitPressed;
            bottomHBox.AddChild(btnSubmit);

            var btnCancel = new Button { Text = L.T("BTN_CANCEL"), CustomMinimumSize = new Vector2(100, 40) };
            btnCancel.Pressed += OnCancelPressed;
            bottomHBox.AddChild(btnCancel);

            _chkTrimSpaces = new CheckBox();
            _chkTrimSpaces.Text = L.T("CHK_TRIM_SPACES");
            _chkTrimSpaces.ButtonPressed = true;
            bottomHBox.AddChild(_chkTrimSpaces);

            Visible = true;
            Pos.RestoreWindowState(this);
        }

        private void OnCloseRequested()
        {
            Pos.SaveWindowState(this);
            QueueFree();
        }

        private void OnSubmitPressed()
        {
            Pos.SaveWindowState(this);
            EmitSignal(SignalName.ContentSubmitted, _textEdit.Text, _chkTrimSpaces.ButtonPressed);
            QueueFree();
        }

        private void OnCancelPressed()
        {
            Pos.SaveWindowState(this);
            QueueFree();
        }

        public void RefreshLocalization()
        {
            Title = L.T("WIN_BATCH_IMPORT");
            if (_textEdit != null)
                _textEdit.PlaceholderText = L.T("IMPORT_PLACEHOLDER");
            if (_importTipLabel != null)
                _importTipLabel.Text = L.T("IMPORT_TIP");
        }
    }
}