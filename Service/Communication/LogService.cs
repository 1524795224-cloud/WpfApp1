using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Service.Communication
{
    public static class LogService
    {
        private static readonly SemaphoreSlim _fileLock = new(1, 1);

        public static async Task WriteAsync(string message)
        {
            await _fileLock.WaitAsync();
            try
            {
                string dirPath = @"D:\Log";
                if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
                string fileName = $"{DateTime.Now:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(dirPath, fileName);
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                await File.AppendAllTextAsync(filePath, logContent, Encoding.UTF8);
            }
            finally { _fileLock.Release(); }
        }
    }
}
