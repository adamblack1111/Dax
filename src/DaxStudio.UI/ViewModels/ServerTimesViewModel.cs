﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
//using DaxStudio.UI.Model;
using System.Linq;
using Microsoft.AnalysisServices;
using System.Text.RegularExpressions;
using System.IO;
using System;
using System.Xml.Serialization;
using Newtonsoft.Json;
using System.Windows;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.QueryTrace;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.ViewModels
{

    public class TraceStorageEngineEvent {
        public DaxStudioTraceEventClass Class;
        public DaxStudioTraceEventSubclass Subclass;
        [JsonIgnore]
        public DaxStudioTraceEventClassSubclass ClassSubclass {
            get {
                return new DaxStudioTraceEventClassSubclass { Class = this.Class, Subclass = this.Subclass };
            }
        }
        public string Query { get;  set; }
        public long Duration { get;  set; }
        public long CpuTime { get;  set; }
        public int RowNumber { get;  set; }
        public long? EstimatedRows { get; set; }
        public long? EstimatedKBytes { get; set; }

        public TraceStorageEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber)
        {
            RowNumber = rowNumber;
            Class = ev.EventClass;
            Subclass = ev.EventSubclass;
            // TODO: we should implement as optional the removal of aliases and lineage
            if (Class == DaxStudioTraceEventClass.DirectQueryEnd) {
                Query = ev.TextData;
            }
            else {
                Query = ev.TextData.RemoveDaxGuids().RemoveXmSqlSquareBrackets().RemoveAlias().RemoveLineage().FixEmptyArguments().RemoveRowNumberGuid();
            }
            // Skip Duration/Cpu Time for Cache Match
            if (ClassSubclass.Subclass != DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch)
            {
                Duration = ev.Duration;
                CpuTime = ev.CpuTime;
            }

            long rows, bytes;
            if (Query.ExtractEstimatedSize( out rows, out bytes )) {
                EstimatedRows = rows;
                EstimatedKBytes = 1 + bytes / 1024;
            }
        }
        public TraceStorageEngineEvent() { }
    }

    public static class TraceStorageEngineExtensions {
        const string searchGuid = @"([_-]\{?([0-9a-fA-F]){8}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){4}-([0-9a-fA-F]){12}\}?)";
        const string searchXmSqlSquareBracketsNoSpace = @"(?<!\.)\[([^\[^ ])*\]";
        const string searchXmSqlSquareBracketsWithSpace = @"(?<!\.)\[([^\[])*\]";
        const string searchXmSqlDotSeparator = @"\.\[";
        const string searchXmSqlParenthesis = @"\ *[\(\)]\ *";
        const string searchXmSqlAlias = @" AS \'[^\']*\'";
        const string searchXmSqlLineage = @" \( [0-9]+ \) ";
        const string searchXmSqlEmptyArguments = @" \(\s*\) ";
        const string searchXmSqlRowNumberGuid = @"\[RowNumber [0-9A-F ]*\]";

        const string searchXmSqlPatternSize = @"Estimated size .* : (?<rows>\d+), (?<bytes>\d+)";

        //const string searchDaxQueryPlanSquareBrackets = @"^\'\[([^\[^ ])*\]";
        //const string searchQuotedIdentifiers = @"\'([^ ])*\'";

        static Regex guidRemoval = new Regex(searchGuid, RegexOptions.Compiled);
        static Regex xmSqlSquareBracketsWithSpaceRemoval = new Regex(searchXmSqlSquareBracketsWithSpace, RegexOptions.Compiled);
        static Regex xmSqlSquareBracketsNoSpaceRemoval = new Regex(searchXmSqlSquareBracketsNoSpace, RegexOptions.Compiled);
        static Regex xmSqlDotSeparator = new Regex(searchXmSqlDotSeparator, RegexOptions.Compiled);
        static Regex xmSqlParenthesis = new Regex(searchXmSqlParenthesis, RegexOptions.Compiled);
        static Regex xmSqlAliasRemoval = new Regex(searchXmSqlAlias, RegexOptions.Compiled);
        static Regex xmSqlLineageRemoval = new Regex(searchXmSqlLineage, RegexOptions.Compiled);
        static Regex xmSqlEmptyArguments = new Regex(searchXmSqlEmptyArguments, RegexOptions.Compiled);
        static Regex xmSqlRowNumberGuidRemoval = new Regex(searchXmSqlRowNumberGuid, RegexOptions.Compiled);

        static Regex xmSqlPatternSize = new Regex(searchXmSqlPatternSize, RegexOptions.Compiled);

        public static string RemoveDaxGuids(this string daxQuery) {
            return guidRemoval.Replace(daxQuery, "");
        }
        private static string RemoveSquareBracketsWithSpace(Match match) {
            return match.Value.Replace("[", "'").Replace("]", "'");
        }
        private static string RemoveSquareBracketsNoSpace(Match match) {
            return match.Value.Replace("[", "").Replace("]", "");
        }
        private static string FixSpaceParenthesis(Match match) {
            string parenthesis = match.Value.Trim();
            return " " + parenthesis + " ";
        }
        public static string RemoveAlias(this string xmSqlQuery) {
            return xmSqlAliasRemoval.Replace(xmSqlQuery, "");
        }
        public static string RemoveLineage(this string xmSqlQuery) {
            return xmSqlLineageRemoval.Replace(xmSqlQuery, "");
        }
        public static string FixEmptyArguments(this string xmSqlQuery) {
            return xmSqlEmptyArguments.Replace(xmSqlQuery, " () ");
        }
        public static string RemoveRowNumberGuid(this string xmSqlQuery) {
            return xmSqlRowNumberGuidRemoval.Replace(xmSqlQuery, "");
        }

        public static string RemoveXmSqlSquareBrackets(this string daxQuery) {
            // TODO: probably it is not necessary to use RemoveSquareBrackets
            // look for a Regex replace expression looking at Regex doc (written on a plane offline)
            string daxQueryNoBracketsOnTableNames = xmSqlSquareBracketsNoSpaceRemoval.Replace(
                        daxQuery,
                        RemoveSquareBracketsNoSpace
                    );
            string daxQueryNoBrackets = xmSqlSquareBracketsWithSpaceRemoval.Replace(
                            daxQueryNoBracketsOnTableNames,
                            RemoveSquareBracketsWithSpace);
            string daxQueryNoDots = xmSqlDotSeparator.Replace(daxQueryNoBrackets, "[");
            string result = xmSqlParenthesis.Replace(daxQueryNoDots, FixSpaceParenthesis);
            return result;
        }

        public static bool ExtractEstimatedSize( this string daxQuery, out long rows, out long bytes ) {
            var m = xmSqlPatternSize.Match(daxQuery);
            string rowsString = m.Groups["rows"].Value;
            string bytesString = m.Groups["bytes"].Value;
            bool foundRows = long.TryParse(rowsString, out rows);
            bool foundBytes = long.TryParse(bytesString, out bytes);
            return foundRows && foundBytes;
        }
    }

    //[Export(typeof(ITraceWatcher)),PartCreationPolicy(CreationPolicy.NonShared)]
    class ServerTimesViewModel
        : TraceWatcherBaseViewModel, ISaveState
        
    {
        [ImportingConstructor]
        public ServerTimesViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions) : base(eventAggregator, globalOptions)
        {
            _storageEngineEvents = new BindableCollection<TraceStorageEngineEvent>();
            //ServerTimingDetails.PropertyChanged += ServerTimingDetails_PropertyChanged;
        }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            //return new List<TraceEventClass> 
            //    { TraceEventClass.QuerySubcube
            //    , TraceEventClass.VertiPaqSEQueryEnd
            //    , TraceEventClass.VertiPaqSEQueryCacheMatch
            //    , TraceEventClass.QueryEnd };

            return new List<DaxStudioTraceEventClass>
                { DaxStudioTraceEventClass.QuerySubcube
                , DaxStudioTraceEventClass.VertiPaqSEQueryEnd
                , DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch
                , DaxStudioTraceEventClass.DirectQueryEnd
                , DaxStudioTraceEventClass.QueryEnd};
        }

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults() {
            FormulaEngineDuration = 0;
            StorageEngineDuration = 0;
            TotalCpuDuration = 0;
            StorageEngineCpu = 0;
            StorageEngineQueryCount = 0;
            VertipaqCacheMatches = 0;
            TotalDuration = 0;
            _storageEngineEvents.Clear();

            if (Events != null) {
                foreach (var traceEvent in Events) {
                    if (traceEvent.EventClass == DaxStudioTraceEventClass.VertiPaqSEQueryEnd) {
                        if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.VertiPaqScan) {
                            StorageEngineDuration += traceEvent.Duration;
                            StorageEngineCpu += traceEvent.CpuTime;
                            StorageEngineQueryCount++;
                        }
                        _storageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, _storageEngineEvents.Count() + 1));
                    }

                    if (traceEvent.EventClass == DaxStudioTraceEventClass.DirectQueryEnd) {
                        StorageEngineDuration += traceEvent.Duration;
                        StorageEngineCpu += traceEvent.CpuTime;
                        StorageEngineQueryCount++;
                        _storageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, _storageEngineEvents.Count() + 1));
                    }

                    if (traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd) {
                        TotalDuration = traceEvent.Duration;
                        TotalCpuDuration = traceEvent.CpuTime;
                        //FormulaEngineDuration = traceEvent.CpuTime;

                    }
                    if (traceEvent.EventClass == DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch) {
                        VertipaqCacheMatches++;
                        _storageEngineEvents.Add(new TraceStorageEngineEvent(traceEvent, _storageEngineEvents.Count() + 1));
                    }
                }

                FormulaEngineDuration = TotalDuration - StorageEngineDuration;
                if (QueryHistoryEvent != null) {
                    QueryHistoryEvent.FEDurationMs = FormulaEngineDuration;
                    QueryHistoryEvent.SEDurationMs = StorageEngineDuration;
                    QueryHistoryEvent.ServerDurationMs = TotalDuration;

                    _eventAggregator.PublishOnUIThread(QueryHistoryEvent);
                }
                Events.Clear();

                NotifyOfPropertyChange(() => StorageEngineEvents);
            }
        }

        private long _totalCpuDuration = 0;
        public long TotalCpuDuration { 
            get { return _totalCpuDuration; } 
            set { _totalCpuDuration = value;
            NotifyOfPropertyChange(() => TotalCpuDuration);
            NotifyOfPropertyChange(() => TotalCpuFactor);
            } 
        }

        public double TotalCpuFactor
        {
            get { return (double)_totalCpuDuration / (double)_totalDuration; }
        }

        public double StorageEngineCpuFactor
        {
            get { return _storageEngineDuration==0 ? 0 : (double)_storageEngineCpu / (double)_storageEngineDuration; }
        }
        public double StorageEngineDurationPercentage {
            get
            {
                return TotalDuration == 0? 0: (double)StorageEngineDuration / (double)TotalDuration;
            }
        }
        public double FormulaEngineDurationPercentage {
            // marcorusso: we use the formula engine total provided by Query End event in CPU Time
            // get { return TotalDuration == 0 ? 0:(TotalDuration-StorageEngineDuration)/TotalDuration;}
            get {
                return TotalDuration == 0 ? 0 : (double)FormulaEngineDuration / (double)TotalDuration;
            }
        }
        public double VertipaqCacheMatchesPercentage {
            get {
                return StorageEngineQueryCount == 0 ? 0 : (double)VertipaqCacheMatches / (double)StorageEngineQueryCount;
            }
        }
        private long _totalDuration = 0;
        public long TotalDuration { get { return _totalDuration; }
            private set { _totalDuration = value;
            NotifyOfPropertyChange(() => TotalDuration);
            NotifyOfPropertyChange(() => StorageEngineDurationPercentage);
            NotifyOfPropertyChange(() => FormulaEngineDurationPercentage);
            NotifyOfPropertyChange(() => VertipaqCacheMatchesPercentage);
            NotifyOfPropertyChange(() => TotalCpuFactor);
            }
        }
        private long _formulaEngineDuration = 0;
        public long FormulaEngineDuration { get { return _formulaEngineDuration; }
            private set { _formulaEngineDuration = value;
                NotifyOfPropertyChange(() => FormulaEngineDuration);
                NotifyOfPropertyChange(() => FormulaEngineDurationPercentage);
            }
        }
        private long _storageEngineDuration = 0;
        public long StorageEngineDuration { get { return _storageEngineDuration; }
            private set {
                _storageEngineDuration = value;
                NotifyOfPropertyChange(() => StorageEngineDuration);
                NotifyOfPropertyChange(() => StorageEngineDurationPercentage);
                NotifyOfPropertyChange(() => StorageEngineCpuFactor);
            }
        }
        private long _storageEngineCpu = 0;
        public long StorageEngineCpu { get { return _storageEngineCpu; }
            private set {
                _storageEngineCpu = value;
                NotifyOfPropertyChange(() => StorageEngineCpu);
                NotifyOfPropertyChange(() => StorageEngineCpuFactor);
            }
        }
        private long _storageEngineQueryCount = 0;
        public long StorageEngineQueryCount { get { return _storageEngineQueryCount; }
            private set {
                _storageEngineQueryCount = value;
                NotifyOfPropertyChange(() => StorageEngineQueryCount);
            }
        }

        private int _vertipaqCacheMatches = 0;
        public int VertipaqCacheMatches { get { return _vertipaqCacheMatches; } 
            set {
                _vertipaqCacheMatches = value;
                NotifyOfPropertyChange(() => VertipaqCacheMatches);
                NotifyOfPropertyChange(() => VertipaqCacheMatchesPercentage);
            }
        }
 
        private readonly BindableCollection<TraceStorageEngineEvent> _storageEngineEvents;

        public IObservableCollection<TraceStorageEngineEvent> StorageEngineEvents 
        {
            get {
                var fse = from e in _storageEngineEvents
                          where
                          (e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.VertiPaqScanInternal && ServerTimingDetails.ShowInternal)
                          ||
                          (e.ClassSubclass.Subclass == DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch && ServerTimingDetails.ShowCache)
                          ||
                          ((e.ClassSubclass.Subclass != DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch && e.ClassSubclass.Subclass != DaxStudioTraceEventSubclass.VertiPaqScanInternal) && ServerTimingDetails.ShowScan)
                          select e;
                return new BindableCollection<TraceStorageEngineEvent>(fse);
            }
        }

        private TraceStorageEngineEvent _selectedEvent;
        public TraceStorageEngineEvent SelectedEvent {
            get {
                return _selectedEvent;
            }
            set {
                _selectedEvent = value;
                NotifyOfPropertyChange(() => SelectedEvent);
            }
        }

        /*
        // Filter for visualization 
        private bool _cacheVisible;
        private bool _internalVisible;
        private bool _scanVisible = true;
        public bool CacheVisible {
            get { return _cacheVisible; }
            set { _cacheVisible = value; NotifyOfPropertyChange(() => StorageEngineEvents); }
        }
        public bool InternalVisible {
            get { return _internalVisible; }
            set { _internalVisible = value; NotifyOfPropertyChange(() => StorageEngineEvents); }
        }
        public bool ScanVisible 
        {
            get { return _scanVisible; }
            set { _scanVisible = value; NotifyOfPropertyChange(() => StorageEngineEvents); }
        }
    */

        // IToolWindow interface
        public override string Title
        {
            get { return "Server Timings"; }
            set { }
        }

        public override string ToolTipText
        {
            get
            {
                return "Runs a server trace to record detailed timing information for performance profiling";
            }
            set { }
        }

        public override void OnReset() {
            IsBusy = false;
            Events.Clear();
            ProcessResults();
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            var m = new ServerTimesModel()
            {
                FormulaEngineDuration = this.FormulaEngineDuration,
                StorageEngineDuration = this.StorageEngineDuration,
                StorageEngineCpu = this.StorageEngineCpu,
                TotalDuration = this.TotalDuration,
                VertipaqCacheMatches = this.VertipaqCacheMatches,
                StorageEngineQueryCount = this.StorageEngineQueryCount,                
                StoreageEngineEvents =  this._storageEngineEvents,
                TotalCpuDuration = this.TotalCpuDuration
            };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(m, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filename + ".serverTimings" , json);

        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".serverTimings";
            if (!File.Exists(filename)) return;

            this.IsChecked = true;
            string data = File.ReadAllText(filename);
            ServerTimesModel m = JsonConvert.DeserializeObject<ServerTimesModel>(data);

            FormulaEngineDuration = m.FormulaEngineDuration;
            StorageEngineDuration = m.StorageEngineDuration;
            StorageEngineCpu = m.StorageEngineCpu;
            TotalDuration = m.TotalDuration;
            VertipaqCacheMatches = m.VertipaqCacheMatches;
            StorageEngineQueryCount = m.StorageEngineQueryCount;
            TotalCpuDuration = m.TotalCpuDuration;

            this._storageEngineEvents.Clear();
            this._storageEngineEvents.AddRange(m.StoreageEngineEvents);
            NotifyOfPropertyChange(() => StorageEngineEvents);
            
        }

        #endregion

        #region Properties to handle layout changes

        public int TextGridRow { get { return ServerTimingDetails.LayoutBottom?2:0; } }
        public int TextGridRowSpan { get { return ServerTimingDetails.LayoutBottom? 1:3; } }
        public int TextGridColumn { get { return ServerTimingDetails.LayoutBottom?2:4; } }

        public GridLength TextColumnWidth { get { return ServerTimingDetails.LayoutBottom ? new GridLength(0) : new GridLength(1, GridUnitType.Star); }  }

        private ServerTimingDetailsViewModel _serverTimingDetails;
        public ServerTimingDetailsViewModel ServerTimingDetails { get { return _serverTimingDetails; } set {
            if (_serverTimingDetails != null) { _serverTimingDetails.PropertyChanged -= ServerTimingDetails_PropertyChanged; }
                _serverTimingDetails = value;
                _serverTimingDetails.PropertyChanged += ServerTimingDetails_PropertyChanged;
                NotifyOfPropertyChange(() => ServerTimingDetails);
            } 
        }
        private void ServerTimingDetails_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "LayoutBottom":
                case "LayoutRight":
                    NotifyOfPropertyChange(() => TextGridColumn);
                    NotifyOfPropertyChange(() => TextGridRow);
                    NotifyOfPropertyChange(() => TextGridRowSpan);
                    NotifyOfPropertyChange(() => TextColumnWidth);
                    break;
                default:
                    NotifyOfPropertyChange(() => StorageEngineEvents);
                    break;
            }
        }

        #endregion
    }
}
