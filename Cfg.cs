//配置管理与字段映射
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using SkyrimModTranslator.Common;

public static class Cfg
{
    public static readonly JsonSerializerOptions UnsafeRelaxedOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public static Dictionary<string, List<string>> LdTransMap()
    {
        string path = ProjectSettings.GlobalizePath("res://FieldMap.json");
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, UnsafeRelaxedOptions);
                if (map != null)
                {
                    return map;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Cfg] 加载字段映射表失败: {e.Message}");
            }
        }
        return GetDefTransMap();
    }

    public static Dictionary<string, List<string>> GetDefTransMap()
    {
        return _defTransMap;
    }

    public static void SvTransMap(Dictionary<string, List<string>> map)
    {
        string path = ProjectSettings.GlobalizePath("res://FieldMap.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string json = JsonSerializer.Serialize(map, UnsafeRelaxedOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[Cfg] 保存字段映射表失败: {e.Message}");
        }
    }

    public static bool AreDictionariesEqual(Dictionary<string, List<string>> dict1, Dictionary<string, List<string>> dict2)
    {
        if (dict1 == null && dict2 == null)
            return true;
        if (dict1 == null || dict2 == null)
            return false;
        if (dict1.Count != dict2.Count)
            return false;
        
        foreach (var kvp in dict1)
        {
            if (!dict2.ContainsKey(kvp.Key))
                return false;
            
            var list1 = kvp.Value;
            var list2 = dict2[kvp.Key];
            
            if (list1.Count != list2.Count)
                return false;
            
            foreach (var item in list1)
            {
                if (!list2.Contains(item))
                    return false;
            }
        }
        
        return true;
    }

    //默认翻译字段映射表
    private static readonly Dictionary<string, List<string>> _defTransMap = new()
    {
        {"GMST", new List<string>{"FULL"}},
        {"KYWD", new List<string>{"FULL","DESC"}},
        {"LCRT", new List<string>{"FULL","CNAM"}},
        {"AACT", new List<string>{"FULL"}},
        {"TXST", new List<string>{"FULL"}},
        {"GLOB", new List<string>{"FULL"}},
        {"CLAS", new List<string>{"FULL","DESC"}},
        {"FACT", new List<string>{"FULL","FNAM","MNAM"}},
        {"HDPT", new List<string>{"FULL","CNAM"}},
        {"EYES", new List<string>{"FULL"}},
        {"RACE", new List<string>{"FULL","DESC"}},
        {"SOUN", new List<string>{"FULL"}},
        {"ASPC", new List<string>{"FULL"}},
        {"MGEF", new List<string>{"FULL","DNAM","DESC"}},
        {"LTEX", new List<string>{"FULL"}},
        {"ENCH", new List<string>{"FULL","DESC"}},
        {"SPEL", new List<string>{"FULL","DESC"}},
        {"SCRL", new List<string>{"FULL","DESC"}},
        {"ACTI", new List<string>{"FULL"}},
        {"TACT", new List<string>{"FULL"}},
        {"ARMO", new List<string>{"FULL","DESC"}},
        {"BOOK", new List<string>{"FULL","DESC","CNAM"}},
        {"CONT", new List<string>{"FULL"}},
        {"DOOR", new List<string>{"FULL"}},
        {"INGR", new List<string>{"FULL","DESC"}},
        {"LIGH", new List<string>{"FULL","DESC"}},
        {"MISC", new List<string>{"FULL"}},
        {"STAT", new List<string>{"FULL"}},
        {"MSTT", new List<string>{"FULL"}},
        {"GRAS", new List<string>{"FULL"}},
        {"TREE", new List<string>{"FULL"}},
        {"FLOR", new List<string>{"FULL"}},
        {"FURN", new List<string>{"FULL"}},
        {"WEAP", new List<string>{"FULL","DESC"}},
        {"AMMO", new List<string>{"FULL","DESC"}},
        {"NPC_", new List<string>{"FULL","SHORT","SHRT"}},
        {"KEYM", new List<string>{"FULL"}},
        {"ALCH", new List<string>{"FULL","DESC"}},
        {"IDLM", new List<string>{"FULL"}},
        {"COBJ", new List<string>{"FULL"}},
        {"HAZD", new List<string>{"FULL"}},
        {"SLGM", new List<string>{"FULL","DESC"}},
        {"LVLI", new List<string>{"FULL"}},
        {"WTHR", new List<string>{"FULL"}},
        {"CLMT", new List<string>{"FULL"}},
        {"SPGD", new List<string>{"FULL"}},
        {"RFCT", new List<string>{"FULL"}},
        {"REGN", new List<string>{"FULL","RNAM"}},
        {"NAVI", new List<string>{"FULL"}},
        {"CELL", new List<string>{"FULL"}},
        {"WRLD", new List<string>{"FULL"}},
        {"DIAL", new List<string>{"FULL"}},
        {"QUST", new List<string>{"FULL","CNAM","NNAM"}},
        {"IDLE", new List<string>{"FULL"}},
        {"PACK", new List<string>{"FULL"}},
        {"CSTY", new List<string>{"FULL"}},
        {"LSCR", new List<string>{"FULL"}},
        {"LVSP", new List<string>{"FULL"}},
        {"ANIO", new List<string>{"FULL"}},
        {"WATR", new List<string>{"FULL"}},
        {"EFSH", new List<string>{"FULL"}},
        {"EXPL", new List<string>{"FULL"}},
        {"DEBR", new List<string>{"FULL"}},
        {"IMGS", new List<string>{"FULL"}},
        {"IMAD", new List<string>{"FULL"}},
        {"FLST", new List<string>{"FULL"}},
        {"PERK", new List<string>{"FULL","DESC"}},
        {"BPTD", new List<string>{"FULL"}},
        {"ADDN", new List<string>{"FULL"}},
        {"AVIF", new List<string>{"FULL","DESC"}},
        {"CAMS", new List<string>{"FULL"}},
        {"CPTH", new List<string>{"FULL"}},
        {"VTYP", new List<string>{"FULL"}},
        {"MATT", new List<string>{"FULL"}},
        {"IPCT", new List<string>{"FULL"}},
        {"IPDS", new List<string>{"FULL"}},
        {"ARMA", new List<string>{"FULL"}},
        {"ECZN", new List<string>{"FULL"}},
        {"LCTN", new List<string>{"FULL"}},
        {"MESG", new List<string>{"FULL","ITXT","DESC","INAM","NAM0","NAM1","NAM2","NAM3","NAM4","NAM5","NAM6","NAM7","NAM8","NAM9"}},
        {"DOBJ", new List<string>{"FULL"}},
        {"LGTM", new List<string>{"FULL"}},
        {"MUSC", new List<string>{"FULL"}},
        {"FSTP", new List<string>{"FULL"}},
        {"FSTS", new List<string>{"FULL"}},
        {"SMBN", new List<string>{"FULL"}},
        {"SMQN", new List<string>{"FULL"}},
        {"SMEN", new List<string>{"FULL"}},
        {"DLBR", new List<string>{"FULL"}},
        {"MUST", new List<string>{"FULL"}},
        {"DLVW", new List<string>{"FULL"}},
        {"WOOP", new List<string>{"FULL"}},
        {"SHOU", new List<string>{"FULL","DESC"}},
        {"EQUP", new List<string>{"FULL"}},
        {"SCEN", new List<string>{"FULL","RNAM"}},
        {"OTFT", new List<string>{"FULL"}},
        {"ARTO", new List<string>{"FULL"}},
        {"MATO", new List<string>{"FULL"}},
        {"MOVT", new List<string>{"FULL"}},
        {"SNDR", new List<string>{"FULL"}},
        {"DUAL", new List<string>{"FULL"}},
        {"SNCT", new List<string>{"FULL"}},
        {"SOPM", new List<string>{"FULL"}},
        {"COLL", new List<string>{"FULL"}},
        {"CLFM", new List<string>{"FULL"}},
        {"REVB", new List<string>{"FULL"}},
        {"INFO", new List<string>{"NAM1","NAM2","NAM3","NAM4","NAM5","NAM6","NAM7","NAM8","NAM9","RNAM"}},
        {"TES4", new List<string>{"CNAM","SNAM"}}
    };
}
