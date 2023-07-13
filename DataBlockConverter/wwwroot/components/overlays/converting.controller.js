angular.module("umbraco").controller("dataBlockConverter.converting.controller", function (
    $http,
    $scope,
    editorService,
    overlayService,
    notificationsService) {

    var vm = this;

    vm.task = "dataTypes";
    vm.currentTask = "Converting data type ";
    vm.percentage = 0;
    vm.report = [];
    vm.showReport = false;

    vm.contentTypes = [$scope.model.content.contentType];

    $http.get("/umbraco/api/ConvertApi/GetDataTypesInContentType?key=" + $scope.model.content.contentType.key).then(function (response) {
        vm.dataTypes = response.data;
        vm.dataTypeCounter = vm.dataTypes.length;

        convertDataTypes(0);
    });

    function convertDataTypes(counter) {

        vm.currentTask = "Converting data types ";
        vm.item = vm.dataTypes[counter].name;

        $http({
            method: "POST",
            url: "/umbraco/api/ConvertApi/ConvertNCDataType",
            data: {
                id: vm.dataTypes[counter].id
            }
        }).then(function (convertedDataTypeResponse) {

            vm.report.push(convertedDataTypeResponse.data)

            var item = convertedDataTypeResponse.data.item;

            if (item !== "") {
                vm.dataTypes[counter] = item;
            }

            counter += 1;

            if (counter !== vm.dataTypeCounter) {
                convertDataTypes(counter);
            } else {
                setTimeout(function () {
                    vm.percentage = 30;
                    getContentTypes(0);
                }, 1000)
            }
        });
    }

    function getContentTypes(counter) {
        $http.get("/umbraco/api/ConvertApi/GetContentTypesElement?dataTypeId=" + vm.dataTypes[counter].matchingBLId).then(function (response) {
            counter += 1;

            vm.contentTypes = vm.contentTypes.concat(response.data);

            if (counter !== vm.dataTypeCounter) {
                getContentTypes(counter);
            } else {
                addDataTypeToContentType(0, 0);
            }
        });
    }

    function addDataTypeToContentType(dataTypeCounter, contentTypecounter) {

        vm.task = "contentTypes";
        vm.currentTask = "Adding data type to document type";
        vm.item = vm.dataTypes[dataTypeCounter].name;
        vm.item2 = vm.contentTypes[contentTypecounter].name;

        $http({
            method: "POST",
            url: "/umbraco/api/ConvertApi/AddDataTypeToContentType",
            data: {
                contentTypeId: vm.contentTypes[contentTypecounter].id,
                newDataTypeId: vm.dataTypes[dataTypeCounter].id,
                oldDataTypeId: vm.dataTypes[dataTypeCounter].matchingBLId,
            }
        }).then(function (converDataTypeResponse) {

            dataTypeCounter += 1;

            //If all data types has been added. Next content type
            if (dataTypeCounter === vm.dataTypeCounter) {
                contentTypecounter += 1;
            }

            if (dataTypeCounter !== vm.dataTypeCounter) {
                addDataTypeToContentType(dataTypeCounter, contentTypecounter);
            } else if (contentTypecounter !== vm.contentTypes.length) {
                //If all data types has been added. Reset data type
                if (dataTypeCounter === vm.dataTypeCounter) {
                    dataTypeCounter = 0;
                }
                addDataTypeToContentType(dataTypeCounter, contentTypecounter);
            } else {
                setTimeout(function () {
                    vm.percentage = 60;
                    convertContent(0)
                }, 1000)
            }
        });
    }

    function convertContent(counter) {

        vm.task = "content";
        vm.currentTask = "Converting content";
        vm.item2 = "";

        $http({
            method: "POST",
            url: "/umbraco/api/ConvertApi/TransferContent",
            data: {
                contentId: $scope.model.content.id,
            }
        }).then(function (converDataTypeResponse) {
            vm.task = "";
            vm.currentTask = "";
            vm.percentage = 100;   

            setTimeout(function () {
                vm.showReport = true;
            }, 500);
            
        });
    }

});