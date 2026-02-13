//数据数据结构
using System;
using System.Collections.Generic;

namespace SkyrimModTranslator.Core
{
    public static class Data
    {
        public class Mod
        {
            public string Path;
            public byte[] Raw;
            public List<Item> Items;
            public bool IsApplied = false;
            public HashSet<uint> NpcIds = new HashSet<uint>();
            public Dictionary<uint, string> NpcNames = new Dictionary<uint, string>();
            public Dictionary<uint, List<uint>> NpcMap = new Dictionary<uint, List<uint>>();
            
            public Mod(string path)
            {
                Path = path;
                Items = new List<Item>();
            }
            
            public int TransCount
            {
                get
                {
                    int count = 0;
                    if (Items != null)
                    {
                        for (int i = 0; i < Items.Count; i++)
                        {
                            Item item = Items[i];
                            if (!string.IsNullOrEmpty(item.Trans))
                            {
                                count++;
                            }
                        }
                    }
                    return count;
                }
            }
            
            public int TotalCount
            {
                get
                {
                    return Items?.Count ?? 0;
                }
            }
            
            public double Progress
            {
                get
                {
                    int total = TotalCount;
                    return total > 0 ? (double)TransCount / total * 100 : 0;
                }
            }
        }
        
        public class Item
        {
            public int ID;
            public Mod Mod;
            public string Type;
            public uint FormID;
            public string FType;
            public int FIdx;
            public string Ori;
            public string Trans;
            public string Cty;
            public byte[] Raw;
            public string Speaker;
            public bool IsDictApplied;
            public bool SkipDistSync;
            public int ExportId;

            public Item()
            {
                Ori = "";
                Trans = "";
                IsDictApplied = false;
                SkipDistSync = false;
            }

            public string UniqueKey
            {
                get
                {
                    string type = Type ?? "";
                    string fType = FType ?? "";
                    return type + "_" + FormID.ToString("X8") + "_" + fType + "_" + FIdx.ToString();
                }
            }

            public bool IsTranslated
            {
                get
                {
                    return !string.IsNullOrEmpty(Trans) && !string.IsNullOrEmpty(Ori) && Trans != Ori;
                }
            }
        }
        
        public class TransEntry : Item
        {
        }
        
        public class CatConfig
        {
            public Dictionary<string, List<string>> Cats;
            
            public CatConfig()
            {
                Cats = new Dictionary<string, List<string>>();
            }
        }
    }
}
