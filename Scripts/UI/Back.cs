//备份管理窗口
using Godot;
using System;
using System.IO;
using System.Linq;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    public partial class Back : Window
    {
        [Signal]
        public delegate void RestoredEventHandler();

        private ItemList _list;
        private Button _btnRestore;
        private Button _btnDel;
        private Button _btnRef;
        private Label _lblStat;

        private string _target;

        public Back()
        {
        }

        public Back(string targetFilePath)
        {
            _target = targetFilePath;
        }

        public override void _Ready()
        {
            Title = L.T("BTN_BACKUP");
            MinSize = new Vector2I(800, 600);
            Size = new Vector2I(800, 600);
            CloseRequested += OnCloseRequested;
            SkyrimModTranslator.Core.Theme.ApplyStdBg(this);
            SetupUI();
            SetupConnections();
            LoadBackups();
            Pos.RestoreWindowState(this);
        }

        private void OnCloseRequested()
        {
            Pos.SaveWindowState(this);
            QueueFree();
        }

        private void SetupUI()
        {
            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            marginContainer.AddThemeConstantOverride("margin_top", 25);
            marginContainer.AddThemeConstantOverride("margin_bottom", 25);
            marginContainer.AddThemeConstantOverride("margin_left", 30);
            marginContainer.AddThemeConstantOverride("margin_right", 30);
            AddChild(marginContainer);
            var mainVBox = new VBoxContainer();
            mainVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVBox.AddThemeConstantOverride("separation", 18);
            marginContainer.AddChild(mainVBox);
            var infoLabel = new Label();
            infoLabel.Text = string.IsNullOrEmpty(_target) ? L.T("ERR_NO_FILE") : string.Format(L.T("TARGET_FILE"), Path.GetFileName(_target));
            mainVBox.AddChild(infoLabel);
            _list = new ItemList();
            _list.SelectMode = ItemList.SelectModeEnum.Multi;
            _list.AllowRmbSelect = true;
            _list.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVBox.AddChild(_list);
            var buttonHBox = new HBoxContainer();
            buttonHBox.AddThemeConstantOverride("separation", 15);
            mainVBox.AddChild(buttonHBox);
            _btnRestore = new Button();
            _btnRestore.Text = L.T("BTN_RESTORE");
            _btnRestore.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            buttonHBox.AddChild(_btnRestore);
            _btnDel = new Button();
            _btnDel.Text = L.T("BTN_DELETE");
            _btnDel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            buttonHBox.AddChild(_btnDel);
            _btnRef = new Button();
            _btnRef.Text = L.T("BTN_REFRESH");
            _btnRef.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            buttonHBox.AddChild(_btnRef);
            _lblStat = new Label();
            _lblStat.Text = L.T("STATUS_READY");
            mainVBox.AddChild(_lblStat);
        }

        private void SetupConnections()
        {
            _btnRestore.Pressed += OnRestorePressed;
            _btnDel.Pressed += OnDeletePressed;
            _btnRef.Pressed += OnRefreshPressed;
            _list.ItemSelected += OnBackupSelected;
        }

        private void LoadBackups()
        {
            _list.Clear();
            if (string.IsNullOrEmpty(_target))
            {
                _list.AddItem(L.T("ERR_NO_FILE"));
                UpdateStat(L.T("ERR_NO_FILE"));
                return;
            }
            var backups = GetBackups(_target);
            if (backups.Length == 0)
            {
                _list.AddItem(L.T("ERR_NO_BACKUPS"));
                UpdateStat(L.T("ERR_NO_BACKUPS"));
                return;
            }
            foreach (var backupPath in backups)
            {
                var fileInfo = new FileInfo(backupPath);
                string timestamp = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                string fileSize = FormatFileSize(fileInfo.Length);
                string itemText = $"{timestamp} - {fileSize} - {Path.GetFileName(backupPath)}";
                _list.AddItem(itemText);
                _list.SetItemMetadata(_list.ItemCount - 1, backupPath);
            }
            UpdateStat(string.Format(L.T("STATUS_BACKUPS_LOADED"), _list.ItemCount));
        }

        private string[] GetBackups(string targetFilePath)
        {
            string directory = Path.GetDirectoryName(targetFilePath);
            if (string.IsNullOrEmpty(directory))
            {
                return new string[0];
            }
            string fileName = Path.GetFileName(targetFilePath);
            string backupPattern = $"{fileName}.bak.*";
            try
            {
                return Directory.GetFiles(directory, backupPattern).OrderByDescending(f => File.GetLastWriteTime(f)).ToArray();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Back] Get backups fail: {e.Message}");
                return new string[0];
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void OnBackupSelected(long index)
        {
            var backupPath = _list.GetItemMetadata((int)index).AsString();
            if (!string.IsNullOrEmpty(backupPath))
            {
                var fileInfo = new FileInfo(backupPath);
                UpdateStat(string.Format(L.T("STAT_BACKUP_SELECTED"), fileInfo.Name, FormatFileSize(fileInfo.Length)));
            }
        }

        private ConfirmationDialog _restoreDialog;

        private void OnRestorePressed()
        {
            var selectedItems = _list.GetSelectedItems();
            if (selectedItems == null || selectedItems.Length == 0)
            {
                UpdateStat(L.T("ERR_SELECT_BACKUP"));
                return;
            }
            var index = (int)selectedItems[0];
            var backupPath = _list.GetItemMetadata(index).AsString();
            if (string.IsNullOrEmpty(backupPath))
            {
                UpdateStat(L.T("ERR_INVALID_BACKUP"));
                return;
            }
            _restoreDialog = new ConfirmationDialog();
            _restoreDialog.Title = L.T("CONFIRM_TITLE");
            _restoreDialog.DialogText = string.Format(L.T("CONFIRM_RESTORE_TXT"), Path.GetFileName(backupPath));
            _restoreDialog.GetOkButton().Text = L.T("BTN_YES");
            _restoreDialog.GetCancelButton().Text = L.T("BTN_NO");
            AddChild(_restoreDialog);
            _restoreDialog.PopupCentered();
            _restoreDialog.Confirmed += OnRestoreConfirm;
            _restoreDialog.SetMeta("backupPath", backupPath);
        }

        private void OnRestoreConfirm()
        {
            if (_restoreDialog != null)
            {
                var backupPath = _restoreDialog.GetMeta("backupPath").AsString();
                if (!string.IsNullOrEmpty(backupPath))
                {
                    ConfirmRestore(backupPath);
                }
            }
        }
        
        private void ConfirmRestore(string backupPath)
        {
            try
            {
                Restore(backupPath, _target);
                UpdateStat(string.Format(L.T("STAT_BACKUP_RESTORED"), Path.GetFileName(backupPath)));
                EmitSignal(SignalName.Restored);
                LoadBackups();
                UpdateStat(L.T("STAT_RESTORE_OK"));
            }
            catch (Exception ex)
            {
                UpdateStat(string.Format(L.T("ERR_RESTORE_FAILED"), ex.Message));
            }
        }

        private void Restore(string backupPath, string targetFilePath)
        {
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException($"[Back] 备份文件不存在: {backupPath}");
            }
            string tempBackupPath = targetFilePath + ".temp.bak";
            if (File.Exists(targetFilePath))
            {
                File.Copy(targetFilePath, tempBackupPath, true);
            }
            try
            {
                File.Copy(backupPath, targetFilePath, true);
            }
            catch
            {
                if (File.Exists(tempBackupPath))
                {
                    File.Copy(tempBackupPath, targetFilePath, true);
                }
                throw;
            }
            finally
            {
                if (File.Exists(tempBackupPath))
                {
                    File.Delete(tempBackupPath);
                }
            }
        }

        private ConfirmationDialog _deleteDialog;

        private void OnDeletePressed()
        {
            var selectedItems = _list.GetSelectedItems();
            if (selectedItems == null || selectedItems.Length == 0)
            {
                UpdateStat(L.T("ERR_SELECT_BACKUP"));
                return;
            }
            _deleteDialog = new ConfirmationDialog();
            _deleteDialog.Title = L.T("CONFIRM_TITLE");
            _deleteDialog.DialogText = string.Format(L.T("CONFIRM_DEL_BATCH"), selectedItems.Length);
            _deleteDialog.GetOkButton().Text = L.T("BTN_YES");
            _deleteDialog.GetCancelButton().Text = L.T("BTN_NO");
            AddChild(_deleteDialog);
            _deleteDialog.PopupCentered();
            _deleteDialog.Confirmed += OnDeleteConfirm;
            _deleteDialog.SetMeta("selectedItems", selectedItems);
        }

        private void OnDeleteConfirm()
        {
            if (_deleteDialog != null)
            {
                var selectedItems = (long[])_deleteDialog.GetMeta("selectedItems");
                if (selectedItems != null && selectedItems.Length > 0)
                {
                    ConfirmDelete(selectedItems);
                }
            }
        }

        private void ConfirmDelete(long[] selectedItems)
        {
            try
            {
                var sortedIndices = selectedItems.OrderByDescending(i => i).ToArray();
                int deletedCount = 0;
                foreach (long idx in sortedIndices)
                {
                    var backupPath = _list.GetItemMetadata((int)idx).AsString();
                    if (!string.IsNullOrEmpty(backupPath))
                    {
                        DeleteBackup(backupPath);
                        deletedCount++;
                    }
                }
                UpdateStat(string.Format(L.T("STAT_BACKUP_DELETED_BATCH"), deletedCount));
                LoadBackups();
            }
            catch (Exception ex)
            {
                UpdateStat(string.Format(L.T("ERR_DELETE_FAILED"), ex.Message));
            }
        }

        private void DeleteBackup(string backupPath)
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }

        private void OnRefreshPressed()
        {
            LoadBackups();
        }

        private void UpdateStat(string message)
        {
            _lblStat.Text = message;
        }
    }
}
