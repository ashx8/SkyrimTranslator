//悬挂窗口（词典匹配与关联条目还有NPC关联对话等）
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SkyrimModTranslator.Core;
using SkyrimModTranslator.Common;

namespace SkyrimModTranslator.UI
{
    using Data = SkyrimModTranslator.Core.Data;
    
    public partial class RefW : Window
    {
        [Signal] public delegate void RequestNavigateEventHandler(int id);
        [Signal] public delegate void RequestJumpEventHandler(long targetId);
        [Signal] public delegate void ItemSelectedEventHandler(int id);
        
        private ItemList _list;
        private Dictionary<int, Data.Item> _itemMap = new Dictionary<int, Data.Item>();
        private Window _parentWin;
        private bool _isUserDragging = false;
        private bool _docked = true;
        private Vector2I _lastParentPos;

        public override void _Ready()
        {
            Title = L.T("WIN_LINKED_ENTRIES");
            MinSize = new Vector2I(200, 500);
            Size = new Vector2I(450, 600);
            Transient = true;
            Visible = false;
            
            var v = new VBoxContainer();
            v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(v);

            _list = new ItemList();
            _list.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _list.AddThemeConstantOverride("v_separation", 8);
            _list.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            _list.AddThemeColorOverride("font_color_disabled", Colors.Gray);
            _list.AddThemeColorOverride("font_color_hover", new Color(0.9f, 0.9f, 0.9f));
            _list.AddThemeColorOverride("font_color_focus", new Color(0.9f, 0.9f, 0.9f));
            _list.AddThemeColorOverride("font_color_pressed", new Color(0.9f, 0.9f, 0.9f));
            _list.ItemActivated += OnItemDoubleClicked;
            v.AddChild(_list);
        }

        private void OnItemDoubleClicked(long index)
        {
            int idx = (int)index;
            if (_itemMap.TryGetValue(idx, out var item))
            {
                EmitSignal(SignalName.ItemSelected, item.ID);
                EmitSignal(SignalName.RequestJump, item.ID);
            }
        }

        private Dictionary<string, string> GetCombinedDictionary(Data.Mod mod)
        {
            var combined = new Dictionary<string, string>();
            
            //先查找当前模组词典
            if (mod != null && !string.IsNullOrEmpty(mod.Path))
            {
                string modName = System.IO.Path.GetFileName(mod.Path);
                var modDict = SkyrimModTranslator.Core.Dict.DictStorage.Load("ModDict", modName);
                foreach (var kvp in modDict)
                {
                    combined[kvp.Key] = kvp.Value;
                }
            }
            
            //其次用户词典
            var userDict = SkyrimModTranslator.Core.Dict.DictStorage.Load("UserDict", "");
            foreach (var kvp in userDict)
            {
                if (!combined.ContainsKey(kvp.Key))
                {
                    combined[kvp.Key] = kvp.Value;
                }
            }
            
            //最后查找所有模组词典
            var allModDicts = SkyrimModTranslator.Core.Dict.DictStorage.LoadAllInSub("ModDict");
            foreach (var kvp in allModDicts)
            {
                if (!combined.ContainsKey(kvp.Key))
                {
                    combined[kvp.Key] = kvp.Value;
                }
            }
            
            return combined;
        }

        private Dictionary<string, string> FindDictMatches(string text, Dictionary<string, string> combinedDict)
        {
            var matches = new Dictionary<string, string>();
            
            string processedText = SkyrimModTranslator.Core.Dict.DictFmt.ToStorage(text);
            if (combinedDict.TryGetValue(processedText, out string fullMatch))
            {
                matches[text] = fullMatch;
            }
            
            //单词拆分匹配
            string cleanStr = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", " ");
            cleanStr = cleanStr.Replace("\n", " " ).Replace("\t", " " ).Replace("[pagebreak]", " ");
            var words = cleanStr.Split(new[] { ' ', '-', '_', '.', ',', '!', '?', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'', '<', '>' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(w => w.Trim())
                                 .Where(w => w.Length >= 2)
                                 .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var w in words)
            {
                if (w.Length > 2)
                {
                    string processedWord = SkyrimModTranslator.Core.Dict.DictFmt.ToStorage(w);
                    if (combinedDict.TryGetValue(processedWord, out string wordMatch))
                    {
                        matches[w] = wordMatch;
                    }
                }
            }
            
            return matches;
        }

        public void LoadEntry(Data.Item current) {
            if (_list == null) return;

            _list.Clear();
            _itemMap.Clear();

            if (current == null) {
                _list.AddItem(L.T("MSG_RELEVANT_EMPTY"));
                return;
            }

            var combinedDict = GetCombinedDictionary(current.Mod);
            var dictMatches = FindDictMatches(current.Ori, combinedDict);

            if (dictMatches.Count > 0) {
                if (_list.ItemCount > 0) {
                    _list.AddItem("");
                }
                int headerIdx = _list.AddItem(L.T("MSG_DICT_CANDIDATES"));
                _list.SetItemDisabled(headerIdx, true);
                _list.SetItemCustomBgColor(headerIdx, new Color(0.2f, 0.2f, 0.2f));

                foreach (var kv in dictMatches) {
                    string tag = kv.Key.Equals(current.Ori, StringComparison.OrdinalIgnoreCase) ? string.Format("({0})", L.T("TAG_FULL")) : string.Format("[{0}]", kv.Key);
                    int mIdx = _list.AddItem(string.Format("{0} {1}", kv.Value, tag));
                    _list.SetItemCustomBgColor(mIdx, new Color(0.1f, 0.1f, 1, 0.1f));
                }
            }

            if (_list.ItemCount == 2) {
                _list.AddItem("");
                _list.AddItem(L.T("MSG_RELEVANT_NONE"));
            }

            if (_list.ItemCount > 0)
            {
                Visible = true;
                Show();
            }
            else
            {
                Visible = false;
                Hide();
            }
        }

        //更新悬挂窗口列表
        public void UpdateList(Data.Item current, List<Data.Item> all)
        {
            if (_list == null) return;

            _list.Clear();
            _itemMap.Clear();

            if (current == null)
            {
                _list.AddItem(L.T("MSG_RELEVANT_EMPTY"));
                return;
            }
            
            var combinedDict = GetCombinedDictionary(current.Mod);
            
            uint tId = 0;
            if (current.Type == "NPC_") 
            {
                tId = current.FormID;
            }
            else if (!string.IsNullOrEmpty(current.Speaker)) 
            {
                try
                {
                    tId = Convert.ToUInt32(current.Speaker, 16);
                }
                catch {}
            }

            if (current.Type == "INFO") 
            {
                int metaTitleIdx = _list.AddItem(L.T("MSG_META"));
                _list.SetItemDisabled(metaTitleIdx, true);
                
                uint sId = 0;
                if (current.Speaker == "PLAYER")
                {
                    sId = 7;
                }
                else if (!string.IsNullOrEmpty(current.Speaker))
                {
                    try
                    {
                        sId = Convert.ToUInt32(current.Speaker, 16);
                    }
                    catch
                    {
                    }
                }
                
                string speakerText = "";
                if (sId != 0)
                {
                    if (current.Mod != null && current.Mod.NpcNames.TryGetValue(sId, out string npcName))
                    {
                        speakerText = $"{npcName} ({sId.ToString("X8")})";
                    }
                    else
                    {
                        speakerText = sId.ToString("X8");
                    }
                }
                
                _list.AddItem(string.Format(L.T("LBL_SPEAKER"), speakerText));
                _list.AddItem(string.Format(L.T("LBL_EMOTION"), current.Cty));
            }

            if (!string.IsNullOrEmpty(current.Ori)) 
            {
                var dictMatches = FindDictMatches(current.Ori, combinedDict);
                
                if (dictMatches.Count > 0)
                {
                    if (_list.ItemCount > 0)
                    {
                        int separatorIdx = _list.AddItem("");
                        _list.SetItemDisabled(separatorIdx, true);
                    }
                    int headerIdx = _list.AddItem(L.T("MSG_DICT_CANDIDATES"));
                    _list.SetItemDisabled(headerIdx, true);
                    foreach (var kv in dictMatches)
                    {
                        string tag = kv.Key.Equals(current.Ori, StringComparison.OrdinalIgnoreCase) ? string.Format("({0})", L.T("TAG_FULL")) : string.Format("[{0}]", kv.Key);
                        int mIdx = _list.AddItem(string.Format("{0} {1}", kv.Value, tag));
                        _list.SetItemCustomBgColor(mIdx, new Color(0.1f, 0.1f, 1, 0.1f));
                    }
                }
            }

            HashSet<uint> associatedEntryIds = new HashSet<uint>();
            
            if (tId != 0) 
            {
                bool hasAssociated = false;
                List<Data.Item> associatedEntries = new List<Data.Item>();
                
                if (current.Mod != null && current.Mod.NpcMap.TryGetValue(tId, out List<uint> infoIds)) 
                {
                    foreach (Data.Item entry in all)
                    {
                        if (entry.Type == "INFO" && infoIds.Contains(entry.FormID) && entry.FType == "NAM1") 
                        {
                            associatedEntries.Add(entry);
                            hasAssociated = true;
                        }
                    }
                }
                
                if (hasAssociated)
                {
                    if (_list.ItemCount > 0)
                    {
                        int separatorIdx = _list.AddItem("");
                        _list.SetItemDisabled(separatorIdx, true);
                    }
                    int assocTitleIdx = _list.AddItem(L.T("MSG_ASSOCIATED"));
                    _list.SetItemDisabled(assocTitleIdx, true);
                    
                    foreach (Data.Item entry in associatedEntries)
                    {
                        string preview = entry.Ori.Replace("\n", " ");
                        if (preview.Length > 30) preview = preview.Substring(0, 30) + "...";
                        string icon = "💬 ";
                        int idx = _list.AddItem(icon + preview);
                        
                        bool hasDirectTranslation = !string.IsNullOrEmpty(entry.Trans);
                        bool hasDictionaryOverride = entry.IsDictApplied || combinedDict.ContainsKey(SkyrimModTranslator.Core.Dict.DictFmt.ToStorage(entry.Ori));
                        bool hasTranslation = hasDirectTranslation || hasDictionaryOverride;
                        _list.SetItemCustomBgColor(idx, hasTranslation ? new Color(Colors.SpringGreen.R, Colors.SpringGreen.G, Colors.SpringGreen.B, 0.2f) : new Color(Colors.Salmon.R, Colors.Salmon.G, Colors.Salmon.B, 0.2f));
                        
                        _itemMap[idx] = entry;
                        associatedEntryIds.Add(entry.FormID);
                    }
                }
            }

            //显示相关条目
            var relevantEntries = new List<Data.Item>();
            foreach (Data.Item item in all)
            {
                if (item.Type == current.Type && item.ID != current.ID && !associatedEntryIds.Contains(item.FormID))
                {
                    relevantEntries.Add(item);
                }
            }

            if (relevantEntries.Count > 0)
            {
                if (_list.ItemCount > 0)
                {
                    int separatorIdx = _list.AddItem("");
                    _list.SetItemDisabled(separatorIdx, true);
                }
                int relevantTitleIdx = _list.AddItem(L.T("MSG_RELEVANT_ENTRIES"));
                _list.SetItemDisabled(relevantTitleIdx, true); 

                foreach (Data.Item e in relevantEntries)
                {
                    string icon = "📄 ";
                    int idx = _list.AddItem($"{icon}[{e.FType}] {e.Ori.Replace("\n", " ")}");
                    
                    bool hasDirectTranslation = !string.IsNullOrEmpty(e.Trans);
                    bool hasDictionaryOverride = e.IsDictApplied || combinedDict.ContainsKey(SkyrimModTranslator.Core.Dict.DictFmt.ToStorage(e.Ori));
                    bool hasTranslation = hasDirectTranslation || hasDictionaryOverride;
                    _list.SetItemCustomBgColor(idx, hasTranslation ? new Color(Colors.SpringGreen.R, Colors.SpringGreen.G, Colors.SpringGreen.B, 0.2f) : new Color(Colors.Salmon.R, Colors.Salmon.G, Colors.Salmon.B, 0.2f));
                    
                    _itemMap[idx] = e;
                }
            }

            if (_list.ItemCount > 0)
            {
                Visible = true;
                Show();
            }
            else
            {
                Visible = false;
                Hide();
            }
        }

        public void SetParent(Window p)
        {
            _parentWin = p;
            _lastParentPos = p.Position;
            WindowInput += OnSelfInput;
            if (_parentWin != null)
            {
                Position = GetTargetDockPosition();
            }
        }

        public override void _Process(double delta)
        {
            if (_parentWin == null) return;

            if (_docked && !_isUserDragging && _lastParentPos != _parentWin.Position)
            {
                Position = GetTargetDockPosition();
                _lastParentPos = _parentWin.Position;
            }
        }

        private void OnSelfInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed) 
                {
                    _isUserDragging = true;
                    _docked = false;
                }
                else 
                {
                    _isUserDragging = false;
                    CheckSnapping();
                }
            }
        }

        private void CheckSnapping()
        {
            Vector2I target = GetTargetDockPosition();
            if (Position.DistanceTo(target) < 50)
            {
                _docked = true;
                Position = target;
            }
        }

        private Vector2I GetTargetDockPosition()
        {
            return _parentWin.Position + new Vector2I(_parentWin.Size.X + 2, 0);
        }
    }
}
