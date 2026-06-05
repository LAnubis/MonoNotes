using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Highlight;
using Lucene.Net.Store;
using Lucene.Net.Util;

// 🌟 解决命名冲突
using OpenMode = Lucene.Net.Index.OpenMode;

namespace MonoNotes.Storage
{
    public class SearchResultItem
    {
        public string ChapterId { get; set; } = "";
        public string ChapterTitle { get; set; } = "";
        public string Snippet { get; set; } = "";
    }

    public static class SearchEngine
    {
        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

        private static FSDirectory GetIndexDirectory(string writeSpaceRoot, string workId)
        {
            var indexPath = Path.Combine(writeSpaceRoot, "Works", workId, "Index");
            if (!System.IO.Directory.Exists(indexPath))
            {
                System.IO.Directory.CreateDirectory(indexPath);
            }
            return FSDirectory.Open(indexPath);
        }

        // 🌟 新增：安全自检方法。如果在搜索前发现没建索引，就立刻建！
        public static async Task EnsureIndexExistsAsync(WriteSpaceRepository repo, string workId)
        {
            bool needsBuild = false;
            using (var dir = GetIndexDirectory(repo.RootPath, workId))
            {
                needsBuild = !DirectoryReader.IndexExists(dir);
            }

            if (needsBuild)
            {
                await BuildFullIndexAsync(repo, workId);
            }
        }

        public static async Task BuildFullIndexAsync(WriteSpaceRepository repo, string workId)
        {
            var index = await repo.GetWorkIndexAsync(workId);
            if (index == null) return;

            using var dir = GetIndexDirectory(repo.RootPath, workId);
            var analyzer = new SmartChineseAnalyzer(AppLuceneVersion);
            var config = new IndexWriterConfig(AppLuceneVersion, analyzer)
            {
                OpenMode = OpenMode.CREATE // 覆盖并强制重建
            };

            using var writer = new IndexWriter(dir, config);

            foreach (var vol in index.Volumes)
            {
                foreach (var chap in vol.Chapters)
                {
                    string content = await repo.ReadChapterContentAsync(workId, chap.Id);
                    AddOrUpdateDocument(writer, workId, chap.Id, chap.Title, content);
                }
            }
            writer.Commit();
        }

        public static void UpdateChapterIndex(string writeSpaceRoot, string workId, string chapterId, string title, string content)
        {
            try
            {
                using var dir = GetIndexDirectory(writeSpaceRoot, workId);
                var analyzer = new SmartChineseAnalyzer(AppLuceneVersion);
                var config = new IndexWriterConfig(AppLuceneVersion, analyzer)
                {
                    OpenMode = OpenMode.CREATE_OR_APPEND
                };

                using var writer = new IndexWriter(dir, config);
                AddOrUpdateDocument(writer, workId, chapterId, title, content);
                writer.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新索引失败: {ex.Message}");
            }
        }

        private static void AddOrUpdateDocument(IndexWriter writer, string workId, string chapterId, string title, string content)
        {
            var doc = new Document
            {
                new StringField("workId", workId, Field.Store.YES),
                new StringField("chapterId", chapterId, Field.Store.YES),
                new TextField("title", title ?? "", Field.Store.YES),
                new TextField("content", content ?? "", Field.Store.YES)
            };

            writer.UpdateDocument(new Term("chapterId", chapterId), doc);
        }

        public static List<SearchResultItem> Search(string writeSpaceRoot, string workId, string keyword, int maxResults = 50)
        {
            var results = new List<SearchResultItem>();
            if (string.IsNullOrWhiteSpace(keyword)) return results;

            using var dir = GetIndexDirectory(writeSpaceRoot, workId);
            if (!DirectoryReader.IndexExists(dir)) return results;

            using var reader = DirectoryReader.Open(dir);
            var searcher = new IndexSearcher(reader);
            var analyzer = new SmartChineseAnalyzer(AppLuceneVersion);

            var parser = new MultiFieldQueryParser(AppLuceneVersion, new[] { "title", "content" }, analyzer);
            var query = parser.Parse(QueryParserBase.Escape(keyword));

            var booleanQuery = new BooleanQuery
            {
                { query, Occur.MUST },
                { new TermQuery(new Term("workId", workId)), Occur.MUST }
            };

            var hits = searcher.Search(booleanQuery, maxResults).ScoreDocs;

            var formatter = new SimpleHTMLFormatter("<span class='highlight-text'>", "</span>");
            var scorer = new QueryScorer(query);
            var highlighter = new Highlighter(formatter, scorer)
            {
                TextFragmenter = new SimpleFragmenter(80)
            };

            foreach (var hit in hits)
            {
                var doc = searcher.Doc(hit.Doc);
                string chapterId = doc.Get("chapterId");
                string title = doc.Get("title");
                string content = doc.Get("content");

                var stream = analyzer.GetTokenStream("content", new StringReader(content));
                string snippet = highlighter.GetBestFragments(stream, content, 2, " ... ");

                if (string.IsNullOrWhiteSpace(snippet))
                {
                    snippet = content.Length > 80 ? content.Substring(0, 80) + "..." : content;
                }

                results.Add(new SearchResultItem
                {
                    ChapterId = chapterId,
                    ChapterTitle = title,
                    Snippet = snippet
                });
            }

            return results;
        }
    }
}