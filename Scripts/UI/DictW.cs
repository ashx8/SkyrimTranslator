//词典管理窗口
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Linq;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    using TransEntry = SkyrimModTranslator.Core.Data.TransEntry;

    public partial class Dictw : Window
    {
        [Signal] public delegate void DictwSettingsChangedEventHandler();
        private Button _btnAdd;
        private LineEdit _searchBar;
        private TabContainer _tabContainer;
        private ItemList _userList;
        private ItemList _modList;
        private OptionButton _modSelector;
        private Button _btnImport;
        private Button _btnExport;
        private Button _btnDelete;
        private Button _btnOpenFolder;
        private Label _lblStat;
        private Button _btnEditExportPrompt;
        private Button _btnDelAllUser;
        private Button _btnDelCurMod;
        private bool _isLoading = false;
        private VBoxContainer _shardPanel;
        private string _currShard = "a";
        private int _searchId = 0;
        
        public string _exportPrompt = "";

        private Dictionary<string, string> _userGlossary = new Dictionary<string, string>();
        private Dictionary<string, string> _modGlossary = new Dictionary<string, string>();
        private Dictionary<Button, string> _buttonToShard = new Dictionary<Button, string>();
        private string _currMod = "";
        private string _editOriKey;

        public Dictw()
        {
            Visible = false;
        }

        private Window _promptEditWindow = null;

        public override void _Ready()
        {
            Title = L.T("WIN_DIST_TITLE");
            MinSize = new Vector2I(900, 700);
            Size = new Vector2I(900, 700);

            CloseRequested += OnCloseRequested;

            SetupUI();
            SetupConnections();
            LoadGlossary();
            
            L.OnLanguageChanged += RefreshLocalization;
            
            SizeChanged += OnSizeChanged;
            
            TreeExited += OnTreeExited;

            LateInitialize();
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

        private void OnCloseRequested()
        {
            Pos.SaveWindowState(this);
            QueueFree();
        }

        private void OnSizeChanged()
        {
            if (_promptEditWindow != null && IsInstanceValid(_promptEditWindow))
            {
                CenterWindow(_promptEditWindow);
            }
        }

        private void OnTreeExited()
        {
            L.OnLanguageChanged -= RefreshLocalization;
        }

        private void CenterWindow(Window window)
        {
            window.Position = new Vector2I(
                (int)(DisplayServer.WindowGetSize().X / 2 - window.Size.X / 2),
                (int)(DisplayServer.WindowGetSize().Y / 2 - window.Size.Y / 2)
            );
        }



        private void UpdateShardPanelStyles()
        {
            foreach (var child in _shardPanel.GetChildren())
            {
                if (child is Button btn)
                {
                    bool isSelected = btn.Text.ToLower() == _currShard.ToLower();
                    btn.Modulate = isSelected ? Colors.White : new Color(0.6f, 0.6f, 0.6f);
                }
            }
        }

        private void RefreshShardButtons()
        {
            foreach (var child in _shardPanel.GetChildren()) child.QueueFree();

            string category = _tabContainer.CurrentTab == 0 ? "UserDict" : "ModDict";
            string sub = _tabContainer.CurrentTab == 0 ? "" : _currMod;
            
            string dirPath = Path.Combine(SkyrimModTranslator.Core.Dict.DictStorage.GetCurrentLangPath(), category, sub);
            if (!Directory.Exists(dirPath)) return;

            var files = Directory.GetFiles(dirPath, "*.json")
                                 .Select(f => Path.GetFileNameWithoutExtension(f))
                                 .OrderBy(f => f);

            foreach (var fileName in files)
            {
                var shardData = SkyrimModTranslator.Core.Dict.DictStorage.LoadByShard(category, sub, fileName);
                
                var btn = new Button { 
                    Text = fileName.ToUpper(), 
                    Flat = true,
                    FocusMode = Control.FocusModeEnum.None
                };
                btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
                
                string shardName = fileName;
                btn.Pressed += () => OnShardButtonPressed(shardName);
                _shardPanel.AddChild(btn);
            }
        }

        private void OnShardButtonPressed(string shardName)
        {
            _currShard = shardName;
            RefreshGlossaryList();
        }

        private void SetupUI()
        {
            SkyrimModTranslator.Core.Theme.ApplyStdBg(this);
            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_top", 25);
            margin.AddThemeConstantOverride("margin_bottom", 25);
            margin.AddThemeConstantOverride("margin_left", 30);
            margin.AddThemeConstantOverride("margin_right", 30);
            AddChild(margin);

            var mainVBox = new VBoxContainer();
            mainVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVBox.AddThemeConstantOverride("separation", 18);
            margin.AddChild(mainVBox);

            _searchBar = new LineEdit { PlaceholderText = L.T("PLACEHOLDER_SEARCH"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            mainVBox.AddChild(_searchBar);

            var controlsHBox = new HBoxContainer();
            controlsHBox.AddThemeConstantOverride("separation", 15);
            mainVBox.AddChild(controlsHBox);

            _btnDelete = new Button { Text = L.T("BTN_DELETE"), CustomMinimumSize = new Vector2(120, 35) };
            controlsHBox.AddChild(_btnDelete);

            _btnAdd = new Button { Text = L.T("BTN_ADD_ENTRY"), CustomMinimumSize = new Vector2(100, 35) };
            controlsHBox.AddChild(_btnAdd);

            _btnOpenFolder = new Button { Text = L.T("BTN_OPEN_FOLDER"), CustomMinimumSize = new Vector2(120, 35) };
            controlsHBox.AddChild(_btnOpenFolder);

            _btnEditExportPrompt = new Button { Text = L.T("BTN_EDIT_EXPORT_PROMPT"), CustomMinimumSize = new Vector2(150, 35) };
            _btnEditExportPrompt.Pressed += OnEditExportPromptPressed;
            controlsHBox.AddChild(_btnEditExportPrompt);

            var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            controlsHBox.AddChild(spacer);

            var chkAutoEffect = new CheckBox { Text = L.T("CHK_USE_DICT"), ButtonPressed = Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "true")) };
            chkAutoEffect.Toggled += OnAutoApplyToggled;
            controlsHBox.AddChild(chkAutoEffect);

            var contentHBox = new HBoxContainer();
            contentHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            contentHBox.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(contentHBox);

            var shardScroll = new ScrollContainer();
            shardScroll.CustomMinimumSize = new Vector2(45, 0);
            shardScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            contentHBox.AddChild(shardScroll);

            _shardPanel = new VBoxContainer();
            _shardPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            shardScroll.AddChild(_shardPanel);

            _tabContainer = new TabContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            contentHBox.AddChild(_tabContainer);

            var userDictVBox = new VBoxContainer();
            _userList = CreateGlossaryList("", userDictVBox);
            _tabContainer.AddChild(userDictVBox);
            _tabContainer.SetTabTitle(_tabContainer.GetChildCount() - 1, L.T("TAB_USER_DICT"));

            var modDictVBox = new VBoxContainer();
            
            var modSelectorHBox = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            modSelectorHBox.AddThemeConstantOverride("separation", 10);
            var modSelectorLabel = new Label { Text = L.T("MOD_SELECTOR_LABEL"), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
            modSelectorHBox.AddChild(modSelectorLabel);
            _modSelector = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _modSelector.ItemSelected += OnModSelected;
            modSelectorHBox.AddChild(_modSelector);
            modDictVBox.AddChild(modSelectorHBox);
            
            _modList = CreateGlossaryList("", modDictVBox);
            _tabContainer.AddChild(modDictVBox);
            _tabContainer.SetTabTitle(_tabContainer.GetChildCount() - 1, L.T("TAB_MOD_DICT"));

            var bottomHBox = new HBoxContainer();
            bottomHBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            mainVBox.AddChild(bottomHBox);
            
            var buttonGroup = new HBoxContainer();
            buttonGroup.AddThemeConstantOverride("separation", 10);
            bottomHBox.AddChild(buttonGroup);

            _btnImport = new Button { Text = L.T("BTN_IMPORT"), CustomMinimumSize = new Vector2(100, 35) };
            buttonGroup.AddChild(_btnImport);

            _btnExport = new Button { Text = L.T("BTN_EXPORT"), CustomMinimumSize = new Vector2(100, 35) };
            buttonGroup.AddChild(_btnExport);
            
            _btnDelAllUser = new Button { Text = L.T("BTN_DELETE_ALL"), Modulate = Colors.Tomato, CustomMinimumSize = new Vector2(120, 35) };
            buttonGroup.AddChild(_btnDelAllUser);

            var bottomSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bottomHBox.AddChild(bottomSpacer);

            _lblStat = new Label { 
                Text = L.T("STAT_TOTAL_ENTRIES").Replace("{count}", "0"), 
                HorizontalAlignment = HorizontalAlignment.Right 
            };
            bottomHBox.AddChild(_lblStat);
        }

        private ItemList CreateGlossaryList(string tabName, Control parent)
        {
            var list = new ItemList {
                Name = string.IsNullOrEmpty(tabName) ? "GlossaryList" : tabName,
                SelectMode = ItemList.SelectModeEnum.Multi,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Modulate = new Color(1, 1, 1, 0.5f)
            };
            list.ItemActivated += OnItemActivated;
            list.SetMeta("listReference", list);
            list.AddToGroup("GlossaryLists");
            parent.AddChild(list);
            return list;
        }

        private void OnItemActivated(long idx)
        {
            ItemList list = null;
            foreach (var child in GetTree().GetNodesInGroup("GlossaryLists"))
            {
                if (child is ItemList itemList && itemList.HasFocus())
                {
                    list = itemList;
                    break;
                }
            }
            if (list != null)
            {
                OnEditEntry(list, (int)idx);
            }
        }

        private void OnEditEntry(ItemList list, int idx)
        {
            _editOriKey = list.GetItemMetadata(idx).AsString();
            string translation = "";
            bool isUserDict = (list == _userList);
            
            if (isUserDict)
                _userGlossary.TryGetValue(_editOriKey, out translation);
            else if (list == _modList)
                _modGlossary.TryGetValue(_editOriKey, out translation);
            
            var entry = new TransEntry { 
                Ori = _editOriKey, 
                Trans = translation, 
                Type = "GLOSSARY" 
            };
            var editor = new Edit(entry);
            GetTree().Root.AddChild(editor);
            
            editor.TextSaved += OnTextSaved;
            editor.Show();
        }

        private void OnTextSaved(string newTrans, string newOri)
        {
            bool isUserDict = (_tabContainer.CurrentTab == 0);
            if (_editOriKey != newOri) {
                if (isUserDict) {
                    _userGlossary.Remove(_editOriKey);
                    SkyrimModTranslator.Core.Dict.DictStorage.Delete("UserDict", "", _editOriKey);
                } else {
                    _modGlossary.Remove(_editOriKey);
                    SkyrimModTranslator.Core.Dict.DictStorage.Delete("ModDict", _currMod, _editOriKey);
                }
            }
            
            var updateDict = new Dictionary<string, string> { { newOri, newTrans } };
            if (isUserDict) {
                _userGlossary[newOri] = newTrans;
                SkyrimModTranslator.Core.Dict.DictStorage.Save("UserDict", "", updateDict);
            } else {
                _modGlossary[newOri] = newTrans;
                SkyrimModTranslator.Core.Dict.DictStorage.Save("ModDict", _currMod, updateDict);
            }
            
            RefreshGlossaryList();
        }

        private void OnAutoApplyToggled(bool on)
        {
            Pos.SaveSetting("apply_dictionary", on.ToString().ToLower());
            EmitSignal(SignalName.DictwSettingsChanged);
        }

        private void OpenEditor(TransEntry entry, bool isNew)
        {
            var editor = new Edit(entry);
            GetTree().Root.AddChild(editor);
            editor.TextSaved += (newTrans, newOri) => OnEditorTextSaved(newTrans, newOri, entry, isNew);
            editor.Show();
        }

        private void OnEditorTextSaved(string newTrans, string newOri, TransEntry entry, bool isNew)
        {
            int currentTab = _tabContainer.CurrentTab;
            if (currentTab == 0)
            {
                if (!isNew) _userGlossary.Remove(entry.Ori);
                _userGlossary[newOri] = newTrans;
                var updateDict = new Dictionary<string, string> { { newOri, newTrans } };
                SkyrimModTranslator.Core.Dict.DictStorage.Save("UserDict", "", updateDict);
            }
            else if (currentTab == 1 && !string.IsNullOrEmpty(_currMod))
            {
                if (!isNew) _modGlossary.Remove(entry.Ori);
                _modGlossary[newOri] = newTrans;
                var updateDict = new Dictionary<string, string> { { newOri, newTrans } };
                SkyrimModTranslator.Core.Dict.DictStorage.Save("ModDict", _currMod, updateDict);
            }
            SaveGlossary();
            RefreshGlossaryList();
            
            bool useDict = Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "true"));
            if (useDict)
            {
                EmitSignal(SignalName.DictwSettingsChanged);
            }
        }

        private void SetupConnections()
        {
            _searchBar.TextChanged += OnSearchChanged;
            _btnDelete.Pressed += OnDeletePressed;
            _btnImport.Pressed += OnMajorImportPressed;
            _btnExport.Pressed += OnExportPressed;
            _btnAdd.Pressed += OnAddRequest;
            _btnOpenFolder.Pressed += OnOpenFolderPressed;
            _tabContainer.TabChanged += OnTabChanged;
            _btnDelAllUser.Pressed += OnDeleteAllUserPressed;
        }

        private void OnOpenFolderPressed()
        {
            string langPath = SkyrimModTranslator.Core.Dict.DictStorage.GetCurrentLangPath();
            
            int currentTab = _tabContainer.CurrentTab;
            string targetPath = langPath;

            if (currentTab == 0)
                targetPath = Path.Combine(langPath, "UserDict");
            else if (currentTab == 1 && !string.IsNullOrEmpty(_currMod))
                targetPath = Path.Combine(langPath, "ModDict", _currMod);

            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

            OS.ShellOpen(targetPath);
        }

        private void OnTabChanged(long tabIndex)
        {
            RefreshShardButtons();
            RefreshGlossaryList();
        }

        private void OnAddRequest()
        {
            var newEntry = new TransEntry { Type = "GLOSSARY", Ori = "", Trans = "" };
            OpenEditor(newEntry, true);
        }

        private void LoadGlossary()
        {
            _exportPrompt = Pos.GetSetting("export_prompt", 
                L.T("PROMPT_PREFIX_GLOSSARY") + L.T("PROMPT_BASE"));
            
            _userGlossary.Clear();
            _modGlossary.Clear();

            try
            {
                _userGlossary = SkyrimModTranslator.Core.Dict.DictStorage.Load("UserDict", "");
                
                LoadModSelector();
                
                UpdateStat($"已加载 {_userGlossary.Count} 个用户词条");
                RefreshShardButtons();
                RefreshGlossaryList();
            }
            catch (Exception ex)
            {
                _userGlossary = new Dictionary<string, string>();
                _modGlossary = new Dictionary<string, string>();
                UpdateStat($"加载词典失败: {ex.Message}");
            }
        }

        private void LoadModSelector()
        {
            _modSelector.Clear();
            var mods = SkyrimModTranslator.Core.Dict.DictStorage.GetModList("ModDict");
            foreach (var mod in mods)
            {
                _modSelector.AddItem(mod);
            }
            
            if (_modSelector.ItemCount > 0)
            {
                _modSelector.Select(0);
                OnModSelected(0);
            }
        }

        private void OnModSelected(long idx)
        {
            _currMod = _modSelector.GetItemText((int)idx);
            _modGlossary = SkyrimModTranslator.Core.Dict.DictStorage.Load("ModDict", _currMod);
            RefreshShardButtons();
            
            var shardButtons = _shardPanel.GetChildren();
            if (shardButtons.Count > 0)
            {
                foreach (var child in shardButtons)
                {
                    if (child is Button btn)
                    {
                        btn.EmitSignal(Button.SignalName.Pressed);
                        break;
                    }
                }
            }
            else
            {
                RefreshGlossaryList();
                UpdateStat($"已加载 {_modGlossary.Count} 个 {_currMod} 词条");
            }
        }

        private void SaveGlossary() {
            try {
                SkyrimModTranslator.Core.Dict.DictStorage.Save("UserDict", "", _userGlossary);
                
                if (!string.IsNullOrEmpty(_currMod)) {
                    SkyrimModTranslator.Core.Dict.DictStorage.Save("ModDict", _currMod, _modGlossary);
                }
                
                UpdateStat(L.T("STAT_DICT_SAVED"));
            } catch (Exception ex) {
                UpdateStat($"保存词典失败: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task RefreshListAsync(string type, string subCategory = "")
        {
            if (_isLoading) return;
            _isLoading = true;

            ItemList targetList = null;
            if (type == "UserDict")
                targetList = _userList;
            else if (type == "ModDict")
                targetList = _modList;

            if (targetList != null)
                targetList.Clear();
            
            var data = await System.Threading.Tasks.Task.Run(() => SkyrimModTranslator.Core.Dict.DictStorage.Load(type, subCategory));
            
            if (data.Count == 0) {
                _isLoading = false;
                return;
            }

            int count = 0;
            string searchText = _searchBar.Text.ToLower().Trim();
            var filtered = data.Where(kvp =>
                string.IsNullOrEmpty(searchText) ||
                kvp.Key.ToLower().Contains(searchText) ||
                kvp.Value.ToLower().Contains(searchText)
            );

            foreach (var kvp in filtered)
            {
                if (targetList != null)
                {
                    string itemText = $"{kvp.Key} -> {kvp.Value}";
                    targetList.AddItem(itemText);
                    targetList.SetItemMetadata(targetList.ItemCount - 1, kvp.Key);
                }

                count++;
                if (count % 200 == 0)
                {
                    await System.Threading.Tasks.Task.Delay(1);
                }
            }
            
            _isLoading = false;
        }

        private async System.Threading.Tasks.Task RefreshGlossaryList() 
        {
            if (_isLoading) return;
            _isLoading = true;

            string searchText = _searchBar.Text.ToLower().Trim();
            int currentTab = _tabContainer.CurrentTab;
            string category = currentTab == 0 ? "UserDict" : "ModDict";
            string sub = currentTab == 0 ? "" : _currMod;

            Dictionary<string, string> data;

            if (string.IsNullOrEmpty(searchText))
            {
                data = SkyrimModTranslator.Core.Dict.DictStorage.LoadByShard(category, sub, _currShard);
                UpdateStat($"分片 [{_currShard.ToUpper()}] 记录: {data.Count}");
            }
            else
            {
                data = await System.Threading.Tasks.Task.Run(() => 
                    SkyrimModTranslator.Core.Dict.DictStorage.Load(category, sub, null)
                );
                UpdateStat($"全局搜索 \"{searchText}\" 中...");
            }

            ItemList targetList = currentTab == 0 ? _userList : _modList;
            targetList.Clear();

            int count = 0;
            foreach (var kvp in data)
            {
                if (string.IsNullOrEmpty(searchText) || 
                    kvp.Key.ToLower().Contains(searchText) || 
                    kvp.Value.ToLower().Contains(searchText))
                {
                    targetList.AddItem($"{kvp.Key} -> {kvp.Value}");
                    targetList.SetItemMetadata(targetList.ItemCount - 1, kvp.Key);
                    
                    count++;
                    if (count % 100 == 0) await System.Threading.Tasks.Task.Delay(1);
                }
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                UpdateStat($"搜索到 {targetList.ItemCount} 条结果");
            }
            
            _isLoading = false;
        }

        private async void OnSearchChanged(string text)
        {
            _searchId++;
            int currentId = _searchId;

            await System.Threading.Tasks.Task.Delay(500);
            
            if (currentId != _searchId) return;

            await RefreshGlossaryList();
        }

        private void OnDeletePressed()
        {
            ItemList list = null;
            Dictionary<string, string> currentGlossary = null;
            string category = "";
            string modName = "";

            int currentTab = _tabContainer.CurrentTab;
            if (currentTab == 0)
            {
                list = _userList;
                currentGlossary = _userGlossary;
                category = "UserDict";
                modName = "";
            }
            else if (currentTab == 1 && !string.IsNullOrEmpty(_currMod))
            {
                list = _modList;
                currentGlossary = _modGlossary;
                category = "ModDict";
                modName = _currMod;
            }

            if (list == null)
            {
                UpdateStat(L.T("MSG_SELECT_FIRST"));
                return;
            }

            var selected = list.GetSelectedItems();
            if (selected.Length == 0) 
            {
                UpdateStat(L.T("MSG_SELECT_FIRST"));
                return;
            }

            var confirmDialog = new ConfirmationDialog();
            confirmDialog.Title = L.T("DLG_CONFIRM_DELETE");
            confirmDialog.DialogText = L.T("DLG_CONFIRM_DELETE_COUNT").Replace("{n}", selected.Length.ToString());
            confirmDialog.GetOkButton().Text = L.T("UI_CONFIRM");
            confirmDialog.GetCancelButton().Text = L.T("UI_CANCEL");

            AddChild(confirmDialog);
            confirmDialog.PopupCentered();

            confirmDialog.Confirmed += () => OnDeleteConfirmed(selected, currentGlossary, category, modName);
        }

        private void OnMajorImportPressed()
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = L.T("BTN_IMPORT_TARGET");
            dialog.DialogText = "请选择导入模板：";

            dialog.GetOkButton().Hide();
            dialog.GetCancelButton().Hide();

            dialog.AddButton(L.T("BTN_USER_DICT"), true, "user");
            dialog.AddButton(L.T("BTN_MOD_DICT"), true, "mod");

            dialog.CustomAction += (action) => {
                if (action.ToString() == "user") OpenImportFileDialog(false);
                else if (action.ToString() == "mod") OpenImportFileDialog(true);
                dialog.QueueFree();
            };

            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void OpenImportFileDialog(bool isMod)
        {
            var fd = new FileDialog {
                FileMode = FileDialog.FileModeEnum.OpenFile,
                Access = FileDialog.AccessEnum.Filesystem,
                Filters = new string[] { "*.txt,*.json ; 兼容文件" },
                UseNativeDialog = true
            };
            fd.Size = new Vector2I(800, 600);
            fd.Exclusive = false;
            fd.CurrentDir = Pos.GetSetting("last_import_dir", "");
            
            string prevPos = Pos.GetSetting("fd_import_pos", "");
            if (!string.IsNullOrEmpty(prevPos)) {
                string[] parts = prevPos.Split('|');
                fd.Position = new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));
            }
            
            fd.FileSelected += (path) => {
                Pos.SaveSetting("last_import_dir", Path.GetDirectoryName(path));
                Pos.SaveSetting("fd_import_pos", $"{fd.Position.X}|{fd.Position.Y}");
                
                if (isMod) {
                    ShowModExtSelector(path);
                } else {
                    ProcessManualImport(path, "UserDict", "");
                }
            };
            GetTree().Root.AddChild(fd);
            fd.PopupCentered();
        }

        private string ReadFileWithAutoEncodingDetection(string filePath)
        {
            Encoding encoding;
            string content = SkyrimModTranslator.Core.EncDet.Read(filePath, out encoding);
            
            if (SkyrimModTranslator.Core.EncDet.HasGarbled(content))
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    var gbEncoding = Encoding.GetEncoding("GB2312");
                    string gbContent = gbEncoding.GetString(fileBytes);
                    
                    if (!SkyrimModTranslator.Core.EncDet.HasGarbled(gbContent))
                    {
                        return gbContent;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DictW] GB2312 编码转换失败: {ex.Message}");
                }
                
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    var gbkEncoding = Encoding.GetEncoding("GBK");
                    string gbkContent = gbkEncoding.GetString(fileBytes);
                    
                    if (!SkyrimModTranslator.Core.EncDet.HasGarbled(gbkContent))
                    {
                        return gbkContent;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[DictW] GBK 编码转换失败: {ex.Message}");
                }
            }
            
            return content;
        }
        
        private void ProcessManualImport(string filePath, string category, string modName)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();
                Dictionary<string, string> importedData = new Dictionary<string, string>();
                
                if (extension == ".json")
                {
                    string json = ReadFileWithAutoEncodingDetection(filePath);
                    
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            importedData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                            if (importedData == null)
                            {
                                importedData = new Dictionary<string, string>();
                            }
                        }
                        catch
                        {
                            byte[] fileBytes = File.ReadAllBytes(filePath);
                            try
                            {
                                var gbEncoding = System.Text.Encoding.GetEncoding(936);
                                json = gbEncoding.GetString(fileBytes);
                                importedData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                                if (importedData == null)
                                {
                                    importedData = new Dictionary<string, string>();
                                }
                            }
                            catch
                            {
                                importedData = new Dictionary<string, string>();
                            }
                        }
                    }
                }
                else if (extension == ".txt")
                {
                    string rawContent = ReadFileWithAutoEncodingDetection(filePath);
                    
                    if (!string.IsNullOrEmpty(rawContent))
                    {
                        string[] lines = rawContent.Split('\n');
                        foreach (string line in lines)
                        {
                            string trimmedLine = line.Trim();
                            if (string.IsNullOrEmpty(trimmedLine)) continue;
                            
                            int pipeIndex = trimmedLine.IndexOf('|');
                            if (pipeIndex > 0 && pipeIndex < trimmedLine.Length - 1)
                            {
                                string original = trimmedLine.Substring(0, pipeIndex).Trim();
                                string translation = trimmedLine.Substring(pipeIndex + 1).Trim();
                                
                                if (!string.IsNullOrEmpty(original) && !string.IsNullOrEmpty(translation))
                                {
                                    importedData[original] = translation;
                                }
                            }
                        }
                    }
                }
                
                if (importedData.Count > 0)
                {
                    SkyrimModTranslator.Core.Dict.DictStorage.Save(category, modName, importedData);
                    
                    LoadGlossary();
                    
                    bool useDict = Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "true"));
                    if (useDict)
                    {
                        EmitSignal(SignalName.DictwSettingsChanged);
                    }
                    
                    string msg = L.T("MSG_IMPORT_SUCCESS").Replace("{path}", modName);
                    UpdateStat(msg);
                    
                    CallDeferred(MethodName.GrabFocus);
                }
                else
                {
                    UpdateStat(L.T("MSG_NO_ENTRIES_TO_IMPORT"));
                }
            }
            catch (Exception ex)
            {
                UpdateStat($"导入词典失败: {ex.Message}");
            }
        }

        private void ShowModExtSelector(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            
            Window typePicker = new Window();
            typePicker.Title = L.T("TITLE_SELECT_TYPE");
            typePicker.Size = new Vector2I(300, 180);
            typePicker.InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen;
            typePicker.Transient = true;
            typePicker.Exclusive = true;
            
            typePicker.CloseRequested += () => {
                typePicker.QueueFree();
            };
            
            VBoxContainer layout = new VBoxContainer();
            layout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 20);
            
            string[] types = { "ESP", "ESM", "ESL" };
            foreach (string type in types)
            {
                Button btn = new Button();
                btn.Text = type;
                btn.CustomMinimumSize = new Vector2(0, 40);
                btn.Pressed += () => {
                    UpdateStat(L.T("STATUS_IMPORTING"));
                    ProcessManualImport(filePath, "ModDict", fileName + "." + type.ToLower());
                    typePicker.QueueFree();
                };
                layout.AddChild(btn);
            }
            
            typePicker.AddChild(layout);
            GetTree().Root.AddChild(typePicker);
            typePicker.Popup();
        }

        private void OnDeleteConfirmed(int[] selected, Dictionary<string, string> currentGlossary, string category, string modName)
        {
            foreach (var idx in selected.Reverse())
            {
                ItemList list = null;
                if (category == "UserDict")
                    list = _userList;
                else if (category == "ModDict")
                    list = _modList;
                
                if (list != null)
                {
                    string key = list.GetItemMetadata(idx).AsString();
                    currentGlossary.Remove(key);
                    SkyrimModTranslator.Core.Dict.DictStorage.Delete(category, modName, key);
                }
            }
            SaveGlossary();
            RefreshGlossaryList();
            UpdateStat($"已删除 {selected.Length} 个词条");
            
            bool useDict = Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "true"));
            if (useDict)
            {
                EmitSignal(SignalName.DictwSettingsChanged);
            }
        }

        private void OnDeleteAllUserPressed()
        {
            int currentTab = _tabContainer.CurrentTab;
            string msg;
            Action deleteAction;

            if (currentTab == 0)
            {
                msg = L.T("MSG_CONFIRM_DELETE_ALL");
                deleteAction = () => {
                    SkyrimModTranslator.Core.Dict.DictStorage.ClearAll("UserDict", "");
                    _userGlossary.Clear();
                };
            }
            else
            {
                if (string.IsNullOrEmpty(_currMod)) return;
                
                msg = L.T("MSG_CONFIRM_DELETE_MOD_ALL").Replace("{mod}", _currMod); 
                deleteAction = () => {
                    SkyrimModTranslator.Core.Dict.DictStorage.ClearAll("ModDict", _currMod, true);
                    _modGlossary.Clear();
                    
                    LoadModSelector();
                };
            }

            var dialog = new ConfirmationDialog { 
                Title = L.T("DLG_CONFIRM_DELETE"),
                DialogText = msg 
            };
            dialog.GetOkButton().Text = L.T("UI_CONFIRM");
            dialog.GetCancelButton().Text = L.T("UI_CANCEL");
            
            dialog.Confirmed += () => {
                deleteAction();
                
                LoadGlossary();
                
                bool useDict = Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "true"));
                if (useDict)
                {
                    EmitSignal(SignalName.DictwSettingsChanged);
                }
                
                UpdateStat(L.T("MSG_DELETED_SUCCESS"));
            };
            
            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void ImportGlossary(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                var imported = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (imported != null)
                {
                    int count = 0;
                    int currentTab = _tabContainer.CurrentTab;
                    var targetDict = currentTab == 0 ? _userGlossary : _modGlossary;
                    string category = currentTab == 0 ? "UserDict" : "ModDict";
                    string modName = currentTab == 0 ? "" : _currMod;

                    foreach (var kvp in imported)
                    {
                        if (!targetDict.ContainsKey(kvp.Key))
                        {
                            targetDict[kvp.Key] = kvp.Value;
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        SkyrimModTranslator.Core.Dict.DictStorage.Save(category, modName, targetDict);
                    }
                    SaveGlossary();
                    RefreshGlossaryList();
                    UpdateStat($"已导入 {count} 个新词条");
                }
            }
            catch (Exception ex)
            {
                UpdateStat($"导入词典失败: {ex.Message}");
            }
        }

        private void ImportTxtDictionary(string filePath)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                int importedCount = 0;
                
                int currentTab = _tabContainer.CurrentTab;
                var targetDict = currentTab == 0 ? _userGlossary : _modGlossary;
                string category = currentTab == 0 ? "UserDict" : "ModDict";
                string modName = currentTab == 0 ? "" : _currMod;
                
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;
                    
                    int pipeIndex = trimmedLine.IndexOf('|');
                    if (pipeIndex > 0 && pipeIndex < trimmedLine.Length - 1)
                    {
                        string original = trimmedLine.Substring(0, pipeIndex).Trim();
                        string translation = trimmedLine.Substring(pipeIndex + 1).Trim();
                        
                        if (!string.IsNullOrEmpty(original) && !string.IsNullOrEmpty(translation))
                        {
                            if (!targetDict.ContainsKey(original))
                            {
                                targetDict[original] = translation;
                                importedCount++;
                            }
                        }
                    }
                }
                
                if (importedCount > 0)
                {
                    SkyrimModTranslator.Core.Dict.DictStorage.Save(category, modName, targetDict);
                }
                SaveGlossary();
                RefreshGlossaryList();
                UpdateStat($"已导入 {importedCount} 个新词条");
            }
            catch (Exception ex)
            {
                UpdateStat($"导入文本词典失败: {ex.Message}");
            }
        }

        private void OnExportPressed()
        {
            var fd = new FileDialog();
            fd.UseNativeDialog = true;
            fd.FileMode = FileDialog.FileModeEnum.SaveFile;
            fd.Access = FileDialog.AccessEnum.Filesystem;
            fd.Filters = new string[] { "*.json ; JSON 文件" };
            fd.Size = new Vector2I(800, 600);
            fd.Exclusive = false;
            fd.CurrentDir = Pos.GetSetting("last_export_dir", "");
            
            string prevPos = Pos.GetSetting("fd_export_pos", "");
            if (!string.IsNullOrEmpty(prevPos)) {
                string[] parts = prevPos.Split('|');
                fd.Position = new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));
            }
            
            fd.FileSelected += (path) => {
                Pos.SaveSetting("last_export_dir", Path.GetDirectoryName(path));
                Pos.SaveSetting("fd_export_pos", $"{fd.Position.X}|{fd.Position.Y}");
                ExportGlossary(path);
            };
            GetTree().Root.AddChild(fd);
            fd.PopupCentered();
        }

        private void ExportGlossary(string path)
        {
            try
            {
                int currentTab = _tabContainer.CurrentTab;
                var exportDict = currentTab == 0 ? _userGlossary : _modGlossary;
                
                string json = JsonSerializer.Serialize(exportDict, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(path, json);
                UpdateStat($"词典已导出到: {path}");
            }
            catch (Exception ex)
            {
                UpdateStat($"导出词典失败: {ex.Message}");
            }
        }

        private void UpdateStat(string message)
        {
            int currentTab = _tabContainer.CurrentTab;
            if (currentTab == 0)
            {
                _lblStat.Text = L.T("STATUS_DICT_ENTRIES").Replace("{num}", _userGlossary.Count.ToString());
            }
            else if (currentTab == 1 && !string.IsNullOrEmpty(_currMod))
            {
                _lblStat.Text = L.T("STATUS_DICT_ENTRIES").Replace("{num}", _modGlossary.Count.ToString());
            }
            else
            {
                _lblStat.Text = message;
            }
        }

        public Dictionary<string, string> GetGlossary()
        {
            int currentTab = _tabContainer.CurrentTab;
            if (currentTab == 0)
                return _userGlossary;
            else if (currentTab == 1 && !string.IsNullOrEmpty(_currMod))
                return _modGlossary;
            return _userGlossary;
        }

        public string FindTranslation(string original)
        {
            int currentTab = _tabContainer.CurrentTab;
            if (currentTab == 0)
                return _userGlossary.TryGetValue(original, out var translation) ? translation : null;
            else if (currentTab == 1 && !string.IsNullOrEmpty(_currMod))
                return _modGlossary.TryGetValue(original, out var translation) ? translation : null;
            return _userGlossary.TryGetValue(original, out var userTranslation) ? userTranslation : null;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                if (keyEvent.Keycode == Key.A && keyEvent.CtrlPressed)
                {
                    var currentList = _tabContainer.GetCurrentTabControl() as ItemList;
                    if (currentList != null)
                    {
                        for (int i = 0; i < currentList.ItemCount; i++) currentList.Select(i);
                    }
                }
                if (keyEvent.Keycode == Key.Delete)
                {
                    OnDeletePressed();
                }
            }
        }

        public void RefreshLocalization()
        {
            Title = L.T("WIN_DIST_TITLE");
            _searchBar.PlaceholderText = L.T("PLACEHOLDER_SEARCH");
            _btnDelete.Text = L.T("BTN_DELETE");
            _btnAdd.Text = L.T("BTN_ADD_ENTRY");
            _btnImport.Text = L.T("BTN_IMPORT");
            _btnExport.Text = L.T("BTN_EXPORT");
            _btnOpenFolder.Text = L.T("BTN_OPEN_FOLDER");
            
            _tabContainer.SetTabTitle(0, L.T("TAB_USER_DICT"));
            _tabContainer.SetTabTitle(1, L.T("TAB_MOD_DICT"));
            
            RefreshGlossaryList();
        }

        public void ResetModSelector()
        {
            _modSelector.Clear();
            _modSelector.AddItem(L.T("MOD_SELECTOR_NONE"));
            _modSelector.Select(0);
            _currMod = "";
            _modGlossary.Clear();
            RefreshGlossaryList();
        }

        private void OnEditExportPromptPressed()
        {
            string key = $"{GetType().Name}_PromptEdit";
            
            var dialog = new Window { Title = L.T("WIN_EDIT_PROMPT") };
            dialog.MinSize = new Vector2I(600, 400);
            dialog.Size = new Vector2I(800, 600);
            SkyrimModTranslator.Core.Theme.ApplyStdBg(dialog);

            string pos = Pos.GetSetting($"win_pos_{key}", "");
            if (!string.IsNullOrEmpty(pos))
            {
                string[] p = pos.Split('|');
                dialog.Position = new Vector2I(int.Parse(p[0]), int.Parse(p[1]));
            }
            else
            {
                dialog.Position = new Vector2I(
                    (int)(DisplayServer.WindowGetSize().X / 2 - dialog.Size.X / 2),
                    (int)(DisplayServer.WindowGetSize().Y / 2 - dialog.Size.Y / 2)
                );
            }

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_top", 25);
            margin.AddThemeConstantOverride("margin_bottom", 25);
            margin.AddThemeConstantOverride("margin_left", 30);
            margin.AddThemeConstantOverride("margin_right", 30);
            dialog.AddChild(margin);

            var vBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            vBox.AddThemeConstantOverride("separation", 18);
            margin.AddChild(vBox);

            var textEdit = new TextEdit { 
                Text = _exportPrompt,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                WrapMode = TextEdit.LineWrappingMode.Boundary,
                ThemeTypeVariation = "Editor"
            };

            var style = new StyleBoxFlat {
                BgColor = new Color(0.1f, 0.1f, 0.12f),
                BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
                BorderColor = new Color(0.3f, 0.3f, 0.35f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
            };
            var focusStyle = style.Duplicate() as StyleBoxFlat;
            focusStyle.BorderColor = new Color(0.2f, 0.6f, 1.0f);
            
            textEdit.AddThemeStyleboxOverride("normal", style);
            textEdit.AddThemeStyleboxOverride("focus", focusStyle);
            textEdit.AddThemeStyleboxOverride("selection", new StyleBoxFlat { BgColor = new Color(0.25f, 0.4f, 0.75f) });
            
            vBox.AddChild(textEdit);

            var hBox = new HBoxContainer();
            var btnSave = new Button { Text = L.T("BTN_SAVE"), CustomMinimumSize = new Vector2(100, 40) };
            var btnCancel = new Button { Text = L.T("BTN_CANCEL"), CustomMinimumSize = new Vector2(100, 40) };

            btnSave.Pressed += () => {
                _exportPrompt = textEdit.Text;
                Pos.SaveSetting("export_prompt", _exportPrompt);
                Pos.SaveSetting($"win_pos_{key}", $"{dialog.Position.X}|{dialog.Position.Y}");
                dialog.QueueFree();
            };

            btnCancel.Pressed += () => {
                Pos.SaveSetting($"win_pos_{key}", $"{dialog.Position.X}|{dialog.Position.Y}");
                dialog.QueueFree();
            };

            hBox.AddChild(btnSave);
            hBox.AddChild(btnCancel);

            vBox.AddChild(hBox);
            _promptEditWindow = dialog;
            GetTree().Root.AddChild(dialog);
            dialog.Visible = true;
            
            dialog.CloseRequested += () => {
                Pos.SaveSetting($"win_pos_{key}", $"{dialog.Position.X}|{dialog.Position.Y}");
                dialog.QueueFree();
                _promptEditWindow = null;
            };
        }
    }
}
