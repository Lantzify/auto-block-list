angular.module("umbraco").controller("autoBlockList.overview.controller", function (
    $q,
    $http,
    editorService,
    overlayService,
    notificationsService) {

    var vm = this;
    vm.loading = true;

    vm.selectedContent = []

    var appSettings = {
        AutoBlockList: {
            NameFormatting: "[Block list] - {0}",
            AliasFormatting: "{0}BL"
        }
    };

    vm.appSettings = JSON.stringify(appSettings, null, 4);

    vm.toggleSelect = function (id) {
        if (vm.selectedContent.indexOf(id) !== -1) {
            vm.selectedContent.splice(vm.selectedContent.indexOf(id), 1);
        } else {
            vm.selectedContent.push(id);
        }
    };

    vm.clearSelection = function () {
        vm.selectedContent = [];
    };

    $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithNC?page=0").then(function (response) {
        vm.loading = false;

        vm.pagedContent = response.data;

    });

    vm.paginator = function (page) {
        $http.get("/umbraco/backoffice/api/AutoBlockListApi/GetAllContentWithNC?page=" + page).then(function (response) {
            vm.pagedContent = response.data;

        });
    }

    vm.nextPage = function () {
        vm.paginator(vm.pagedContent.pageNumber += 1);
    }

    vm.prevPage = function () {
        vm.paginator(vm.pagedContent.pageNumber);
    }

    vm.goToPage = function (pageNumber) {
        vm.paginator(pageNumber);
    }

    vm.convertContent = function (content) {

        var confirmOptions = {
            title: "Confirm '" + content.name + "' convert",
            view: "/App_Plugins/AutoBlockList/components/overlays/confirm.html",
            submit: function () {
                var options = {
                    view: "/App_Plugins/AutoBlockList/components/overlays/converting.html",
                    title: "Converting",
                    content: content,
                    disableBackdropClick: true,
                    disableEscKey: true,
                    disableSubmitButton: true,
                    submitButtonLabel: "Confirm",
                    closeButtonLabel: "Close",
                    submit: function (model) {
                        overlayService.close();
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
    }
});