using MonoNotes.Core;
using MonoNotes.Core.Models;
using MonoNotes.Core.Serialization;
using System.Text.Json;

namespace MonoNotes.Storage
{
    public class JsonIndexService
    {
        private readonly string _indexPath;
        private List<NoteIndexItem> _memoryCache = new();
        private bool _isCacheLoaded = false;

        // 防抖保存机制相关
        private CancellationTokenSource? _debounceCts;
        private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(2);
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        public JsonIndexService()
        {
            var baseDir = PathHelper.GetWorkspacesDirectory();


            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            _indexPath = PathHelper.GetCacheDirectory();
        }

        // 🚀 毫秒级加载：软件启动时调用
        public async Task<List<NoteIndexItem>> LoadIndexAsync()
        {
            if (_isCacheLoaded) return _memoryCache;

            if (!File.Exists(_indexPath))
            {
                _isCacheLoaded = true;
                return _memoryCache;
            }

            await _fileLock.WaitAsync();
            try
            {
                using var stream = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

                // 🌟 使用源生成器 MonoNotesJsonContext.Default，速度极快且无反射！
                var items = await JsonSerializer.DeserializeAsync(
                    stream,
                    MonoNotesJsonContext.Default.ListNoteIndexItem);

                if (items != null)
                {
                    _memoryCache = items;
                }
                _isCacheLoaded = true;
            }
            catch
            {
                // 如果 JSON 损坏，返回空列表，后续可以通过扫描 Markdown 文件重建
                _memoryCache = new List<NoteIndexItem>();
            }
            finally
            {
                _fileLock.Release();
            }

            return _memoryCache;
        }

        // 🔄 更新索引：新增或修改笔记时调用
        public void UpsertItem(NoteIndexItem item)
        {
            var existing = _memoryCache.FirstOrDefault(x => x.Id == item.Id);
            if (existing != null)
            {
                _memoryCache.Remove(existing);
            }
            _memoryCache.Add(item);

            TriggerDebouncedSave();
        }

        // 🗑️ 删除索引
        public void RemoveItem(string id)
        {
            _memoryCache.RemoveAll(x => x.Id == id);
            TriggerDebouncedSave();
        }

        // ⏱️ 防抖保存逻辑：避免频繁写入全量 JSON 卡死 IO
        private void TriggerDebouncedSave()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            var token = _debounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_debounceDelay, token);
                    if (!token.IsCancellationRequested)
                    {
                        await SaveIndexToDiskAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                    // 被取消是正常的防抖行为，忽略
                }
            });
        }

        private async Task SaveIndexToDiskAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                using var stream = new FileStream(_indexPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

                // 🌟 同样使用源生成器写入，速度起飞
                await JsonSerializer.SerializeAsync(
                    stream,
                    _memoryCache,
                    MonoNotesJsonContext.Default.ListNoteIndexItem);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}