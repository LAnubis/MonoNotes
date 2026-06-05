using MonoNotes.Core.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonoNotes.Storage
{
    public static class ExportEngine
    {
        public static async Task<string> ExportAsync(WriteSpaceRepository repository, string workId, string format)
        {
            var works = await repository.GetAllWorksAsync();
            var meta = works.FirstOrDefault(w => w.Id == workId);
            var index = await repository.GetWorkIndexAsync(workId);

            if (meta == null || index == null) return string.Empty;

            // 🌟 1. 定位系统级的“下载(Downloads)”目录 (跨平台兼容 Windows/macOS)
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloadsPath = Path.Combine(userProfile, "Downloads");
            if (!Directory.Exists(downloadsPath))
            {
                Directory.CreateDirectory(downloadsPath); // 极小概率没有下载目录时的防抖
            }

            // 🌟 2. 组装绝对防重名的文件名：小说名字_时间_随机数
            string randomStr = new Random().Next(1000, 9999).ToString();
            string fileName = $"{meta.Title}_{DateTime.Now:yyyyMMdd_HHmmss}_{randomStr}.{format}";
            string fullPath = Path.Combine(downloadsPath, fileName);

            StringBuilder sb = new StringBuilder();

            // 🌟 3. 写入前言/简介
            if (format == "md")
            {
                sb.AppendLine($"# {meta.Title}");
                sb.AppendLine();
                sb.AppendLine($"> {meta.Synopsis?.Replace("\n", "\n> ")}");
                sb.AppendLine();
            }
            else // txt
            {
                sb.AppendLine($"《{meta.Title}》");
                sb.AppendLine();
                sb.AppendLine($"简介：{meta.Synopsis}");
                sb.AppendLine();
                sb.AppendLine("========================================");
                sb.AppendLine();
            }

            // 🌟 4. 遍历所有卷和章，进行核心拼接与洗稿
            foreach (var volume in index.Volumes)
            {
                if (format == "md")
                    sb.AppendLine($"## {volume.Title}");
                else
                    sb.AppendLine($"【{volume.Title}】");

                sb.AppendLine();

                foreach (var chapter in volume.Chapters)
                {
                    if (format == "md")
                        sb.AppendLine($"### {chapter.Title}");
                    else
                        sb.AppendLine(chapter.Title); // TXT格式下，章名不缩进，直接顶格居中或靠左

                    sb.AppendLine();

                    string rawContent = await repository.ReadChapterContentAsync(workId, chapter.Id);
                    if (string.IsNullOrWhiteSpace(rawContent)) continue;

                    var lines = rawContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        if (format == "md")
                        {
                            sb.AppendLine(trimmed);
                            sb.AppendLine();
                        }
                        else // txt
                        {
                            // 极简洗稿：去除 Markdown 的标题符和加粗符
                            trimmed = Regex.Replace(trimmed, @"^#+\s+", "");
                            trimmed = trimmed.Replace("**", "").Replace("*", "");

                            // 🌟 核心：网文排版精髓，强行加上两个全角空格（\u3000）
                            sb.AppendLine($"  {trimmed}");
                            sb.AppendLine();
                        }
                    }
                }
            }

            // 5. 写入文件并返回最终路径
            await File.WriteAllTextAsync(fullPath, sb.ToString(), Encoding.UTF8);
            return fullPath;
        }
    }
}