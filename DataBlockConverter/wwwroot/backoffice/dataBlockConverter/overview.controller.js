angular.module("umbraco").controller("dataBlockConverter.overview.controller", function (
    $q,
    $http,
    notificationsService,
    $location) {

	var vm = this;
    vm.loading = true;
    var title = "Data block converter";

	vm.page = {
        title: title,
		description: "Overview of all avaible nested content you can convert."
	};

    $q.all({
        dataTypes: $http.get("/umbraco/api/ConvertApi/GetAllNCDataTypes"),
        contentTypes: $http.get("/umbraco/api/ConvertApi/GetAllNCContentTypes"),
        content: $http.get("/umbraco/api/ConvertApi/GetAllContentWithNC")
    }).then(function (promises) {
        vm.loading = false;

        vm.dataTypes = promises.dataTypes.data;
        vm.contentTypes = promises.contentTypes.data;
        vm.content = promises.content.data;
    }, function (err) {
        if (err.data && (err.data.message || err.data.Detail)) {
            notificationsService.error(title, err.data.message ?? err.data.Detail);
        }
    });

    vm.convertDataType = function (dataType) {
        $http.get("/umbraco/api/ConvertApi/ConverNCDataType?id=" + dataType.id).then(function (response) {
            notificationsService.success(title, "");
            $location.url("/uSupport/ticketStatuses/edit/" + id);
        }, function (err) {
            if (err.data && (err.data.message || err.data.Detail)) {
                notificationsService.error(title, err.data.message ?? err.data.Detail);
            }
        });
    }

    vm.convertContentType = function (contentType) {
        $http.get("/umbraco/api/ConvertApi/ConvertNCInContentType?id=" + contentType.id).then(function (response) {
            notificationsService.success(title, "")
        }, function (err) {
            if (err.data && (err.data.message || err.data.Detail)) {
                notificationsService.error(title, err.data.message ?? err.data.Detail);
            }
        });
    }
});