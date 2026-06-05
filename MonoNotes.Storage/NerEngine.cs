using JiebaNet.Segmenter;
using JiebaNet.Segmenter.PosSeg;
using MonoNotes.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonoNotes.Storage
{
    public class ExtractedEntity
    {
        public string Name { get; set; } = "";
        public MaterialType Type { get; set; }
        public int Frequency { get; set; }
        public bool IsSelected { get; set; } = true;
    }

    public static class NerEngine
    {
        // 🌟 移除静态直接初始化，改为按需懒加载 (Lazy)
        private static PosSegmenter? _tagger;

        private static PosSegmenter Tagger
        {
            get
            {
                if (_tagger == null)
                {
                    // 🌟 核心修复：避开 MAUI 的 Resources 文件夹冲突！
                    // 强制让结巴分词去 "JiebaDict" 文件夹下找字典
                    string dictPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JiebaDict");
                    ConfigManager.ConfigFileBaseDir = dictPath;

                    _tagger = new PosSegmenter();
                }
                return _tagger;
            }
        }

        // 网文垂直领域正则规则库
        private static readonly Regex SkillRegex = new Regex(@"《([\u4e00-\u9fa5]{2,8})》|([\u4e00-\u9fa5]{2,5})(诀|法|神功|真经|指|掌|拳|剑法|大阵)", RegexOptions.Compiled);
        private static readonly Regex ArtifactRegex = new Regex(@"([\u4e00-\u9fa5]{2,5})(剑|刀|枪|棍|戟|鼎|印|钟|塔|符|盘|镜|尺|梭)", RegexOptions.Compiled);
        private static readonly Regex LevelRegex = new Regex(@"([\u4e00-\u9fa5]{2,4})(境|期|阶|层|转|段)", RegexOptions.Compiled);

        public static void SyncUserDictionary(List<MaterialItem> localMaterials)
        {
            // 确保触发 Tagger 初始化
            var ensureInit = Tagger;

            foreach (var m in localMaterials)
            {
                if (string.IsNullOrWhiteSpace(m.Name)) continue;

                string tag = m.Type switch
                {
                    MaterialType.Character => "nr",
                    MaterialType.Geography => "ns",
                    MaterialType.Faction => "nt",
                    _ => "nz"
                };

                WordDictionary.Instance.AddWord(m.Name, 99999, tag);
                if (m.Aliases != null)
                {
                    foreach (var alias in m.Aliases)
                    {
                        if (!string.IsNullOrWhiteSpace(alias))
                            WordDictionary.Instance.AddWord(alias, 99999, tag);
                    }
                }
            }
        }

        public static List<ExtractedEntity> ExtractEntities(string text, int minFreq = 3)
        {
            var results = new List<ExtractedEntity>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            var wordStats = new Dictionary<string, (MaterialType Type, int Count)>();

            // 阶段 1：Regex 垂直领域嗅探
            void ProcessRegex(Regex regex, MaterialType targetType, string rawText)
            {
                var matches = regex.Matches(rawText);
                foreach (Match match in matches)
                {
                    string word = match.Groups[1].Success && match.Groups[1].Value.Length > 0
                                  ? match.Groups[1].Value : match.Value;

                    if (word.Length < 2 || word.Length > 8) continue;

                    if (wordStats.ContainsKey(word))
                        wordStats[word] = (targetType, wordStats[word].Count + 1);
                    else
                        wordStats[word] = (targetType, 1);
                }
            }

            ProcessRegex(SkillRegex, MaterialType.Item, text);
            ProcessRegex(ArtifactRegex, MaterialType.Item, text);
            ProcessRegex(LevelRegex, MaterialType.Worldview, text);

            // 阶段 2：Jieba NLP 分词
            var tokens = Tagger.Cut(text);

            foreach (var pair in tokens)
            {
                if (pair.Word.Length < 2) continue;

                if (wordStats.ContainsKey(pair.Word))
                {
                    wordStats[pair.Word] = (wordStats[pair.Word].Type, wordStats[pair.Word].Count + 1);
                    continue;
                }

                if (pair.Flag == "nr" || pair.Flag == "ns" || pair.Flag == "nt")
                {
                    MaterialType type = pair.Flag switch
                    {
                        "nr" => MaterialType.Character,
                        "ns" => MaterialType.Geography,
                        "nt" => MaterialType.Faction,
                        _ => MaterialType.Item
                    };
                    wordStats[pair.Word] = (type, 1);
                }
            }

            // 阶段 3：清洗与组装
            foreach (var kvp in wordStats.Where(x => x.Value.Count >= minFreq).OrderByDescending(x => x.Value.Count))
            {
                results.Add(new ExtractedEntity
                {
                    Name = kvp.Key,
                    Type = kvp.Value.Type,
                    Frequency = kvp.Value.Count,
                    IsSelected = true
                });
            }

            return results;
        }

        public static async Task<string> ReadFullWorkTextAsync(WriteSpaceRepository repo, string workId)
        {
            var index = await repo.GetWorkIndexAsync(workId);
            if (index == null) return "";

            StringBuilder sb = new StringBuilder();
            foreach (var vol in index.Volumes)
            {
                foreach (var chap in vol.Chapters)
                {
                    string content = await repo.ReadChapterContentAsync(workId, chap.Id);
                    sb.AppendLine(content);
                }
            }
            return sb.ToString();
        }
    }
}