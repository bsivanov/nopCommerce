using Microsoft.AspNetCore.Hosting;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Media;
using Nop.Services.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Nop.Plugin.Feed.Pazaruvaj
{
    public class PazaruvajService : BasePlugin,  IMiscPlugin
    {
        private const string FEED_FILE_NAME = "pazaruvaj.xml";

        private const string WATCHES_CATEGORY_NAME = "Часовници";
        private const string MEN_CATEGORY_NAME = "Мъжки";
        private const string WOMEN_CATEGORY_NAME = "Дамски";
        private const string CATEGORY_BREADCRUMB_SEPARATOR = " > ";
        private const int DELIVERY_TIME_IN_DAYS = 1;

        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IPictureService _pictureService;
        private readonly IWebHelper _webHelper;
        private readonly ISettingService _settingService;
        private readonly INopFileProvider _nopFileProvider;
        private readonly IWebHostEnvironment _webHostEnvironment;

        private readonly PazaruvajSettings _pazaruvajSettings;

        private readonly TextInfo _enUsTextInfo = new CultureInfo("en-US").TextInfo;

        public PazaruvajService(IProductService productService,
            ICategoryService categoryService, 
            IManufacturerService manufacturerService, 
            IPictureService pictureService,
            IWebHostEnvironment webHostEnvironment,
            IWebHelper webHelper,
            ISettingService settingService,
            INopFileProvider nopFileProvider,
            PazaruvajSettings pazaruvajSettings)
        {
            _productService = productService;
            _categoryService = categoryService;
            _manufacturerService = manufacturerService;
            _pictureService = pictureService;
            _webHelper = webHelper;
            _settingService = settingService;
            _webHostEnvironment = webHostEnvironment;
            _nopFileProvider = nopFileProvider;
            _pazaruvajSettings = pazaruvajSettings;
        }

        /// <summary>
        /// Generate a feed
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>Generated feed</returns>
        public void GenerateFeed(Stream stream, Store store)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (store == null)
                throw new ArgumentNullException("store");

            using var writer = XmlWriter.Create(stream);
            writer.WriteStartDocument();
            writer.WriteStartElement("shop");
            WriteProducts(store, writer);
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private void WriteProducts(Store store, XmlWriter writer)
        {
            writer.WriteStartElement("products");

            var products = _productService.SearchProducts();
            foreach (var product in products)
            {
                if (product.StockQuantity == 0 || !product.Published)
                {
                    continue;
                }

                WriteProduct(store, writer, product);
            }

            writer.WriteEndElement();
        }

        private void WriteProduct(Store store, XmlWriter writer, Product product)
        {
            writer.WriteStartElement("product");

            var productManufacturers = _manufacturerService.GetProductManufacturersByProductId(product.Id);
            var manufacturerName = productManufacturers.Count > 0 ? _manufacturerService.GetManufacturerById(productManufacturers[0].ManufacturerId).Name : string.Empty;
            var productName = CapitalizeProductName(product.Name, manufacturerName);

            var productUrl = string.Format("{0}{1}", _webHelper.GetStoreLocation(false), product.Name);

            var imageUrls = new List<string>();
            var pictures = _pictureService.GetPicturesByProductId(product.Id, 1);

            //always use HTTP when getting image URL
            if (pictures.Count == 0)
            {
                imageUrls.Add(_pictureService.GetDefaultPictureUrl(storeLocation: store.Url));
            }
            else
            {
                imageUrls.AddRange(pictures.Select(p => _pictureService.GetPictureUrl(p.Id, storeLocation: store.Url)));
            }

            var price = product.Price.ToString("N2", CultureInfo.InvariantCulture);

            var description = product.FullDescription;
            if (string.IsNullOrEmpty(description))
            {
                description = product.ShortDescription;
            }
            if (string.IsNullOrEmpty(description))
            {
                description = product.Name;
            }

            var categoryBreadcrumb = GetCategoryBreadcrumb(product.Id);

            writer.WriteElementString("identifier", product.Id.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("manufacturer", manufacturerName);
            writer.WriteElementString("category", categoryBreadcrumb);
            writer.WriteElementString("name", productName);
            writer.WriteElementString("product_url", productUrl);
            writer.WriteElementString("price", price);

            WriteImageUrls(writer, imageUrls);

            writer.WriteElementString("description", description);
            writer.WriteElementString("delivery_time", DELIVERY_TIME_IN_DAYS.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("delivery_cost", "Free");

            writer.WriteEndElement();
        }

        private string CapitalizeProductName(string productName, string manufacturerName)
        {
            if (!productName.StartsWith(manufacturerName, StringComparison.OrdinalIgnoreCase))
            {
                return productName;
            }

            var capitalizedManufacturer = _enUsTextInfo.ToTitleCase(manufacturerName.ToLowerInvariant());

            if (productName.StartsWith(capitalizedManufacturer))
            {
                return productName;
            }

            var nameWithoutManufacturer = productName.Remove(0, manufacturerName.Length);

            return capitalizedManufacturer + nameWithoutManufacturer;
        }

        private static void WriteImageUrls(XmlWriter writer, List<string> imageUrls)
        {
            for (var i = 0; i < imageUrls.Count; i++)
            {
                var elementName = i == 0 ? "image_url" : string.Format("image_url_{0}", i);
                writer.WriteElementString(elementName, imageUrls[i]);
            }
        }

        private string GetCategoryBreadcrumb(int productId)
        {
            var categoryBreadcrumbBuilder = new StringBuilder();

            categoryBreadcrumbBuilder.Append(WATCHES_CATEGORY_NAME);
            categoryBreadcrumbBuilder.Append(CATEGORY_BREADCRUMB_SEPARATOR);

            var productCategories = _categoryService.GetProductCategoriesByProductId(productId);

            if (productCategories.Any(pc => string.Equals(_categoryService.GetCategoryById(pc.Id).Name, MEN_CATEGORY_NAME, StringComparison.InvariantCultureIgnoreCase)))
            {
                categoryBreadcrumbBuilder.Append(MEN_CATEGORY_NAME);
            }
            else if (productCategories.Any(pc => string.Equals(_categoryService.GetCategoryById(pc.Id).Name, WOMEN_CATEGORY_NAME, StringComparison.InvariantCultureIgnoreCase)))
            {
                categoryBreadcrumbBuilder.Append(WOMEN_CATEGORY_NAME);
            }

            return categoryBreadcrumbBuilder.ToString();
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new PazaruvajSettings();
            _settingService.SaveSetting(settings);
            
            base.Install();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PazaruvajSettings>();

            base.Uninstall();
        }

        /// <summary>
        /// Generate a static file for pazaruvaj
        /// </summary>
        /// <param name="store">Store</param>
        public virtual void GenerateStaticFiles(Store store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            var filePath = _nopFileProvider.Combine(_webHostEnvironment.WebRootPath, "files", "exportimport", FEED_FILE_NAME);
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            GenerateFeed(fs, store);
        }
    }
}
