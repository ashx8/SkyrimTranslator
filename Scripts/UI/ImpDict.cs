using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    public partial class ImpDict : Window
    {
        [Signal] public delegate void DoneEventHandler();
        
        private LineEdit _srcPath;
        private LineEdit _dstPath;
        private LineEdit _ruleName;
        private Button _btnSelSrc;
        private Button _btnSelDst;
        private Button _btnImp;

        private string _lastDir = "";
        private StrLoader _strLoader;
        
        public ImpDict()
        {
            Visible = false;
        }
        
        public override void _Ready()
        {
            Title = L.T("WIN_IMP_DICT");
            MinSize = new Vector2I(600, 280);
            Size = new Vector2I(650, 320);
            CloseRequested += OnClose;
            Transient = true;
            
            SkyrimModTranslator.Core.Theme.ApplyStdBg(this);
            SetupUI();
            SetupConnections();
            
            _strLoader = new StrLoader(false);
            _lastDir = Pos.GetSetting("last_other_dict_dir", "");
            Pos.RestoreWindowState(this);
            
            L.OnLanguageChanged += RefreshLocalization;
        }
        
        private void RefreshLocalization()
        {
            Title = L.T("WIN_IMP_DICT");
            if (_ruleName != null) _ruleName.PlaceholderText = L.T("PH_NAME");
            if (_srcPath != null) _srcPath.PlaceholderText = L.T("PH_SEL");
            if (_dstPath != null) _dstPath.PlaceholderText = L.T("PH_SEL");
            if (_btnSelSrc != null) _btnSelSrc.Text = L.T("BTN_BRW");
            if (_btnSelDst != null) _btnSelDst.Text = L.T("BTN_BRW");
            if (_btnImp != null) _btnImp.Text = L.T("BTN_IMP");
        }
        
        private void OnClose()
        {
            L.OnLanguageChanged -= RefreshLocalization;
            Pos.SaveWindowState(this);
            QueueFree();
        }
        
        private void SetupUI()
        {
            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_top", 20);
            margin.AddThemeConstantOverride("margin_bottom", 15);
            margin.AddThemeConstantOverride("margin_left", 30);
            margin.AddThemeConstantOverride("margin_right", 30);
            AddChild(margin);
            
            var mainVBox = new VBoxContainer();
            mainVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVBox.AddThemeConstantOverride("separation", 12);
            margin.AddChild(mainVBox);
            
            var nameHBox = new HBoxContainer();
            nameHBox.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(nameHBox);
            
            var nameLabel = new Label { Text = L.T("LBL_NAME"), CustomMinimumSize = new Vector2(80, 0) };
            nameHBox.AddChild(nameLabel);
            
            _ruleName = new LineEdit { PlaceholderText = L.T("PH_NAME"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nameHBox.AddChild(_ruleName);
            
            var srcHBox = new HBoxContainer();
            srcHBox.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(srcHBox);
            
            var srcLabel = new Label { Text = L.T("LBL_SRC"), CustomMinimumSize = new Vector2(80, 0) };
            srcHBox.AddChild(srcLabel);
            
            _srcPath = new LineEdit
            {
                PlaceholderText = L.T("PH_SEL"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Editable = false
            };
            srcHBox.AddChild(_srcPath);
            
            _btnSelSrc = new Button { Text = L.T("BTN_BRW"), CustomMinimumSize = new Vector2(80, 35) };
            srcHBox.AddChild(_btnSelSrc);
            
            var dstHBox = new HBoxContainer();
            dstHBox.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(dstHBox);
            
            var dstLabel = new Label { Text = L.T("LBL_DST"), CustomMinimumSize = new Vector2(80, 0) };
            dstHBox.AddChild(dstLabel);
            
            _dstPath = new LineEdit
            {
                PlaceholderText = L.T("PH_SEL"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Editable = false
            };
            dstHBox.AddChild(_dstPath);
            
            _btnSelDst = new Button { Text = L.T("BTN_BRW"), CustomMinimumSize = new Vector2(80, 35) };
            dstHBox.AddChild(_btnSelDst);
            
            var tip = new Label
            {
                Text = L.T("TIP_STR"),
                AutowrapMode = TextServer.AutowrapMode.Word,
                Modulate = new Color(0.7f, 0.7f, 0.7f)
            };
            mainVBox.AddChild(tip);
            
            var bottom = new HBoxContainer();
            bottom.AddThemeConstantOverride("separation", 15);
            mainVBox.AddChild(bottom);
            
            var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bottom.AddChild(spacer);
            
            _btnImp = new Button { Text = L.T("BTN_IMP"), CustomMinimumSize = new Vector2(120, 40) };
            bottom.AddChild(_btnImp);
            
            var spacer2 = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bottom.AddChild(spacer2);
        }
        
        private void SetupConnections()
        {
            _btnSelSrc.Pressed += OnSelSrc;
            _btnSelDst.Pressed += OnSelDst;
            _btnImp.Pressed += OnImp;
        }
        
        private void OnSelSrc()
        {
            var fd = new FileDialog();
            fd.UseNativeDialog = true;
            fd.FileMode = FileDialog.FileModeEnum.OpenFiles;
            fd.Access = FileDialog.AccessEnum.Filesystem;
            fd.Filters = new string[] { "*.strings,*.dlstrings,*.ilstrings ; String Table Files" };
            fd.Transient = true;
            
            if (!string.IsNullOrEmpty(_lastDir) && Directory.Exists(_lastDir))
                fd.CurrentDir = _lastDir;
            
            fd.FilesSelected += (paths) => { 
                _srcPath.Text = string.Join("|", paths);
                _lastDir = Path.GetDirectoryName(paths[0]);
                Pos.SaveSetting("last_other_dict_dir", _lastDir);
            };
            AddChild(fd);
            fd.PopupCentered();
        }
        
        private void OnSelDst()
        {
            var fd = new FileDialog();
            fd.UseNativeDialog = true;
            fd.FileMode = FileDialog.FileModeEnum.OpenFiles;
            fd.Access = FileDialog.AccessEnum.Filesystem;
            fd.Filters = new string[] { "*.strings,*.dlstrings,*.ilstrings ; String Table Files" };
            fd.Transient = true;
            
            if (!string.IsNullOrEmpty(_lastDir) && Directory.Exists(_lastDir))
                fd.CurrentDir = _lastDir;
            
            fd.FilesSelected += (paths) => { 
                _dstPath.Text = string.Join("|", paths);
                _lastDir = Path.GetDirectoryName(paths[0]);
                Pos.SaveSetting("last_other_dict_dir", _lastDir);
            };
            AddChild(fd);
            fd.PopupCentered();
        }
        

        private async void OnImp()
        {
            string name = _ruleName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowMessage(L.T("ERR_NAME"));
                return;
            }
            
            if (string.IsNullOrEmpty(_srcPath.Text) || string.IsNullOrEmpty(_dstPath.Text))
            {
                ShowMessage(L.T("ERR_SEL"));
                return;
            }
            
            var srcs = _srcPath.Text.Split('|');
            var dsts = _dstPath.Text.Split('|');
            
            _btnImp.Disabled = true;
            
            try
            {
                var dict = new Dictionary<string, string>();
                int totalProcessed = 0;
                int skippedEmpty = 0;
                int skippedSame = 0;
                int skippedRatio = 0;
                int addedCount = 0;
                
                foreach (string srcPath in srcs)
                {
                    string srcFileName = Path.GetFileName(srcPath);
                    string srcBaseName = Path.GetFileNameWithoutExtension(srcFileName);
                    
                    string matchingDstPath = null;
                    string srcExtension = Path.GetExtension(srcPath).ToLower();
                    
                    string srcBase = srcBaseName.Replace("_english", "", StringComparison.OrdinalIgnoreCase);
                    
                    foreach (string dstPath in dsts)
                    {
                        string dstFileName = Path.GetFileName(dstPath);
                        string dstBaseName = Path.GetFileNameWithoutExtension(dstFileName);
                        string dstExtension = Path.GetExtension(dstPath).ToLower();
                        
                        if (srcExtension != dstExtension)
                        {
                            continue;
                        }
                        
                        string dstBase = dstBaseName.Replace("_chinese", "", StringComparison.OrdinalIgnoreCase)
                                              .Replace("_Chinese", "", StringComparison.OrdinalIgnoreCase);
                        
                        if (srcBase.Equals(dstBase, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingDstPath = dstPath;
                            break;
                        }
                    }
                    
                    if (matchingDstPath == null)
                    {
                        GD.Print($"[ImpDict] 未找到匹配的译文文件: {srcFileName}");
                    }
                    
                    if (matchingDstPath != null)
                    {
                        GD.Print($"[ImpDict] 匹配成功: {srcFileName} -> {Path.GetFileName(matchingDstPath)}");
                        var srcMap = await System.Threading.Tasks.Task.Run(() => _strLoader.ParseFile(srcPath));
                        var dstMap = await System.Threading.Tasks.Task.Run(() => _strLoader.ParseFile(matchingDstPath));
                        GD.Print($"[ImpDict] 解析结果: 原文 {srcMap.Count} 条, 译文 {dstMap.Count} 条");
                        
                        foreach (var kv in srcMap)
                        {
                            totalProcessed++;
                            if (dstMap.TryGetValue(kv.Key, out string trans))
                            {
                                if (string.IsNullOrWhiteSpace(trans))
                                {
                                    skippedEmpty++;
                                    continue;
                                }
                                
                                if (kv.Value == trans)
                                {
                                    skippedSame++;
                                    continue;
                                }
                                
                                string key = kv.Value;
                                if (!dict.ContainsKey(key))
                                {
                                    dict[key] = trans;
                                    addedCount++;
                                    if (addedCount % 1000 == 0)
                                    {
                                        GD.Print($"[ImpDict] 已添加 {addedCount} 个条目");
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (dict.Count == 0)
                {
                    ShowMessage(string.Format(L.T("ERR_NO_MATCH_DETAIL"), totalProcessed, skippedEmpty, skippedSame, skippedRatio));
                    _btnImp.Disabled = false;
                    return;
                }
                
                await System.Threading.Tasks.Task.Run(() => 
                    SkyrimModTranslator.Core.Dict.DictStorage.Save("OtherDict", name, dict));
                
                var otherDicts = SkyrimModTranslator.Core.Dict.DictStorage.GetModList("OtherDict");
                if (otherDicts.Count == 1)
                {
                    Pos.SaveSetting("default_other_dict", name);
                }
                
                ShowMessageWithCallback(string.Format(L.T("STAT_IMP_OK_DETAIL"), addedCount, totalProcessed, skippedEmpty, skippedSame, skippedRatio, name), () => {
                    EmitSignal(SignalName.Done);
                    QueueFree();
                });
            }
            catch (Exception e)
            {
                ShowMessage($"{L.T("ERR_FAIL")}: {e.Message}");
                _btnImp.Disabled = false;
            }
        }
        
        private void ShowMessage(string msg)
        {
            var dialog = new AcceptDialog();
            dialog.Title = L.T("WIN_ALERT_TITLE");
            dialog.DialogText = msg;
            dialog.Transient = true;
            AddChild(dialog);
            dialog.PopupCentered();
        }
        
        private void ShowMessageWithCallback(string msg, Action callback)
        {
            var dialog = new AcceptDialog();
            dialog.Title = L.T("WIN_ALERT_TITLE");
            dialog.DialogText = msg;
            dialog.Transient = true;
            dialog.Confirmed += () => callback?.Invoke();
            AddChild(dialog);
            dialog.PopupCentered();
        }
        
        public override void _ExitTree()
        {
            L.OnLanguageChanged -= RefreshLocalization;
            base._ExitTree();
        }
    }
}