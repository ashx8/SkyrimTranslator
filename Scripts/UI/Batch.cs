//批量操作窗口
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    using TransEntry = SkyrimModTranslator.Core.Data.TransEntry;
    public partial class Batch : Window
    {
        [Signal] public delegate void ProcessFinishedEventHandler();
        
        private List<TransEntry> _entries;
        private Dictionary<string, TransEntry> _entryDict;
        private Tree _resultTree;
        private LineEdit _searchEdit;
        private LineEdit _modifyEdit;
        private CheckBox _caseSensitive;
        private CheckBox _wholeWord;
        private Button _btnReplace;
        private Button _btnReplaceAll;
        private Button _btnSearch;
        
        private enum ActionType { Replace }
        
        public Batch(List<TransEntry> entries)
        {
            _entries = entries;
            _entryDict = new Dictionary<string, TransEntry>();
            foreach (var entry in entries)
            {
                if (!_entryDict.ContainsKey(entry.Ori))
                {
                    _entryDict[entry.Ori] = entry;
                }
            }
        }
        
        public override void _Ready()
        {
            Title = L.T("BATCH_WIN_TITLE");
            MinSize = new Vector2I(800, 600);
            Size = new Vector2I(800, 600);
            CloseRequested += OnCloseRequested;
            SetupUI();
            SetupConnections();
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
            
            var searchBar = new HBoxContainer();
            searchBar.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(searchBar);
            
            _searchEdit = new LineEdit {
                PlaceholderText = L.T("BATCH_REPLACE_SRC"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            searchBar.AddChild(_searchEdit);
            
            _btnSearch = new Button {
                Text = L.T("BTN_CONFIRM_BATCH"),
                CustomMinimumSize = new Vector2(80, 35)
            };
            searchBar.AddChild(_btnSearch);
            
            var optionsHBox = new HBoxContainer();
            optionsHBox.AddThemeConstantOverride("separation", 20);
            mainVBox.AddChild(optionsHBox);
            
            _caseSensitive = new CheckBox { Text = L.T("BATCH_CASE_SENSITIVE") };
            optionsHBox.AddChild(_caseSensitive);
            
            _wholeWord = new CheckBox { Text = L.T("BATCH_WHOLE_WORD") };
            optionsHBox.AddChild(_wholeWord);
            
            _resultTree = new Tree {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            _resultTree.Columns = 2;
            _resultTree.SetColumnTitlesVisible(false);
            _resultTree.SetColumnExpand(0, true);
            _resultTree.SetColumnExpand(1, true);
            _resultTree.SelectMode = Tree.SelectModeEnum.Multi;
            mainVBox.AddChild(_resultTree);
            
            mainVBox.AddChild(new Label { Text = L.T("BATCH_OPERATION_HINT") });
            
            _modifyEdit = new LineEdit {
                PlaceholderText = L.T("BATCH_REPLACE_DST"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 35)
            };
            mainVBox.AddChild(_modifyEdit);
            
            var buttonHBox = new HBoxContainer();
            buttonHBox.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(buttonHBox);
            
            _btnReplace = new Button {
                Text = L.T("BTN_REPLACE_SELECTED"),
                CustomMinimumSize = new Vector2(120, 35)
            };
            buttonHBox.AddChild(_btnReplace);
            
            _btnReplaceAll = new Button {
                Text = L.T("BTN_REPLACE_ALL"),
                CustomMinimumSize = new Vector2(150, 35)
            };
            buttonHBox.AddChild(_btnReplaceAll);
        }
        
        private void SetupConnections()
        {
            _btnSearch.Pressed += OnSearchPressed;
            _searchEdit.TextSubmitted += OnSearchTextSubmitted;
            _btnReplace.Pressed += OnReplacePressed;
            _btnReplaceAll.Pressed += OnReplaceAllPressed;
        }
        
        private void OnSearchPressed()
        {
            UpdateSearchResults();
        }
        
        private void OnSearchTextSubmitted(string text)
        {
            UpdateSearchResults();
            _searchEdit.GrabFocus();
        }
        
        private void OnReplacePressed()
        {
            ExecuteBatchAction(ActionType.Replace);
        }
        
        private void OnReplaceAllPressed()
        {
            ExecuteReplaceAll();
        }
        
        private void OnCloseRequested()
        {
            Pos.SaveWindowState(this);
            QueueFree();
        }
        
        //判断文本是否匹配搜索关键词
        private bool IsMatch(string text, string searchText, bool caseSensitive, bool wholeWord)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchText))
                return false;
                
            if (wholeWord)
            {
                string pattern = $"\\b{Regex.Escape(searchText)}\\b";
                Regex regex = new Regex(pattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                return regex.IsMatch(text);
            }
            else
            {
                return caseSensitive ? 
                    text.Contains(searchText) : 
                    text.ToLower().Contains(searchText.ToLower());
            }
        }
        
        //判断译文是否匹配搜索关键词
        private bool IsEntryMatch(TransEntry entry, string searchText, bool caseSensitive, bool wholeWord)
        {
            if (string.IsNullOrEmpty(searchText))
                return true;
                
            return IsMatch(entry.Ori, searchText, caseSensitive, wholeWord) || 
                   (!string.IsNullOrEmpty(entry.Trans) && IsMatch(entry.Trans, searchText, caseSensitive, wholeWord));
        }
        
        //替换文本中的关键词
        private string ReplaceText(string text, string search, string replace, bool caseSensitive, bool wholeWord)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            string pattern = wholeWord ? $@"\b{Regex.Escape(search)}\b" : Regex.Escape(search);
            
            return Regex.Replace(text, pattern, replace ?? "", options);
        }

        private void UpdateSearchResults()
        {
            _resultTree.Clear();
            
            string searchText = _searchEdit.Text;
            bool caseSensitive = _caseSensitive.ButtonPressed;
            bool wholeWord = _wholeWord.ButtonPressed;
            
            foreach (var entry in _entries)
            {
                bool match = IsEntryMatch(entry, searchText, caseSensitive, wholeWord);
                
                if (match || string.IsNullOrEmpty(searchText))
                {
                    var root = _resultTree.GetRoot();
                    var item = _resultTree.CreateItem(root);
                    string cleanSource = entry.Ori.Replace("\n", " ↵ ");
                    string cleanTrans = (entry.Trans ?? "").Replace("\n", " ↵ ");
                    
                    item.SetText(0, cleanSource);
                    item.SetText(1, cleanTrans);
                    item.SetMetadata(0, entry.Ori);
                    
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        HighlightCell(item, 0, entry.Ori, searchText);
                        HighlightCell(item, 1, entry.Trans ?? "", searchText);
                    }
                }
            }
        }
        
        //高亮显示匹配项（仅背景高亮）
        private void HighlightCell(TreeItem item, int col, string text, string keyword)
        {
            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(text))
            {
                item.SetCustomColor(col, new Color(1, 1, 1));
                return;
            }

            bool caseSensitive = _caseSensitive.ButtonPressed;
            bool wholeWord = _wholeWord.ButtonPressed;
            
            if (IsMatch(text, keyword, caseSensitive, wholeWord))
            {
                item.SetCustomBgColor(col, new Color(1, 0.84f, 0, 0.2f));
                item.SetCustomColor(col, new Color(1, 1, 1));
            }
        }
        
        //批量操作选中项的译文
        private void ExecuteBatchAction(ActionType type)
        {
            string searchText = _searchEdit.Text;
            string modText = _modifyEdit.Text;
            bool caseSensitive = _caseSensitive.ButtonPressed;
            bool wholeWord = _wholeWord.ButtonPressed;
            
            int count = 0;
            
            TreeItem item = _resultTree.GetNextSelected(null);
            while (item != null)
            {
                string originalText = item.GetMetadata(0).AsString();
                var entry = _entryDict.TryGetValue(originalText, out TransEntry foundEntry) ? foundEntry : null;
                if (entry != null && type == ActionType.Replace)
                {
                    if (IsMatch(entry.Ori, searchText, caseSensitive, wholeWord) && string.IsNullOrEmpty(entry.Trans))
                    {
                        entry.Trans = entry.Ori;
                    }
                    
                    if (!string.IsNullOrEmpty(entry.Trans))
                    {
                        entry.Trans = ReplaceText(entry.Trans, searchText, modText, caseSensitive, wholeWord);
                    }
                    
                    item.SetText(1, entry.Trans ?? "");
                    count++;
                }
                item = _resultTree.GetNextSelected(item);
            }
            
            EmitSignal(SignalName.ProcessFinished);
        }
        
        //批量替换所有搜索出来的译文
        private void ExecuteReplaceAll()
        {
            string searchText = _searchEdit.Text;
            string modText = _modifyEdit.Text;
            
            if (string.IsNullOrEmpty(searchText))
                return;
            
            int count = 0;
            bool caseSensitive = _caseSensitive.ButtonPressed;
            bool wholeWord = _wholeWord.ButtonPressed;
            
            foreach (var entry in _entries)
            {
                if (IsEntryMatch(entry, searchText, caseSensitive, wholeWord))
                {
                    if (IsMatch(entry.Ori, searchText, caseSensitive, wholeWord) && string.IsNullOrEmpty(entry.Trans))
                    {
                        entry.Trans = entry.Ori;
                    }
                    
                    if (!string.IsNullOrEmpty(entry.Trans))
                    {
                        entry.Trans = ReplaceText(entry.Trans, searchText, modText, caseSensitive, wholeWord);
                    }
                    count++;
                }
            }
            
            UpdateSearchResults();
            
            EmitSignal(SignalName.ProcessFinished);
        }
    }
}
