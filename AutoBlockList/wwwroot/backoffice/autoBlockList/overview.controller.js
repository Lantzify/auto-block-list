angular.module("umbraco").controller("autoBlockList.overview.controller", function (
    $q,
    $http,
    $route,
    editorService,
    overlayService) {

    var vm = this;
    vm.loading = true;

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

    vm.toggleSelect = function (content) {
        var pos = vm.findIndex(vm.selectedContent, content.id);

        if (pos !== -1) {
            vm.selectedContent.splice(pos, 1);
        } else {
            vm.selectedContent.push(content);
        }
    };

    vm.toggleSelectMacro = function (content) {
        var pos = vm.findIndex(vm.selectedMacroContent, content.id);

        if (pos !== -1) {
            vm.selectedMacroContent.splice(pos, 1);
        } else {
            vm.selectedMacroContent.push(content);
        }
    };

    vm.toggleSelectAll = function (macro) {
        if (macro) {
            vm.pagedContentWithMacros.items.forEach(function (e) {
                if (vm.findIndex(vm.selectedMacroContent, e.id) === -1) {
                    vm.selectedMacroContent.push(e);
                }
            });
        } else {
            vm.pagedContent.items.forEach(function (e) {
                if (vm.findIndex(vm.selectedContent, e.id) === -1) {
                    vm.selectedContent.push(e);
                }
            });
        }
    };

    vm.clearSelection = function (macro) {
        if (macro) {
            vm.selectedMacroContent = []
        } else {
            vm.selectedContent = [];
        }
    };

    $q.all({
        getAllContentWithNC: $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithNC?page=0"),
        getAllContentWithTinyMce: $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithTinyMce?page=0"),
    }).then(function (promises) {
        vm.loading = false;
        vm.pagedContent = promises.getAllContentWithNC.data;
        vm.pagedContent.pageNumber += 1;

        vm.pagedContentWithMacros = promises.getAllContentWithTinyMce.data;
        vm.pagedContentWithMacros.pageNumber += 1;
    });


    vm.paginatorMacro = function (page) {
        vm.loadingMacroTable = true;
        $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithTinyMce?page=" + page).then(function (response) {
            vm.pagedContentWithMacros = response.data;
            vm.pagedContentWithMacros.pageNumber += 1;
            vm.loadingMacroTable = false;
        });
    };

    vm.nextMacroPage = function () {
        vm.paginatorMacro(vm.pagedContentWithMacros.pageNumber);
    };

    vm.prevMacroPage = function () {
        vm.paginatorMacro(vm.pagedContentWithMacros.pageNumber -= 2);
    };

    vm.goToMacroPage = function (pageNumber) {
        vm.paginatorMacro(pageNumber - 1);
    };


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
});