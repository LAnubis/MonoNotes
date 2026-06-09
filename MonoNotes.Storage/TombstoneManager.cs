using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MonoNotes.Core;

namespace MonoNotes.Storage
{
    public static class TombstoneManager
    {
        private static readonly object _tombstoneLock = new();
        // 记录在根目录下的隐藏文件
        private static string TombstoneFile => Path.Combine(PathHelper.GetBaseDirectory(), ".tombstones.json");

        public static void Add(string relativePath)
        {
            lock (_tombstoneLock)
            {
                var list = GetAll();
                if (!list.Contains(relativePath))
                {
                    list.Add(relativePath);
                    Save(list);
                }
            }
        }

        public static void Remove(string relativePath)
        {
            lock (_tombstoneLock)
            {
                var list = GetAll();
                if (list.Remove(relativePath))
                {
                    Save(list);
                }
            }
        }

        public static List<string> GetAll()
        {
            try
            {
                if (File.Exists(TombstoneFile))
                {
                    var json = File.ReadAllText(TombstoneFile);
                    return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch { }
            return new List<string>();
        }

        private static void Save(List<string> list)
        {
            try
            {
                var json = JsonSerializer.Serialize(list);
                File.WriteAllText(TombstoneFile, json);
            }
            catch { }
        }
    }
}