// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//解析脚本，负责读取模组文件
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using SkyrimModTranslator.Common;
using Godot;

namespace SkyrimModTranslator.Core
{
    public class Parser
    {
        public Dictionary<string, List<string>> TranslationMap { get; set; }
        private StrLoader _strLoader;

        public Parser()
        {
            TranslationMap = Cfg.LoadTransMap();
        }
        
        public void SetStrLoader(StrLoader strLoader)
        {
            _strLoader = strLoader;
        }

        public Data.Mod Parse(string path, Action<float> progressCallback = null)
        {
            var totalSw = Stopwatch.StartNew();
            string modName = System.IO.Path.GetFileName(path);

            
            Data.Mod mod = new Data.Mod(path);
            
            var sw = Stopwatch.StartNew();
            byte[] fileData = File.ReadAllBytes(path);
            sw.Stop();

            
            mod.Raw = fileData;

            ScanAllRecords(fileData, mod, progressCallback);
            ProcessAllRecords(fileData, mod, progressCallback);
            
            totalSw.Stop();

            
            return mod;
        }
        
        private bool TryReadRecord(BinaryReader br, out string label, out uint formId, out byte[] data)
        {
            label = null;
            formId = 0;
            data = null;
            
            if (br.BaseStream.Length - br.BaseStream.Position < 8)
                return false;

            label = Encoding.ASCII.GetString(br.ReadBytes(4));
            uint size = br.ReadUInt32();

            if (label == "GRUP")
            {
                if (br.BaseStream.Length - br.BaseStream.Position >= 16)
                    br.ReadBytes(16);
                return false;
            }
            
            if (br.BaseStream.Length - br.BaseStream.Position < 16)
                return false;
            
            uint flags = br.ReadUInt32();
            formId = br.ReadUInt32();
            br.ReadBytes(8);

            if ((flags & 0x00040000) != 0)
            {
                if (br.BaseStream.Length - br.BaseStream.Position < 4)
                    return false;
                uint decompressedSize = br.ReadUInt32();
                int compressedSize = (int)size - 4;
                if (br.BaseStream.Length - br.BaseStream.Position >= compressedSize)
                {
                    byte[] compressedData = br.ReadBytes(compressedSize);
                    data = Decompress(compressedData, decompressedSize);
                }
                else
                    return false;
            }
            else
            {
                if (br.BaseStream.Length - br.BaseStream.Position >= size)
                    data = br.ReadBytes((int)size);
                else
                    return false;
            }
            
            return true;
        }
        
        private void ScanAllRecords(byte[] fileData, Data.Mod mod, Action<float> progressCallback)
        {
            var sw = Stopwatch.StartNew();
            string modName = System.IO.Path.GetFileNameWithoutExtension(mod.Path);

            
            int totalFourByteFields = 0;
            int totalTranslatableFields = 0;
            int validStringIds = 0;
            int recordCount = 0;
            
            using (MemoryStream ms = new MemoryStream(fileData))
            using (BinaryReader br = new BinaryReader(ms))
            {
                while (ms.Position < ms.Length)
                {
                    if (TryReadRecord(br, out string label, out uint formId, out byte[] data))
                    {
                        ScanRecordFields(label, data, ref totalFourByteFields, ref totalTranslatableFields, ref validStringIds);
                        recordCount++;
                    }
                    progressCallback?.Invoke((float)ms.Position / ms.Length * 0.5f);
                }
            }
            
            sw.Stop();

            
            if (totalTranslatableFields > 0)
            {
                float fourByteRatio = (float)totalFourByteFields / totalTranslatableFields;
                float validIdRatio = (float)validStringIds / totalTranslatableFields;
                

                
                if (fourByteRatio > 0.45f || validIdRatio > 0.40f)
                {
                    mod.Type = Data.ModType.StringTable;

                }
            }
        }
        
        private void ScanRecordFields(string rectype, byte[] data, ref int totalFourByteFields, ref int totalTranslatableFields, ref int validStringIds)
        {
            string rType = rectype.Trim().ToUpper();
            
            if (TranslationMap == null || !TranslationMap.ContainsKey(rType)) return;

            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader br = new BinaryReader(ms))
            {
                uint nextFieldSizeOverride = 0;
                while (ms.Position < ms.Length)
                {
                    if (ms.Length - ms.Position < 6) break;

                    string fLabel = Encoding.ASCII.GetString(br.ReadBytes(4));
                    ushort fSize = br.ReadUInt16();

                    if (fLabel == "XXXX")
                    {
                        if (ms.Length - ms.Position >= 4)
                            nextFieldSizeOverride = br.ReadUInt32();
                        continue;
                    }

                    int actualSize = (nextFieldSizeOverride > 0) ? (int)nextFieldSizeOverride : fSize;
                    if (ms.Position + actualSize > ms.Length) break;
                    
                    byte[] fData = br.ReadBytes(actualSize);
                    nextFieldSizeOverride = 0;

                    if (TranslationMap[rType].Contains(fLabel))
                    {
                        totalTranslatableFields++;
                        if (fData.Length == 4)
                        {
                            totalFourByteFields++;
                            uint testId = BitConverter.ToUInt32(fData, 0);
                            if (testId >= 1 && testId <= 0xFFFFFF)
                                validStringIds++;
                        }
                    }
                }
            }
        }
        
        private struct FieldInfo
        {
            public string Label;
            public int Size;
            public byte[] Data;
        }
        
        private IEnumerable<FieldInfo> EnumerateFields(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader br = new BinaryReader(ms))
            {
                uint nextFieldSizeOverride = 0;
                while (ms.Position < ms.Length)
                {
                    if (ms.Length - ms.Position < 6) yield break;

                    string fLabel = Encoding.ASCII.GetString(br.ReadBytes(4));
                    ushort fSize = br.ReadUInt16();

                    if (fLabel == "XXXX")
                    {
                        if (ms.Length - ms.Position >= 4)
                            nextFieldSizeOverride = br.ReadUInt32();
                        continue;
                    }

                    int actualSize = (nextFieldSizeOverride > 0) ? (int)nextFieldSizeOverride : fSize;
                    if (ms.Position + actualSize > ms.Length) yield break;
                    
                    byte[] fData = br.ReadBytes(actualSize);
                    nextFieldSizeOverride = 0;

                    yield return new FieldInfo { Label = fLabel, Size = actualSize, Data = fData };
                }
            }
        }
        
        private void ProcessAllRecords(byte[] fileData, Data.Mod mod, Action<float> progressCallback)
        {
            var sw = Stopwatch.StartNew();
            string modName = System.IO.Path.GetFileNameWithoutExtension(mod.Path);

            
            int recordCount = 0;
            using (MemoryStream ms = new MemoryStream(fileData))
            using (BinaryReader br = new BinaryReader(ms))
            {
                while (ms.Position < ms.Length)
                {
                    if (TryReadRecord(br, out string label, out uint formId, out byte[] data))
                    {
                        ProcessFields(label, formId, data, mod);
                        recordCount++;
                    }
                    progressCallback?.Invoke(0.5f + (float)ms.Position / ms.Length * 0.5f);
                }
            }
            
            sw.Stop();

        }
        
        private void ProcessFields(string rectype, uint formId, byte[] data, Data.Mod mod)
        {
            string rType = rectype.Trim().ToUpper();
            bool isNPC = rType == "NPC_";
            bool hasTranslatableFields = TranslationMap != null && TranslationMap.ContainsKey(rType);
            
            if (!isNPC && !hasTranslatableFields) return;
            
            if (isNPC) mod.NpcIds.Add(formId);

            uint sId = 0;
            string emo = "Normal";
            int fieldIdx = 0;
            string npcName = null;
            
            string gmstEdid = null;
            byte? perkEpft = null;
            
            foreach (var field in EnumerateFields(data))
            {
                if (rType == "GMST" && field.Label == "EDID")
                    gmstEdid = ParseSkyrimString(field.Data);
                
                if (rType == "PERK" && field.Label == "EPFT" && field.Data.Length >= 1)
                    perkEpft = field.Data[0];
                
                if (rType == "PERK" && field.Label == "PRKF")
                    perkEpft = null;
                
                if (rType == "INFO")
                {
                    if (field.Label == "ANAM" && field.Size >= 4)
                        sId = BitConverter.ToUInt32(field.Data, 0);
                    else if (field.Label == "TRDT" && field.Size >= 4)
                        emo = GetEmotionName(BitConverter.ToInt32(field.Data, 0));
                    else if (field.Label == "CTDA" && field.Size >= 32)
                    {
                        ushort func = BitConverter.ToUInt16(field.Data, 8);
                        uint p1 = BitConverter.ToUInt32(field.Data, 12);
                        uint cv = BitConverter.ToUInt32(field.Data, 4);
                        if (func == 32) sId = p1;
                        else if (p1 > 0x00 && p1 < 0xFFFFFFFF) sId = p1;
                        else if (cv > 0x00 && cv < 0xFFFFFFFF) sId = cv;
                    }
                }
                
                if (isNPC && field.Label == "FULL")
                    npcName = ParseSkyrimString(field.Data);
                
                if (hasTranslatableFields && TranslationMap[rType].Contains(field.Label))
                {
                    ProcessTranslationField(field, formId, mod, rType, fieldIdx, emo, sId, gmstEdid, perkEpft);
                }
                
                fieldIdx++;
            }
            
            if (isNPC && !string.IsNullOrEmpty(npcName))
                mod.NpcNames[formId] = npcName;
        }
        
        private void ProcessTranslationField(FieldInfo field, uint formId, Data.Mod mod, string rType, int fieldIdx, string emo, uint sId, string gmstEdid, byte? perkEpft)
        {
            string modBase = System.IO.Path.GetFileNameWithoutExtension(mod.Path);
            bool isStringId = false;
            uint strId = 0;
            string text = "";
            
            //处理STR类型的字段
            if (
                (rType == "GMST" && field.Label == "DATA") ||
                
                (rType == "PERK" && field.Label == "EPFD") ||
                
                (mod.Type == Data.ModType.StringTable && rType != "TES4")
            )
            {
                if (rType == "GMST" && field.Label == "DATA")
                {
                    if (string.IsNullOrEmpty(gmstEdid) || !gmstEdid.StartsWith("s"))
                        return;
                }
                
                else if (rType == "PERK" && field.Label == "EPFD")
                {
                    if (!perkEpft.HasValue)
                        return;
                    if (perkEpft.Value != 0x06 && perkEpft.Value != 0x07)
                        return;
                }
                
                if (field.Data.Length != 4)
                    return;
                
                strId = BitConverter.ToUInt32(field.Data, 0);
                if (strId < 1 || strId > 0xFFFFFF)
                    return;
                
                if (!_strLoader.HasString(strId))
                    return;
                
                text = _strLoader.Get(modBase, strId, field.Label);
                isStringId = true;
            }
            else if (field.Data.Length == 4 && mod.Type == Data.ModType.StringTable && rType != "TES4")
            {
                strId = BitConverter.ToUInt32(field.Data, 0);
                if (strId >= 1 && strId <= 0xFFFFFF && _strLoader.HasString(strId))
                {
                    text = _strLoader.Get(modBase, strId, field.Label);
                    isStringId = true;
                }
                else
                {
                    text = ParseSkyrimString(field.Data);
                }
            }
            else
            {
                text = ParseSkyrimString(field.Data);
            }
            
            if (string.IsNullOrWhiteSpace(text)) return;
            if (IsBinary(field.Data)) return;
            

            
            Data.Item item = new Data.Item();
            item.ID = mod.Items.Count + 1;
            item.Mod = mod;
            item.Type = rType;
            item.FormID = formId;
            item.FType = field.Label;
            item.FIdx = fieldIdx;
            item.Ori = text;
            item.Trans = "";
            item.Cty = emo;
            item.Speaker = (sId != 0) ? sId.ToString("X8") : "";
            if (isStringId) item.StringId = strId;
            mod.Items.Add(item);
            
            if (sId != 0)
            {
                if (!mod.NpcMap.ContainsKey(sId))
                    mod.NpcMap[sId] = new List<uint>();
                if (!mod.NpcMap[sId].Contains(formId))
                    mod.NpcMap[sId].Add(formId);
            }
        }
        
        private byte[] Decompress(byte[] compressedData, uint decompressedSize)
        {
            using (MemoryStream ms = new MemoryStream(compressedData))
            {
                if (ms.Length >= 2) { ms.ReadByte(); ms.ReadByte(); }
                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                using (MemoryStream outMs = new MemoryStream())
                {
                    ds.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }
        }
        
        private string ParseSkyrimString(byte[] data)
        {
            bool allZero = true;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0) { allZero = false; break; }
            }
            if (allZero) return "";
            
            string raw = Encoding.UTF8.GetString(data).TrimEnd('\0');
            
            if (raw.Length > 0 && raw[0] == 0xFF)
                return "";
            
            if (EncDet.HasGarbled(raw))
            {
                try
                {
                    var gb = System.Text.Encoding.GetEncoding("GBK");
                    string gbStr = gb.GetString(data).TrimEnd('\0');
                    if (!EncDet.HasGarbled(gbStr))
                        return gbStr;
                }
                catch { }
            }
            
            try { return System.Text.RegularExpressions.Regex.Unescape(raw); }
            catch { return raw; }
        }
        
        private bool IsBinary(byte[] data)
        {
            if (data.Length <= 4) return false;
            int nulls = 0;
            for (int i = 0; i < Math.Min(data.Length, 20); i++)
                if (data[i] == 0) nulls++;
            return nulls > 5;
        }
        
        private string GetEmotionName(int emotionId)
        {
            switch(emotionId)
            {
                case 0: return L.T("EMOTION_NORMAL");
                case 1: return L.T("EMOTION_ANGER");
                case 2: return L.T("EMOTION_DISGUST");
                case 3: return L.T("EMOTION_FEAR");
                case 4: return L.T("EMOTION_HAPPINESS");
                case 5: return L.T("EMOTION_SADNESS");
                case 6: return L.T("EMOTION_SURPRISE");
                default: return L.T("EMOTION_NORMAL");
            }
        }

        public void LoadStr(string modName)
        {
            _strLoader.LoadMod(modName);
        }
    }
}