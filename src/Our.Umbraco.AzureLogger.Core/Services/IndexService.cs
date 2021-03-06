﻿namespace Our.Umbraco.AzureLogger.Core.Services
{
    using Our.Umbraco.AzureLogger.Core.Models.TableEntities;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Singleton used to update and supply collections of distinct machine names / logger names that exist in a given log
    /// </summary>
    internal sealed partial class IndexService
    {
        private static readonly IndexService indexService = new IndexService();

        /// <summary>
        /// index data of machine names for each appender (key = appender name, value = list of machine names)
        /// </summary>
        private ConcurrentDictionary<string, List<string>> appenderMachineNames = new ConcurrentDictionary<string, List<string>>();

        /// <summary>
        /// index data of logger names for each appender (key = appender name, value = list of logger names)
        /// </summary>
        private ConcurrentDictionary<string, List<string>> appenderLoggerNames = new ConcurrentDictionary<string, List<string>>();

        static IndexService()
        {
        }

        /// <summary>
        /// Singleton constructor
        /// </summary>
        private IndexService()
        {
        }

        /// <summary>
        /// Get a reference to the singleton instance of the IndexService
        /// </summary>
        internal static IndexService Instance
        {
            get
            {
                return indexService;
            }
        }

        /// <summary>
        /// Update the indexes for a given appender
        /// </summary>
        /// <param name="appenderName">appender name</param>
        /// <param name="logTableEntities">a collection of log item POCOs from which to extract indexing data</param>
        internal void Process(string appenderName, IEnumerable<LogTableEntity> logTableEntities)
        {
            IEnumerable<string> machineNames = logTableEntities
                                                .Select(x => x.log4net_HostName)
                                                .Where(x => !this.GetMachineNames(appenderName).Any(y => y == x))
                                                .Distinct();

            if (machineNames.Any())
            {
                lock (this.appenderMachineNames[appenderName])
                {
                    // re-check
                    machineNames = machineNames.Where(x => !this.GetMachineNames(appenderName).Any(y => y == x));

                    if (machineNames.Any())
                    {
                        TableService.Instance.CreateIndexTableEntities(appenderName, "machineNames", machineNames.ToArray());

                        // update local collection
                        this.appenderMachineNames[appenderName].AddRange(machineNames);
                    }
                }
            }

            IEnumerable<string> loggerNames = logTableEntities
                                                .Select(x => x.LoggerName)
                                                .Where(x => !this.GetLoggerNames(appenderName).Any(y => y == x))
                                                .Distinct();

            if (loggerNames.Any())
            {
                lock (this.appenderLoggerNames[appenderName])
                {
                    // re-check
                    loggerNames = loggerNames.Where(x => !this.GetLoggerNames(appenderName).Any(y => y == x));

                    if (loggerNames.Any())
                    {
                        TableService.Instance.CreateIndexTableEntities(appenderName, "loggerNames", loggerNames.ToArray());

                        // update local collection
                        this.appenderLoggerNames[appenderName].AddRange(loggerNames);
                    }
                }
            }
        }

        /// <summary>
        /// Get a distinct collection of all machine names in this log
        /// </summary>
        /// <param name="appenderName"></param>
        /// <returns></returns>
        internal List<string> GetMachineNames(string appenderName)
        {
            if (!this.appenderMachineNames.ContainsKey(appenderName))
            {
                this.appenderMachineNames[appenderName] = TableService.Instance.ReadIndexTableEntities(appenderName, "machineNames")
                                                                        .Select(x => x.RowKey)
                                                                        .ToList();
            }

            return this.appenderMachineNames[appenderName];
        }

        /// <summary>
        /// Get a distinct collection of all logger names in this log
        /// </summary>
        /// <param name="appenderName"></param>
        /// <returns></returns>
        internal List<string> GetLoggerNames(string appenderName)
        {
            if (!this.appenderLoggerNames.ContainsKey(appenderName))
            {
                this.appenderLoggerNames[appenderName] = TableService.Instance.ReadIndexTableEntities(appenderName, "loggerNames")
                                                                        .Select(x => x.RowKey)
                                                                        .ToList();
            }

            return this.appenderLoggerNames[appenderName];
        }
    }
}
