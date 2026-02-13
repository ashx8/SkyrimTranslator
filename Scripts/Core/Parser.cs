// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//解析脚本
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using SkyrimModTranslator.Common;
using Godot;

namespace SkyrimModTranslator.Core
{
    public class Parser
    {
        public Dictionary<string, List<string>> TranslationMap { get; set; }

        public Data.Mod Parse(string path, Action<float> progressCallback = null)
        {
            Data.Mod mod = new Data.Mod(path);
            byte[] fileData = File.ReadAllBytes(path);
            mod.Raw = fileData;

            using (MemoryStream ms = new MemoryStream(fileData))
            using (BinaryReader br = new BinaryReader(ms))
            {
                while (ms.Position < ms.Length)
                {
                    ReadGroupOrRecord(br, mod);
                    progressCallback?.Invoke((float)ms.Position / ms.Length);
                }
            }
            return mod;
        }
        
        //解析NPC名称（用于显示悬挂窗口中的NPC名称）
        private string ParseNPCName(byte[] data)
        {
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
                        nextFieldSizeOverride = br.ReadUInt32();
                        continue;
                    }

                    int actualSize = (nextFieldSizeOverride > 0) ? (int)nextFieldSizeOverride : fSize;
                    if (ms.Position + actualSize > ms.Length)
                    {
                        break;
                    }
                    byte[] fData = br.ReadBytes(actualSize);
                    nextFieldSizeOverride = 0;

                    if (fLabel == "FULL")
                    {
                        return ParseSkyrimString(fData);
                    }
                }
            }
            return null;
        }
        
        //读取
        private void ReadGroupOrRecord(BinaryReader br, Data.Mod mod)
        {
            if (br.BaseStream.Length - br.BaseStream.Position < 8)
            {
                return;
            }

            string label = Encoding.ASCII.GetString(br.ReadBytes(4));
            uint size = br.ReadUInt32();

            if (label == "GRUP")
            {
                if (br.BaseStream.Length - br.BaseStream.Position >= 16)
                {
                    br.ReadBytes(16);
                }
            }
            else
            {
                if (br.BaseStream.Length - br.BaseStream.Position < 16)
                {
                    return;
                }
                uint flags = br.ReadUInt32();
                uint formId = br.ReadUInt32();
                br.ReadBytes(8);

                byte[] data;
                if ((flags & 0x00040000) != 0)
                {
                    if (br.BaseStream.Length - br.BaseStream.Position < 4)
                    {
                        return;
                    }
                    uint decompressedSize = br.ReadUInt32();
                    int compressedSize = (int)size - 4;
                    if (br.BaseStream.Length - br.BaseStream.Position >= compressedSize)
                    {
                        byte[] compressedData = br.ReadBytes(compressedSize);
                        data = Decompress(compressedData, decompressedSize);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    if (br.BaseStream.Length - br.BaseStream.Position >= size)
                    {
                        data = br.ReadBytes((int)size);
                    }
                    else
                    {
                        return;
                    }
                }

                ParseFields(label, formId, data, mod);
            }
        }
        
        //解析记录字段
        private void ParseFields(string rectype, uint formId, byte[] data, Data.Mod mod)
        {
            string rType = rectype.Trim().ToUpper();
            
            if (rType == "NPC_")
            {
                mod.NpcIds.Add(formId);
                string npcName = ParseNPCName(data);
                if (!string.IsNullOrEmpty(npcName))
                {
                    mod.NpcNames[formId] = npcName;
                }
            }
            
            if (TranslationMap == null || !TranslationMap.ContainsKey(rType)) return;

            uint sId = 0;
            string emo = "Normal";

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
                        {
                            nextFieldSizeOverride = br.ReadUInt32();
                        }
                        continue;
                    }

                    int actualSize = (nextFieldSizeOverride > 0) ? (int)nextFieldSizeOverride : fSize;
                    if (ms.Position + actualSize > ms.Length)
                    {
                        break;
                    }
                    byte[] fData = br.ReadBytes(actualSize);
                    nextFieldSizeOverride = 0;

                    if (rType == "INFO")
                    {
                        if (fLabel == "ANAM" && actualSize >= 4)
                        {
                            sId = BitConverter.ToUInt32(fData, 0);
                        }
                        else if (fLabel == "TRDT" && actualSize >= 4)
                        {
                            emo = GetEmotionName(BitConverter.ToInt32(fData, 0));
                        }
                        else if (fLabel == "CTDA" && actualSize >= 32)
                        {
                            ushort func = BitConverter.ToUInt16(fData, 8);
                            uint p1 = BitConverter.ToUInt32(fData, 12);
                            uint cv = BitConverter.ToUInt32(fData, 4);
                            if (func == 32) sId = p1;
                            else if (p1 > 0x00 && p1 < 0xFFFFFFFF) sId = p1;
                            else if (cv > 0x00 && cv < 0xFFFFFFFF) sId = cv;
                        }
                    }
                }
            }

            string sHex = (sId != 0) ? sId.ToString("X8") : "";
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader br = new BinaryReader(ms))
            {
                uint nextFieldSizeOverride = 0;
                int fieldIndex = 0;
                while (ms.Position < ms.Length)
                {
                    if (ms.Length - ms.Position < 6) break;
                    string fLabel = Encoding.ASCII.GetString(br.ReadBytes(4));
                    ushort fSize = br.ReadUInt16();

                    if (fLabel == "XXXX")
                    {
                        if (ms.Length - ms.Position >= 4)
                        {
                            nextFieldSizeOverride = br.ReadUInt32();
                        }
                        continue;
                    }

                    int actualSize = (nextFieldSizeOverride > 0) ? (int)nextFieldSizeOverride : fSize;
                    if (ms.Position + actualSize > ms.Length)
                    {
                        break;
                    }
                    byte[] fData = br.ReadBytes(actualSize);
                    nextFieldSizeOverride = 0;

                    if (TranslationMap[rType].Contains(fLabel))
                    {
                        string text = ParseSkyrimString(fData);
                        if (!string.IsNullOrEmpty(text) && !IsBinary(fData))
                        {
                            Data.Item item = new Data.Item();
                            item.ID = mod.Items.Count + 1;
                            item.Mod = mod;
                            item.Type = rType;
                            item.FormID = formId;
                            item.FType = fLabel;
                            item.FIdx = fieldIndex;
                            item.Ori = text;
                            item.Trans = "";
                            item.Cty = emo;
                            item.Speaker = sHex;
                            mod.Items.Add(item);

                            if (sId != 0)
                            {
                                if (!mod.NpcMap.ContainsKey(sId))
                                {
                                    mod.NpcMap[sId] = new List<uint>();
                                }
                                if (!mod.NpcMap[sId].Contains(formId))
                                {
                                    mod.NpcMap[sId].Add(formId);
                                }
                            }
                        }
                    }
                    fieldIndex++;
                }
            }
        }
        

        private byte[] Decompress(byte[] compressedData, uint decompressedSize)
        {
            using (MemoryStream ms = new MemoryStream(compressedData))
            {
                if (ms.Length >= 2)
                {
                    ms.ReadByte(); ms.ReadByte();
                }
                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                using (MemoryStream outMs = new MemoryStream())
                {
                    ds.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }
        }
        
        //解析Skyrim字符串
        private string ParseSkyrimString(byte[] data)
        {
            string raw = Encoding.UTF8.GetString(data).TrimEnd('\0');
            return ParseTextWithEmojis(raw);
        }

        private string ParseTextWithEmojis(string text)
        {
            try
            {
                return System.Text.RegularExpressions.Regex.Unescape(text);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Parser] 正则解码失败: {e.Message}");
                return text;
            }
        }
        
        //检查是否为二进制数据
        private bool IsBinary(byte[] data)
        {
            if (data.Length <= 4) return false;
            int nulls = 0;
            int maxCheck = Math.Min(data.Length, 20);
            for (int i = 0; i < maxCheck; i++)
            {
                if (data[i] == 0) nulls++;
            }
            return nulls > 5;
        }
        
        //用于显示表情
        private string GetEmotionName(int emotionId)
        {
            Dictionary<int, string> emotionMap = new Dictionary<int, string>
            {
                {0, L.T("EMOTION_NORMAL")},
                {1, L.T("EMOTION_ANGER")},
                {2, L.T("EMOTION_DISGUST")},
                {3, L.T("EMOTION_FEAR")},
                {4, L.T("EMOTION_HAPPINESS")},
                {5, L.T("EMOTION_SADNESS")},
                {6, L.T("EMOTION_SURPRISE")}
            };
            
            if (emotionMap.TryGetValue(emotionId, out string emotionKey))
            {
                return L.T(emotionKey);
            }
            return L.T("EMOTION_NORMAL");
        }
    }

    public static class LKeys
    {
        public const string LOG_PARSING_START = "LOG_PARSING_START";
        public const string LOG_PARSING_FINISH = "LOG_PARSING_FINISH";
    }
}
