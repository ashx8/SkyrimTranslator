//主界面
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    public static class MainSingleton
    {
        public static Main Instance { get; private set; }

        public static void SetInstance(Main main) { Instance = main; }
        
        public static int GetSameOriginalCount(string original)
        {
            return Instance?.GetSameOriginalCount(original) ?? 0;
        }
        
        public static void ApplyTranslationToSameOriginal(string original, string translation)
        {
            Instance?.ApplyTranslationToSameOriginal(original, translation);
        }
        
        public static void AutoFillOrClear(SkyrimModTranslator.Core.Data.Item item)
        {
            Instance?.AutoFillOrClear(item);
        }
    }


    public partial class Main : Control  {
        private TextureRect _background;
        private ColorRect _overlay;
        private Control _bgContainer;
        private Control _bgInputIntercept;
        private ColorRect _blurRect;
        private Label _emptyTip;
        private string _bgPath = "user://custom_bg.png";
        private bool _isLoaded = false;

        private TabContainer _tabs;
        private LineEdit _search;
        private OptionButton _selMod;
        private Button _btnOpen;
        private Button _btnSave;
        private Button _btnBackup;
        private Button _btnDist;
        private Button _btnImport;
        private Button _btnSettings;
        private Button _btnBatch;
        private CheckBox _chkFilter;
        private OptionButton _selLang;
        private StatusBar _statusBar;

        private Dictw _distWin = null;
        private Back _backupWin = null;
        private BatchImport _batchImportWin = null;
        private Batch _batchWin = null;
        private Book _bookWin = null;
        private Parser _parser;
        private ModFileWriter _writer;
        private List<Data.Mod> _projects = new List<Data.Mod>();
        private Data.Mod _currentProject;
        private Dictionary<string, Tree> _treeMap = new Dictionary<string, Tree>();
        private Dictionary<string, Data.Item> _map = new Dictionary<string, Data.Item>();
        private Edit _editW = null;
        private RefW _refW = null;
        private List<int> _selectedTabIndices = new List<int>();
        private bool _isMouseOverWindow = true;
        private AcceptDialog _activeMessageDialog = null;
        private bool _isAttached = true;
        private bool _isSyncing = false;
        private const int SNAP_DIST = 30;
        private Vector2I _lastEditPos = Vector2I.Zero;
        private Vector2I _lastRefPos = Vector2I.Zero;
        private int _searchId = 0;

        public override void _Ready()
        {
            GetTree().SetAutoAcceptQuit(false);
            
            var defaultMap = Cfg.GetDefTransMap();
            Cfg.SvTransMap(defaultMap);
            
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            
            MainSingleton.SetInstance(this);
            
            string transFolder = ProjectSettings.GlobalizePath("res://Localization/");
            
            string savedLang = Pos.GetSetting("user_selected_lang", "");
            if (string.IsNullOrEmpty(savedLang))
            {
                savedLang = GetAutoMatchSystemLanguage(transFolder);
                Pos.SaveSetting("user_selected_lang", savedLang);
            }

            L.Load(savedLang);

            SetAnchorsPreset(Control.LayoutPreset.FullRect);
            
            LoadWindowTransform();

            _parser = new Parser();
            _parser.TranslationMap = Cfg.LdTransMap();
            _writer = new ModFileWriter();
            
            L.OnLanguageChanged += OnLanguageChanged;

            CreateLayout();
            SetupConnections();

            CallDeferred(nameof(AddWindowEventListeners));

            _isLoaded = true;
            LoadBackgroundSettings();
            
            UpdateUIStrings();
            RefreshStatusBar();
        }

        private void DeferredRestore()
        {
            Size += new Vector2I(1, 1);
            Size -= new Vector2I(1, 1);
        }

        private void CreateLayout()
        {
            _btnOpen = new Button { Text = L.T("BTN_OPEN") };
            _btnSave = new Button { Text = L.T("BTN_SAVE") };
            _btnBackup = new Button { Text = L.T("BTN_BACKUP") };
            _btnDist = new Button { Text = L.T("BTN_GLOSSARY") };
            _btnImport = new Button { Text = L.T("BTN_IMPORT") };

            _bgContainer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            _bgContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _bgContainer.ClipContents = true;
            AddChild(_bgContainer);

            _background = new TextureRect {
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                Scale = new Vector2(1.05f, 1.05f),
                Modulate = new Color(1, 1, 1, 0.4f)
            };
            _bgContainer.AddChild(_background);

            _bgInputIntercept = new Control {
                ZIndex = 100,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _bgInputIntercept.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_bgInputIntercept);

            _overlay = new ColorRect { Color = new Color(0, 0, 0, 0.3f), MouseFilter = Control.MouseFilterEnum.Ignore };
            _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_overlay);

            _emptyTip = new Label {
                Text = L.T("DROP_TIP"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _emptyTip.AddThemeFontSizeOverride("font_size", 36);
            _emptyTip.SetAnchorsPreset(Control.LayoutPreset.Center);
            _emptyTip.OffsetLeft = -300;
            _emptyTip.OffsetRight = 300;
            _emptyTip.OffsetTop = -50;
            _emptyTip.OffsetBottom = 50;
            AddChild(_emptyTip);

            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 30);
            margin.AddThemeConstantOverride("margin_right", 30);
            margin.AddThemeConstantOverride("margin_top", 25);
            margin.AddThemeConstantOverride("margin_bottom", 25);
            AddChild(margin);

            var vBox = new VBoxContainer { 
                SizeFlagsVertical = Control.SizeFlags.ExpandFill 
            };
            vBox.AddThemeConstantOverride("separation", 15);
            margin.AddChild(vBox);

            var titleBar = new HBoxContainer();
            titleBar.AddThemeConstantOverride("separation", 12);
            vBox.AddChild(titleBar);

            _btnOpen.Text = L.T("BTN_OPEN");
            _btnOpen.CustomMinimumSize = new Vector2(120, 40);
            titleBar.AddChild(_btnOpen);

            _selMod = new OptionButton { CustomMinimumSize = new Vector2(250, 0) };
            _selMod.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            _selMod.ClipText = true;
            _selMod.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _selMod.AddItem(L.T("MOD_SELECTOR_DEFAULT"));
            titleBar.AddChild(_selMod);

            _search = new LineEdit
            {
                PlaceholderText = L.T("SEARCH_PLACEHOLDER"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            titleBar.AddChild(_search);

            _btnDist.Text = L.T("BTN_GLOSSARY");
            _btnDist.CustomMinimumSize = new Vector2(100, 40);
            titleBar.AddChild(_btnDist);

            _btnBackup.Text = L.T("BTN_BACKUP");
            _btnBackup.CustomMinimumSize = new Vector2(100, 40);
            titleBar.AddChild(_btnBackup);

            _selLang = new OptionButton();
            LoadLanguages();
            titleBar.AddChild(_selLang);

            _tabs = new TabContainer();
            _tabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            vBox.AddChild(_tabs);

            var tabBar = _tabs.GetTabBar();
            ApplyTabBarStyle(tabBar);
            tabBar.GuiInput += OnTabBarGuiInput;

            var actionBar = new HBoxContainer();
            actionBar.AddThemeConstantOverride("separation", 10);
            vBox.AddChild(actionBar);

            _statusBar = new StatusBar();
            _statusBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            actionBar.AddChild(_statusBar);

            var rightButtons = new HBoxContainer();
            rightButtons.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
            rightButtons.AddThemeConstantOverride("separation", 10);
            actionBar.AddChild(rightButtons);

            _chkFilter = new CheckBox { Text = L.T("CHK_FILTER_UNTRANSLATED") };
            _chkFilter.Toggled += OnFilterUntranslatedToggled;
            rightButtons.AddChild(_chkFilter);

            _btnImport.Text = L.T("BTN_IMPORT_TRANSLATION");
            _btnImport.CustomMinimumSize = new Vector2(140, 40);
            _btnImport.Pressed += OnImportPressed;
            rightButtons.AddChild(_btnImport);

            _btnBatch = new Button { Text = L.T("BTN_BATCH_PROCESS"), CustomMinimumSize = new Vector2(100, 40) };
            _btnBatch.Pressed += OnBatchProcessPressed;
            rightButtons.AddChild(_btnBatch);

            _btnSave.Text = L.T("BTN_SAVE");
            _btnSave.CustomMinimumSize = new Vector2(100, 40);
            rightButtons.AddChild(_btnSave);

            var btnSettings = new Button { Text = L.T("BTN_SETTINGS"), CustomMinimumSize = new Vector2(100, 40) };
            _btnSettings = btnSettings;
            btnSettings.Pressed += OnSettingsPressed;
            rightButtons.AddChild(btnSettings);

            Connect("resized", Callable.From(OnWindowResized));

            LoadBackgroundSettings();
            _isLoaded = true;
        }
        
        //应用标签栏样式
        private void ApplyTabBarStyle(TabBar bar)
        {
            bar.AddThemeConstantOverride("outline_size", 0);

            var hoverStyle = new StyleBoxFlat {
                BgColor = new Color(0.3f, 0.3f, 0.4f, 0.6f),
                ContentMarginLeft = 12,
                ContentMarginRight = 12
            };
            bar.AddThemeStyleboxOverride("tab_hovered", hoverStyle);

            var selStyle = new StyleBoxFlat {
                BgColor = new Color(0.2f, 0.5f, 0.9f, 0.8f),
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                BorderWidthBottom = 3,
                BorderColor = Colors.White
            };
            bar.AddThemeStyleboxOverride("tab_selected", selStyle);

            bar.AddThemeColorOverride("font_selected_color", Colors.White);
            bar.AddThemeColorOverride("font_color", Colors.Gray);

            bar.TabSelected += OnTabSelected;
        }



        //刷新状态栏
        private void RefreshStatusBar()
        {
            if (_currentProject == null)
            {
                _statusBar.Clear();
                return;
            }

            int total = _currentProject.Items.Count;
            int trans = _currentProject.Items.Count(i => !string.IsNullOrEmpty(i.Trans));
            _statusBar.Update(Path.GetFileName(_currentProject.Path), total, trans);
        }

        private void RefreshTabHighlights(TabBar bar)
        {
            for (int i = 0; i < bar.TabCount; i++)
            {
                string baseTitle = bar.GetTabTitle(i).Replace(" ● ", "").Replace(" ◈ ", "");

                if (_selectedTabIndices.Contains(i))
                {
                    bar.SetTabTitle(i, " ◈ " + baseTitle);
                }
                else
                {
                    bar.SetTabTitle(i, baseTitle);
                }
            }
        }
        
        //加载背景设置
        private void LoadBackgroundSettings()
        {
            string bgOpacity = Pos.GetSetting("bg_opacity", "0.4");
            float opacity = float.Parse(bgOpacity);
            _background.Modulate = new Color(1, 1, 1, opacity);

            _background.SetAnchorsPreset(Control.LayoutPreset.Center);
            _background.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;

            string bgPath = Pos.GetSetting("bg_path", "user://custom_bg.png");
            string fullPath = ProjectSettings.GlobalizePath(bgPath);
            
            if (File.Exists(fullPath)) {
                _background.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(fullPath));
            } else {
                _background.Texture = GD.Load<Texture2D>("res://icon.svg");
            }
            
            OnWindowResized();
        }
        
        //默认语言
        private string GetAutoMatchSystemLanguage(string folder)
        {
            if (!Directory.Exists(folder)) return L.T("DEFAULT_LANGUAGE");
            string[] filePaths = Directory.GetFiles(folder, "*.json");
            List<string> files = new List<string>();
            for (int i = 0; i < filePaths.Length; i++)
            {
                files.Add(Path.GetFileNameWithoutExtension(filePaths[i]));
            }
            if (files.Count == 0) return "";

            string sysLocale = OS.GetLocale();
            if (files.Contains(sysLocale)) return sysLocale;

            string prefix = sysLocale.Split('_')[0];
            string match = "";
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].ToLower().StartsWith(prefix.ToLower()))
                {
                    match = files[i];
                    break;
                }
            }
            
            return string.IsNullOrEmpty(match) ? files[0] : match;
        }
        
        //加载语言
        private void LoadLanguages()
        {
            _selLang.Clear();
            string transFolder = ProjectSettings.GlobalizePath("res://Localization/");
            if (!Directory.Exists(transFolder)) return;
            
            var files = Directory.GetFiles(transFolder, "*.json");
            for (int i = 0; i < files.Length; i++)
            {
                string langName = Path.GetFileNameWithoutExtension(files[i]);
                _selLang.AddItem(langName);
                
                if (langName == L.Current) 
                {
                    _selLang.Select(i);
                }
            }
        }
    
        private void OnWindowResized()
        {
            if (!_isLoaded) return;
            
            _background.Size = (Vector2)Size * 1.1f;
            _background.Position = -(_background.Size - (Vector2)Size) / 2;
        }

        private void UpdateParallaxBackground(InputEventMouseMotion motion)
        {
            if (!_isMouseOverWindow) return;
            
            Vector2 windowCenter = (Vector2)Size / 2.0f;
            Vector2 offsetRange = (motion.Position - windowCenter) / windowCenter;
            
            float intensity = 20.0f;
            Vector2 targetPos = -(_background.Size - (Vector2)Size) / 2 - (offsetRange * intensity);
            
            _background.Position = _background.Position.Lerp(targetPos, 0.1f);
        }

        //选择语言
        private void OnLanguageSelected(long index)
        {
            string langFile = _selLang.GetItemText((int)index);
            string langCode = langFile.Split('.')[0];
            L.Load(langCode);
            
            Pos.SaveSetting("user_selected_lang", langCode);

            RefreshAllInterfaceTexts();
            
            if (IsInstanceValid(_editW))
            {
                _editW.RefreshLocalization();
            }
            if (IsInstanceValid(_distWin))
            {
                _distWin.RefreshLocalization();
            }
            
            Set settingsWin = null;
            var children = GetTree().Root.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Set)
                {
                    settingsWin = (Set)children[i];
                    break;
                }
            }
            if (settingsWin != null)
            {
                settingsWin.RefreshLocalization();
            }
            
            RefreshTabLabels();
        }

        private void RefreshAllInterfaceTexts()
        {
            _btnOpen.Text = L.T("BTN_OPEN");
            _btnSave.Text = L.T("BTN_SAVE");
            _btnBackup.Text = L.T("BTN_BACKUP");
            _btnDist.Text = L.T("BTN_GLOSSARY");
            _btnImport.Text = L.T("BTN_IMPORT_TRANSLATION");
            if (_btnSettings != null)
                _btnSettings.Text = L.T("BTN_SETTINGS");
            if (_btnBatch != null)
                _btnBatch.Text = L.T("BTN_BATCH_PROCESS");

            _emptyTip.Text = L.T("DROP_TIP");
            _search.PlaceholderText = L.T("SEARCH_PLACEHOLDER");
            
            if (_projects.Count == 0)
                _selMod.SetItemText(0, L.T("MOD_SELECTOR_DEFAULT"));
            
            RefreshStatusBar();
        }

        private void RefreshTabLabels()
        {
            if (_currentProject != null)
            {
                RefreshUI();
            }
        }

        private void OnLanguageChanged()
        {
            _btnOpen.Text = L.T("BTN_OPEN");
            _btnSave.Text = L.T("BTN_SAVE");
            _btnBackup.Text = L.T("BTN_BACKUP");
            _btnDist.Text = L.T("BTN_GLOSSARY");
            _search.PlaceholderText = L.T("PLACEHOLDER_SEARCH");
            _emptyTip.Text = L.T("DROP_TIP");
            
            if (_chkFilter != null)
            {
                _chkFilter.Text = L.T("CHK_FILTER_UNTRANSLATED");
            }
            
            if (_btnBatch != null)
            {
                _btnBatch.Text = L.T("BTN_BATCH_PROCESS");
            }
            
            if (_btnImport != null)
            {
                _btnImport.Text = L.T("BTN_IMPORT_TRANSLATION");
            }
            
            if (_btnSettings != null)
            {
                _btnSettings.Text = L.T("BTN_SETTINGS");
            }
        }

        private void LoadWindowTransform()
        {
            string data = Pos.GetSetting("win_trans_main", "");
            if (!string.IsNullOrEmpty(data))
            {
                string[] parts = data.Split('|');
                var root = GetTree().Root;
                if (root != null && IsInstanceValid(root))
                {
                    root.Size = new Vector2I(int.Parse(parts[2]), int.Parse(parts[3]));
                    root.Position = new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));
                }
            }
        }

        private void OnCloseRequested()
        {
            bool hasUnsavedChanges = false;
            if (_currentProject != null && 
                !_selMod.GetItemText(_selMod.Selected).StartsWith(L.T("PREFIX_APPLIED")))
            {
                foreach (var entry in _currentProject.Items)
                {
                    if (entry.IsTranslated && !entry.IsDictApplied)
                    {
                        hasUnsavedChanges = true;
                        break;
                    }
                }
            }

            if (hasUnsavedChanges)
            {
                var confirmDialog = new ConfirmationDialog();
                confirmDialog.Title = L.T("MSG_UNSAVED_TITLE");
                confirmDialog.DialogText = L.T("MSG_UNSAVED_TEXT");
                confirmDialog.GetOkButton().Text = L.T("BTN_EXIT_CONFIRM");
                confirmDialog.GetCancelButton().Text = L.T("BTN_RETURN_EDIT");
                var btnOk = confirmDialog.GetOkButton();
                btnOk.CustomMinimumSize = new Vector2(130, 45);
                var btnCancel = confirmDialog.GetCancelButton();
                btnCancel.CustomMinimumSize = new Vector2(130, 45);
                GetTree().Root.AddChild(confirmDialog);
                confirmDialog.PopupCentered();

                confirmDialog.Confirmed += OnExitConfirm;
                confirmDialog.Canceled += OnExitCancel;
                confirmDialog.SetMeta("dialog", confirmDialog);
                return;
            }

            PerformAppQuit();
        }

        private void OnExitConfirm()
        {
            var confirmDialog = (ConfirmationDialog)GetTree().Root.GetChild(GetTree().Root.GetChildCount() - 1);
            if (confirmDialog != null)
            {
                PerformAppQuit();
            }
        }

        public override void _ExitTree()
        {
            var root = GetTree().Root;
            if (root != null && IsInstanceValid(root))
            {
                Pos.SaveSetting("win_trans_main", $"{root.Position.X}|{root.Position.Y}|{root.Size.X}|{root.Size.Y}");
            }
            base._ExitTree();
        }

        private void OnExitCancel()
        {
            var confirmDialog = (ConfirmationDialog)GetTree().Root.GetChild(GetTree().Root.GetChildCount() - 1);
            if (confirmDialog != null)
            {
                confirmDialog.QueueFree();
            }
        }
        
        private void PerformAppQuit()
        {
            var root = GetTree().Root;
            Pos.SaveSetting("win_trans_main", $"{root.Position.X}|{root.Position.Y}|{root.Size.X}|{root.Size.Y}");

            CloseAllSubWins();

            GetTree().Quit();
        }



        private void OnMouseEntered()
        {
            _isMouseOverWindow = true;
        }

        private void OnMouseExited()
        {
            _isMouseOverWindow = false;
        }

        private void HideRootWindow()
        {
            var root = GetTree().Root;
            if (root != null && IsInstanceValid(root))
            {
                root.Visible = true;
            }
        }

        private void SetupConnections()
        {
            _btnOpen.Pressed += OnOpenRequest;
            _btnSave.Pressed += OnSaveRequest;
            _btnBackup.Pressed += OnBackupRequest;
            _btnDist.Pressed += OnDistRequest;
            _selMod.ItemSelected += OnModSelected;
            _search.TextChanged += OnSearchChanged;
            _selLang.ItemSelected += OnLanguageSelected;

            _selMod.GuiInput += OnModSelectorGuiInput;
        }

        private void AddWindowEventListeners()
        {
            var window = GetWindow();
            if (window != null && IsInstanceValid(window)) {
                window.FilesDropped += OnFilesDropped;
                window.MouseEntered += OnMouseEntered;
                window.MouseExited += OnMouseExited;
                window.CloseRequested += OnCloseRequested;
                var root = GetTree().Root;
                if (root != null && IsInstanceValid(root)) {
                    root.SizeChanged += OnRootWindowResized;
                }
            }
        }

        private void OnRootWindowResized()
        {
            var root = GetTree().Root;
            if (root != null && IsInstanceValid(root) && _isLoaded) {
                _background.Size = (Vector2)Size * 1.1f;
                _background.Position = -(_background.Size - (Vector2)Size) / 2;
            }
        }

        //处理文件拖动
        private void OnFilesDropped(string[] files)
        {
            List<string> modsToLoad = new List<string>();

            foreach (var path in files)
            {
                if (Directory.Exists(path))
                {
                    string[] allFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                    List<string> foundFilesList = new List<string>();
                    foreach (var file in allFiles)
                    {
                        if (file.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                        {
                            foundFilesList.Add(file);
                        }
                    }
                    modsToLoad.AddRange(foundFilesList);
                }
                else if (File.Exists(path))
                {
                    if (path.EndsWith(".esp") || path.EndsWith(".esm") || path.EndsWith(".esl"))
                    {
                        modsToLoad.Add(path);
                    }
                }
            }

            if (modsToLoad.Count > 0)
            {
                foreach (var modPath in modsToLoad)
                {
                    LoadMod(modPath);
                }
            }
            else
            {
                RefreshStatusBar();
            }
        }

        private void OnOpenRequest()
        {
            var fd = new FileDialog();
            
            fd.UseNativeDialog = true;
            
            fd.Title = L.T("FD_OPEN_TITLE");
            fd.FileMode = FileDialog.FileModeEnum.OpenFiles;
            fd.Access = FileDialog.AccessEnum.Filesystem;
            
            fd.Filters = new string[] { "*.esp,*.esm,*.esl ; " + L.T("FD_FILTER_DESC") };
            
            fd.CurrentDir = Pos.GetSetting("last_mod_dir", "C:/");
            
            fd.FilesSelected += OnFilesSelected;
            
            GetTree().Root.AddChild(fd);
            fd.PopupCentered();
        }

        private async void LoadMod(string path)
        {
            string canonicalPath = Path.GetFullPath(path).ToLowerInvariant();

            var existingProject = _projects.FirstOrDefault(p => Path.GetFullPath(p.Path).ToLowerInvariant() == canonicalPath);

            if (existingProject != null)
            {
                _projects.Remove(existingProject);
            }

            try
            {
                var mod = await System.Threading.Tasks.Task.Run(() => 
                    _parser.Parse(path, UpdateProgressCallback));

                if (mod.Items.Count == 0)
                {
                    RefreshStatusBar();
                    return; 
                }

                bool alreadyLoaded = false;
                foreach (Data.Mod existingMod in _projects)
                {
                    if (Path.GetFullPath(existingMod.Path).ToLowerInvariant() == canonicalPath)
                    {
                        alreadyLoaded = true;
                        break;
                    }
                }
                if (alreadyLoaded)
                {
                    RefreshStatusBar();
                    return;
                }

                _projects.Add(mod);
                
                _projects.Sort((a, b) => b.Items.Count.CompareTo(a.Items.Count));

                for (int i = 0; i < mod.Items.Count; i++)
                {
                    mod.Items[i].ExportId = i + 1;
                }

                ApplyDictionaryAutoTranslation(mod);

                UpdateModSelector();

                int index = _projects.IndexOf(mod);
                _selMod.Select(index);
                SwitchProject(index);

                RefreshStatusBar();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[main] 载入模组失败: {ex.Message}");
                RefreshStatusBar();
            }
        }

        private void UpdateModSelector()
        {
            int currentIdx = _selMod.Selected;
            _selMod.Clear();
            
            foreach (Data.Mod p in _projects)
            {
                string fileName = Path.GetFileName(p.Path);
                string prefix = p.IsApplied ? L.T("PREFIX_APPLIED") : "";
                
                _selMod.AddItem($"{prefix}{fileName} ({p.Items.Count})");
            }
            
            if (currentIdx >= 0 && currentIdx < _selMod.ItemCount)
                _selMod.Select(currentIdx);
        }

        public void ReloadAllMods()
        {
            _parser.TranslationMap = Cfg.LdTransMap();
            
            int currentIndex = _selMod.Selected;
            
            List<string> modPaths = new List<string>();
            foreach (var mod in _projects)
            {
                modPaths.Add(mod.Path);
            }
            
            _projects.Clear();
            
            foreach (var modPath in modPaths)
            {
                LoadMod(modPath);
            }
            
            UpdateModSelector();
            
            if (currentIndex >= 0 && currentIndex < _selMod.ItemCount)
            {
                _selMod.Select(currentIndex);
                OnModSelected(currentIndex);
            }
            
            RefreshStatusBar();
        }

        private void UpdateProgress(float progress)
        {
        }

        private void OnModSelected(long index)
        {
            if (index < 0) return;
            SwitchProject((int)index);
        }

        private void SwitchProject(int index)
        {
            _currentProject = _projects[index];
            RefreshUI();
            RefreshStatusBar();
        }

        private void RefreshUI()
        {
            if (_currentProject == null) return;
            
            _emptyTip.Visible = false;

            string savedCategoryName = "";
            int savedTabIndex = _tabs.CurrentTab;

            if (savedTabIndex >= 0 && savedTabIndex < _tabs.GetTabCount())
            {
                var currentChild = _tabs.GetChild<Control>(savedTabIndex);
                if (currentChild != null && currentChild.HasMeta("raw_category_name"))
                {
                    savedCategoryName = currentChild.GetMeta("raw_category_name").AsString();
                }
            }

            ClearTabs();
            _map.Clear();
            foreach (var entry in _currentProject.Items)
            {
                _map[entry.UniqueKey] = entry;
            }
            FilterAndPopulate();

            bool restored = false;
            if (!string.IsNullOrEmpty(savedCategoryName))
            {
                for (int i = 0; i < _tabs.GetTabCount(); i++)
                {
                    var child = _tabs.GetChild<Control>(i);
                    if (child != null && child.HasMeta("raw_category_name"))
                    {
                        if (child.GetMeta("raw_category_name").AsString() == savedCategoryName)
                        {
                            _tabs.CurrentTab = i;
                            restored = true;
                            break;
                        }
                    }
                }
            }

            if (!restored && savedTabIndex >= 0 && savedTabIndex < _tabs.GetTabCount())
            {
                _tabs.CurrentTab = savedTabIndex;
            }

            _selectedTabIndices.Clear();
            int currentTab = _tabs.CurrentTab;
            if (currentTab >= 0 && currentTab < _tabs.GetTabCount())
            {
                _selectedTabIndices.Add(currentTab);
            }

            RefreshTabHighlights(_tabs.GetTabBar());
            
            RefreshStatusBar();
        }

        private void ClearTabs()
        {
            foreach (var child in _tabs.GetChildren())
            {
                _tabs.RemoveChild(child);
                child.QueueFree();
            }
            _treeMap.Clear();
        }

        private void FilterAndPopulate()
        {
            string searchText = _search.Text.ToLower().Trim();
            bool isSearching = !string.IsNullOrEmpty(searchText);

            bool filterUntranslated = _chkFilter != null && _chkFilter.ButtonPressed;

            List<Data.Item> filtered = new List<Data.Item>();
            foreach (Data.Item e in _currentProject.Items)
            {
                bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                        e.Ori.ToLower().Contains(searchText) ||
                        (e.Trans != null && e.Trans.ToLower().Contains(searchText));

                bool matchesFilter = !filterUntranslated || string.IsNullOrEmpty(e.Trans);

                if (matchesSearch && matchesFilter)
                {
                    filtered.Add(e);
                }
            }

            foreach (var tree in _treeMap.Values)
            {
                tree.Clear();
            }

            foreach (var entry in filtered)
            {
                var category = GetCategoryForRecord(entry.Type);
                var tree = GetCategoryTab(category);

                var root = tree.GetRoot();
                if (root == null)
                {
                    root = tree.CreateItem();
                }

                var item = tree.CreateItem(root);

                string formIdStr = entry.FormID.ToString("X8");
                item.SetText(0, formIdStr);
                item.SetText(1, $"{entry.Type} {entry.FType}");

                string displayOriginal = entry.Ori.Replace("\r", "").Replace("\n", " ↵ ");
                string displayTranslated = string.IsNullOrEmpty(entry.Trans) 
                    ? L.T("STATUS_UNTRANSLATED") 
                    : (entry.IsDictApplied ? string.Format("{0}{1}", L.T("PREFIX_DICT"), entry.Trans) : entry.Trans).Replace("\r", "").Replace("\n", " ↵ ");

                if (isSearching && !string.IsNullOrEmpty(searchText))
                {
                    displayOriginal = displayOriginal.Replace(searchText, $"{searchText.ToUpper()}", System.StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(displayTranslated) && displayTranslated != "[未翻译]")
                    {
                        displayTranslated = displayTranslated.Replace(searchText, $"{searchText.ToUpper()}", System.StringComparison.OrdinalIgnoreCase);
                    }
                }

                item.SetText(2, displayOriginal);
                item.SetText(3, displayTranslated);

                item.SetMetadata(0, entry.UniqueKey);

                if (isSearching)
                {
                    if (entry.Ori.ToLower().Contains(searchText) || 
                        (entry.Trans != null && entry.Trans.ToLower().Contains(searchText)))
                    {
                        SetHlCell(item, 2, entry.Ori, searchText);
                        SetHlCell(item, 3, entry.Trans ?? "[未翻译]", searchText);
                    }
                }

                if (entry.IsDictApplied)
                {
                    item.SetCustomColor(3, new Color(0.4f, 0.6f, 1.0f));
                }
                else if (entry.IsTranslated)
                {
                    item.SetCustomColor(3, Colors.SpringGreen);
                }
                else
                {
                    item.SetCustomColor(3, Colors.Salmon);
                }
            }
        }

        private string GetCategoryForRecord(string recordType)
        {
            var categories = L.GetCats();
            if (categories != null && categories.Count > 0)
            {
                foreach (var category in categories)
                {
                    if (category.Value.Contains(recordType))
                    {
                        return category.Key;
                    }
                }
            }
            return L.T("CATEGORY_UNCATEGORIZED");
        }

        private Tree GetCategoryTab(string categoryName)
        {
            if (_treeMap.ContainsKey(categoryName))
            {
                return _treeMap[categoryName];
            }

            var tree = new Tree
            {
                SelectMode = Tree.SelectModeEnum.Multi,
                HideRoot = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            
            var treeBg = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0.0f),
            };
            tree.AddThemeStyleboxOverride("panel", treeBg);

            var popup = new PopupMenu();
            popup.AddItem(L.T("MENU_COPY_ORIGINAL"), id: 2);
            popup.AddSeparator();
            popup.AddItem(L.T("MENU_BATCH_PRE"), id: 10);
            popup.AddItem(L.T("MENU_BATCH_SUF"), id: 11);
            popup.AddSeparator();
            popup.AddItem(L.T("MENU_CLEAR_TRANSLATION"), id: 3);
            
            popup.IdPressed += OnPopupIdPressed;
            popup.Name = "ContextMenu";
            tree.AddChild(popup);

            tree.GuiInput += OnTreeGuiInput;

            tree.SetColumns(4);
            tree.SetColumnTitle(0, "FormID");
            tree.SetColumnExpand(0, false);
            tree.SetColumnCustomMinimumWidth(0, 110);

            tree.SetColumnTitle(1, L.T("COLUMN_TYPE"));
            tree.SetColumnExpand(1, false);
            tree.SetColumnCustomMinimumWidth(1, 120);

            tree.SetColumnTitle(2, L.T("COLUMN_ORIGINAL"));
            tree.SetColumnExpand(2, true);
            tree.SetColumnClipContent(2, true);

            tree.SetColumnTitle(3, L.T("COLUMN_TRANSLATED"));
            tree.SetColumnExpand(3, true);
            tree.SetColumnClipContent(3, true);

            tree.ItemActivated += OnTreeItemActivated;

            var scroll = new ScrollContainer();
            scroll.Name = "CategoryScroll";
            scroll.SetMeta("raw_category_name", categoryName);
            scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.AddChild(tree);

            _tabs.AddChild(scroll);
            _tabs.SetTabTitle(_tabs.GetTabCount() - 1, categoryName);
            _treeMap[categoryName] = tree;

            return tree;
        }

        private void OnTreeItemActivated()
        {
            var currentScroll = _tabs.GetChild<ScrollContainer>(_tabs.CurrentTab);
            var categoryName = currentScroll.GetMeta("raw_category_name").AsString();
            var tree = _treeMap[categoryName];
            var selected = tree.GetSelected();

            if (selected == null) 
            {
                return;
            }

            var key = selected.GetMetadata(0).AsString();
            if (!_map.TryGetValue(key, out var entry)) 
            {
                return;
            }
            
            bool needsRefWindow = !IsInstanceValid(_refW);
            
            SetupEditor(entry, selected);
            
            if (needsRefWindow)
            {
                bool hasPotentialContent = !string.IsNullOrEmpty(entry.Ori) || entry.Type == "INFO" || entry.Type == "NPC_";
                
                if (hasPotentialContent)
                {
                    _refW = new RefW();
                    
                    _refW.RequestJump += OnRefWRequestJump;
                    
                    _refW.CloseRequested += CloseAllEditors;
                    
                    _refW.Transient = true;
                    
                    _refW.Visible = false;
                    GetTree().Root.AddChild(_refW);
                    
                    _refW.Position = new Vector2I((int)this.Position.X, (int)this.Position.Y) + new Vector2I((int)this.Size.X + 10, 0);
                    _refW.Size = new Vector2I(300, 400);
                    
                    _refW.UpdateList(entry, _currentProject.Items);
                }
            }
            else
            {
                _refW.UpdateList(entry, _currentProject.Items);
            }
            
            if (IsInstanceValid(_refW) && _refW.Visible)
            {
                _refW.GrabFocus();
            }
            
            if (IsInstanceValid(_editW))
            {
                _editW.GrabFocus();
                _editW.FocusTranslatedTextEdit();
            }
        }

        private void CloseAllEditors()
        {
            if (IsInstanceValid(_editW))
            {
                _editW.QueueFree();
                _editW = null;
            }
            if (IsInstanceValid(_refW))
            {
                _refW.QueueFree();
                _refW = null;
            }
        }
        
        private void CloseAllEditorsWithSave()
        {
            if (IsInstanceValid(_editW))
            {
                _editW.ManualSave();
                _editW.QueueFree();
                _editW = null;
            }
            if (IsInstanceValid(_refW))
            {
                _refW.QueueFree();
                _refW = null;
            }
        }
        
        private void CloseAllSubWins()
        {
            CloseAllEditors();
            
            if (IsInstanceValid(_distWin))
            {
                _distWin.QueueFree();
                _distWin = null;
            }
            if (IsInstanceValid(_backupWin))
            {
                _backupWin.QueueFree();
                _backupWin = null;
            }
            if (IsInstanceValid(_batchImportWin))
            {
                _batchImportWin.QueueFree();
                _batchImportWin = null;
            }
            if (IsInstanceValid(_batchWin))
            {
                _batchWin.QueueFree();
                _batchWin = null;
            }
            if (IsInstanceValid(_bookWin))
            {
                _bookWin.QueueFree();
                _bookWin = null;
            }
        }
        
        private void SetupEditor(Data.Item entry, TreeItem selected)
        {
            string displayTrans = entry.Trans ?? "";
            if (displayTrans.StartsWith("[已应用] ")) displayTrans = displayTrans.Substring(6);
            
            entry.Trans = displayTrans;
            
            if (IsInstanceValid(_editW))
            {
                _editW.ManualSave();
                
                _editW.UpdateEntry(entry); 
                
                _editW.GrabFocus();
                _editW.FocusTranslatedTextEdit();
                
                bool canPreview = (entry.Type == "BOOK" && entry.FType == "DESC");
                _editW.UpdatePreviewConnectivity(canPreview);
            }
            else
            {
                _editW = new Edit(entry); 
                _editW.TreeExited += OnEditorTreeExited;
                
                _editW.ChangedNotify -= UpdateVisibleTreeContent; 
                _editW.ChangedNotify += UpdateVisibleTreeContent;
                
                _editW.CloseRequested += OnEditWindowCloseRequested;
                
                GetTree().Root.AddChild(_editW);
                _editW.PopupCentered();
            }
            
            _editW.GrabFocus();
            
            CallDeferred(MethodName.SyncRefToEdit);
        }
        
        private void SyncRefToEdit() {
            if (!IsInstanceValid(_editW) || !IsInstanceValid(_refW) || !_isAttached) return;
            _isSyncing = true;
            _refW.Position = new Vector2I(
                _editW.Position.X + _editW.Size.X + 2,
                _editW.Position.Y
            );
            _refW.Size = new Vector2I(_refW.Size.X, _editW.Size.Y);
            _isSyncing = false;
        }

        private void OnEditMoved() {
            if (_isAttached && !_isSyncing) {
                SyncRefToEdit();
            }
        }

        private void OnRefMoved() {
            if (_isSyncing || !IsInstanceValid(_editW)) return;

            Vector2I targetPosition = new Vector2I(_editW.Position.X + _editW.Size.X, _editW.Position.Y);
            bool shouldSnap = WinDockHelper.ShouldSnap(_refW.Position, targetPosition, SNAP_DIST);

            if (_isAttached) {
                if (!shouldSnap) {
                    _isAttached = false;
                }
            } else {
                if (shouldSnap) {
                    _isAttached = true;
                    SyncRefToEdit();
                }
            }
        }
        
        public override void _Process(double delta)
        {
            bool isMainVisible = Visible && !GetWindow().Mode.HasFlag(Window.ModeEnum.Minimized);
            
            if (IsInstanceValid(_editW)) _editW.Visible = isMainVisible;

            if (IsInstanceValid(_editW) && _editW.Position != _lastEditPos) {
                _lastEditPos = _editW.Position;
                OnEditMoved();
            }
            if (IsInstanceValid(_refW) && _refW.Position != _lastRefPos) {
                _lastRefPos = _refW.Position;
                OnRefMoved();
            }
        }
        
        private void UpdateRefWindow(Data.Item entry)
        {
            if (IsInstanceValid(_refW))
            {
                _refW.UpdateList(entry, _currentProject.Items);
            }
        }
        
        private void OnRefWRequestJump(long targetId)
        {
            if (IsInstanceValid(_editW)) _editW.ManualSave();
            
            JumpToTreeItem(targetId);
        }
        


        private void JumpToTreeItem(long targetId)
        {
            foreach (var tree in _treeMap.Values)
            {
                TreeItem root = tree.GetRoot();
                if (root == null) continue;

                TreeItem item = root.GetFirstChild();
                if (FindAndSelectItem(item, targetId, tree))
                {
                    Control treeParent = tree.GetParent() as Control;
                    int tabIndex = -1;
                    for (int i = 0; i < _tabs.GetChildCount(); i++)
                    {
                        if (_tabs.GetChild(i) == treeParent)
                        {
                            tabIndex = i;
                            break;
                        }
                    }
                    if (tabIndex >= 0)
                    {
                        _tabs.CurrentTab = tabIndex;
                    }
                    break;
                }
            }
        }
        
        private bool FindAndSelectItem(TreeItem item, long targetId, Tree tree)
        {
            while (item != null)
            {
                var key = item.GetMetadata(0).AsString();
                if (_map.TryGetValue(key, out var entry))
                {
                    if (entry.ID == targetId)
                    {
                        tree.DeselectAll();
                        item.Select(0);
                        tree.ScrollToItem(item);
                        
                        SetupEditor(entry, item);
                        
                        if (IsInstanceValid(_refW))
                        {
                            _refW.UpdateList(entry, _currentProject.Items);
                        }
                        
                        if (IsInstanceValid(_editW))
                        {
                            _editW.GrabFocus();
                            _editW.FocusTranslatedTextEdit();
                        }
                        
                        return true;
                    }
                }
                
                TreeItem child = item.GetFirstChild();
                if (child != null && FindAndSelectItem(child, targetId, tree))
                {
                    return true;
                }
                
                item = item.GetNext();
            }
            return false;
        }
        
        private void NavigateItems(int step)
        {
            var currentScroll = _tabs.GetChild<ScrollContainer>(_tabs.CurrentTab);
            if (currentScroll == null) return;
            
            var categoryName = currentScroll.GetMeta("raw_category_name").AsString();
            if (!_treeMap.TryGetValue(categoryName, out var tree)) return;
            
            var currentItem = tree.GetSelected();
            if (currentItem == null) return;
            
            TreeItem next = null;
            if (step > 0)
            {
                next = currentItem.GetNextVisible();
            }
            else
            {
                next = currentItem.GetPrevVisible();
            }
            
            if (next != null)
            {
                tree.DeselectAll();
                next.Select(0);
                tree.ScrollToItem(next);
                
                var key = next.GetMetadata(0).AsString();
                if (_map.TryGetValue(key, out var entry))
                {
                    SetupEditor(entry, next);
                }
            }
        }

        private void ContextMenuAction(int actionId, Tree tree)
        {
            var selectedItems = new List<TreeItem>();
            TreeItem item = tree.GetNextSelected(null);
            while (item != null)
            {
                selectedItems.Add(item);
                item = tree.GetNextSelected(item);
            }

            if (selectedItems.Count == 0)
            {
                SafeShowMessage(L.T("MSG_NO_SELECTION"));
                return;
            }

            Dictionary<Data.Item, string> selectedEntries = new Dictionary<Data.Item, string>();
            foreach (var selectedItem in selectedItems)
            {
                string key = selectedItem.GetMetadata(0).AsString();
                if (_map.TryGetValue(key, out var entry))
                    selectedEntries.Add(entry, selectedItem.GetText(2));
            }

            switch (actionId)
            {
                case 1:
                case 2:
                    bool skipEnabled = _chkFilter != null && _chkFilter.ButtonPressed;
                    List<Data.Item> toExport = new List<Data.Item>();
                    foreach (var entry in selectedEntries.Keys)
                    {
                        if (!skipEnabled || !entry.IsTranslated)
                        {
                            toExport.Add(entry);
                        }
                    }

                    if (toExport.Count == 0) {
                    ShowAlert(L.T("MSG_EXPORT_CANCEL"));
                } else {
                    ExportEntries(toExport);
                }
                break;
                case 3:
                    ConfirmClearTranslation(selectedItems.ToArray());
                    break;

                case 100:
                case 101:
                    ShowBatchInputDialog((int)actionId);
                    break;
            }
        }

        private void ShowMessage(string message)
        {
            Feedback.Instance.ShowMessage("WIN_ALERT_TITLE", message);
        }

        private void SafeShowMessage(string message)
        {
            Feedback.Instance.ShowMessage(L.T("WIN_ALERT_TITLE"), message);
        }

        private void ShowAlert(string msgKey, params object[] args)
        {
            Feedback.Instance.Show(L.T("WIN_ALERT_TITLE"), msgKey, args);
        }

        private void PopupImportTranslation()
        {
            if (_batchImportWin != null && IsInstanceValid(_batchImportWin))
            {
                _batchImportWin.GrabFocus();
                return;
            }
            
            _batchImportWin = new BatchImport();
            GetTree().Root.AddChild(_batchImportWin);
            
            _batchImportWin.ContentSubmitted += OnBatchImportContentSubmitted;
            _batchImportWin.CloseRequested += OnBatchImportCloseRequested;
            
            _batchImportWin.Show();
        }

        private void ProcessBatchImport(string content)
        {
            if (_currentProject == null) return;

            var importData = ParseImportContent(content);
            if (importData.Count == 0) return;

            int applied = 0;
            foreach (var pair in importData)
            {
                var entry = _currentProject.Items.Find(e => e.ExportId == pair.Key);
                if (entry == null) continue;

                string normO = entry.Ori.Replace("\r\n", "\n").Trim();
                string normT = pair.Value.Replace("\r\n", "\n").Trim();

                if (normO == normT) continue;

                entry.Trans = pair.Value;
                entry.IsDictApplied = false;
                applied++;
            }

            RefreshUI();
            Feedback.Instance.Show(L.T("WIN_ALERT_TITLE"), L.T("STAT_IMPORT_OK"), applied); 
        }

        //解析导入内容
        private Dictionary<int, string> ParseImportContent(string content)
        {
            var result = new Dictionary<int, string>();
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            string detectedMod = "";

            foreach (var line in lines)
            {
                string trm = line.Trim();
                if (string.IsNullOrEmpty(trm)) continue;

                var modMatch = System.Text.RegularExpressions.Regex.Match(trm, @"^\[(.*?)\]$");
                if (modMatch.Success && !trm.Contains("|"))
                {
                    detectedMod = modMatch.Groups[1].Value.Replace("[mod]", "").Trim();
                    continue;
                }

                var oldMatch = System.Text.RegularExpressions.Regex.Match(trm, @"Mod[:：]\s*\[(.*?)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (oldMatch.Success)
                {
                    detectedMod = oldMatch.Groups[1].Value.Replace("[mod]", "").Trim();
                    continue;
                }

                var itemMatch = System.Text.RegularExpressions.Regex.Match(trm, @"^(\d+)\|(.+)$");
                if (itemMatch.Success)
                {
                    int id = int.Parse(itemMatch.Groups[1].Value);
                    string trans = itemMatch.Groups[2].Value;

                    trans = trans.Replace("\\n", "\n");

                    if (!string.IsNullOrEmpty(trans))
                    {
                        result[id] = trans;
                    }
                }
            }

            if (string.IsNullOrEmpty(detectedMod))
            {
                ShowAlert(L.T("ERR_NO_MOD_TAG"));
                return new Dictionary<int, string>();
            }

            string currentModName = Path.GetFileName(_currentProject.Path).Replace("[mod]", "").Trim();
            if (!detectedMod.Equals(currentModName, System.StringComparison.OrdinalIgnoreCase))
            {
                ShowAlert(L.T("ERR_MOD_MISMATCH"), currentModName, detectedMod);
                return new Dictionary<int, string>();
            }

            return result;
        }



        private void CollectRefTerms(string ori, System.Collections.Generic.Dictionary<string, string> dict)
        {
            string[] words = ori.Split(new[] { ' ', '.', ',', '!', '?' }, System.StringSplitOptions.RemoveEmptyEntries);
            var glossary = SkyrimModTranslator.Core.Dict.DictStorage.Load("UserDict", "");
            foreach (var w in words)
            {
                if (dict.ContainsKey(w)) continue;
                string trans = "";
                if (glossary.TryGetValue(w, out trans) && !string.IsNullOrEmpty(trans))
                {
                    dict[w] = trans;
                }
            }
        }

        private void ConfirmClearTranslation(TreeItem[] items)
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = L.T("MSG_CLEAR_TITLE");
            dialog.DialogText = L.T("MSG_CLEAR_CONFIRM");
            var btnOk = dialog.GetOkButton();
            btnOk.CustomMinimumSize = new Vector2(130, 45);
            var btnCancel = dialog.GetCancelButton();
            btnCancel.CustomMinimumSize = new Vector2(130, 45);
            GetTree().Root.AddChild(dialog);
            dialog.PopupCentered();

            dialog.Confirmed += OnConfirmClearTranslation;
        }





        private void ShowPopupMenu(PopupMenu popup, Vector2I mousePos)
        {
            if (popup == null)
            {
                return;
            }

            if (!popup.IsInsideTree())
            {
                return;
            }

            popup.Position = mousePos + new Vector2I(2, 2);
            popup.Popup();
        }

        private void SetHlCell(TreeItem it, int col, string txt, string key)
        {
            if (it == null) return;
            
            it.SetText(col, txt.Replace("\n", " ↵ "));
            
            it.ClearCustomBgColor(col);
            it.ClearCustomColor(col);

            if (!string.IsNullOrEmpty(key) && txt.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                it.SetCustomBgColor(col, new Color(1, 0.84f, 0, 0.3f));
                it.SetCustomColor(col, new Color(1, 1, 1));
            }
        }

        private async void OnSearchChanged(string text)
        {
            _searchId++;
            int currentId = _searchId;

            await System.Threading.Tasks.Task.Delay(500);
            
            if (currentId != _searchId) return;

            if (_currentProject != null)
            {
                RefreshUI();
            }
        }

        private void OnSaveRequest()
        {
            if (_currentProject == null) return;

            var confirm = new ConfirmationDialog();
            confirm.Title = L.T("CONFIRM_SAVE_TITLE");
            confirm.DialogText = string.Format(L.T("CONFIRM_SAVE_MSG"), Path.GetFileName(_currentProject.Path));
            confirm.GetOkButton().Text = L.T("BTN_YES");
            confirm.GetCancelButton().Text = L.T("BTN_NO");
            AddChild(confirm);
            confirm.PopupCentered();

            confirm.Confirmed += PerformSaveInternal;
        }

        //执行保存操作
        private void PerformSaveInternal()
        {
            if (_currentProject == null) return;

            try
            {
                string bakName = _writer.Write(_currentProject.Path, _currentProject);

                if (!string.IsNullOrEmpty(bakName))
                {
                    SaveModDictionary();
                    
                    var alert = new AcceptDialog();
                    alert.Title = L.T("SAVE_SUCCESS_TITLE");
                    alert.DialogText = string.Format(L.T("MSG_SAVE_OK_BAK"), bakName);
                    AddChild(alert);
                    alert.PopupCentered();

                    UpdateModSelectorAppliedStatus();
                    RefreshStatusBar();
                }
            }
            catch (Exception ex)
            {
                ShowAlert(L.T("ERR_SAVE_FAILED"), ex.Message);
                RefreshStatusBar();
            }
        }

        //保存模组字典
        private void SaveModDictionary()
        {
            if (_currentProject == null) return;

            try
            {
                Dictionary<string, string> modDict = new Dictionary<string, string>();
                foreach (var item in _currentProject.Items)
                {
                    if (!string.IsNullOrEmpty(item.Trans) && item.Trans != item.Ori)
                    {
                        string processedOri = SkyrimModTranslator.Core.Dict.DictFmt.ToStorage(item.Ori);
                        string processedTrans = SkyrimModTranslator.Core.Dict.DictFmt.ToStorage(item.Trans);
                        modDict[processedOri] = processedTrans;
                    }
                }

                string modName = Path.GetFileName(_currentProject.Path);
                SkyrimModTranslator.Core.Dict.DictStorage.Save("ModDict", modName, modDict);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[main] 保存模组字典失败: {ex.Message}");
            }
        }

        private void UpdateModSelectorAppliedStatus()
        {
            if (_currentProject != null)
            {
                _currentProject.IsApplied = true;
                UpdateModSelector();
            }
        }

        private void OnBackupRequest()
    {
        if (_currentProject == null) return;

        if (_backupWin == null || !IsInstanceValid(_backupWin))
        {
            _backupWin = new Back(_currentProject.Path);
            GetTree().Root.AddChild(_backupWin);
            
            _backupWin.Restored += OnBackupRestored;

            _backupWin.Show();
        }
        else
        {
            _backupWin.GrabFocus();
        }
    }

        private void OnBackupRestored()
        {
            if (_currentProject == null) return;

            string path = _currentProject.Path;
            
            _projects.Remove(_currentProject); 
            LoadMod(path); 
        }

        private void OnDistRequest()
        {
            if (_distWin == null || !IsInstanceValid(_distWin))
            {
                _distWin = new Dictw();
                _distWin.DictwSettingsChanged += OnDistSettingsChanged;
                GetTree().Root.AddChild(_distWin);
                _distWin.Show();
                Pos.RestoreWindowState(_distWin);
            }
            else
            {
                _distWin.GrabFocus();
            }
        }

        private void OnDistSettingsChanged()
        {
            bool isAuto = Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "false"));
            
            if (_currentProject == null)
            {
                return;
            }

            Dictionary<string, string> dict = new Dictionary<string, string>();
            if (isAuto)
            {
                dict = LoadCombinedDictionary(_currentProject.Path);
            }

            foreach (Data.Item item in _currentProject.Items)
                {
                    if (isAuto)
                    {
                        if (string.IsNullOrEmpty(item.Trans) || item.IsDictApplied)
                        {
                            string processedOri = SkyrimModTranslator.Core.Dict.DictFmt.ToStorage(item.Ori);
                            if (dict.TryGetValue(processedOri, out string val))
                            {
                                item.Trans = val;
                                item.IsDictApplied = true;
                            }
                            else if (item.IsDictApplied)
                            {
                                item.Trans = "";
                                item.IsDictApplied = false;
                            }
                        }
                    }
                    else
                    {
                        if (item.IsDictApplied)
                        {
                            item.Trans = "";
                            item.IsDictApplied = false;
                        }
                    }
                }

            UpdateVisibleTreeContent();
            RefreshStatusBar();
        }

        private void OnTabRightClicked(int index, Vector2I mousePos)
        {
            var popup = new PopupMenu();
            popup.AddItem(L.T("MENU_EXPORT_TAB"), 100);
            popup.AddItem(L.T("MENU_EXPORT_ALL"), 101);
            popup.IdPressed += OnTabPopupIdPressed;
            GetTree().Root.AddChild(popup);

            ShowPopupMenu(popup, mousePos);
        }

        private void OnTabPopupIdPressed(long id)
        {
            if (id == 100) 
            {
                ExportCurrentTabEntries();
            }
            else if (id == 101) 
            {
                ExportAllEntries();
            }
        }

        private void ExportCurrentTabEntries()
        {
            if (_currentProject == null) return;

            var list = new List<Data.Item>();
            
            //导出所有选中的标签页内容
            if (_selectedTabIndices.Count > 0)
            {
                foreach (int tabIndex in _selectedTabIndices)
                {
                    if (tabIndex >= 0 && tabIndex < _tabs.GetTabCount())
                    {
                        var scroll = _tabs.GetChild<ScrollContainer>(tabIndex);
                        var categoryName = scroll.GetMeta("raw_category_name").AsString();
                        
                        foreach (var item in _currentProject.Items)
                        {
                            if (GetCategoryForRecord(item.Type) == categoryName)
                            {
                                list.Add(item);
                            }
                        }
                    }
                }
            }
            else
            {
                //如果没有选中标签页，只导出当前标签页
                int currentTabIdx = _tabs.CurrentTab;
                var currentScroll = _tabs.GetChild<ScrollContainer>(currentTabIdx);
                var categoryName = currentScroll.GetMeta("raw_category_name").AsString();
                
                foreach (var item in _currentProject.Items)
                {
                    if (GetCategoryForRecord(item.Type) == categoryName)
                    {
                        list.Add(item);
                    }
                }
            }
            
            ExportEntries(list);
        }

        private void ExportAllEntries()
        {
            if (_currentProject == null) return;
            
            ExportEntries(_currentProject.Items);
        }

        //获取最终翻译文本
        private string GetFinalTrans(Data.Item item, System.Collections.Generic.Dictionary<string, string> dictCache)
        {
            if (!string.IsNullOrEmpty(item.Trans)) return item.Trans;

            if (dictCache.TryGetValue(item.Ori, out string dictMatch))
            {
                return dictMatch;
            }

            var words = item.Ori.Split(new[] { ' ', '-', '_', '.', ',', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var translatedParts = new System.Collections.Generic.List<string>();
            bool hasAnyMatch = false;

            foreach (var w in words)
            {
                if (dictCache.TryGetValue(w, out string part))
                {
                    translatedParts.Add(part);
                    hasAnyMatch = true;
                }
                else
                {
                    translatedParts.Add(w);
                }
            }

            return hasAnyMatch ? string.Join(" ", translatedParts) : "";
        }

        //导出条目
        private void ExportEntries(List<Data.Item> items)
        {
            if (items == null || items.Count == 0) return;

            bool onlyUntranslated = _chkFilter != null && _chkFilter.ButtonPressed;
            bool useDict = Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "false"));

            System.Collections.Generic.Dictionary<string, string> dictCache = new System.Collections.Generic.Dictionary<string, string>();
            if (useDict)
            {
                dictCache = SkyrimModTranslator.Core.Dict.DictStorage.Load("UserDict", "");
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            
            string modName = Path.GetFileName(_currentProject.Path).Replace("[mod]", "");
            sb.Append("[").Append(modName).Append("]");

            int exportedCount = 0;
            int skippedCount = 0;
            System.Collections.Generic.Dictionary<string, string> dictMatches = new System.Collections.Generic.Dictionary<string, string>();

            foreach (var e in items)
            {
                if (onlyUntranslated && !string.IsNullOrEmpty(e.Trans))
                {
                    skippedCount++;
                    continue;
                }

                string safeOri = e.Ori.Replace("\r\n", "\\n").Replace("\n", "\\n");
                sb.Append("\n").Append(e.ExportId).Append("|").Append(safeOri);
                exportedCount++;

                if (useDict && dictCache.Count > 0)
                {
                    if (dictCache.TryGetValue(e.Ori, out string val))
                    {
                        dictMatches[e.Ori] = val;
                    }
                    
                    string[] words = e.Ori.Split(new[] { ' ', '.', ',', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var w in words)
                    {
                        if (w.Length > 2 && dictCache.TryGetValue(w, out string wVal))
                        {
                            dictMatches[w] = wVal;
                        }
                    }
                }
            }

            if (exportedCount == 0) return;

            if (useDict && dictMatches.Count > 0)
            {
                sb.Append("\n\n======= " + L.T("DICT_TITLE") + " =======\n");
                foreach (var kv in dictMatches)
                {
                    sb.Append("\n").Append(kv.Key).Append(" -> " + kv.Value);
                }
            }

            string prompt = L.T("PROMPT_BASE").Replace("{count}", exportedCount.ToString());
            if (useDict && dictMatches.Count > 0)
            {
                prompt = L.T("PROMPT_PREFIX_GLOSSARY") + prompt;
            }
            sb.Append("\n\n").Append(prompt);

            DisplayServer.ClipboardSet(sb.ToString());
            Feedback.Instance.Show(L.T("WIN_ALERT_TITLE"), L.T("MSG_EXPORT_SUCCESS"), exportedCount);

        }

        private void CloseTab(int index)
        {
            if (index >= 0 && index < _tabs.GetTabCount())
            {
                var child = _tabs.GetChild<Control>(index);
                if (child != null)
                {
                    string categoryName = child.GetMeta("raw_category_name").AsString();
                    _treeMap.Remove(categoryName);
                    _tabs.RemoveChild(child);
                    child.QueueFree();
                }
            }
        }

        private void CloseOtherTabs(int keepIndex)
        {
            for (int i = _tabs.GetTabCount() - 1; i >= 0; i--)
            {
                if (i != keepIndex)
                {
                    CloseTab(i);
                }
            }
        }

        private void CloseAllTabs()
        {
            for (int i = _tabs.GetTabCount() - 1; i >= 0; i--)
            {
                CloseTab(i);
            }
        }







        //载入顺序:当前模组词典>用户词典>其他模组词典
        private Dictionary<string, string> LoadCombinedDictionary(string modPath)
        {
            Dictionary<string, string> glossary = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(modPath))
            {
                string currentModName = Path.GetFileName(modPath);
                var modDict = SkyrimModTranslator.Core.Dict.DictStorage.Load("ModDict", currentModName);
                foreach (var kvp in modDict)
                {
                    glossary[kvp.Key] = kvp.Value;
                }
            }
            
            var userDict = SkyrimModTranslator.Core.Dict.DictStorage.Load("UserDict", "");
            foreach (var kvp in userDict)
            {
                if (!glossary.ContainsKey(kvp.Key))
                {
                    glossary[kvp.Key] = kvp.Value;
                }
            }
            
            var allModDicts = SkyrimModTranslator.Core.Dict.DictStorage.LoadAllInSub("ModDict");
            foreach (var kvp in allModDicts)
            {
                if (!glossary.ContainsKey(kvp.Key))
                {
                    glossary[kvp.Key] = kvp.Value;
                }
            }

            return glossary;
        }

        //应用字典自动翻译
        private void ApplyDictionaryAutoTranslation(Data.Mod project)
        {
            //防御性检查
            if (project == null || !Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "false")))
            {
                return;
            }

            Dictionary<string, string> glossary = LoadCombinedDictionary(project.Path);

            //空检查
            if (glossary == null || glossary.Count == 0)
            {
                return;
            }

            foreach (Data.Item entry in project.Items)
                {
                    //仅处理未翻译条目
                    if (string.IsNullOrEmpty(entry.Trans))
                    {
                        //使用与存储时相同的处理方式（去除首尾空格）
                        string processedOri = SkyrimModTranslator.Core.Dict.DictFmt.ToStorage(entry.Ori);
                        if (glossary.TryGetValue(processedOri, out string trans))
                        {
                            entry.Trans = trans;
                            entry.IsDictApplied = true;
                        }
                    }
                }
        }

        private void UpdateUIStrings()
        {
            if (_btnOpen != null) _btnOpen.Text = L.T("BTN_OPEN");
            if (_btnSave != null) _btnSave.Text = L.T("BTN_SAVE");
            if (_btnBackup != null) _btnBackup.Text = L.T("BTN_BACKUP");
            if (_btnDist != null) _btnDist.Text = L.T("BTN_GLOSSARY");
            if (_btnImport != null) _btnImport.Text = L.T("BTN_IMPORT_TRANSLATION");
            if (_btnSettings != null) _btnSettings.Text = L.T("BTN_SETTINGS");
            if (_btnBatch != null) _btnBatch.Text = L.T("BTN_BATCH_PROCESS");
            if (_emptyTip != null) _emptyTip.Text = L.T("DROP_TIP");
            if (_search != null) _search.PlaceholderText = L.T("SEARCH_PLACEHOLDER");
            if (_selMod != null && _selMod.ItemCount > 0)
                _selMod.SetItemText(0, L.T("MOD_SELECTOR_DEFAULT"));
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseMotion motion)
            {
                UpdateParallaxBackground(motion);
            }
        }

        private void UpdateProgressCallback(float progress)
        {
            CallDeferred(nameof(UpdateProgress), progress);
        }

        private void UpdateVisibleTreeContent()
        {
            foreach (var pair in _treeMap)
            {
                Tree tree = pair.Value;
                TreeItem root = tree.GetRoot();
                if (root == null)
                {
                    continue;
                }

                SyncItemRecursive(root.GetFirstChild());
            }
        }

        private void SyncItemRecursive(TreeItem it)
        {
            while (it != null)
            {
                string key = it.GetMetadata(0).AsString();
                
                if (_map.TryGetValue(key, out Data.Item e))
                {
                    string txt;
                    Color col;

                    if (string.IsNullOrEmpty(e.Trans))
                    {
                        txt = L.T("STATUS_UNTRANSLATED");
                        col = Colors.Salmon;
                    }
                    else if (e.IsDictApplied)
                    {
                        txt = L.T("PREFIX_DICT") + e.Trans;
                        col = new Color(0.4f, 0.6f, 1.0f);
                    }
                    else
                    {
                        txt = e.Trans;
                        col = Colors.SpringGreen;
                    }

                    txt = txt.Replace("\r", "").Replace("\n", " ↵ ");

                    it.SetText(3, txt);
                    it.SetCustomColor(3, col);
                }

                SyncItemRecursive(it.GetFirstChild());
                it = it.GetNext();
            }
        }
        
        //鼠标滚轮切换模组
        private void OnModSelectorGuiInput(InputEvent ev)
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                int current = _selMod.Selected;
                if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    if (current > 0)
                    {
                        _selMod.Select(current - 1);
                        _selMod.EmitSignal("item_selected", current - 1);
                    }
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    if (current < _selMod.ItemCount - 1)
                    {
                        _selMod.Select(current + 1);
                        _selMod.EmitSignal("item_selected", current + 1);
                    }
                }
            }
        }

        private void OnEditorTreeExited()
        {
            if (IsInstanceValid(_editW))
            {
                _editW.ChangedNotify -= UpdateVisibleTreeContent;
                _editW.TreeExited -= OnEditorTreeExited;
                _editW.CloseRequested -= OnEditWindowCloseRequested;
            }
            _editW = null;
        }
        
        private void OnEditWindowCloseRequested()
        {
            if (IsInstanceValid(_editW))
            {
                _editW.ChangedNotify -= UpdateVisibleTreeContent;
                _editW.TreeExited -= OnEditorTreeExited;
                _editW.CloseRequested -= OnEditWindowCloseRequested;
            }
            if (IsInstanceValid(_refW)) _refW.QueueFree();
            _editW = null;
        }

        private void OnTabBarGuiInput(InputEvent ev)
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                Vector2 mousePos = mb.Position;
                int index = -1;

                var tabBar = _tabs.GetTabBar();
                for (int i = 0; i < _tabs.GetTabCount(); i++)
                {
                    if (tabBar.GetTabRect(i).HasPoint(mousePos))
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0) return;

                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (Input.IsKeyPressed(Key.Ctrl))
                    {
                        if (_selectedTabIndices.Contains(index))
                            _selectedTabIndices.Remove(index);
                        else
                            _selectedTabIndices.Add(index);
                    }
                    else
                    {
                        _selectedTabIndices.Clear();
                        _selectedTabIndices.Add(index);
                    }
                    RefreshTabHighlights(tabBar);
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    if (!_selectedTabIndices.Contains(index))
                    {
                        _selectedTabIndices.Clear();
                        _selectedTabIndices.Add(index);
                        RefreshTabHighlights(tabBar);
                    }
                    Vector2I globalMousePos = (Vector2I)DisplayServer.MouseGetPosition();
                    OnTabRightClicked(index, globalMousePos);
                }
            }
        }

        private void OnFilterUntranslatedToggled(bool toggled)
        {
            RefreshUI();
        }

        private void OnImportPressed()
        {
            PopupImportTranslation();
        }
        
        //批量处理
        private void OnBatchProcessPressed()
        {
            if (_currentProject != null)
            {
                if (_batchWin != null && IsInstanceValid(_batchWin))
                {
                    _batchWin.GrabFocus();
                    return;
                }
                
                var transEntries = _currentProject.Items.ConvertAll(item => {
                    var transEntry = new SkyrimModTranslator.Core.Data.TransEntry {
                        ID = item.ID,
                        Type = item.Type,
                        FormID = item.FormID,
                        FType = item.FType,
                        FIdx = item.FIdx,
                        Ori = item.Ori,
                        Trans = item.Trans,
                        Cty = item.Cty,
                        Raw = item.Raw,
                        Speaker = item.Speaker,
                        IsDictApplied = item.IsDictApplied,
                        SkipDistSync = item.SkipDistSync
                    };
                    return transEntry;
                });
                
                _batchWin = new Batch(transEntries);
                GetTree().Root.AddChild(_batchWin);
                
                _batchWin.ProcessFinished += OnBatchProcessFinished;
                _batchWin.CloseRequested += OnBatchWinCloseRequested;
                
                _batchWin.Show();
            }
        }

        private void OnBatchProcessFinished()
        {
            RefreshUI();
            RefreshStatusBar();
        }

        private void OnBatchWinCloseRequested()
        {
            _batchWin = null;
        }

        private void OnSettingsPressed()
        {
            var settingsWindow = SkyrimModTranslator.UI.Set.Instance;
            
            if (settingsWindow.GetParent() == null)
            {
                GetTree().Root.AddChild(settingsWindow);
                settingsWindow.SettingsChanged += OnSettingsChanged;
            }
            
            settingsWindow.Show();
            settingsWindow.GrabFocus();
            Pos.RestoreWindowState(settingsWindow);
        }

        private void OnSettingsChanged()
        {
            GD.PrintErr($"[main] 检测到配置更改，正在刷新背景...");
            LoadBackgroundSettings();
        }

        private void OnTabSelected(long idx)
        {
            var bar = _tabs.GetTabBar();
            RefreshTabHighlights(bar);
        }

        private void OnFilesSelected(string[] paths)
        {
            var fd = (FileDialog)GetTree().Root.GetChild(GetTree().Root.GetChildCount() - 1);
            if (fd != null)
            {
                Pos.SaveSetting("last_mod_dir", Path.GetDirectoryName(paths[0]));
                Pos.SaveSetting("fd_open_pos", $"{fd.Position.X}|{fd.Position.Y}");
                foreach (var path in paths) LoadMod(path);
            }
        }

        private void OnPopupIdPressed(long id)
        {
            if (id == 10 || id == 11) {
                ShowBatchInputDialog((int)id);
                return;
            }
            
            var tree = GetCurrentTree();
            if (tree != null)
            {
                ContextMenuAction((int)id, tree);
            }
        }

        private void OnTreeGuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
            {
                               var currentScroll = _tabs.GetChild<ScrollContainer>(_tabs.CurrentTab);
                if (currentScroll != null)
                {
                    var categoryName = currentScroll.GetMeta("raw_category_name").AsString();
                    if (_treeMap.TryGetValue(categoryName, out var tree))
                    {
                        var popup = tree.GetNodeOrNull<PopupMenu>("ContextMenu");
                        if (popup != null)
                        {
                            Vector2I mousePos = (Vector2I)DisplayServer.MouseGetPosition();
                            ShowPopupMenu(popup, mousePos);
                            GetViewport().SetInputAsHandled();
                        }
                    }
                }
            }
        }

        private void OnActiveMessageDialogConfirmed()
        {
            _activeMessageDialog = null;
        }

        private void OnActiveMessageDialogCanceled()
        {
            _activeMessageDialog = null;
        }

        private void OnBatchImportContentSubmitted(string content)
        {
            ProcessBatchImport(content);
            _batchImportWin = null;
        }
        
        //显示批量处理输入框
        private void ShowBatchInputDialog(int type)
        {
            var dialog = new ConfirmationDialog {
                Title = type == 10 ? L.T("TITLE_BATCH_PRE") : L.T("TITLE_BATCH_SUF"),
                MinSize = new Vector2I(400, 120)
            };

            var edit = new LineEdit { PlaceholderText = L.T("EDIT_BATCH_HINT") };
            dialog.AddChild(edit);
            
            dialog.SetMeta("batch_type", type);
            dialog.SetMeta("batch_edit", edit);
            dialog.Confirmed += OnBatchInputDialogConfirmed;
            AddChild(dialog);
            dialog.PopupCentered();
            edit.GrabFocus();
        }
        
        private void OnBatchInputDialogConfirmed()
        {
            var dialog = (ConfirmationDialog)GetTree().Root.GetChild(GetTree().Root.GetChildCount() - 1);
            if (dialog != null)
            {
                int type = (int)dialog.GetMeta("batch_type");
                var edit = (LineEdit)dialog.GetMeta("batch_edit");
                if (edit != null)
                {
                    ExecuteOptimizedBatch(type, edit.Text);
                }
                dialog.QueueFree();
            }
        }
        
        private void ExecuteOptimizedBatch(int type, string val)
        {
            if (string.IsNullOrEmpty(val)) return;

            var tree = GetCurrentTree();
            if (tree == null) return;

            int modifiedCount = 0;
            TreeItem it = tree.GetNextSelected(null);

            while (it != null)
            {
                string key = it.GetMetadata(0).AsString();
                if (_map.TryGetValue(key, out var entry))
                {
                    if (entry.IsDictApplied || string.IsNullOrEmpty(entry.Trans)) 
                    {
                        it = tree.GetNextSelected(it);
                        continue; 
                    }

                    if (type == 10) entry.Trans = val + entry.Trans;
                    else entry.Trans = entry.Trans + val;

                    entry.IsDictApplied = false;
                    
                    modifiedCount++;
                }
                it = tree.GetNextSelected(it);
            }

            UpdateVisibleTreeContent();
            RefreshStatusBar();
            
            Feedback.Instance.Show(L.T("WIN_ALERT_TITLE"), L.T("STAT_BATCH_OK"), modifiedCount);
        }
        

        


        private void OnBatchImportCloseRequested()
        {
            _batchImportWin = null;
        }

        private void OnConfirmClearTranslation()
        {
            var tree = GetCurrentTree();
            if (tree == null) return;

            TreeItem it = tree.GetNextSelected(null);
            int count = 0;
            while (it != null)
            {
                string key = it.GetMetadata(0).AsString();
                if (_map.TryGetValue(key, out var entry))
                {
                    AutoFillOrClear(entry);
                    count++;
                }
                it = tree.GetNextSelected(it);
            }

            RefreshUI();
            Feedback.Instance.Show(L.T("WIN_ALERT_TITLE"), L.T("STAT_CLEARED"), count);
        }

        public void AutoFillOrClear(Data.Item entry)
        {
            if (entry == null) return;

            entry.Trans = "";
            entry.IsDictApplied = false;

            if (Convert.ToBoolean(Pos.GetSetting("apply_dictionary", "false")))
            {
                var dict = SkyrimModTranslator.Core.Dict.DictStorage.Load("UserDict", "");
                if (dict.TryGetValue(entry.Ori, out string val))
                {
                    entry.Trans = val;
                    entry.IsDictApplied = true;
                }
            }
        }
        
        private string RemoveNumberSuffix(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            var match = System.Text.RegularExpressions.Regex.Match(text, @"\s*(?:\[(\d+)\]|\((\d+)\)|(\d+)|【(\d+)】)$" , System.Text.RegularExpressions.RegexOptions.RightToLeft);
            if (match.Success)
            {
                string baseText = text.Substring(0, match.Index).TrimEnd();
                if (string.IsNullOrEmpty(baseText))
                {
                    return text;
                }
                return baseText;
            }
            return text;
        }
        
        private bool IsSameBaseContent(string text1, string text2)
        {
            string base1 = RemoveNumberSuffix(text1);
            string base2 = RemoveNumberSuffix(text2);
            return base1 == base2;
        }

        //获取相同原文的条目数量（包括数字后缀不同的）
        public int GetSameOriginalCount(string original)
        {
            if (_currentProject == null) return 0;
            
            int count = 0;
            string baseOriginal = RemoveNumberSuffix(original);
            
            foreach (var entry in _currentProject.Items)
            {
                string baseEntry = RemoveNumberSuffix(entry.Ori);
                if (baseEntry == baseOriginal)
                {
                    count++;
                }
            }
            return count;
        }
        
        //应用翻译到所有相同原文[带后缀的]的条目（包括数字后缀不同的）
        public void ApplyTranslationToSameOriginal(string original, string translation)
        {
            if (_currentProject == null) return;
            
            string baseOriginal = RemoveNumberSuffix(original);
            
            //检查原文是否为纯数字
            bool isPureNumberOriginal = IsPureNumber(original);
            
            //收集所有相同原文条目的后缀
            List<string> originalSuffixes = new List<string>();
            foreach (var entry in _currentProject.Items)
            {
                string baseEntry = RemoveNumberSuffix(entry.Ori);
                if (baseEntry == baseOriginal)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(entry.Ori, @"\s*(?:\[(\d+)\]|\((\d+)\)|(\d+)|【(\d+)】)$" , System.Text.RegularExpressions.RegexOptions.RightToLeft);
                    if (match.Success)
                    {
                        string suffix = match.Groups[2].Success ? match.Groups[2].Value : 
                                       match.Groups[3].Success ? match.Groups[3].Value : 
                                       match.Groups[4].Success ? match.Groups[4].Value : "";
                        originalSuffixes.Add(suffix);
                    }
                }
            }
            
            foreach (var entry in _currentProject.Items)
            {
                string baseEntry = RemoveNumberSuffix(entry.Ori);
                if (baseEntry == baseOriginal)
                {
                    if (isPureNumberOriginal)
                    {
                        entry.Trans = translation;
                    }
                    else
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(entry.Ori, @"(\s*(?:\[(\d+)\]|\((\d+)\)|(\d+)|【(\d+)】))$" , System.Text.RegularExpressions.RegexOptions.RightToLeft);
                        if (match.Success)
                        {
                            string originalSuffix = match.Groups[1].Value;
                            string originalNumber = match.Groups[2].Success ? match.Groups[2].Value : 
                                                   match.Groups[3].Success ? match.Groups[3].Value : 
                                                   match.Groups[4].Success ? match.Groups[4].Value : "";
                            
                            var transMatch = System.Text.RegularExpressions.Regex.Match(translation, @"(\s*(?:\[(\d+)\]|\((\d+)\)|(\d+)|【(\d+)】))$" , System.Text.RegularExpressions.RegexOptions.RightToLeft);
                            
                            if (transMatch.Success)
                            {
                                string transSuffix = transMatch.Groups[2].Success ? transMatch.Groups[2].Value : 
                                                    transMatch.Groups[3].Success ? transMatch.Groups[3].Value : 
                                                    transMatch.Groups[4].Success ? transMatch.Groups[4].Value : "";
                                
                                string transPrefix = translation.Substring(0, transMatch.Index).TrimEnd();
                                
                                if (string.IsNullOrEmpty(transPrefix))
                                {
                                    entry.Trans = translation + originalSuffix;
                                }
                                else
                                {
                                    string transSuffixFormat = transMatch.Value;
                                    
                                    var transFormatMatch = System.Text.RegularExpressions.Regex.Match(transSuffixFormat, @"^(\s*)(?:\[|\(|【)(.*?)(?:\]|\)|】)$");
                                    if (transFormatMatch.Success)
                                    {
                                        string spacePrefix = transFormatMatch.Groups[1].Value;
                                        string bracketPrefix = transSuffixFormat.Contains("[") ? "[" : 
                                                             transSuffixFormat.Contains("(") ? "(" : 
                                                             transSuffixFormat.Contains("【") ? "【" : "";
                                        string bracketSuffix = transSuffixFormat.Contains("]") ? "]" : 
                                                                  transSuffixFormat.Contains(")") ? ")" : 
                                                                  transSuffixFormat.Contains("】") ? "】" : "";
                                        string finalSuffix = spacePrefix + bracketPrefix + originalNumber + bracketSuffix;
                                        entry.Trans = transPrefix + finalSuffix;
                                    }
                                    else
                                    {
                                        string finalSuffix = transSuffixFormat.Replace(transSuffix, originalNumber);
                                        entry.Trans = transPrefix + finalSuffix;
                                    }
                                }
                            }
                            else
                            {
                                entry.Trans = translation + originalSuffix;
                            }
                        }
                        else
                        {
                            entry.Trans = translation;
                        }
                    }
                    entry.IsDictApplied = false;
                }
            }
            
            RefreshUI();
        }
        
        private bool IsPureNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            string cleanedText = text.Trim();
            cleanedText = cleanedText.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "").Replace("【", "").Replace("】", "");
            
            return int.TryParse(cleanedText, out _);
        }

        //获取当前标签页对应的组件
        private Tree GetCurrentTree()
        {
            var currentScroll = _tabs.GetChild<ScrollContainer>(_tabs.CurrentTab);
            if (currentScroll != null)
            {
                var categoryName = currentScroll.GetMeta("raw_category_name").AsString();
                if (_treeMap.TryGetValue(categoryName, out var tree))
                {
                    return tree;
                }
            }
            return null;
        }


    }
}
