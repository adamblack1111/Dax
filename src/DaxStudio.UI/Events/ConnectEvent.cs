﻿

using ADOTabular.Enums;
using DaxStudio.UI.Interfaces;
using System.Collections.Generic;
using static Dax.Vpax.Tools.VpaxTools;

namespace DaxStudio.UI.Events
{
    public class ConnectEvent
    {


        public ConnectEvent(string connectionString, bool powerPivotModeSelected, string applicationName, string fileName, ServerType serverType, bool refreshDatabases) 
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
            ApplicationName = applicationName;
            FileName = fileName;
            ServerType = serverType;
            RefreshDatabases = refreshDatabases;
        }

        public ConnectEvent(string applicationName, VpaxContent vpaxContent)
        {
            ConnectionString = string.Empty;
            PowerPivotModeSelected = false;
            ApplicationName = applicationName;
            ServerType = ServerType.Offline;
            RefreshDatabases = false;
            VpaxContent = vpaxContent;
        }

        public string ConnectionString{get;  }
        public bool PowerPivotModeSelected { get;  }
        public string ApplicationName { get;  }
        public string FileName { get; set; }
        public ServerType ServerType { get; internal set; }

        public string DatabaseName { get; set; }
        public bool RefreshDatabases { get; set; }
        public VpaxContent VpaxContent { get;  }

        public List<ITraceWatcher> ActiveTraces { get; set; }
    }
}
