using System;
using System.Collections.Generic;
using System.Linq;
using Toolkit.Runtime.Extensions;
using UnityEditor;
using UnityEngine;

namespace SearchHelper.Editor.Core.Logger
{
    public class MessageInfo
    {
        public LogType LogType { get; set; }
        public string Message { get; set; }
    }

    public class Logger
    {
        private Stack<MessageInfo> Messages { get; set; }

        private MessageInfo NoMessage { get; set; } = new MessageInfo()
        {
            LogType = LogType.Log,
            Message = string.Empty,
        };

        private int Depth { get; set; }

        public Logger(int depth)
        {
            Messages = new Stack<MessageInfo>(depth);
            Depth = depth;
        }

        public void AddLog(LogType logType, string message)
        {
            if (Messages.Count >= Depth)
            {
                Messages.Pop();
            }

            Messages.Push(new MessageInfo() { LogType = logType, Message = message });
        }

        public MessageInfo Peek()
        {
            return Messages.TryPeek(out var message) ? message : NoMessage;
        }

        public List<string> History()
        {
            return Messages.IsNullOrEmpty() ? new List<string>() { "No history." } : Messages.Select(message => $"{message.LogType}: {message.Message}").ToList();
        }
    }
}
