﻿namespace Our.Umbraco.AzureLogger.Core
{
    using Extensions;
    using Microsoft.WindowsAzure.Storage.Table;
    using Models;
    using Models.TableEntities;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed partial class TableService
    {
        /// <summary>
        /// https://azure.microsoft.com/en-gb/documentation/articles/storage-dotnet-how-to-use-tables/
        /// </summary>
        /// <param name="rowKey">(optional) start at the row key after this one</param>
        /// <param name="take">max number of items to return</param>
        /// <returns></returns>
        internal IEnumerable<LogTableEntity> ReadLogTableEntities(Level minLevel, string hostName, bool loggerNamesInclude, string[] loggerNames, string rowKey)
        {
            TableQuery<LogTableEntity> tableQuery = new TableQuery<LogTableEntity>();

            tableQuery.AndWhere(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.NotEqual, TableService.SearchItemPartitionKey));

            if (minLevel != Level.DEBUG)
            {
                switch (minLevel)
                {
                    case Level.INFO: // show all except debug
                        tableQuery.AndWhere(TableQuery.GenerateFilterCondition("Level", QueryComparisons.NotEqual, Level.DEBUG.ToString()));
                        break;

                    case Level.WARN: // show all except debug and info
                        tableQuery.AndWhere(TableQuery.CombineFilters(
                                                TableQuery.GenerateFilterCondition("Level", QueryComparisons.NotEqual, Level.DEBUG.ToString()),
                                                TableOperators.And,
                                                TableQuery.GenerateFilterCondition("Level", QueryComparisons.NotEqual, Level.INFO.ToString())));
                        break;

                    case Level.ERROR: // show if error or fatal
                        tableQuery.AndWhere(TableQuery.CombineFilters(
                                                TableQuery.GenerateFilterCondition("Level", QueryComparisons.Equal, Level.ERROR.ToString()),
                                                TableOperators.Or,
                                                TableQuery.GenerateFilterCondition("Level", QueryComparisons.Equal, Level.FATAL.ToString())));
                        break;

                    case Level.FATAL: // show fatal only
                        tableQuery.AndWhere(TableQuery.GenerateFilterCondition("Level", QueryComparisons.Equal, Level.FATAL.ToString()));

                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(hostName))
            {
                tableQuery.AndWhere(TableQuery.GenerateFilterCondition("log4net_HostName", QueryComparisons.Equal, hostName));
            }

            if (loggerNames.Any())
            {
                string queryComparison = loggerNamesInclude ? QueryComparisons.Equal : QueryComparisons.NotEqual;
                string tableOperator = loggerNamesInclude ? TableOperators.Or : TableOperators.And;

                // full nested clause of filters
                string loggerNamesFilter;

                // each filter
                List<string> loggerNameFilters = new List<string>();

                foreach (string loggerName in loggerNames)
                {
                    loggerNameFilters.Add(TableQuery.GenerateFilterCondition("LoggerName", queryComparison, loggerName));
                }

                loggerNamesFilter = loggerNameFilters.First();

                foreach (string loggerNameFilter in loggerNameFilters.Skip(1))
                {
                    loggerNamesFilter += " " + tableOperator + " " + loggerNameFilter;
                }

                tableQuery.AndWhere(loggerNamesFilter);
            }

            if (!string.IsNullOrWhiteSpace(rowKey))
            {
                tableQuery.AndWhere(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThan, rowKey));
            }

            return this.Connected.HasValue && this.Connected.Value
                    ? this.CloudTable.ExecuteQuery(tableQuery) // if connected
                    : Enumerable.Empty<LogTableEntity>(); // fallback for not connected
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="partitionKey"></param>
        /// <param name="rowKey"></param>
        /// <returns></returns>
        internal LogTableEntity ReadLogTableEntity(string partitionKey, string rowKey)
        {
            return this.Connected.HasValue && this.Connected.Value // if connected
                    ? this.CloudTable
                        .Execute(TableOperation.Retrieve<LogTableEntity>(partitionKey, rowKey))
                        .Result as LogTableEntity
                    : null;
        }

        ///// <summary>
        ///// WARNING: this will delete all
        ///// </summary>
        //internal void DeleteLog()
        //{
        //    // http://stackoverflow.com/questions/16170915/best-practice-in-deleting-azure-table-entities-in-foreach-loop
        //    // HACK: delete the table - issue puts the table name out of action for a long time
        //    this.CloudTable.Delete();
        //}
    }
}
