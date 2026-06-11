using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MonoNotes.Core;
using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;

namespace MonoNotes.Storage
{
    public class SyncLogService : ISyncLogService
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private const string LogFileName = ".sync-log.json";

        private string GetLogFilePath()
        {
            // 锁定全局顶层根目录 MonoNotes
            return Path.Combine(PathHelper.GetBaseDirectory(), LogFileName);
        }

        public async Task LogChangeAsync(string relativePath, SyncActionType action)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;

            await _lock.WaitAsync();
            try
            {
                var logs = await ReadLogsInternalAsync();
                var existingLog = logs.FirstOrDefault(l => l.RelativePath == relativePath);

                if (existingLog != null)
                {
                    if (action == SyncActionType.Delete)
                    {
                        existingLog.Action = SyncActionType.Delete;
                        existingLog.Timestamp = DateTimeOffset.UtcNow;
                    }
                    else if (existingLog.Action != SyncActionType.Delete)
                    {
                        existingLog.Timestamp = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        existingLog.Action = SyncActionType.Upsert;
                        existingLog.Timestamp = DateTimeOffset.UtcNow;
                    }
                }
                else
                {
                    logs.Add(new SyncLogEntry
                    {
                        RelativePath = relativePath,
                        Action = action,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }

                await WriteLogsInternalAsync(logs);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<SyncLogEntry>> GetLogsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return await ReadLogsInternalAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ClearLogsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                await WriteLogsInternalAsync(new List<SyncLogEntry>());
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<List<SyncLogEntry>> ReadLogsInternalAsync()
        {
            string path = GetLogFilePath();
            if (!File.Exists(path)) return new List<SyncLogEntry>();

            try
            {
                var json = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(json)) return new List<SyncLogEntry>();
                return JsonSerializer.Deserialize<List<SyncLogEntry>>(json) ?? new List<SyncLogEntry>();
            }
            catch
            {
                return new List<SyncLogEntry>();
            }
        }

        private async Task WriteLogsInternalAsync(List<SyncLogEntry> logs)
        {
            string path = GetLogFilePath();
            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }
    }
}