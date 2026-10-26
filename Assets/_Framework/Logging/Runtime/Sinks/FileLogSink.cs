using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RPGFramework.Logging
{
    /// <summary>
    /// Buffered text-file sink with size-based rotation and retention.
    /// Entries are written when the buffer fills, when an entry at or above <see cref="FlushSeverity"/>
    /// arrives, or on <see cref="Flush"/>/<see cref="Dispose"/>. Thread-safe.
    /// After an I/O failure the sink disables itself instead of throwing.
    /// </summary>
    public sealed class FileLogSink : ILogSink, IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<string> _pending = new List<string>();
        private readonly string _directory;
        private readonly string _filePrefix;
        private readonly int _bufferSize;
        private readonly long _maxFileBytes;
        private readonly int _maxFiles;
        private bool _disposed;
        private int _sequence;

        /// <param name="directory">Folder for log files (created if missing).</param>
        /// <param name="filePrefix">File name prefix; files are named prefix_yyyyMMdd_HHmmss_fff_NNNN.log.</param>
        /// <param name="bufferSize">Entries kept in memory before an automatic write.</param>
        /// <param name="maxFileBytes">Rotate to a new file once the current one exceeds this size.</param>
        /// <param name="maxFiles">Older files beyond this count are deleted.</param>
        /// <param name="header">Optional text written at the top of every new file.</param>
        public FileLogSink(
            string directory,
            string filePrefix = "log",
            int bufferSize = 64,
            long maxFileBytes = 10L * 1024 * 1024,
            int maxFiles = 5,
            string header = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory is required", nameof(directory));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (maxFileBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
            if (maxFiles <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFiles));

            _directory = directory;
            _filePrefix = filePrefix;
            _bufferSize = bufferSize;
            _maxFileBytes = maxFileBytes;
            _maxFiles = maxFiles;
            Header = header;
        }

        /// <summary>
        /// Entries at or above this severity are written immediately.
        /// </summary>
        public LogSeverity FlushSeverity { get; set; } = LogSeverity.Error;

        public string Header { get; }

        /// <summary>
        /// Path of the file currently written to, or null before the first write.
        /// </summary>
        public string CurrentFilePath { get; private set; }

        public bool IsDisabled { get; private set; }

        public void Write(in LogEntry entry)
        {
            lock (_gate)
            {
                if (_disposed || IsDisabled)
                    return;

                _pending.Add(entry.ToString());
                if (_pending.Count >= _bufferSize || entry.Severity >= FlushSeverity)
                    FlushLocked();
            }
        }

        public void Flush()
        {
            lock (_gate)
                FlushLocked();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                FlushLocked();
                _disposed = true;
            }
        }

        private void FlushLocked()
        {
            if (_pending.Count == 0 || IsDisabled)
                return;

            try
            {
                if (CurrentFilePath == null || new FileInfo(CurrentFilePath).Length > _maxFileBytes)
                    StartNewFile();

                var builder = new StringBuilder();
                foreach (var line in _pending)
                    builder.Append(line).Append(Environment.NewLine);
                File.AppendAllText(CurrentFilePath, builder.ToString());
                _pending.Clear();
            }
            catch (Exception)
            {
                IsDisabled = true;
                _pending.Clear();
            }
        }

        private void StartNewFile()
        {
            Directory.CreateDirectory(_directory);

            // Names sort chronologically: prefix_yyyyMMdd_HHmmss_fff_NNNN.log
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            string path;
            do
            {
                path = Path.Combine(_directory, $"{_filePrefix}_{stamp}_{_sequence++:D4}.log");
            }
            while (File.Exists(path));

            File.WriteAllText(path, Header == null ? string.Empty : Header + Environment.NewLine);
            CurrentFilePath = path;
            DeleteOldFiles();
        }

        private void DeleteOldFiles()
        {
            var files = new DirectoryInfo(_directory)
                .GetFiles($"{_filePrefix}_*.log")
                .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                .ToList();

            string current = Path.GetFullPath(CurrentFilePath);
            for (int i = _maxFiles; i < files.Count; i++)
            {
                if (!string.Equals(files[i].FullName, current, StringComparison.OrdinalIgnoreCase))
                    files[i].Delete();
            }
        }
    }
}
