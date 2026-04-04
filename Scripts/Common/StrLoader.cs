//STR文件的加载
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using SkyrimModTranslator.Core;

namespace SkyrimModTranslator.Common
{
    public class StrLoader
    {
        private Dictionary<string, Dictionary<uint, string>> _cache = new();
        private HashSet<uint> _allIds = new();
        private string _dir;
        private string _lang;

        public StrLoader(bool showLoadInfo = true)
        {
            string exe = OS.GetExecutablePath();
            string exeDir = Path.GetDirectoryName(exe);
            
            string dir1 = Path.Combine(Directory.GetCurrentDirectory(), "Strings");
            string dir2 = Path.Combine(exeDir, "Strings");
            
            if (Directory.Exists(dir1))
                _dir = dir1;
            else if (Directory.Exists(dir2))
                _dir = dir2;
            else
                _dir = dir1;
            
            _lang = L.GetGameLang();
            
            if (showLoadInfo && Directory.Exists(_dir))
            {
                var files = Directory.GetFiles(_dir);
            }
        }

        public void SetLang(string lang)
        {
            _lang = lang;
            Pos.SaveSetting("game_lang", lang);
            _cache.Clear();
            _allIds.Clear();
        }

        public void LoadMod(string mod)
        {
            var sw = Stopwatch.StartNew();
            string modBase = Path.GetFileNameWithoutExtension(mod);
            
            int loadedFiles = 0;
            string[] exts = { ".strings", ".dlstrings", ".ilstrings" };
            foreach (string ext in exts)
            {
                string exactName = $"{modBase}_english{ext}";
                string path = Path.Combine(_dir, exactName);
                
                if (File.Exists(path))
                {
                    var fileSw = Stopwatch.StartNew();
                    LoadAndCache(path, exactName);
                    fileSw.Stop();
                    loadedFiles++;
                }
                else
                {
                    if (Directory.Exists(_dir))
                    {
                        foreach (string f in Directory.GetFiles(_dir))
                        {
                            string name = Path.GetFileName(f);
                            if (name.ToLower() == exactName.ToLower())
                            {
                                var fileSw = Stopwatch.StartNew();
                                LoadAndCache(f, exactName);
                                fileSw.Stop();

                                loadedFiles++;
                                break;
                            }
                        }
                    }
                }
            }
            
            sw.Stop();
        }

        private void LoadAndCache(string path, string cacheKey)
        {
            var dict = ParseFile(path);
            if (dict.Count > 0)
            {
                _cache[cacheKey] = dict;
                foreach (var id in dict.Keys)
                {
                    _allIds.Add(id);
                }
            }
        }
        
        public bool HasString(uint id)
        {
            return _allIds.Contains(id);
        }

        public Dictionary<uint, string> ParseFile(string path)
        {
            var sw = Stopwatch.StartNew();
            var dict = new Dictionary<uint, string>();
            string fileName = Path.GetFileName(path);
            
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                using var br = new BinaryReader(ms);

                uint cnt = br.ReadUInt32();
                uint dataSize = br.ReadUInt32();
                
                
                var readDirSw = Stopwatch.StartNew();

                var dir = new List<(uint id, uint off)>();
                for (int i = 0; i < cnt; i++)
                {
                    uint id = br.ReadUInt32();
                    uint off = br.ReadUInt32();
                    dir.Add((id, off));
                }
                readDirSw.Stop();

                bool isDl = path.EndsWith(".dlstrings", StringComparison.OrdinalIgnoreCase);
                bool isIl = path.EndsWith(".ilstrings", StringComparison.OrdinalIgnoreCase);
                long startPos = ms.Position;
                
                var readDataSw = Stopwatch.StartNew();

                foreach (var (id, off) in dir)
                {
                    ms.Position = startPos + off;
                    string txt = "";
                    
                    try
                    {
                        if (isDl || isIl)
                        {
                            uint len = br.ReadUInt32();
                            if (len == 0 || len > 65535) continue;
                            
                            byte[] raw = br.ReadBytes((int)len);
                            while (raw.Length > 0 && raw[raw.Length - 1] == 0)
                                Array.Resize(ref raw, raw.Length - 1);
                            
                            txt = DecodeText(raw);
                        }
                        else
                        {
                            var raw = new List<byte>();
                            byte b;
                            while ((b = br.ReadByte()) != 0)
                                raw.Add(b);
                            txt = DecodeText(raw.ToArray());
                        }
                    }
                    catch (Exception e)
                    {
                        continue;
                    }
                    
                    if (!string.IsNullOrEmpty(txt))
                    {
                        dict[id] = txt;
                    }
                }
                readDataSw.Stop();
                
                sw.Stop();

            }
            catch (Exception e)
            {
                Godot.GD.PrintErr($"[StrLoader] 解析文件失败: {e.Message}");
            }
            
            return dict;
        }

        private string DecodeText(byte[] raw)
        {
            if (raw == null || raw.Length == 0) return "";
            
            try
            {
                string utf8 = Encoding.UTF8.GetString(raw);
                if (!HasGarbled(utf8))
                    return utf8;
            }
            catch { }

            try
            {
                var gb = Encoding.GetEncoding("GB18030");
                string gbStr = gb.GetString(raw);
                if (!HasGarbled(gbStr))
                    return gbStr;
            }
            catch { }

            try
            {
                return Encoding.UTF8.GetString(raw);
            }
            catch
            {
                return "";
            }
        }
        
        private bool HasGarbled(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            int invalid = 0;
            for (int i = 0; i < Math.Min(text.Length, 100); i++)
            {
                char c = text[i];
                if (c == '\uFFFD') invalid++;
                else if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') invalid++;
            }
            return (float)invalid / Math.Min(text.Length, 100) > 0.3f;
        }

        public string Get(string mod, uint id, string ftype = null)
        {
            string modBase = Path.GetFileNameWithoutExtension(mod);
            string ext = GetExt(ftype);
            string key = $"{modBase}_english{ext}";
            
            if (_cache.TryGetValue(key, out var dict) && dict.TryGetValue(id, out string txt))
                return txt;
            
            string[] fallback = { ".strings", ".dlstrings", ".ilstrings" };
            foreach (string e in fallback)
            {
                if (e == ext) continue;
                string fbKey = $"{modBase}_english{e}";
                if (_cache.TryGetValue(fbKey, out var fbDict) && fbDict.TryGetValue(id, out txt))
                    return txt;
            }
            
            return string.Format(L.T("STR_HOLDER"), id.ToString("X8"));
        }

        public string GetDirect(string mod, uint id, string ftype = null)
        {
            string modBase = Path.GetFileNameWithoutExtension(mod);
            string ext = GetExt(ftype);
            string key = $"{modBase}_english{ext}";
            
            if (_cache.TryGetValue(key, out var dict) && dict.TryGetValue(id, out string txt))
                return txt;
            
            string[] fallback = { ".strings", ".dlstrings", ".ilstrings" };
            foreach (string e in fallback)
            {
                if (e == ext) continue;
                string fbKey = $"{modBase}_english{e}";
                if (_cache.TryGetValue(fbKey, out var fbDict) && fbDict.TryGetValue(id, out txt))
                    return txt;
            }
            
            return string.Format(L.T("STR_HOLDER"), id.ToString("X8"));
        }

        private string GetExt(string ftype)
        {
            if (string.IsNullOrEmpty(ftype)) return ".strings";
            
            if (ftype == "DESC" || ftype == "CNAM" || ftype == "ITXT" || ftype == "DTXT") 
                return ".dlstrings";
            
            if (ftype.StartsWith("NAM") || ftype == "RNAM" || ftype == "FULL") 
                return ".ilstrings";
            
            return ".strings";
        }

        public void LoadAll()
        {
            if (!Directory.Exists(_dir))
                return;
            
            var files = Directory.GetFiles(_dir);
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (string f in files)
            {
                string name = Path.GetFileName(f);
                if (name.EndsWith(".strings", StringComparison.OrdinalIgnoreCase) || 
                    name.EndsWith(".dlstrings", StringComparison.OrdinalIgnoreCase) || 
                    name.EndsWith(".ilstrings", StringComparison.OrdinalIgnoreCase))
                {
                    string baseName = Path.GetFileNameWithoutExtension(name);
                    int idx = baseName.LastIndexOf('_');
                    if (idx > 0)
                    {
                        string mod = baseName.Substring(0, idx);
                        if (!loaded.Contains(mod))
                        {
                            LoadMod(mod);
                            loaded.Add(mod);
                        }
                    }
                }
            }
        }
    }
}