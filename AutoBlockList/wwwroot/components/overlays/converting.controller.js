angular.module("umbraco").controller("autoBlockList.converting.controller", function ($http, $scope, assetsService) {

    var vm = this;
    vm.report = [];
    vm.showReport = false;
    vm.task = "";
    vm.currentTask = "";
    vm.item = "";

    $scope.model.title = "Converting 'Foo'";
    $scope.model.subtitle = "1 of 100";

    var signalRScript = Umbraco.Sys.ServerVariables.umbracoSettings.umbracoPath + "/lib/signalr/signalr.min.js";

    assetsService.loadJs(signalRScript).then(function () {
        var connection = new signalR.HubConnectionBuilder({
            logging: true
        }).withUrl("/umbraco/AutoBlockList/SyncHub").build();

        connection.start().then(function () {

            $http({
                method: "POST",
                url: "/umbraco/backoffice/api/AutoBlockListApi/Convert",
                data: {
                    Contents: $scope.model.content,
                    ConnectionId: connection.connectionId
                }
            }).then(function (response) {

            });

        });

        connection.on("AddReport", function (report) {
            console.log("Runs")
            console.log(report)

            vm.report.push(report);
        });

        connection.on("UpdateTask", function (task) {
            console.log("Runs")
            console.log(task)
            vm.task = task; 
        });

        connection.on("UpdateItem", function (item) {
            console.log("Runs")
            console.log(item)
            vm.item = item;
        });

        connection.on("Done", function (item) {
            vm.showReport = true;
        });
    });
});