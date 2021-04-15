﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaxStudio.Common.Enums;

namespace DaxStudio.QueryTrace.Interfaces
{
    public enum QueryTraceStatus
    {
        Error,
        Stopped,
        Stopping,
        Started,
        Starting,
        Unknown
    }

    public interface IQueryTrace
    {
        Task StartAsync( int startTimeoutSec);
        void Stop();
        void Update();
        void Update(string databaseName, string sessionId);

        event EventHandler<DaxStudioTraceEventArgs> TraceEvent;
        event EventHandler TraceCompleted;
        event EventHandler TraceStarted;
        event EventHandler<string> TraceError;
        event EventHandler<string> TraceWarning;
        /*
        public void ClearEvents();
        void OnQueryEnd();
        void OnTraceError();
        void OnTracedStarted();
        */
        List<DaxStudioTraceEventClass> Events { get; }
        QueryTraceStatus Status {get;}
        void Dispose();
    }
}
