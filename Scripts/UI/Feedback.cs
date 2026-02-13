//反馈对话框管理
using Godot;
using System;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    public partial class Feedback : Node
    {
        private static Feedback _instance;
        private AcceptDialog _currDlg;

        public static Feedback Instance => _instance ??= new Feedback();

        private Feedback() { }

        public void Show(string titleKey, string messageKey, params object[] args)
        {
            if (_currDlg != null)
            {
                _currDlg.Hide();
                _currDlg.GetParent()?.RemoveChild(_currDlg);
                _currDlg = null;
            }

            var dialog = new AcceptDialog();
            dialog.Title = L.T(titleKey);
            dialog.DialogText = args.Length > 0 ? string.Format(L.T(messageKey), args) : L.T(messageKey);

            var okBtn = dialog.GetOkButton();
            okBtn.CustomMinimumSize = new Vector2(130, 45);
            dialog.Confirmed += OnDialogConfirmed;

            var mainLoop = Godot.Engine.GetMainLoop();
            if (mainLoop is SceneTree sceneTree)
            {
                sceneTree.Root.AddChild(dialog);
                dialog.PopupCentered();
                _currDlg = dialog;
            }
        }

        public void ShowMessage(string titleKey, string message)
        {
            if (_currDlg != null)
            {
                _currDlg.Hide();
                _currDlg.GetParent()?.RemoveChild(_currDlg);
                _currDlg = null;
            }

            var dialog = new AcceptDialog();
            dialog.Title = L.T(titleKey);
            dialog.DialogText = message;

            var okBtn = dialog.GetOkButton();
            okBtn.CustomMinimumSize = new Vector2(130, 45);
            dialog.Confirmed += OnDialogConfirmed;

            var mainLoop = Godot.Engine.GetMainLoop();
            if (mainLoop is SceneTree sceneTree)
            {
                sceneTree.Root.AddChild(dialog);
                dialog.PopupCentered();
                _currDlg = dialog;
            }
        }

        private void OnDialogConfirmed()
        {
            if (_currDlg != null)
            {
                _currDlg.Hide();
                _currDlg.GetParent()?.RemoveChild(_currDlg);
                _currDlg = null;
            }
        }

        public void ShowConfirm(string titleKey, string messageKey, Action onConfirm, Action onCancel = null, params object[] args)
        {
            if (_currDlg != null)
            {
                _currDlg.Hide();
                _currDlg.GetParent()?.RemoveChild(_currDlg);
                _currDlg = null;
            }

            var dialog = new ConfirmationDialog();
            dialog.Title = L.T(titleKey);
            dialog.DialogText = args.Length > 0 ? string.Format(L.T(messageKey), args) : L.T(messageKey);

            var okBtn = dialog.GetOkButton();
            okBtn.Text = L.T("BTN_YES");
            okBtn.CustomMinimumSize = new Vector2(130, 45);

            var cancelBtn = dialog.GetCancelButton();
            cancelBtn.Text = L.T("BTN_NO");
            cancelBtn.CustomMinimumSize = new Vector2(130, 45);

            dialog.Confirmed += () =>
            {
                onConfirm?.Invoke();
                OnDialogConfirmed();
            };

            dialog.Canceled += () =>
            {
                onCancel?.Invoke();
                OnDialogConfirmed();
            };

            var mainLoop = Godot.Engine.GetMainLoop();
            if (mainLoop is SceneTree sceneTree)
            {
                sceneTree.Root.AddChild(dialog);
                dialog.PopupCentered();
                _currDlg = dialog;
            }
        }
    }
}
