// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//模块回写，把翻译写回模组文件
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Godot;

using FileAccess = System.IO.FileAccess;

namespace SkyrimModTranslator.Core
{
    public class ModFileWriter
    {
        public string Write(string filePath, Data.Mod mod)
        {
            if (mod.Type == Data.ModType.StringTable)
            {
                return WriteStringTable(filePath, mod);
            }
            
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string tempFile = filePath + ".tmp";
            string bakFile = filePath + ".bak." + timestamp;
            try
            {
                using (var fsInput = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fsInput))
                using (var fsOutput = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fsOutput))
                {
                    ProcessStream(br, bw, mod, fsInput.Length);
                }
                if (File.Exists(filePath))
                {
                    File.Move(filePath, bakFile);
                    File.Move(tempFile, filePath);
                }
                else
                {
                    File.Move(tempFile, filePath);
                }
                return Path.GetFileName(bakFile);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Writer] 写入文件失败: {e.Message}");
                if (File.Exists(tempFile)) File.Delete(tempFile);
                return null;
            }
        }
        
        private string WriteStringTable(string filePath, Data.Mod mod)
        {
            try
            {
                string exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
                string outDir = Path.Combine(exeDir, "Strings_output");
                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);
                
                string modName = Path.GetFileNameWithoutExtension(filePath);
                var transMap = new Dictionary<uint, string>();
                foreach (var item in mod.Items)
                {
                    if (!string.IsNullOrEmpty(item.Trans) && item.StringId != 0)
                    {
                        transMap[item.StringId] = item.Trans;
                    }
                }
                
                var loader = new SkyrimModTranslator.Common.StrLoader();
                var srcMap = loader.ParseFile(Path.Combine(exeDir, "Strings", $"{modName}_english.strings"));
                
                var engMap = new Dictionary<uint, string>();
                foreach (var kv in srcMap)
                {
                    engMap[kv.Key] = kv.Value;
                }
                WriteStringsFile(Path.Combine(outDir, $"{modName}_english.strings"), engMap);
                
                var chnMap = new Dictionary<uint, string>();
                foreach (var kv in srcMap)
                {
                    if (transMap.TryGetValue(kv.Key, out string trans) && !string.IsNullOrEmpty(trans))
                        chnMap[kv.Key] = trans;
                    else
                        chnMap[kv.Key] = kv.Value;
                }
                WriteStringsFile(Path.Combine(outDir, $"{modName}_chinese.strings"), chnMap);
                
                WriteDlIlFiles(outDir, modName, srcMap, transMap);
                
                return $"Strings_output\\{modName}_chinese.strings";
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Writer] 生成Strings文件失败: {e.Message}");
                return null;
            }
        }
        
        private void WriteStringsFile(string path, Dictionary<uint, string> data)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            
            uint totalDataSize = 0;
            var offsets = new Dictionary<uint, uint>();
            
            foreach (var kv in data)
            {
                byte[] strBytes = Encoding.UTF8.GetBytes(kv.Value + "\0");
                offsets[kv.Key] = totalDataSize;
                totalDataSize += (uint)strBytes.Length;
            }
            
            bw.Write((uint)data.Count);
            bw.Write(totalDataSize);
            
            foreach (var kv in data)
            {
                bw.Write(kv.Key);
                bw.Write(offsets[kv.Key]);
            }
            
            foreach (var kv in data)
            {
                byte[] strBytes = Encoding.UTF8.GetBytes(kv.Value + "\0");
                bw.Write(strBytes);
            }
            
            File.WriteAllBytes(path, ms.ToArray());
        }
        
        private void WriteDlIlFiles(string outDir, string modName, Dictionary<uint, string> srcMap, Dictionary<uint, string> transMap)
        {
            string exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
            string srcDir = Path.Combine(exeDir, "Strings");
            
            string[] types = { "dlstrings", "ilstrings" };
            foreach (string type in types)
            {
                string srcPath = Path.Combine(srcDir, $"{modName}_english.{type}");
                if (File.Exists(srcPath))
                {
                    var loader = new SkyrimModTranslator.Common.StrLoader();
                    var src = loader.ParseFile(srcPath);
                    
                    var outMap = new Dictionary<uint, string>();
                    foreach (var kv in src)
                    {
                        if (transMap.TryGetValue(kv.Key, out string trans) && !string.IsNullOrEmpty(trans))
                            outMap[kv.Key] = trans;
                        else
                            outMap[kv.Key] = kv.Value;
                    }
                    
                    WriteDlIlFile(Path.Combine(outDir, $"{modName}_chinese.{type}"), outMap);
                    WriteDlIlFile(Path.Combine(outDir, $"{modName}_english.{type}"), src);
                }
            }
        }
        
        private void WriteDlIlFile(string path, Dictionary<uint, string> data)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            
            uint totalDataSize = 0;
            var offsets = new Dictionary<uint, uint>();
            
            foreach (var kv in data)
            {
                byte[] strBytes = Encoding.UTF8.GetBytes(kv.Value + "\0");
                uint len = (uint)strBytes.Length;
                offsets[kv.Key] = totalDataSize;
                totalDataSize += len + 4;
            }
            
            bw.Write((uint)data.Count);
            bw.Write(totalDataSize);
            
            foreach (var kv in data)
            {
                bw.Write(kv.Key);
                bw.Write(offsets[kv.Key]);
            }
            
            foreach (var kv in data)
            {
                byte[] strBytes = Encoding.UTF8.GetBytes(kv.Value + "\0");
                bw.Write((uint)strBytes.Length);
                bw.Write(strBytes);
            }
            
            File.WriteAllBytes(path, ms.ToArray());
        }
        
        //递归处理组（Group）和记录（Record）
        private void ProcessStream(BinaryReader br, BinaryWriter bw, Data.Mod mod, long limit)
        {
            while (br.BaseStream.Position < limit)
            {
                if (limit - br.BaseStream.Position < 24)
                {
                    bw.Write(br.ReadBytes((int)(limit - br.BaseStream.Position)));
                    break;
                }
                string sig = Encoding.ASCII.GetString(br.ReadBytes(4));
                uint size = br.ReadUInt32();
                uint flags = br.ReadUInt32();
                uint formID = br.ReadUInt32();
                byte[] headerRemainder = br.ReadBytes(8);
                if (sig == "GRUP")
                {
                    bw.Write(Encoding.ASCII.GetBytes("GRUP"));
                    long groupSizePos = bw.BaseStream.Position;
                    bw.Write(size);
                    bw.Write(flags);
                    bw.Write(formID);
                    bw.Write(headerRemainder);
                    long grupDataEnd = br.BaseStream.Position + (size - 24);
                    ProcessStream(br, bw, mod, grupDataEnd);
                    long currentPos = bw.BaseStream.Position;
                    uint newGroupSize = (uint)(currentPos - (groupSizePos - 4));
                    bw.BaseStream.Position = groupSizePos;
                    bw.Write(newGroupSize);
                    bw.BaseStream.Position = currentPos;
                }
                else
                {
                    bool isCompressed = (flags & 0x00040000) != 0;
                    byte[] rawData;
                    if (isCompressed)
                    {
                        uint uncompressedSize = br.ReadUInt32();
                        byte[] compressedData = br.ReadBytes((int)size - 4);
                        rawData = Decompress(compressedData, uncompressedSize);
                    }
                    else
                    {
                        rawData = br.ReadBytes((int)size);
                    }
                    byte[] updatedData = UpdateRecordFields(sig, formID, rawData, mod);
                    if (sig == "TES4") flags &= ~0x80u;
                    bw.Write(Encoding.ASCII.GetBytes(sig));
                    long recSizePos = bw.BaseStream.Position;
                    bw.Write((uint)0);
                    bw.Write(flags);
                    bw.Write(formID);
                    bw.Write(headerRemainder);
                    if (isCompressed)
                    {
                        bw.Write((uint)updatedData.Length);
                        byte[] compressed = Compress(updatedData);
                        UpdateSizeField(bw, recSizePos, (uint)compressed.Length + 4);
                        bw.Write(compressed);
                    }
                    else
                    {
                        UpdateSizeField(bw, recSizePos, (uint)updatedData.Length);
                        bw.Write(updatedData);
                    }
                }
            }
        }
        
        //更新记录里的大小字段
        private void UpdateSizeField(BinaryWriter bw, long pos, uint value)
        {
            long current = bw.BaseStream.Position;
            bw.BaseStream.Position = pos;
            bw.Write(value);
            bw.BaseStream.Position = current;
        }

        private byte[] UpdateRecordFields(string sig, uint formId, byte[] data, Data.Mod mod)
        {
            if (mod?.Items == null) return data;
            List<Data.Item> items = new List<Data.Item>();
            foreach (Data.Item item in mod.Items)
            {
                if (item.Type == sig && item.FormID == formId)
                {
                    items.Add(item);
                }
            }
            if (items.Count == 0) return data;
            using (var msIn = new MemoryStream(data))
            using (var br = new BinaryReader(msIn))
            using (var msOut = new MemoryStream())
            using (var bw = new BinaryWriter(msOut))
            {
                int fieldIdx = 0;
                uint nextLargeSize = 0;
                while (msIn.Position < msIn.Length)
                {
                    if (msIn.Length - msIn.Position < 6) break;
                    string fType = Encoding.ASCII.GetString(br.ReadBytes(4));
                    ushort fSize = br.ReadUInt16();
                    if (fType == "XXXX")
                    {
                        nextLargeSize = br.ReadUInt32();
                        bw.Write(Encoding.ASCII.GetBytes("XXXX"));
                        bw.Write((ushort)4);
                        bw.Write(nextLargeSize);
                        continue;
                    }
                    int actualSize = nextLargeSize > 0 ? (int)nextLargeSize : fSize;
                    byte[] fData = br.ReadBytes(actualSize);
                    nextLargeSize = 0;
                    Data.Item entry = null;
                    foreach (Data.Item item in items)
                    {
                        if (item.FType == fType && item.FIdx == fieldIdx)
                        {
                            entry = item;
                            break;
                        }
                    }
                    if (entry != null && !string.IsNullOrEmpty(entry.Trans))
                    {
                        byte[] newText = Encoding.UTF8.GetBytes(entry.Trans + "\0");
                        bw.Write(Encoding.ASCII.GetBytes(fType));
                        if (newText.Length > ushort.MaxValue)
                        {
                            GD.PrintErr($"[Writer] 翻译文本过长，跳过: FormID={formId:X8}, 类型={fType}, 长度={newText.Length}");
                            bw.Write((ushort)fSize);
                            bw.Write(fData);
                        }
                        else
                        {
                            bw.Write((ushort)newText.Length);
                            bw.Write(newText);
                        }
                    }
                    else
                    {
                        bw.Write(Encoding.ASCII.GetBytes(fType));
                        bw.Write((ushort)fSize);
                        bw.Write(fData);
                    }
                    fieldIdx++;
                }
                return msOut.ToArray();
            }
        }

        private byte[] Decompress(byte[] data, uint uncompressedSize)
        {
            if (data.Length < 2) return new byte[0];
            using (var ms = new MemoryStream(data))
            {
                ms.ReadByte(); ms.ReadByte();
                using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                using (var oms = new MemoryStream())
                {
                    ds.CopyTo(oms);
                    return oms.ToArray();
                }
            }
        }

        private byte[] Compress(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(new byte[] { 0x78, 0x9C }, 0, 2);
                using (var ds = new DeflateStream(ms, CompressionMode.Compress, true))
                {
                    ds.Write(data, 0, data.Length);
                }
                uint adler = CalculateAdler32(data);
                byte[] adlerBytes = BitConverter.GetBytes(adler);
                if (BitConverter.IsLittleEndian) Array.Reverse(adlerBytes);
                ms.Write(adlerBytes, 0, 4);
                return ms.ToArray();
            }
        }

        private uint CalculateAdler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte val in data)
            {
                a = (a + val) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }
    }
}