using MonoNotes.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonoNotes.Storage
{
    // 🌟 用于内存预览的临时数据结构
    public class ImportPreviewWork
    {
        public string BookName { get; set; } = "导入的作品";
        public int TotalWords { get; set; } = 0;
        public List<ImportPreviewVolume> Volumes { get; set; } = new();
    }

    public class ImportPreviewVolume
    {
        public string Title { get; set; } = "正文卷";
        public List<ImportPreviewChapter> Chapters { get; set; } = new();
    }

    public class ImportPreviewChapter
    {
        public string Title { get; set; } = "引言";
        public StringBuilder Content { get; set; } = new();
        public int WordCount => Content.Length;
    }

    public static class ImportEngine
    {
        // 🌟 核心正则：兼容 "第15章"、"第一百零三回"、"第十二节" 等网文常用格式
        private static readonly Regex ChapterRegex = new Regex(@"^\s*第\s*([零一二三四五六七八九十百千万0-9]+)\s*[章回节]\s*(.*)", RegexOptions.Compiled);
        private static readonly Regex VolumeRegex = new Regex(@"^\s*第\s*([零一二三四五六七八九十百千万0-9]+)\s*[卷部]\s*(.*)", RegexOptions.Compiled);

        // 兼容 Markdown 格式的二级/三级标题
        private static readonly Regex MdVolumeRegex = new Regex(@"^\s*##\s+(.*)", RegexOptions.Compiled);
        private static readonly Regex MdChapterRegex = new Regex(@"^\s*###\s+(.*)", RegexOptions.Compiled);

        /// <summary>
        /// 阶段 1：智能解析流，生成内存预览树 (不落盘)
        /// </summary>
        public static async Task<ImportPreviewWork> ParseStreamAsync(Stream fileStream, string fileName)
        {
            var work = new ImportPreviewWork { BookName = Path.GetFileNameWithoutExtension(fileName) };
            var currentVolume = new ImportPreviewVolume();
            var currentChapter = new ImportPreviewChapter();

            work.Volumes.Add(currentVolume);
            currentVolume.Chapters.Add(currentChapter);

            using var reader = new StreamReader(fileStream, Encoding.UTF8);
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                // 1. 判断是否是卷名 (Volume)
                var volMatch = VolumeRegex.Match(line);
                if (!volMatch.Success) volMatch = MdVolumeRegex.Match(line);

                if (volMatch.Success)
                {
                    currentVolume = new ImportPreviewVolume { Title = line.Trim() };
                    work.Volumes.Add(currentVolume);
                    currentChapter = new ImportPreviewChapter { Title = "卷首语" };
                    currentVolume.Chapters.Add(currentChapter);
                    continue;
                }

                // 2. 判断是否是章名 (Chapter)
                var chapMatch = ChapterRegex.Match(line);
                if (!chapMatch.Success) chapMatch = MdChapterRegex.Match(line);

                if (chapMatch.Success)
                {
                    currentChapter = new ImportPreviewChapter { Title = line.Trim() };
                    currentVolume.Chapters.Add(currentChapter);
                    continue;
                }

                // 3. 正文清洗与拼接
                if (!string.IsNullOrWhiteSpace(line))
                {
                    string cleanLine = line.Trim();
                    if (!string.IsNullOrWhiteSpace(cleanLine))
                    {
                        // 暴力扒掉所有加粗、斜体、下划线等多余 Markdown 标记
                        cleanLine = Regex.Replace(cleanLine, @"(\*\*|__|\*|_|#|`|>)", "");
                        cleanLine = cleanLine.Trim();

                        if (!string.IsNullOrWhiteSpace(cleanLine))
                        {
                            // 🌟 使用全角中文空格进行首行缩进排版洗稿
                            if (!cleanLine.StartsWith("  "))
                            {
                                cleanLine = "  " + cleanLine.TrimStart();
                            }

                            currentChapter.Content.AppendLine(cleanLine);
                            currentChapter.Content.AppendLine(); // 段落之间加一个空行，保证渲染不粘连
                            work.TotalWords += cleanLine.Length;
                        }
                    }
                }
            }

            // 清理空的结构
            foreach (var vol in work.Volumes)
            {
                vol.Chapters.RemoveAll(c => c.WordCount == 0 && (c.Title == "卷首语" || c.Title == "引言"));
            }
            work.Volumes.RemoveAll(v => v.Chapters.Count == 0);

            if (work.Volumes.Count == 0)
            {
                work.Volumes.Add(new ImportPreviewVolume { Chapters = new List<ImportPreviewChapter> { currentChapter } });
            }

            return work;
        }

        /// <summary>
        /// 阶段 2：用户确认后，将内存树正式转化为底层物理文件
        /// </summary>
        public static async Task<string> ExecuteImportAsync(WriteSpaceRepository repo, ImportPreviewWork previewWork)
        {
            var newWorkMeta = await repo.CreateNewWorkAsync(previewWork.BookName, "novel");
            var workIndex = new WorkIndex { WorkId = newWorkMeta.Id };
            var chaptersToSave = new Dictionary<string, string>(); // 用于批量写入

            foreach (var pVol in previewWork.Volumes)
            {
                var realVol = new Volume { Title = pVol.Title };
                workIndex.Volumes.Add(realVol);

                foreach (var pChap in pVol.Chapters)
                {
                    var realChap = new Chapter
                    {
                        Title = pChap.Title,
                        WordCount = pChap.WordCount
                    };
                    realVol.Chapters.Add(realChap);

                    // 暂存准备批量落盘
                    chaptersToSave.Add(realChap.Id, pChap.Content.ToString());
                }
            }

            // 🌟 调用仓储层批量并发写入，极大加速大文件导入
            await repo.BatchSaveChapterContentsAsync(newWorkMeta.Id, chaptersToSave);

            // 保存索引结构
            await repo.SaveWorkIndexAsync(workIndex);

            // 导入完毕后立刻建好全文索引引擎！
            await SearchEngine.BuildFullIndexAsync(repo, newWorkMeta.Id);

            return newWorkMeta.Id;
        }
    }
}