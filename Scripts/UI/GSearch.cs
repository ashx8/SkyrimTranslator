//全局搜索窗口，可以在所有模组里搜内容
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    using Data = SkyrimModTranslator.Core.Data;

    public partial class GSearch : Window
    {
        [Signal] public delegate void JumpReqEventHandler(string modPath, long itemId);
        
        private LineEdit _search;
        private Button _btnGo;
        private Button _btnExcludeChinese;
        private Tree _tree;
        private Label _lblStat;
        private List<Data.Mod> _mods;
        private Dictionary<TreeItem, (string ModPath, Data.Item Item)> _map = new();
        
        public GSearch(List<Data.Mod> mods)
        {
            _mods = mods;
        }
        
        public override void _Ready()
        {
            Title = L.T("WIN_G_SEARCH");
            MinSize = new Vector2I(900, 600);
            Size = new Vector2I(900, 600);
            CloseRequested += OnCloseRequested;
            
            //让窗口在屏幕中间弹出
            var screen = DisplayServer.ScreenGetSize();
            Position = new Vector2I(
                (int)(screen.X / 2 - Size.X / 2),
                (int)(screen.Y / 2 - Size.Y / 2)
            );
            
            SkyrimModTranslator.Core.Theme.ApplyStdBg(this);
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
        
        private void OnCloseRequested()
        {
            Pos.SaveWindowState(this);
            QueueFree();
        }
        
        private void SetupUI()
        {
            var margin = new MarginContainer();
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_top", 25);
            margin.AddThemeConstantOverride("margin_bottom", 25);
            margin.AddThemeConstantOverride("margin_left", 30);
            margin.AddThemeConstantOverride("margin_right", 30);
            AddChild(margin);
            
            var mainVBox = new VBoxContainer();
            mainVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            mainVBox.AddThemeConstantOverride("separation", 18);
            margin.AddChild(mainVBox);
            
            var searchHBox = new HBoxContainer();
            searchHBox.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(searchHBox);
            
            _search = new LineEdit
            {
                PlaceholderText = L.T("SEARCH_PLACEHOLDER"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 40)
            };
            searchHBox.AddChild(_search);
            
            _btnGo = new Button
            {
                Text = L.T("BTN_G_SEARCH"),
                CustomMinimumSize = new Vector2(100, 40)
            };
            searchHBox.AddChild(_btnGo);
            
            _btnExcludeChinese = new Button
            {
                Text = L.T("BTN_EXCLUDE_CHINESE"),
                CustomMinimumSize = new Vector2(100, 40)
            };
            searchHBox.AddChild(_btnExcludeChinese);
            
            _tree = new Tree
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HideRoot = true,
                SelectMode = Tree.SelectModeEnum.Single
            };
            _tree.SetColumns(2);
            _tree.SetColumnTitle(0, L.T("COL_MOD"));
            _tree.SetColumnTitle(1, L.T("COL_CONTENT"));
            _tree.SetColumnExpand(0, false);
            _tree.SetColumnExpand(1, true);
            _tree.SetColumnCustomMinimumWidth(0, 250);
            _tree.ItemActivated += OnItemActivated;
            mainVBox.AddChild(_tree);
            
            _lblStat = new Label
            {
                Text = L.T("STATUS_READY"),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            mainVBox.AddChild(_lblStat);
        }
        
        private void SetupConnections()
        {
            _btnGo.Pressed += OnSearch;
            _btnExcludeChinese.Pressed += OnSearchExcludeChinese;
            _search.TextSubmitted += OnSearchSubmitted;
        }
        
        private void OnSearch()
        {
            DoSearch();
        }
        
        private void OnSearchSubmitted(string text)
        {
            DoSearch();
        }
        
        private void OnSearchExcludeChinese()
        {
            DoSearchExcludeChinese();
        }
        
        private void DoSearchExcludeChinese()
        {
            string kw = _search.Text.Trim();
            
            _tree.Clear();
            _map.Clear();
            
            int total = 0;
            string kwLow = kw.ToLower();
            System.Text.RegularExpressions.Regex chineseRegex = new System.Text.RegularExpressions.Regex(@"[\u4e00-\u9fff]");
            
            foreach (var mod in _mods)
            {
                if (mod?.Items == null) continue;
                
                List<Data.Item> matches = new List<Data.Item>();
                foreach (var item in mod.Items)
                {
                    if (string.IsNullOrEmpty(item.Ori)) continue;
                    
                    bool containsKeyword = string.IsNullOrEmpty(kw) || 
                        item.Ori.ToLower().Contains(kwLow) ||
                        (!string.IsNullOrEmpty(item.Trans) && item.Trans.ToLower().Contains(kwLow));
                    bool hasChinese = chineseRegex.IsMatch(item.Ori) || 
                        (!string.IsNullOrEmpty(item.Trans) && chineseRegex.IsMatch(item.Trans));
                    
                    if (containsKeyword && !hasChinese)
                    {
                        matches.Add(item);
                    }
                }
                
                if (matches.Count == 0) continue;
                
                string modName = System.IO.Path.GetFileName(mod.Path);
                var root = _tree.GetRoot();
                if (root == null) root = _tree.CreateItem();
                
                var modItem = _tree.CreateItem(root);
                modItem.SetText(0, $"{modName} ({matches.Count})");
                string firstMatch = $"[{matches[0].Type}] {matches[0].FType}: {matches[0].Ori}";
                if (firstMatch.Length > 80) firstMatch = firstMatch.Substring(0, 80) + "...";
                modItem.SetText(1, firstMatch);
                modItem.SetSelectable(0, false);
                modItem.SetCustomColor(0, new Color(0.6f, 0.8f, 1.0f));
                modItem.SetCollapsed(true);
                
                for (int i = 1; i < matches.Count; i++)
                {
                    var item = matches[i];
                    var child = _tree.CreateItem(modItem);
                    string disp = $"[{item.Type}] {item.FType}: {item.Ori}";
                    if (disp.Length > 80) disp = disp.Substring(0, 80) + "...";
                    
                    child.SetText(0, "");
                    child.SetText(1, disp);
                    child.SetMetadata(0, mod.Path);
                    child.SetMetadata(1, item.ID);
                    _map[child] = (mod.Path, item);
                    total++;
                }
                
                _map[modItem] = (mod.Path, matches[0]);
                total++;
            }
            
            if (string.IsNullOrEmpty(kw))
            {
                UpdateStat(string.Format("找到 {0} 条不含中文的内容", total));
            }
            else
            {
                UpdateStat(string.Format(L.T("STAT_SEARCH_RESULT"), total, kw));
            }
        }
        
        private void DoSearch()
        {
            string kw = _search.Text.Trim();
            if (string.IsNullOrEmpty(kw))
            {
                UpdateStat(L.T("MSG_ENTER_KEYWORD"));
                return;
            }
            
            _tree.Clear();
            _map.Clear();
            
            int total = 0;
            string kwLow = kw.ToLower();
            
            foreach (var mod in _mods)
            {
                if (mod?.Items == null) continue;
                
                List<Data.Item> matches = new List<Data.Item>();
                foreach (var item in mod.Items)
                {
                    if (string.IsNullOrEmpty(item.Ori)) continue;
                    
                    if (item.Ori.ToLower().Contains(kwLow) ||
                        (!string.IsNullOrEmpty(item.Trans) && item.Trans.ToLower().Contains(kwLow)))
                    {
                        matches.Add(item);
                    }
                }
                
                if (matches.Count == 0) continue;
                
                string modName = System.IO.Path.GetFileName(mod.Path);
                var root = _tree.GetRoot();
                if (root == null) root = _tree.CreateItem();
                
                var modItem = _tree.CreateItem(root);
                //第一列放模组名字和匹配数量
                modItem.SetText(0, $"{modName} ({matches.Count})");
                //第二列放第一条匹配的内容，让用户有个预览
                string firstMatch = $"[{matches[0].Type}] {matches[0].FType}: {matches[0].Ori}";
                if (firstMatch.Length > 80) firstMatch = firstMatch.Substring(0, 80) + "...";
                modItem.SetText(1, firstMatch);
                modItem.SetSelectable(0, false);
                modItem.SetCustomColor(0, new Color(0.6f, 0.8f, 1.0f));
                modItem.SetCollapsed(true);
                
                //从第二条开始创建子节点，第一条已经用在父节点上了
                for (int i = 1; i < matches.Count; i++)
                {
                    var item = matches[i];
                    var child = _tree.CreateItem(modItem);
                    string disp = $"[{item.Type}] {item.FType}: {item.Ori}";
                    if (disp.Length > 80) disp = disp.Substring(0, 80) + "...";
                    
                    child.SetText(0, "");
                    child.SetText(1, disp);
                    child.SetMetadata(0, mod.Path);
                    child.SetMetadata(1, item.ID);
                    _map[child] = (mod.Path, item);
                    total++;
                }
                
                //第一条匹配也塞进映射里，挂在模组节点本身上
                _map[modItem] = (mod.Path, matches[0]);
                total++;
            }
            
            UpdateStat(string.Format(L.T("STAT_SEARCH_RESULT"), total, kw));
        }
        
        private void OnItemActivated()
        {
            var sel = _tree.GetSelected();
            if (sel == null) return;
            
            if (_map.TryGetValue(sel, out var data))
            {
                EmitSignal(SignalName.JumpReq, data.ModPath, data.Item.ID);
                QueueFree();
            }
        }
        
        private void UpdateStat(string msg)
        {
            _lblStat.Text = msg;
        }
    }
}