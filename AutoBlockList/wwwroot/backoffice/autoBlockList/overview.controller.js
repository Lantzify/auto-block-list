angular.module("umbraco").controller("autoBlockList.overview.controller", function (
    $q,
    $http,
    $route,
    editorService,
    overlayService,
    notificationsService) {

    var vm = this;
    vm.loadingTable = true;
    vm.loadingMacroTable = true;

    vm.selectedContent = []
    vm.selectedMacroContent = []

    var appSettings = {
        AutoBlockList: {
            BlockListEditorSize: "medium",
            SaveAndPublish: true,
            NameFormatting: "[Block list] - {0}",
            AliasFormatting: "{0}BL",
            FolderNameForContentTypes: "[Rich text editor] - Components"
        }
    };

    vm.appSettings = JSON.stringify(appSettings, null, 4);

    $q.all({
        getAllContentWithNC: $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithNC?page=0"),
        getAllContentWithTinyMce: $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithTinyMce?page=1"),
    }).then(function (promises) {
        vm.loadingMacroTable = false;
        vm.loadingTable = false;
        vm.pagedContent = promises.getAllContentWithNC.data;
        vm.pagedContent.pageNumber += 1;
        vm.pagedContentWithMacros = promises.getAllContentWithTinyMce.data;
    }, function (err) {
        if (err.data && (err.data.message || err.data.Detail)) {
            notificationsService.error("Auto block list", err.data.message ?? err.data.Detail);
        } else {
            notificationsService.error("Auto block list", "Failed to load. Try again or check logs for further information.")
        }
    });

    vm.toggleSelect = function (selectedArray, content) {
        var pos = vm.findIndex(selectedArray, content.id);

        if (pos !== -1) {
            selectedArray.splice(pos, 1);
        } else {
            selectedArray.push(content);
        }
    };

    vm.toggleSelectAll = function (pagedArray, selectedArray) {
        pagedArray.items.forEach(function (e) {
            if (vm.findIndex(selectedArray, e.id) === -1) {
                selectedArray.push(e);
            }
        });
    };

    vm.clearSelection = function (selectedArray) {
        selectedArray.splice(0, selectedArray.length);
    };

    vm.openContent = function (contentId) {
        var options = {
            id: contentId,
            size: "large",
            submit: function (model) {
                editorService.close();
            },
            close: function () {
                editorService.close();
            }
        };
        editorService.contentEditor(options);
    };

    vm.findIndex = function (arr, id) {
        return arr.findIndex(function (index) {
            return index.id === id;
        });
    };


    //Macro
    vm.paginatorMacro = function (page) {
        vm.loadingMacroTable = true;
        $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithTinyMce?page=" + page).then(function (response) {
            vm.pagedContentWithMacros = response.data;
            vm.pagedContentWithMacros.pageNumber;
            vm.loadingMacroTable = false;
        });
    };

    vm.nextMacroPage = function () {
        vm.paginatorMacro(vm.pagedContentWithMacros.pageNumber += 1);
    };

    vm.prevMacroPage = function () {
        vm.paginatorMacro(vm.pagedContentWithMacros.pageNumber -= 1);
    };

    vm.goToMacroPage = function (pageNumber) {
        vm.paginatorMacro(pageNumber);
    };

    vm.convertMacro = function () {

        var confirmOptions = {
            title: "Confirm convert",
            view: "/App_Plugins/AutoBlockList/components/overlays/confirmMacro.html",
            submit: function () {
                var options = {
                    view: "/App_Plugins/AutoBlockList/components/overlays/converting.html",
                    title: "Converting",
                    content: vm.selectedMacroContent,
                    convertType: "Macro",
                    disableBackdropClick: true,
                    disableEscKey: true,
                    disableSubmitButton: true,
                    submitButtonLabel: "Confirm",
                    closeButtonLabel: "Close",
                    submit: function (model) {
                        $route.reload();
                        overlayService.close();
                        document.body.classList.remove("hideClose");
                    },
                    close: function () {
                        overlayService.close();
                    }
                };

                overlayService.open(options);
            }
        }

        overlayService.confirm(confirmOptions);
    }

    //NC
    vm.paginator = function (page) {
        vm.loadingTable = true;
        $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithNC?page=" + page).then(function (response) {
            vm.pagedContent = response.data;
            vm.pagedContent.pageNumber += 1;
            vm.loadingTable = false;
        });
    };

    vm.nextPage = function () {
        vm.paginator(vm.pagedContent.pageNumber);
    };

    vm.prevPage = function () {
        vm.paginator(vm.pagedContent.pageNumber -= 2);
    };

    vm.goToPage = function (pageNumber) {
        vm.paginator(pageNumber - 1);
    };

    vm.convertContent = function () {

        var confirmOptions = {
            title: "Confirm convert",
            view: "/App_Plugins/AutoBlockList/components/overlays/confirm.html",
            submit: function () {
                var options = {
                    view: "/App_Plugins/AutoBlockList/components/overlays/converting.html",
                    title: "Converting",
                    content: vm.selectedContent,
                    convertType: "NC",
                    disableBackdropClick: true,
                    disableEscKey: true,
                    disableSubmitButton: true,
                    submitButtonLabel: "Confirm",
                    closeButtonLabel: "Close",
                    submit: function (model) {
                        $route.reload();
                        overlayService.close();
                        document.body.classList.remove("hideClose");
                    },
                    close: function () {
                        overlayService.close();
                    }
                };

                overlayService.open(options);
            }
        }

        overlayService.confirm(confirmOptions);
    }
});