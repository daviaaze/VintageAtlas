using Vintagestory.API.Common;
using System.Text;
using System.Collections.Generic;
using System;

/// <summary>
/// Mock implementation of ILogger for testing
/// </summary>

namespace VintageAtlas.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of ILogger for testing
    /// </summary>
    public class MockLogger : ILogger
    {
        private readonly List<string> _debugMessages = new();
        private readonly List<string> _notifications = new();
        private readonly List<string> _warnings = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _fatalErrors = new();
        private readonly Dictionary<EnumLogType, List<string>> _logTypeToListMap;
        private bool _traceLog;

        public MockLogger()
        {
            _logTypeToListMap = new Dictionary<EnumLogType, List<string>>
            {
                { EnumLogType.Debug, _debugMessages },
                { EnumLogType.Notification, _notifications },
                { EnumLogType.Warning, _warnings },
                { EnumLogType.Error, _errors },
                { EnumLogType.Fatal, _fatalErrors }
            };
        }

        public IEnumerable<string> DebugMessages => _debugMessages;
        public IEnumerable<string> Notifications => _notifications;
        public IEnumerable<string> Warnings => _warnings;
        public IEnumerable<string> Errors => _errors;
        public IEnumerable<string> FatalErrors => _fatalErrors;

        public EnumLogType LogLevel { get; set; } = EnumLogType.Notification;
        public ILogger TraceLog => this;
#pragma warning disable CS0067 // Event is never used
        public event LogEntryDelegate? EntryAdded;
#pragma warning restore CS0067

        public void VerboseDebug(string message)
        {
            throw new NotImplementedException();
        }

        public void VerboseDebug(string message, params object[] args)
        {
            Debug(message, args);
        }
        
        public void Debug(string message, params object[] args)
        {
            Log(EnumLogType.Debug, message, args);
        }

        public void Debug(string message)
        {
            Debug(message, Array.Empty<object>());
        }

        public void Notification(string message, params object[] args)
        {
            Log(EnumLogType.Notification, message, args);
        }

        public void Notification(string message)
        {
            Notification(message, Array.Empty<object>());
        }

        public void Warning(string message, params object[] args)
        {
            Log(EnumLogType.Warning, message, args);
        }

        public void Warning(string message)
        {
            Warning(message, Array.Empty<object>());
        }

        public void Warning(Exception exception)
        {
            Warning(exception?.ToString() ?? "Exception");
        }

        public void Error(string message)
        {
            Log(EnumLogType.Error, message);
        }

        public void Error(string message, params object[] args)
        {
            Log(EnumLogType.Error, message, args);
        }

        public void Error(Exception exception)
        {
            Error(exception?.ToString() ?? "Exception");
        }

        public void Fatal(string message)
        {
            Log(EnumLogType.Fatal, message);
        }

        public void Fatal(string message, params object[] args)
        {
            Log(EnumLogType.Fatal, message, args);
        }

        public void Fatal(Exception exception)
        {
            Fatal(exception?.ToString() ?? "Exception");
        }

        public void ClearWatchers()
        {
            throw new NotImplementedException();
        }

        public void Log(EnumLogType logType, string message, params object[] args)
        {
            var formattedMessage = Format(message, args);
            if (_logTypeToListMap.TryGetValue(logType, out var logList))
            {
                logList.Add(formattedMessage);
            }
        }

        public void Log(EnumLogType logType, string message)
        {
            Log(logType, message, Array.Empty<object>());
        }

        public void LogException(EnumLogType logType, Exception e)
        {
            throw new NotImplementedException();
        }

        public void Build(string message)
        {
            throw new NotImplementedException();
        }

        public void Chat(string message)
        {
            throw new NotImplementedException();
        }

        public void Event(string message, params object[] args)
        {
            Notification(message, args);
        }

        public void Event(string message)
        {
            throw new NotImplementedException();
        }

        public void StoryEvent(string message, params object[] args)
        {
            Notification(message, args);
        }

        public void StoryEvent(string message)
        {
            throw new NotImplementedException();
        }

        public void Build(string message, params object[] args)
        {
            Notification(message, args);
        }

        public void Chat(string message, params object[] args)
        {
            Notification(message, args);
        }

        public void Audit(string message)
        {
            Notification(message);
        }

        bool ILogger.TraceLog
        {
            get => _traceLog;
            set => _traceLog = value;
        }

        public void Audit(string message, params object[] args)
        {
            Notification(message, args);
        }

        public void TrackStart(string key)
        {
            // No-op for testing
        }

        public double TrackStop(string key)
        {
            return 0;
        }

        public void ClearWatches()
        {
            // No-op for testing
        }

        public void Clear()
        {
            foreach (var logList in _logTypeToListMap.Values)
            {
                logList.Clear();
            }
        }

        public bool HasErrors() => _errors.Count > 0 || _fatalErrors.Count > 0;

        public bool HasWarnings() => _warnings.Count > 0;

        public string GetAllMessages()
        {
            var sb = new StringBuilder();
        
            AppendLogMessages(sb, _debugMessages, "[DEBUG]");
            AppendLogMessages(sb, _notifications, "[INFO]");
            AppendLogMessages(sb, _warnings, "[WARN]");
            AppendLogMessages(sb, _errors, "[ERROR]");
            AppendLogMessages(sb, _fatalErrors, "[FATAL]");
            
            return sb.ToString();
        }

        private static void AppendLogMessages(StringBuilder sb, IEnumerable<string> messages, string prefix)
        {
            foreach (var msg in messages)
            {
                sb.AppendLine($"{prefix} {msg}");
            }
        }

        private static string Format(string message, params object[] args)
        {
            try
            {
                return args.Length > 0 ? string.Format(message, args) : message;
            }
            catch
            {
                return message;
            }
        }
    }
}

