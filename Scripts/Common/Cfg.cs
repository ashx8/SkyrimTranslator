//配置管理和翻译字段映射
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SkyrimModTranslator.Common
{
    public static class Cfg
    {
    public static readonly JsonSerializerOptions UnsafeRelaxedOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public static Dictionary<string, List<string>> LoadTransMap()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FieldMap.json");
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
        var defaultMap = GetDefaultTransMap();
        SaveTransMap(defaultMap);
        return defaultMap;
    }

    public static Dictionary<string, List<string>> GetDefaultTransMap()
    {
        return _defaultTransMap;
    }

    public static void SaveTransMap(Dictionary<string, List<string>> map)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FieldMap.json");
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

    //默认的翻译字段映射表，定义哪些字段需要翻译
    private static readonly Dictionary<string, List<string>> _defaultTransMap = new()
    {
        //天际（Skyrim）的字段
        {"GMST", new List<string>{"FULL","DATA"}},
        {"SNCT", new List<string>{"FULL"}},
        {"TXST", new List<string>{"FULL"}},
        {"GLOB", new List<string>{"FULL"}},
        {"CLAS", new List<string>{"FULL","DESC"}},
        {"FACT", new List<string>{"FULL"}},
        {"HDPT", new List<string>{"FULL"}},
        {"EYES", new List<string>{"FULL"}},
        {"RACE", new List<string>{"FULL","DESC"}},
        {"SOUN", new List<string>{"FULL"}},
        {"ASPC", new List<string>{"FULL"}},
        {"MGEF", new List<string>{"FULL","DESC","DNAM"}},
        {"LTEX", new List<string>{"FULL"}},
        {"ENCH", new List<string>{"FULL","DESC"}},
        {"SPEL", new List<string>{"FULL","DESC"}},
        {"SCRL", new List<string>{"FULL","DESC"}},
        {"ACTI", new List<string>{"FULL","RNAM"}},
        {"TACT", new List<string>{"FULL"}},
        {"APPA", new List<string>{"FULL","DESC"}},
        {"ARMO", new List<string>{"FULL","DESC"}},
        {"BOOK", new List<string>{"FULL","DESC","CNAM"}},
        {"CONT", new List<string>{"FULL"}},
        {"DOOR", new List<string>{"FULL"}},
        {"INGR", new List<string>{"FULL","DESC"}},
        {"LIGH", new List<string>{"FULL"}},
        {"MISC", new List<string>{"FULL"}},
        {"STAT", new List<string>{"FULL"}},
        {"MSTT", new List<string>{"FULL"}},
        {"GRAS", new List<string>{"FULL"}},
        {"TREE", new List<string>{"FULL"}},
        {"FLOR", new List<string>{"FULL","RNAM"}},
        {"REFR", new List<string>{"FULL"}},
        {"FURN", new List<string>{"FULL"}},
        {"WEAP", new List<string>{"FULL","DESC"}},
        {"AMMO", new List<string>{"FULL","DESC"}},
        {"NPC_", new List<string>{"FULL"}},
        {"KEYM", new List<string>{"FULL"}},
        {"ALCH", new List<string>{"FULL","DESC"}},
        {"IDLM", new List<string>{"FULL"}},
        {"COBJ", new List<string>{"FULL"}},
        {"HAZD", new List<string>{"FULL"}},
        {"SLGM", new List<string>{"FULL","DESC"}},
        {"LVLI", new List<string>{"FULL"}},
        {"LVLN", new List<string>{"FULL"}},
        {"LVLC", new List<string>{"FULL"}},
        {"WTHR", new List<string>{"FULL"}},
        {"CLMT", new List<string>{"FULL"}},
        {"SPGD", new List<string>{"FULL"}},
        {"RFCT", new List<string>{"FULL"}},
        {"REGN", new List<string>{"FULL"}},
        {"NAVI", new List<string>{"FULL"}},
        {"CELL", new List<string>{"FULL"}},
        {"WRLD", new List<string>{"FULL"}},
        {"DIAL", new List<string>{"FULL"}},
        {"QUST", new List<string>{"FULL","NNAM","CNAM"}},
        {"IDLE", new List<string>{"FULL"}},
        {"PACK", new List<string>{"FULL"}},
        {"CSTY", new List<string>{"FULL"}},
        {"LSCR", new List<string>{"FULL","DESC"}},
        {"LVSP", new List<string>{"FULL"}},
        {"ANIO", new List<string>{"FULL"}},
        {"WATR", new List<string>{"FULL"}},
        {"EFSH", new List<string>{"FULL"}},
        {"EXPL", new List<string>{"FULL"}},
        {"DEBR", new List<string>{"FULL"}},
        {"IMGS", new List<string>{"FULL"}},
        {"IMAD", new List<string>{"FULL"}},
        {"FLST", new List<string>{"FULL"}},
        {"PERK", new List<string>{"FULL","DESC","EPFD"}},
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
        {"MESG", new List<string>{"FULL","ITXT","DTXT","DESC"}},
        {"WOOP", new List<string>{"FULL"}},
        {"LENS", new List<string>{"FULL"}},
        {"VOLI", new List<string>{"FULL"}},
        {"BNDS", new List<string>{"FULL"}},
        {"DMGT", new List<string>{"FULL","DESC"}},
        {"TRNS", new List<string>{"FULL"}},
        {"SCOL", new List<string>{"FULL"}},
        {"KYWD", new List<string>{"FULL"}},
        {"LCRT", new List<string>{"FULL"}},
        {"CHAL", new List<string>{"FULL","DESC"}},
        {"SHOU", new List<string>{"FULL","DESC"}},
        {"INFO", new List<string>{"NAM1","RNAM"}},
        {"TES4", new List<string>{"CNAM","SNAM"}},

        //辐射4和新维加斯的字段
        {"OMOD", new List<string>{"FULL","DESC"}},
        {"TERM", new List<string>{"FULL","DESC"}},
        {"NOTE", new List<string>{"FULL","DESC"}},
        {"IMOD", new List<string>{"FULL","DESC"}},
        {"SCEN", new List<string>{"FULL"}},
        {"LGTM", new List<string>{"FULL"}},
        {"REPU", new List<string>{"FULL"}},
        {"RADS", new List<string>{"FULL","DESC"}},
        {"DEHY", new List<string>{"FULL","DESC"}},
        {"HUNG", new List<string>{"FULL","DESC"}},
        {"SLPD", new List<string>{"FULL","DESC"}},
        {"CMNY", new List<string>{"FULL"}},
        {"CHIP", new List<string>{"FULL"}},
        {"CCRD", new List<string>{"FULL"}},
        {"CDCK", new List<string>{"FULL"}},
        {"CSNO", new List<string>{"FULL"}},
        {"HAIR", new List<string>{"FULL"}},
        {"RCPE", new List<string>{"FULL","DESC"}},
        {"RCCT", new List<string>{"FULL"}},
        {"CREA", new List<string>{"FULL"}},
        {"PGRE", new List<string>{"FULL"}},
        {"PMIS", new List<string>{"FULL"}},
        {"ALOC", new List<string>{"FULL"}},
        {"AMEF", new List<string>{"FULL","DESC"}},
        {"MSET", new List<string>{"FULL"}},
        {"LSCT", new List<string>{"FULL"}},
        {"MAPM", new List<string>{"FULL"}},
        {"CMPO", new List<string>{"FULL"}},
        {"MICN", new List<string>{"FULL"}},

        //星空（Starfield）的字段
        {"AACT", new List<string>{"FULL"}},
        {"AAMD", new List<string>{"FULL"}},
        {"AAPD", new List<string>{"FULL"}},
        {"AFFE", new List<string>{"FULL","DESC"}},
        {"AMBS", new List<string>{"FULL"}},
        {"AMDL", new List<string>{"FULL"}},
        {"AOPF", new List<string>{"FULL"}},
        {"AOPS", new List<string>{"FULL"}},
        {"AORU", new List<string>{"FULL"}},
        {"ARTO", new List<string>{"FULL"}},
        {"ATMO", new List<string>{"FULL"}},
        {"AVMD", new List<string>{"FULL"}},
        {"BIOM", new List<string>{"FULL"}},
        {"BMMO", new List<string>{"FULL"}},
        {"BMOD", new List<string>{"FULL"}},
        {"CLDF", new List<string>{"FULL"}},
        {"CLFM", new List<string>{"FULL"}},
        {"CNDF", new List<string>{"FULL"}},
        {"COLL", new List<string>{"FULL"}},
        {"CUR3", new List<string>{"FULL"}},
        {"CURV", new List<string>{"FULL"}},
        {"DFOB", new List<string>{"FULL"}},
        {"DLBR", new List<string>{"FULL"}},
        {"DOBJ", new List<string>{"FULL"}},
        {"EFSQ", new List<string>{"FULL"}},
        {"EQUP", new List<string>{"FULL"}},
        {"FOGV", new List<string>{"FULL"}},
        {"FORC", new List<string>{"FULL"}},
        {"GBFM", new List<string>{"FULL"}},
        {"GBFT", new List<string>{"FULL"}},
        {"GCVR", new List<string>{"FULL"}},
        {"INNR", new List<string>{"FULL"}},
        {"IRES", new List<string>{"FULL"}},
        {"KSSM", new List<string>{"FULL"}},
        {"LAYR", new List<string>{"FULL"}},
        {"LGDI", new List<string>{"FULL"}},
        {"LMSW", new List<string>{"FULL"}},
        {"LVSC", new List<string>{"FULL"}},
        {"MAAM", new List<string>{"FULL"}},
        {"MOVT", new List<string>{"FULL"}},
        {"MRPH", new List<string>{"FULL"}},
        {"MTPT", new List<string>{"FULL"}},
        {"MUSC", new List<string>{"FULL"}},
        {"MUST", new List<string>{"FULL"}},
        {"NOCM", new List<string>{"FULL"}},
        {"OSWP", new List<string>{"FULL"}},
        {"OTFT", new List<string>{"FULL"}},
        {"OVIS", new List<string>{"FULL"}},
        {"PCBN", new List<string>{"FULL"}},
        {"PCCN", new List<string>{"FULL"}},
        {"PCMT", new List<string>{"FULL"}},
        {"PDCL", new List<string>{"FULL"}},
        {"PKIN", new List<string>{"FULL"}},
        {"PMFT", new List<string>{"FULL"}},
        {"PNDT", new List<string>{"FULL"}},
        {"PSDC", new List<string>{"FULL"}},
        {"PTST", new List<string>{"FULL"}},
        {"REVB", new List<string>{"FULL"}},
        {"RFGP", new List<string>{"FULL"}},
        {"RSGD", new List<string>{"FULL"}},
        {"RSPJ", new List<string>{"FULL"}},
        {"SDLT", new List<string>{"FULL"}},
        {"SECH", new List<string>{"FULL"}},
        {"SFBK", new List<string>{"FULL"}},
        {"SFPC", new List<string>{"FULL"}},
        {"SFPT", new List<string>{"FULL"}},
        {"SFTR", new List<string>{"FULL"}},
        {"SPCH", new List<string>{"FULL"}},
        {"STAG", new List<string>{"FULL"}},
        {"STBH", new List<string>{"FULL"}},
        {"STDT", new List<string>{"FULL"}},
        {"STMP", new List<string>{"FULL"}},
        {"STND", new List<string>{"FULL"}},
        {"SUNP", new List<string>{"FULL"}},
        {"TMLM", new List<string>{"FULL","BTXT","ITXT","UNAM"}},
        {"TODD", new List<string>{"FULL"}},
        {"TRAV", new List<string>{"FULL"}},
        {"WBAR", new List<string>{"FULL"}},
        {"WKMF", new List<string>{"FULL"}},
        {"WTHS", new List<string>{"FULL"}},
        {"WWED", new List<string>{"FULL"}},
        {"ZOOM", new List<string>{"FULL"}},
        {"FFKW", new List<string>{"FULL"}},
        {"LVLP", new List<string>{"FULL"}},
        {"LVLB", new List<string>{"FULL"}},
        {"BOIM", new List<string>{"FULL"}},
        {"PROJ", new List<string>{"FULL"}}
    };
    }
}
