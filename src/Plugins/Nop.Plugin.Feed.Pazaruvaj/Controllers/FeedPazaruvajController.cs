using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Feed.Pazaruvaj.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using System;
using System.Collections.Generic;

namespace Nop.Plugin.Feed.Pazaruvaj.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class FeedPazaruvajController : BasePluginController
    {
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly ILogger _logger;
        private readonly IPermissionService _permissionService;
        private readonly IPluginService _pluginService;
        private readonly IStoreService _storeService;
        private readonly PazaruvajSettings _pazaruvajSettings;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;

        public FeedPazaruvajController(ILocalizationService localizationService, 
            INotificationService notificationService,
            ILogger logger,
            IPermissionService permissionService,
            IPluginService pluginFinder,
            IStoreService storeService,
            PazaruvajSettings pazaruvajSettings, 
            ISettingService settingService,
            IStoreContext storeContext)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _logger = logger;
            _permissionService = permissionService;
            _pluginService = pluginFinder;
            _storeService = storeService;
            _pazaruvajSettings = pazaruvajSettings;
            _settingService = settingService;
            _storeContext = storeContext;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var model = new FeedPazaruvajModel();
            return View("~/Plugins/Feed.Pazaruvaj/Views/FeedPazaruvaj/Configure.cshtml", model);
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [HttpPost]
        [FormValueRequired("save")]
        [AutoValidateAntiforgeryToken]
        public IActionResult Configure(FeedPazaruvajModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            if (!ModelState.IsValid)
            {
                return Configure();
            }
            
            //save settings
            _settingService.SaveSetting(_pazaruvajSettings);

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            //redisplay the form
            return Configure();
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("generate")]
        [AutoValidateAntiforgeryToken]
        public IActionResult GenerateFeed(FeedPazaruvajModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;

            try
            {
                //plugin
                var pluginDescriptor = _pluginService.GetPluginDescriptorBySystemName<IPlugin>("PromotionFeed.Pazaruvaj");
                if (pluginDescriptor == null || !(pluginDescriptor.Instance<IPlugin>() is PazaruvajService plugin))
                    throw new Exception("Cannot load the plugin");

                var stores = new List<Store>();
                var storeById = _storeService.GetStoreById(storeScope);
                if (storeScope > 0)
                    stores.Add(storeById);
                else
                    stores.AddRange(_storeService.GetAllStores());

                foreach (var store in stores)
                    plugin.GenerateStaticFiles(store);

                _notificationService.SuccessNotification("Pazaruvaj feed has been successfully generated.");
            }
            catch (Exception exc)
            {
                _notificationService.ErrorNotification(exc.Message);
                _logger.Error(exc.Message, exc);
            }

            return Configure();
        }

    }
}
