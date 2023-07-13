angular.module("umbraco").controller("dataBlockConverter.overview.controller", function (
    $q,
    $http,
    editorService,
    overlayService,
    notificationsService) {

    var vm = this;
    vm.loading = true;
    var title = "Data block converter";

    vm.selectedContent = []
    vm.selectedContentTypes = []
    vm.selectedDataTypes = []

    var appSettings = {
        DataBlockConverter: {
            NameFormatting: "[Block list] - {0}",
            AliasFormatting: "{0}BL"
        }
    };

    vm.appSettings = JSON.stringify(appSettings, null, 4);

    vm.page = {
        title: title,
        description: "Overview of all avaible nested content you can convert."
    };

    vm.toggleSelect = function (id, array) {
        if (array.indexOf(id) !== -1) {
            array.splice(array.indexOf(id), 1);
        } else {
            array.push(id);
        }
    };

    vm.clearSelection = function (array) {
        array.splice(0, array.length);
    };

    $q.all({
        //dataTypes: $http.get("/umbraco/api/ConvertApi/GetAllNCDataTypes"),
        //contentTypes: $http.get("/umbraco/api/ConvertApi/GetAllNCContentTypes"),
        content: $http.get("/umbraco/api/ConvertApi/GetAllContentWithNC")
    }).then(function (promises) {
        vm.loading = false;

        //vm.dataTypes = promises.dataTypes.data;
        //vm.contentTypes = promises.contentTypes.data;
        vm.content = promises.content.data;
    }, function (err) {
        if (err.data && (err.data.message || err.data.Detail)) {
            notificationsService.error(title, err.data.message ?? err.data.Detail);
        }
    });

    vm.convertContent = function (content) {
        var options = {
            view: "/App_Plugins/DataBlockConverter/components/overlays/converting.html",
            title: "Converting",
            content: content,
            disableBackdropClick: true,
            disableEscKey: true,
            disableSubmitButton: true,
            submitButtonLabel: "Confirm",
            closeButtonLabel: "Close",
            submit: function (model) {
                
            },
            close: function () {
                overlayService.close();
            }
        };

        overlayService.open(options);

        //$http({
        //    method: "POST",
        //    url: "/umbraco/api/ConvertApi/ConverNCDataType",
        //    data: vm.selectedDataTypes
        //}).then(function (response) {
        //    vm.convertDataTypesButtonState = "success";
        //    notificationsService.success(title, "");
        //}, function (err) {
        //    vm.convertDataTypesButtonState = "error";
        //    if (err.data && (err.data.message || err.data.Detail)) {
        //        notificationsService.error(title, err.data.message ?? err.data.Detail);
        //    }
        //});
    }

    vm.convertDataTypes = function () {

        vm.convertDataTypesButtonState = "busy";

        $http({
            method: "POST",
            url: "/umbraco/api/ConvertApi/ConverNCDataType",
            data: vm.selectedDataTypes
        }).then(function (response) {
            vm.convertDataTypesButtonState = "success";
            notificationsService.success(title, "");
        }, function (err) {
            vm.convertDataTypesButtonState = "error";
            if (err.data && (err.data.message || err.data.Detail)) {
                notificationsService.error(title, err.data.message ?? err.data.Detail);
            }
        });
    }

    vm.convertContentType = function () {
        vm.convertContentTypeButtonState = "busy";
        $http({
            method: "POST",
            url: "/umbraco/api/ConvertApi/ConvertNCInContentType",
            data: vm.selectedContentTypes
        }).then(function (response) {
            notificationsService.success(title, "")
        }, function (err) {
            if (err.data && (err.data.message || err.data.Detail)) {
                notificationsService.error(title, err.data.message ?? err.data.Detail);
            }
        });
    }
    vm.openContent = function (contentId) {
        var options = {
            id: contentId,
            size: "medium",
            submit: function (model) {
                editorService.close();
            },
            close: function () {
                editorService.close();
            }
        };
        editorService.contentEditor(options);
    }

    vm.openDocumentType = function (documentType) {
        var options = {
            id: documentType.id,
            size: "medium",
            submit: function (model) {
                editorService.close();
            },
            close: function () {
                editorService.close();
            }
        };
        editorService.documentTypeEditor(options);
    }

    vm.openDataTypeSettings = function (dataTypeId) {
        var dataTypeSettings = {
            view: "views/common/infiniteeditors/datatypesettings/datatypesettings.html",
            id: dataTypeId,
            size: "medium",
            submit: function (model) {
                editorService.close();
            },
            close: function () {
                editorService.close();
            }
        };

        editorService.open(dataTypeSettings);
    }
});