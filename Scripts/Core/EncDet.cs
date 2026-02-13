// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//编码检测（残缺，主要为中文检测）
using System;
using System.IO;
using System.Text;
using Godot;

using FileAccess = System.IO.FileAccess;

namespace SkyrimModTranslator.Core
{
    public static class EncDet
    {
        private static readonly Encoding[] SupportedEncodings = new Encoding[]
        {
            Encoding.UTF8,
            Encoding.GetEncoding("GB2312"),
            Encoding.GetEncoding("GBK"),
            Encoding.GetEncoding("GB18030"),
            Encoding.Unicode,
            Encoding.BigEndianUnicode,
            Encoding.UTF32,
            Encoding.GetEncoding("windows-1252"),
            Encoding.GetEncoding("ISO-8859-1"),
            Encoding.ASCII
        };

        public static string Read(string filePath, out Encoding encoding)
        {
            if (!File.Exists(filePath))
            {
                encoding = Encoding.Default;
                return string.Empty;
            }

            var bomEncoding = DetectBOM(filePath);
            if (bomEncoding != null)
            {
                encoding = bomEncoding;
                return File.ReadAllText(filePath, bomEncoding);
            }

            byte[] fileBytes = File.ReadAllBytes(filePath);
            encoding = DetectByStat(fileBytes);

            try
            {
                return encoding.GetString(fileBytes);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EncDet] 解码失败: {e.Message}");
                encoding = Encoding.UTF8;
                return Encoding.UTF8.GetString(fileBytes);
            }
        }

        private static Encoding DetectBOM(string filePath)
        {
            byte[] bom = new byte[4];
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                fs.Read(bom, 0, 4);
            }

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;

            if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;

            if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
                return Encoding.UTF32;

            if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
                return Encoding.GetEncoding("utf-32BE");

            return null;
        }

        private static Encoding DetectByStat(byte[] data)
        {
            if (data == null || data.Length == 0)
                return Encoding.Default;

            if (IsValidUTF8(data))
                return Encoding.UTF8;

            Encoding gbEncoding = DetectGB(data);
            if (gbEncoding != null)
                return gbEncoding;

            if (HasChineseChars(data))
            {
                foreach (var encoding in new[] { "GB18030", "GBK", "GB2312" })
                {
                    try
                    {
                        var testEncoding = Encoding.GetEncoding(encoding);
                        string testString = testEncoding.GetString(data);
                        if (IsValidDecoding(testString, data))
                            return testEncoding;
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"[EncDet] 测试GB编码失败: {e.Message}");
                    }
                }
            }

            foreach (var encoding in SupportedEncodings)
            {
                try
                {
                    string testString = encoding.GetString(data);
                    if (IsValidDecoding(testString, data))
                        return encoding;
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[EncDet] 测试编码失败: {e.Message}");
                }
            }

            return Encoding.UTF8;
        }

        private static bool IsValidUTF8(byte[] data)
        {
            int length = data.Length;
            for (int i = 0; i < length; i++)
            {
                byte current = data[i];
                if (current <= 0x7F) continue;
                if (current >= 0xC2 && current <= 0xF4)
                {
                    int expectedLength = 0;
                    if (current <= 0xDF) expectedLength = 2;
                    else if (current <= 0xEF) expectedLength = 3;
                    else if (current <= 0xF4) expectedLength = 4;
                    else return false;
                    if (i + expectedLength > length) return false;
                    for (int j = 1; j < expectedLength; j++)
                    {
                        if ((data[i + j] & 0xC0) != 0x80)
                            return false;
                    }
                    i += expectedLength - 1;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static Encoding DetectGB(byte[] data)
        {
            int gbCount = 0;
            int totalMultiByte = 0;
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b >= 0xA1 && b <= 0xFE)
                {
                    if (i + 1 < data.Length)
                    {
                        byte next = data[i + 1];
                        if ((b >= 0xA1 && b <= 0xA9) && (next >= 0xA1 && next <= 0xFE))
                            gbCount++;
                        else if ((b >= 0xB0 && b <= 0xF7) && (next >= 0xA1 && next <= 0xFE))
                            gbCount++;
                        else if ((b >= 0x81 && b <= 0xA0) && (next >= 0x40 && next <= 0xFE))
                            gbCount++;
                        else if ((b >= 0xAA && b <= 0xFE) && (next >= 0x40 && next <= 0xA0))
                            gbCount++;
                        totalMultiByte++;
                        i++;
                    }
                }
            }
            if (totalMultiByte > 0 && (float)gbCount / totalMultiByte > 0.7f)
            {
                try { return Encoding.GetEncoding("GB18030"); }
                catch (Exception e) { GD.PrintErr($"[EncDet] 测试GB18030编码失败: {e.Message}"); }
                try { return Encoding.GetEncoding("GBK"); }
                catch (Exception e) { GD.PrintErr($"[EncDet] 测试GBK编码失败: {e.Message}"); }
                try { return Encoding.GetEncoding("GB2312"); }
                catch (Exception e) { GD.PrintErr($"[EncDet] 测试GB2312编码失败: {e.Message}"); }
            }
            return null;
        }

        private static bool HasChineseChars(byte[] data)
        {
            for (int i = 0; i < data.Length - 1; i++)
            {
                byte b1 = data[i];
                byte b2 = data[i + 1];
                if (b1 >= 0xB0 && b1 <= 0xF7 && b2 >= 0xA1 && b2 <= 0xFE)
                    return true;
                if (b1 == 0xA1 && b2 >= 0xA2 && b2 <= 0xAF)
                    return true;
                if (b1 == 0xA3 && b2 >= 0xB0 && b2 <= 0xFE)
                    return true;
            }
            return false;
        }

        private static bool IsValidDecoding(string text, byte[] originalData)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            int invalidCharCount = 0;
            foreach (char c in text)
            {
                if (char.IsControl(c) && c != '\t' && c != '\r' && c != '\n')
                    invalidCharCount++;
                if (c == '\uFFFD' || c == '\u0000')
                    invalidCharCount++;
            }
            if ((float)invalidCharCount / text.Length > 0.1f)
                return false;
            try
            {
                byte[] reencoded = Encoding.UTF8.GetBytes(text);
                if (Math.Abs(reencoded.Length - originalData.Length) > originalData.Length * 0.1f)
                    return false;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[EncDet] 重编码失败: {e.Message}");
            }
            return true;
        }
        
        //检查文本是否包含乱码字符
        public static bool HasGarbled(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            int invalidCharCount = 0;
            foreach (char c in text)
            {
                if (c == '\uFFFD')
                    invalidCharCount++;
                else if (char.IsControl(c) && c != '\t' && c != '\r' && c != '\n')
                    invalidCharCount++;
            }
            return (float)invalidCharCount / text.Length > 0.1f;
        }
    }
}
