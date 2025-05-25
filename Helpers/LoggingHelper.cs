using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace eComBox.Helpers
{
    public static class LoggingHelper
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly List<string> _logBuffer = new List<string>();
        private static readonly int _maxBufferSize = 1000; // 最多保留1000条日志
        private static bool _isInitialized = false;
        private static readonly string _logFileName = "app_debug_log.txt";
        private static StorageFile _logFile;

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                await _semaphore.WaitAsync();

                // 获取日志文件
                var folder = ApplicationData.Current.LocalFolder;
                _logFile = await folder.CreateFileAsync(_logFileName, CreationCollisionOption.OpenIfExists);

                // 读取现有日志（如果存在）
                if (_logFile != null)
                {
                    string existingLogs = await FileIO.ReadTextAsync(_logFile);
                    if (!string.IsNullOrEmpty(existingLogs))
                    {
                        string[] logEntries = existingLogs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        // 只保留最新的日志（避免文件过大）
                        int startIndex = Math.Max(0, logEntries.Length - _maxBufferSize);
                        for (int i = startIndex; i < logEntries.Length; i++)
                        {
                            _logBuffer.Add(logEntries[i]);
                        }
                    }
                }

                // 添加启动日志
                _logBuffer.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] 应用启动，日志系统初始化完成");

                // 设置调试输出重定向
                Trace.Listeners.Add(new LoggingTraceListener());
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化日志系统失败: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        public static async Task LogAsync(string message, string level = "INFO")
        {
            try
            {
                if (!_isInitialized)
                    await InitializeAsync();

                string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

                await _semaphore.WaitAsync();

                // 添加到内存缓冲区
                _logBuffer.Add(formattedMessage);

                // 如果缓冲区太大，移除最旧的日志
                if (_logBuffer.Count > _maxBufferSize)
                {
                    _logBuffer.RemoveAt(0);
                }

                // 异步写入文件
                await WriteToFileAsync(formattedMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"写入日志失败: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 导出所有日志
        /// </summary>
        public static async Task<string> ExportLogsAsync()
        {
            try
            {
                await _semaphore.WaitAsync();

                if (!_isInitialized)
                    await InitializeAsync();

                // 构建完整日志内容
                StringBuilder logContent = new StringBuilder();

                foreach (var logEntry in _logBuffer)
                {
                    logContent.AppendLine(logEntry);
                }

                return logContent.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"导出日志失败: {ex.Message}");
                return $"导出日志失败: {ex.Message}";
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 将日志写入文件
        /// </summary>
        private static async Task WriteToFileAsync(string message)
        {
            try
            {
                if (_logFile != null)
                {
                    await FileIO.AppendTextAsync(_logFile, message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"写入日志文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重定向调试输出的监听器
        /// </summary>
        private class LoggingTraceListener : TraceListener
        {
            public override void Write(string message)
            {
                // 异步记录日志
                _ = LogAsync(message, "DEBUG");
            }

            public override void WriteLine(string message)
            {
                // 异步记录日志
                _ = LogAsync(message, "DEBUG");
            }
        }
    }
}

