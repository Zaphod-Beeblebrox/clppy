using System;
using System.IO;

namespace Clppy.Core.Logging;

public interface IFileLogger
{
    void Log(string message);
    void LogError(string message, Exception? ex = null);
}

public class FileLogger : IFileLogger, IDisposable
{
    private readonly string _logPath;
    private readonly object _lock = new object();
    private bool _disposed;

    public FileLogger()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = Path.Combine(appData, "Clppy");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, "clppy.log");
    }

    public void Log(string message)
    {
        lock (_lock)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(_logPath, $"[{timestamp}] {message}\n");
            }
            catch (Exception ex)
            {
                // If we can't write to the log, we can't really do anything about it
                System.Diagnostics.Debug.WriteLine($"Failed to write to log: {ex.Message}");
            }
        }
    }

    public void LogError(string message, Exception? ex = null)
    {
        lock (_lock)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var errorText = ex != null 
                    ? $"[{timestamp}] ERROR: {message}\n{ex}\n" 
                    : $"[{timestamp}] ERROR: {message}\n";
                File.AppendAllText(_logPath, errorText);
            }
            catch (Exception writeEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write error to log: {writeEx.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Log("Application shutting down");
            _disposed = true;
        }
    }
}
