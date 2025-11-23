angular.module("umbraco").controller("autoBlockList.converting.controller", function (
    $http,
    $scope,
    assetsService,
    notificationsService) {

    var vm = this;
    vm.report = [];
    vm.showReport = false;
    vm.task = "";
    vm.currentTask = "";
    vm.item = "";

    setTitle($scope.model.content[0].name);
    setSubTitle(0);

    var signalRScript = Umbraco.Sys.ServerVariables.umbracoSettings.umbracoPath + "/lib/signalr/signalr.min.js";

    assetsService.loadJs(signalRScript).then(function () {
        var connection = new signalR.HubConnectionBuilder().withUrl("/umbraco/AutoBlockList/SyncHub").withAutomaticReconnect().build();

        connection.start().then(function () {
            $http({
                method: "POST",
                url: "/umbraco/backoffice/api/AutoBlockListApi/Convert" + $scope.model.convertType,
                data: {
                    Contents: $scope.model.content,
                    ConnectionId: connection.connectionId
                }
            }, function (err) {
                if (err.data && (err.data.message || err.data.Detail)) {
                    notificationsService.error("Auto block list", err.data.message ?? err.data.Detail);
                } else {
                    notificationsService.error("Auto block list", "Failed to convert everything. Try again or check logs for further information.")
                }
            });
        });

        connection.on("AddReport", function (report) {
            vm.report.push(report);
        });

        connection.on("CurrentTask", function (task) {
            vm.currentTask = task;
        });

        connection.on("UpdateStep", function (task) {
            vm.task = task;
        });

        connection.on("UpdateItem", function (item) {
            vm.item = item;
        });

        connection.on("UpdateTitle", function (item) {
            setTitle(item);
        });

        connection.on("UpdateSubTitle", function (item) {
            setSubTitle(item);
        });

        connection.on("Done", function (item) {
            vm.showReport = true;
            setSubTitle(item);

            if (item === $scope.model.content.length) {
                $scope.model.disableSubmitButton = false;
                document.body.classList.add("hideClose");
                notificationsService.success("Auto block list", "successfully converted everything.")
            }
        });
    });

    function setTitle(item) {
        $scope.model.title = "Converting '" + item + "'";
    };

    function setSubTitle(item) {
        $scope.model.subtitle = item + " of " + $scope.model.content.length + " converted";
    };
});