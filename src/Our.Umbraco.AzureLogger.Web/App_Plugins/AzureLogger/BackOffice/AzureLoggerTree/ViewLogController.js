﻿angular
    .module('umbraco')
    .controller('AzureLogger.ViewLogController', [
        '$scope', '$http', '$routeParams', 'navigationService', '$q',
        function ($scope, $http, $routeParams, navigationService, $q) {

            var appenderName = $routeParams.id.split('|')[0];
            $scope.headline = $routeParams.id.split('|')[1];

            // forces the tree to highlight the associated search item for this view
            // https://our.umbraco.org/forum/umbraco-7/developing-umbraco-7-packages/48870-Make-selected-node-in-custom-tree-appear-selected
            navigationService.syncTree({ tree: 'azureLoggerTree', path: ['-1', 'appender|' + appenderName], forceReload: false });

            //$scope.startEventTimestamp; // set with date picker
            //$scope.threadIdentity; // built from AppDomainId + ProcessId + ThreadName (set by clicking in details view)
            $scope.logItems = [];
            //$scope.logItemLimit = 1000; // size of logItems array before it is reset (and a new start date time in the filter)
            //$scope.currentlyLoading = false; // true when getting data awaiting a response to set
            //$scope.finishedLoading = false; // true once server indicates that there is no more data

            $scope.$on('TrimmedLog', function (event, arg) {
                if (arg == appenderName) {
                    $scope.logItems = [];
                    $scope.getMoreLogItems();
                }
            });

            // returns a promise with a bool result
            $scope.getMoreLogItems = function () {

                var deferred = $q.defer();

                // only request, if there isn't already a request pending and there might be data to get
                if (!$scope.finishedLoading && !$scope.currentlyLoading) {

                    $scope.currentlyLoading = true;

                    // get last known partitionKey and rowKey
                    var partitionKey = null;
                    var rowKey = null;
                    if ($scope.logItems.length > 0) {

                        var lastLogItem = $scope.logItems[$scope.logItems.length - 1];

                        partitionKey = lastLogItem.partitionKey;
                        rowKey = lastLogItem.rowKey;
                    }

                    $http({
                        method: 'GET',
                        url: 'BackOffice/AzureLogger/Api/ReadLogItemIntros',
                        params: {
                            'appenderName' : appenderName,
                            'partitionKey' : partitionKey != null ? escape(partitionKey) : '',
                            'rowKey': rowKey != null ? escape(rowKey) : '',
                            'take': 5
                        }
                    })
                    .then(function (response) {

                        if (response.data.length > 0) {
                            $scope.logItems = $scope.logItems.concat(response.data); // add new data to array
                        } else {
                            $scope.finishedLoading = true;
                        }

                        $scope.currentlyLoading = false;

                        deferred.resolve(!$scope.finishedLoading); // when true indicates the caller could try again
                    });
                }
                else
                {
                    deferred.resolve(false); // return false to indicate caller shouldn't try again
                }

                return deferred.promise;
            };

            $scope.toggleLogItemDetails = function ($event, logItem) {

                var logItemRow = $($event.currentTarget);
                var logItemDetailsRow = logItemRow.next();

                // update ui
                logItemRow.toggleClass('open');
                logItemDetailsRow.toggle();

                // get data if missing
                if (logItemDetailsRow.is(':visible') && logItem.details === undefined) {

                    // TODO: prevent multiple requests for the same data

                    $http({
                        method: 'GET',
                        url: 'BackOffice/AzureLogger/Api/ReadLogItemDetail',
                        params: {
                            appenderName: appenderName,
                            partitionKey: logItem.partitionKey,
                            rowKey: logItem.rowKey
                        }
                    })
                   .then(function (response) {
                       logItem.details = response.data;
                   });
                }

            };

            $scope.differentDays = function (logItem, lastLogItem) {

                if (lastLogItem === undefined) { return true; } // if there wasn't a last one

                var date = new Date(logItem.eventTimestamp);
                var lastDate = new Date(lastLogItem.eventTimestamp);

                return date.toDateString() !== lastDate.toDateString();
            };

        }]);
