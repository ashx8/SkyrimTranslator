//设置窗口
using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    public partial class Set : Window
    {
        [Signal]
        public delegate void SettingsChangedEventHandler();

        private static Set _instance;
        public static Set Instance
        {
            get
            {
                if (_instance == null || _instance.IsQueuedForDeletion())
                {
                    _instance = new Set();
                }
                else
                {
                    _instance.Show();
                    _instance.GrabFocus();
                }
                return _instance;
            }
        }

        private float _currentOpacity;
        private string _selectedBgPath;
        private string _savedBgPath;
        private TextureRect _previewRect;
        private Label _labelCurrentPreview;
        private Button _btnSelectBg;
        private Button _btnSave;
        private Tree _tree;
        private PopupMenu _popupMenu;
        private Dictionary<string, List<string>> _tempTranslationMap;
        private Dictionary<string, List<string>> _tempCategories;

        private Set()
        {
            Visible = false;
            _savedBgPath = SkyrimModTranslator.Core.Theme.CurrentBackgroundPath;
            _selectedBgPath = _savedBgPath;
            _currentOpacity = SkyrimModTranslator.Core.Theme.BackgroundOpacity;
        }

        public override void _Ready()
        {
            Visible = false;
            WinPersist.Load(this, "set", new Vector2I(720, 520));
            MinSize = new Vector2I(700, 500);
            Unresizable = false;

            SkyrimModTranslator.Core.Theme.ApplyStdBg(this);

            var rootVBox = new VBoxContainer();
            rootVBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            rootVBox.AddThemeConstantOverride("margin", 20);
            AddChild(rootVBox);

            var tabs = new TabContainer();
            tabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            rootVBox.AddChild(tabs);

            var translationMapScroll = new ScrollContainer();
            translationMapScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            tabs.AddChild(translationMapScroll);
            translationMapScroll.AddChild(CreateTranslationMapTab());

            var themeVBox = new VBoxContainer();
            themeVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            themeVBox.AddThemeConstantOverride("separation", 15);
            tabs.AddChild(themeVBox);

            tabs.SetTabTitle(0, L.T("TAB_TRANSLATION_MAP"));
            tabs.SetTabTitle(1, L.T("TAB_THEME"));

            _btnSelectBg = new Button();
            _btnSelectBg.Text = L.T("BTN_SELECT_BG");
            _btnSelectBg.CustomMinimumSize = new Vector2(0, 45);
            _btnSelectBg.Pressed += OnSelectBgPressed;
            themeVBox.AddChild(_btnSelectBg);

            _labelCurrentPreview = new Label();
            _labelCurrentPreview.Text = L.T("LABEL_CURRENT_PREVIEW");
            themeVBox.AddChild(_labelCurrentPreview);
            _previewRect = new TextureRect();
            _previewRect.CustomMinimumSize = new Vector2(0, 200);
            _previewRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _previewRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            themeVBox.AddChild(_previewRect);
            UpdateImagePreview(Pos.GetSetting("bg_path", ""));

            _btnSave = new Button();
            _btnSave.Text = L.T("BTN_SAVE_SETTINGS");
            _btnSave.CustomMinimumSize = new Vector2(200, 50);
            _btnSave.Pressed += OnSavePressed;
            rootVBox.AddChild(_btnSave);

            CloseRequested += OnCloseRequested;
            L.OnLanguageChanged += RefreshLocalization;
            Show();
        }

        private void OnSelectBgPressed()
        {
            var fd = new FileDialog();
            fd.UseNativeDialog = true;
            fd.FileMode = FileDialog.FileModeEnum.OpenFile;
            fd.Access = FileDialog.AccessEnum.Filesystem;
            fd.Filters = new string[] { "*.png,*.jpg,*.jpeg ; 图片文件" };
            fd.CurrentDir = Pos.GetSetting("last_image_dir", "");
            fd.FileSelected += OnFileSelected;
            fd.Canceled += () => {
                // 确保设置窗口保持在顶层
                Show();
                GrabFocus();
            };
            AddChild(fd);
            fd.PopupCentered();
        }

        private void OnFileSelected(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                _selectedBgPath = path;
                Pos.SaveSetting("last_image_dir", Path.GetDirectoryName(path));
                var fd = GetNodeOrNull<FileDialog>("FileDialog");
                if (fd != null)
                {
                    Pos.SaveSetting("fd_win_pos", $"{fd.Position.X}|{fd.Position.Y}");
                }
                UpdateImagePreview(path);
                Show();
                GrabFocus();
            }
        }

        private void OnSavePressed()
        {
            SaveAndClose();
        }

        private void OnCloseRequested()
        {
            WinPersist.Save(this, "set");
            QueueFree();
        }

        private void SaveAndClose()
        {
            SkyrimModTranslator.Core.Theme.SaveBackgroundSettings(_selectedBgPath, _currentOpacity);
            
            bool needReload = false;
            
            if (_tempTranslationMap != null)
            {
                var originalMap = Cfg.LdTransMap();
                if (!Cfg.AreDictionariesEqual(_tempTranslationMap, originalMap))
                {
                    Cfg.SvTransMap(_tempTranslationMap);
                    needReload = true;
                }
            }
            
            if (_tempCategories != null)
            {
                var originalCategories = L.GetCats();
                if (!Cfg.AreDictionariesEqual(_tempCategories, originalCategories))
                {
                    L.SaveCats(_tempCategories);
                    needReload = true;
                }
            }
            
            WinPersist.Save(this, "set");
            EmitSignal(SignalName.SettingsChanged);
            
            if (needReload)
            {
                Feedback.Instance.ShowConfirm("WIN_ALERT_TITLE", "MSG_SETTINGS_CHANGED",
                    () => {
                        if (MainSingleton.Instance != null)
                        {
                            MainSingleton.Instance.ReloadAllMods();
                        }
                        Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_SETTINGS_SAVED"));
                        QueueFree();
                    },
                    () => {
                        Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_SETTINGS_SAVED"));
                        QueueFree();
                    }
                );
            }
            else
            {
                Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_SETTINGS_SAVED"));
                QueueFree();
            }
        }
        


        private void UpdateImagePreview(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            
            _previewRect.Texture = SkyrimModTranslator.Core.Theme.LoadBackgroundTexture(path);
        }



        //创建翻译映射标签页
        private VBoxContainer CreateTranslationMapTab()
        {
            var v = new VBoxContainer();
            v.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            v.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            v.AddThemeConstantOverride("separation", 10);
            
            var label = new Label();
            label.Text = L.T("LABEL_TRANSLATION_MAP");
            label.AutowrapMode = TextServer.AutowrapMode.Word;
            v.AddChild(label);
            
            var marginContainer = new MarginContainer();
            marginContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            marginContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            marginContainer.AddThemeConstantOverride("margin_left", 10);
            marginContainer.AddThemeConstantOverride("margin_right", 10);
            marginContainer.AddThemeConstantOverride("margin_top", 5);
            marginContainer.AddThemeConstantOverride("margin_bottom", 5);
            v.AddChild(marginContainer);
            
            var tree = new Tree();
            _tree = tree;
            tree.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            tree.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            tree.HideRoot = true;
            tree.SelectMode = Tree.SelectModeEnum.Multi;
            tree.SetColumns(3);
            tree.SetColumnTitle(0, L.T("COLUMN_TYPE"));
            tree.SetColumnTitle(1, L.T("COLUMN_CATEGORY"));
            tree.SetColumnTitle(2, L.T("COLUMN_FIELDS"));
            tree.SetColumnExpand(0, false);
            tree.SetColumnExpand(1, false);
            tree.SetColumnExpand(2, true);
            tree.SetColumnCustomMinimumWidth(0, 100);
            tree.SetColumnCustomMinimumWidth(1, 120);
            marginContainer.AddChild(tree);
            
            _popupMenu = new PopupMenu();
            _popupMenu.AddItem(L.T("MENU_ADD_CATEGORY"), id: 1);
            _popupMenu.AddItem(L.T("MENU_CLEAR_CATEGORY"), id: 2);
            _popupMenu.AddItem(L.T("MENU_DELETE_CATEGORY"), id: 4);
            _popupMenu.AddSeparator();
            _popupMenu.AddItem(L.T("MENU_DELETE_TYPE"), id: 3);
            tree.AddChild(_popupMenu);
            
            tree.GuiInput += OnTreeGuiInput;
            
            _popupMenu.IdPressed += OnPopupMenuIdPressed;
            
            var translationMap = Cfg.LdTransMap();
            _tempTranslationMap = new Dictionary<string, List<string>>();
            foreach (var entry in translationMap)
            {
                _tempTranslationMap[entry.Key] = new List<string>(entry.Value);
            }
            
            var categories = L.GetCats();
            _tempCategories = new Dictionary<string, List<string>>();
            if (categories != null)
            {
                foreach (var entry in categories)
                {
                    _tempCategories[entry.Key] = new List<string>(entry.Value);
                }
            }
            
            var sortedEntries = _tempTranslationMap.OrderBy(entry => entry.Key).ToList();
            foreach (var entry in sortedEntries)
            {
                var root = tree.GetRoot();
                if (root == null)
                {
                    root = tree.CreateItem();
                }
                var item = tree.CreateItem(root);
                item.SetText(0, entry.Key);
                
                string category = GetTypeCategoryFromTemp(entry.Key);
                item.SetText(1, category);
                item.SetCustomColor(1, new Color(0.4f, 0.6f, 1.0f));
                
                item.SetText(2, string.Join(", ", entry.Value));
                item.SetMetadata(0, entry.Key);
            }
            
            var buttonHBox = new HBoxContainer();
            buttonHBox.AddThemeConstantOverride("separation", 10);
            v.AddChild(buttonHBox);
            
            var editButton = new Button();
            editButton.Text = L.T("BTN_EDIT_TRANSLATION_MAP");
            editButton.Pressed += OnEditTranslationMapPressed;
            buttonHBox.AddChild(editButton);
            
            var addButton = new Button();
            addButton.Text = L.T("BTN_ADD_TYPE");
            addButton.Pressed += OnAddTypePressed;
            buttonHBox.AddChild(addButton);
            
            var deleteButton = new Button();
            deleteButton.Text = L.T("BTN_DELETE_TYPE");
            deleteButton.Pressed += OnDeleteTypePressed;
            buttonHBox.AddChild(deleteButton);
            
            var resetButton = new Button();
            resetButton.Text = L.T("BTN_RESET_TRANSLATION_MAP");
            resetButton.Pressed += () => {
                var defaultMap = Cfg.GetDefTransMap();
                _tempTranslationMap.Clear();
                foreach (var entry in defaultMap)
                {
                    _tempTranslationMap[entry.Key] = new List<string>(entry.Value);
                }
                
                _tempCategories.Clear();
                var defaultCategories = L.GetCats();
                if (defaultCategories != null)
                {
                    foreach (var entry in defaultCategories)
                    {
                        _tempCategories[entry.Key] = new List<string>(entry.Value);
                    }
                }
                
                tree.Clear();
                foreach (var entry in _tempTranslationMap.OrderBy(e => e.Key))
                {
                    var root = tree.GetRoot();
                    if (root == null)
                    {
                        root = tree.CreateItem();
                    }
                    var item = tree.CreateItem(root);
                    item.SetText(0, entry.Key);
                    
                    string category = GetTypeCategoryFromTemp(entry.Key);
                    item.SetText(1, category);
                    item.SetCustomColor(1, new Color(0.4f, 0.6f, 1.0f));
                    
                    item.SetText(2, string.Join(", ", entry.Value));
                    item.SetMetadata(0, entry.Key);
                }
            };
            buttonHBox.AddChild(resetButton);
            
            return v;
        }

        private string GetTypeCategoryFromTemp(string typeCode)
        {
            if (_tempCategories != null)
            {
                foreach (var category in _tempCategories)
                {
                    if (category.Value.Contains(typeCode))
                    {
                        return category.Key;
                    }
                }
            }
            return L.T("CATEGORY_UNCATEGORIZED");
        }
        
        private void ShowAddCategoryDialog(List<TreeItem> selectedItems)
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = L.T("TITLE_ADD_CATEGORY");
            dialog.Size = new Vector2I(400, 220);
            
            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 8);
            dialog.AddChild(vBox);
            
            var label = new Label();
            label.Text = L.T("LABEL_SELECT_CATEGORY");
            vBox.AddChild(label);
            
            var categoryList = new OptionButton();
            categoryList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            
            if (_tempCategories != null)
            {
                var sortedCategories = _tempCategories.Keys.OrderBy(key => key).ToList();
                foreach (var category in sortedCategories)
                {
                    categoryList.AddItem(category);
                }
            }
            categoryList.AddItem(L.T("OPTION_CUSTOM_CATEGORY"));
            vBox.AddChild(categoryList);
            
            var customCategoryLabel = new Label();
            customCategoryLabel.Text = L.T("LABEL_CUSTOM_CATEGORY");
            customCategoryLabel.Visible = false;
            vBox.AddChild(customCategoryLabel);
            
            var customCategoryInput = new LineEdit();
            customCategoryInput.PlaceholderText = L.T("PLACEHOLDER_CUSTOM_CATEGORY");
            customCategoryInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            customCategoryInput.Visible = false;
            vBox.AddChild(customCategoryInput);
            
            categoryList.ItemSelected += (long index) => {
                bool isCustom = categoryList.GetItemText((int)index) == L.T("OPTION_CUSTOM_CATEGORY");
                customCategoryLabel.Visible = isCustom;
                customCategoryInput.Visible = isCustom;
                if (isCustom)
                {
                    customCategoryInput.GrabFocus();
                }
            };
            
            dialog.GetOkButton().Text = L.T("BTN_CONFIRM");
            dialog.GetCancelButton().Text = L.T("BTN_CANCEL");
            
            dialog.Confirmed += () => {
                string selectedCategory = categoryList.GetItemText(categoryList.Selected);
                
                if (selectedCategory == L.T("OPTION_CUSTOM_CATEGORY"))
                {
                    selectedCategory = customCategoryInput.Text.Trim();
                    if (string.IsNullOrEmpty(selectedCategory))
                    {
                        Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_CUSTOM_CATEGORY_CANNOT_BE_EMPTY"));
                        return;
                    }
                }
                
                AddCategoryToTypes(selectedItems, selectedCategory);
                Show();
                GrabFocus();
            };
            
            dialog.Canceled += () => {
                Show();
                GrabFocus();
            };
            
            AddChild(dialog);
            dialog.PopupCentered();
        }
        
        //添加分类到选中的类型
        private void AddCategoryToTypes(List<TreeItem> selectedItems, string category)
        {
            if (!_tempCategories.ContainsKey(category))
            {
                _tempCategories[category] = new List<string>();
            }
            
            foreach (var item in selectedItems)
            {
                string typeCode = item.GetMetadata(0).AsString();
                
                foreach (var catEntry in _tempCategories)
                {
                    if (catEntry.Value.Contains(typeCode))
                    {
                        catEntry.Value.Remove(typeCode);
                    }
                }
                
                if (!_tempCategories[category].Contains(typeCode))
                {
                    _tempCategories[category].Add(typeCode);
                }
                
                item.SetText(1, category);
                item.SetCustomColor(1, new Color(0.4f, 0.6f, 1.0f));
            }
        }
        
        private void ClearCategory(List<TreeItem> selectedItems)
        {
            foreach (var item in selectedItems)
            {
                string typeCode = item.GetMetadata(0).AsString();
                
                foreach (var catEntry in _tempCategories)
                {
                    if (catEntry.Value.Contains(typeCode))
                    {
                        catEntry.Value.Remove(typeCode);
                    }
                }
                
                item.SetText(1, L.T("CATEGORY_UNCATEGORIZED"));
                item.SetCustomColor(1, new Color(0.4f, 0.6f, 1.0f));
            }
        }
        
        //删除选中的类型
        private void DeleteType(List<TreeItem> selectedItems)
        {
            Feedback.Instance.ShowConfirm("TITLE_DELETE_TYPE", "LABEL_DELETE_MULTIPLE_TYPES_CONFIRM",
                () => {
                    foreach (var item in selectedItems)
                    {
                        string typeCode = item.GetMetadata(0).AsString();
                        
                        _tempTranslationMap.Remove(typeCode);
                        
                        foreach (var catEntry in _tempCategories)
                        {
                            if (catEntry.Value.Contains(typeCode))
                            {
                                catEntry.Value.Remove(typeCode);
                            }
                        }
                        
                        item.GetParent().RemoveChild(item);
                    }
                    
                    Show();
                    GrabFocus();
                },
                () => {
                    Show();
                    GrabFocus();
                },
                new object[] { selectedItems.Count }
            );
        }
        
        //删除选中的分类
        private void DeleteCategory(List<TreeItem> selectedItems)
        {
            string categoryToDelete = null;
            foreach (var item in selectedItems)
            {
                string category = item.GetText(1);
                if (category != L.T("CATEGORY_UNCATEGORIZED"))
                {
                    categoryToDelete = category;
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(categoryToDelete))
            {
                Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_NO_CATEGORY_TO_DELETE"));
                return;
            }
            
            Feedback.Instance.ShowConfirm("TITLE_DELETE_CATEGORY", "LABEL_DELETE_CATEGORY_CONFIRM",
                () => {
                    if (_tempCategories.Remove(categoryToDelete))
                    {
                        var root = _tree.GetRoot();
                        if (root != null)
                        {
                            var currentItem = root.GetFirstChild();
                            while (currentItem != null)
                            {
                                var nextItem = currentItem.GetNext();
                                if (currentItem.GetText(1) == categoryToDelete)
                                {
                                    currentItem.SetText(1, L.T("CATEGORY_UNCATEGORIZED"));
                                    currentItem.SetCustomColor(1, new Color(0.4f, 0.6f, 1.0f));
                                }
                                currentItem = nextItem;
                            }
                        }
                    }
                    Show();
                    GrabFocus();
                },
                () => {
                    Show();
                    GrabFocus();
                },
                new object[] { categoryToDelete }
            );
        }



        public void RefreshLocalization()
        {
            Title = L.T("WIN_SETTINGS");
            
            var tabs = GetNodeOrNull<TabContainer>("VBoxContainer/TabContainer");
            if (tabs != null)
            {
                tabs.SetTabTitle(0, L.T("TAB_TRANSLATION_MAP"));
                tabs.SetTabTitle(1, L.T("TAB_THEME"));
            }
            
            if (_btnSelectBg != null)
            {
                _btnSelectBg.Text = L.T("BTN_SELECT_BG");
            }
            if (_btnSave != null)
            {
                _btnSave.Text = L.T("BTN_SAVE_SETTINGS");
            }
            
            if (_labelCurrentPreview != null)
            {
                _labelCurrentPreview.Text = L.T("LABEL_CURRENT_PREVIEW");
            }
        }

        //编辑选中的类型的翻译映射
        private void OnEditTranslationMapPressed()
        {
            var selectedItems = new List<TreeItem>();
            TreeItem item = _tree.GetNextSelected(null);
            while (item != null)
            {
                selectedItems.Add(item);
                item = _tree.GetNextSelected(item);
            }
            
            if (selectedItems.Count == 1)
            {
                var selectedItem = selectedItems[0];
                var type = selectedItem.GetMetadata(0).AsString();
                var currentFields = selectedItem.GetText(2);
                
                var dialog = new ConfirmationDialog();
                dialog.Title = L.T("TITLE_EDIT_TRANSLATION_MAP");
                dialog.Size = new Vector2I(600, 200);
                
                var vBox = new VBoxContainer();
                dialog.AddChild(vBox);
                
                var label = new Label();
                label.Text = string.Format(L.T("LABEL_EDIT_TRANSLATION_MAP"), type);
                vBox.AddChild(label);
                
                var lineEdit = new LineEdit();
                lineEdit.Text = currentFields;
                lineEdit.PlaceholderText = L.T("PLACEHOLDER_FIELDS");
                lineEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                vBox.AddChild(lineEdit);
                
                dialog.GetOkButton().Text = L.T("BTN_SAVE");
                dialog.GetCancelButton().Text = L.T("BTN_CANCEL");
                
                dialog.Confirmed += () => {
                    var newFields = lineEdit.Text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    
                    if (newFields.Count == 0)
                    {
                        Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_FIELDS_CANNOT_BE_EMPTY"));
                        return;
                    }
                    
                    _tempTranslationMap[type] = newFields;
                    selectedItem.SetText(2, string.Join(", ", newFields));
                    Show();
                    GrabFocus();
                };
                
                dialog.Canceled += () => {
                    Show();
                    GrabFocus();
                };
                
                AddChild(dialog);
                dialog.PopupCentered();
            }
            else if (selectedItems.Count > 1)
            {
                Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_CANNOT_EDIT_MULTIPLE"));
            }
            else
            {
                Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_NO_SELECTION"));
            }
        }

        //添加类型
        private void OnAddTypePressed()
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = L.T("TITLE_ADD_TYPE");
            dialog.Size = new Vector2I(500, 300);
            
            var vBox = new VBoxContainer();
            dialog.AddChild(vBox);
            
            var typeLabel = new Label();
            typeLabel.Text = L.T("LABEL_TYPE_CODE");
            vBox.AddChild(typeLabel);
            
            var typeInput = new LineEdit();
            typeInput.PlaceholderText = L.T("PLACEHOLDER_TYPE_CODE");
            typeInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vBox.AddChild(typeInput);
            
            var fieldsLabel = new Label();
            fieldsLabel.Text = L.T("LABEL_FIELDS");
            vBox.AddChild(fieldsLabel);
            
            var fieldsInput = new LineEdit();
            fieldsInput.PlaceholderText = L.T("PLACEHOLDER_FIELDS");
            fieldsInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vBox.AddChild(fieldsInput);
            
            var categoryLabel = new Label();
            categoryLabel.Text = L.T("LABEL_CATEGORY");
            vBox.AddChild(categoryLabel);
            
            var categoryInput = new OptionButton();
            categoryInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            
            if (_tempCategories != null)
            {
                var sortedCategories = _tempCategories.Keys.OrderBy(key => key).ToList();
                foreach (var category in sortedCategories)
                {
                    categoryInput.AddItem(category);
                }
            }
            categoryInput.AddItem(L.T("CATEGORY_UNCATEGORIZED"));
            vBox.AddChild(categoryInput);
            
            dialog.GetOkButton().Text = L.T("BTN_CONFIRM");
            dialog.GetCancelButton().Text = L.T("BTN_CANCEL");
            
            dialog.Confirmed += () => {
                string typeCode = typeInput.Text.Trim();
                string fieldsText = fieldsInput.Text.Trim();
                string selectedCategory = categoryInput.GetItemText(categoryInput.Selected);
                
                if (string.IsNullOrEmpty(typeCode))
                {
                    Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_TYPE_CODE_CANNOT_BE_EMPTY"));
                    return;
                }
                
                var newFields = fieldsText.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (newFields.Count == 0)
                {
                    Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_FIELDS_CANNOT_BE_EMPTY"));
                    return;
                }
                
                if (_tempTranslationMap.ContainsKey(typeCode))
                {
                    Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_TYPE_ALREADY_EXISTS"));
                    return;
                }
                
                _tempTranslationMap[typeCode] = newFields;
                
                if (selectedCategory != L.T("CATEGORY_UNCATEGORIZED"))
                {
                    if (!_tempCategories.ContainsKey(selectedCategory))
                    {
                        _tempCategories[selectedCategory] = new List<string>();
                    }
                    
                    if (!_tempCategories[selectedCategory].Contains(typeCode))
                    {
                        _tempCategories[selectedCategory].Add(typeCode);
                    }
                }
                
                var root = _tree.GetRoot();
                if (root == null)
                {
                    root = _tree.CreateItem();
                }
                var newItem = _tree.CreateItem(root);
                newItem.SetText(0, typeCode);
                newItem.SetText(1, selectedCategory);
                newItem.SetCustomColor(1, new Color(0.4f, 0.6f, 1.0f));
                newItem.SetText(2, string.Join(", ", newFields));
                newItem.SetMetadata(0, typeCode);
                
                Show();
                GrabFocus();
            };
            
            dialog.Canceled += () => {
                Show();
                GrabFocus();
            };
            
            AddChild(dialog);
            dialog.PopupCentered();
        }

        //删除选中的类型
        private void OnDeleteTypePressed()
        {
            var selectedItems = new List<TreeItem>();
            TreeItem item = _tree.GetNextSelected(null);
            while (item != null)
            {
                selectedItems.Add(item);
                item = _tree.GetNextSelected(item);
            }
            
            if (selectedItems.Count > 0)
            {
                DeleteType(selectedItems);
            }
            else
            {
                Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_NO_SELECTION"));
            }
        }

        private void OnTreeGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Right)
            {
                if (_popupMenu != null)
                {
                    Vector2I mousePos = DisplayServer.MouseGetPosition();
                    _popupMenu.Position = mousePos + new Vector2I(2, 2);
                    _popupMenu.Popup();
                }
            }
        }

        private void OnPopupMenuIdPressed(long id)
        {
            var selectedItems = new List<TreeItem>();
            TreeItem item = _tree.GetNextSelected(null);
            while (item != null)
            {
                selectedItems.Add(item);
                item = _tree.GetNextSelected(item);
            }
            
            if (selectedItems.Count == 0)
            {
                Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), L.T("MSG_NO_SELECTION"));
                return;
            }
            
            switch (id)
            {
                case 1:
                    ShowAddCategoryDialog(selectedItems);
                    break;
                case 2:
                    ClearCategory(selectedItems);
                    break;
                case 3:
                    DeleteType(selectedItems);
                    break;
                case 4:
                    DeleteCategory(selectedItems);
                    break;
            }
        }

        public override void _ExitTree()
        {
            L.OnLanguageChanged -= RefreshLocalization;
            _instance = null;
            base._ExitTree();
        }
    }
}


