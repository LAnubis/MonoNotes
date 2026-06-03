using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MonoNotes.Core.Utils
{
    public static class JsonSafeWriter
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 防止中文被转码为 \uXXXX
        };

        /// <summary>
        /// 执行绝对安全的原子化 JSON 写入
        /// </summary>
        public static async Task WriteAtomicallyAsync<T>(string targetFilePath, T data)
        {
            string tempPath = targetFilePath + ".tmp";
            string bakPath = targetFilePath + ".bak";

            // 1. 将数据序列化并写入临时文件
            string jsonString = JsonSerializer.Serialize(data, _options);

            // 确保目录存在
            var dir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(tempPath, jsonString);

            // 2. 校验 JSON 合法性 (防止内存溢出导致写入了半截文本)
            try
            {
                var verifyJson = await File.ReadAllTextAsync(tempPath);
                JsonDocument.Parse(verifyJson); // 如果解析失败会抛出异常
            }
            catch (Exception)
            {
                File.Delete(tempPath); // 写入的数据损坏，删掉临时文件并终止
                throw new InvalidOperationException("JSON 序列化或写入不完整，已终止替换保护原数据。");
            }

            // 3. 安全替换
            if (File.Exists(targetFilePath))
            {
                // 原子替换，并自动生成 .bak 备份
                File.Replace(tempPath, targetFilePath, bakPath, ignoreMetadataErrors: true);
            }
            else
            {
                // 第一次创建文件
                File.Move(tempPath, targetFilePath);
            }
        }

        /// <summary>
        /// 安全读取，自带简单的故障恢复
        /// </summary>
        public static async Task<T?> ReadSafeAsync<T>(string targetFilePath) where T : class
        {
            if (File.Exists(targetFilePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(targetFilePath);
                    return JsonSerializer.Deserialize<T>(json, _options);
                }
                catch (JsonException)
                {
                    // 如果主文件损坏，尝试读取备份文件
                    string bakPath = targetFilePath + ".bak";
                    if (File.Exists(bakPath))
                    {
                        string bakJson = await File.ReadAllTextAsync(bakPath);
                        return JsonSerializer.Deserialize<T>(bakJson, _options);
                    }
                }
            }
            return null;
        }
    }
}
