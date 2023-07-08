angular.module("umbraco").controller("dataBlockConverter.overview.controller", function ($http, dataTypeResource, $location) {

	var vm = this;
	vm.loading = true;
    vm.dataTypes = []; 
    vm.contentTypes = [];

	vm.page = {
		title: "Data block converter",
		description: "Overview of all avaible views to genarte dictionaries to."
	};

	$http.get("/umbraco/api/ConvertApi/GetAllNCDataTypes").then(function (response) {
        vm.loading = false;
        vm.dataTypes = response.data;
    });

    $http.get("/umbraco/api/ConvertApi/GetAllNCContentTypes").then(function (response) {
        vm.loading = false;
        console.log(response.data);


    });

    vm.convertDataType = function (dataType) {
        $http.get("/umbraco/api/ConvertApi/ConverNCDataType?id=" + dataType.id).then(function (response) {
            vm.loading = false;
        });
    }
});