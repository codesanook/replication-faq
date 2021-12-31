// SETUP
// SNAPSHOT
// TRANSACTIONAL
// PEER TO PEER
// MERGE
// SYNC SERVICES
// ALWAYS ON
// CONTACT US
using OrchardCore.Data.Migration;
using OrchardCore.ContentManagement;
using OrchardCore.Layers.Services;
using OrchardCore.Rules;
using OrchardCore.DisplayManagement.Notify;
using Microsoft.AspNetCore.Mvc.Localization;
using System.Threading.Tasks;
using OrchardCore.Menu.Models;
using YesSql;
using System.Linq;
using System.Text.RegularExpressions;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Layers.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using ReplicationFaq.Theme.Models;
using OrchardCore.Autoroute.Models;
using Codesanook.OrganizationProfile.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Taxonomies.Models;
using OrchardCore.Title.Models;
using OrchardCore.ContentFields.Fields;
using OrchardCore.Settings;

namespace ReplicationFaq.Theme
{
    public class Migrations : DataMigration
    {
        private readonly IContentManager _contentManager;
        private readonly IContentHandleManager _contentHandleManager;
        private readonly ISession _session;
        private readonly ILayerService _layerService;
        private readonly IConditionIdGenerator _conditionIdGenerator;
        private readonly INotifier _notifier;
        private readonly IHtmlLocalizer<Migrations> H;
        private static readonly Regex pattern = new Regex(@"\s+", RegexOptions.Compiled);
        private readonly IContentDefinitionManager _contentDefinitionManager;
        private readonly ISiteService _siteService;

        public Migrations(
            INotifier notifier,
            IHtmlLocalizer<Migrations> localizer,
            IConditionIdGenerator conditionIdGenerator,
            IContentManager contentManager,
            IContentHandleManager contentHandleManager,
            ISession session,
            IContentDefinitionManager contentDefinitionManager,
            ISiteService siteService
        )
        {
            _contentManager = contentManager;
            _contentHandleManager = contentHandleManager;
            _session = session;
            _notifier = notifier;
            H = localizer;
            _conditionIdGenerator = conditionIdGenerator;
            _contentDefinitionManager = contentDefinitionManager;
            _siteService = siteService;
        }

        public async Task<int> CreateAsync()
        {
            await CreateMenuAsync();

            // Create widgets
            CreateHomeBannerPart();
            await CreateHomeBannerWidgetAsync();

            CreateRecentBlogPostsPart();
            await CreateRecentBlogPostsWidget();

            CreateBlockListPart();
            await CreateBlockListWidgetAsync();

            UpdateBlogPostType();
            await RemoveExistingBlogPost();

            await CreateBlogPostTaxonomyAsync(
                "Categories",
                "Category",
                new[] { "Setup", "Snapshot", "Transactional", "Peer to Peer", "Merge", "Sync Services", "Always On" },
                "fas fa-globe-americas"
            );
            await CreateBlogPostTaxonomyAsync(
                "Tags",
                "Tag",
                new[] { "indexing", "backup", "performance" }
            );

            await CreateOrganizationProfileAsync();
            await RemoveFooterWidgetAsync();

            return 1;
        }

        private void CreateRecentBlogPostsPart()
        {
            _contentDefinitionManager.AlterPartDefinition(
                nameof(RecentBlogPostsPart),
                part => part
                    .Attachable(true)
                    .WithDescription("Provide a recent blog posts part for a content item.")
            );
        }

        private async Task CreateOrganizationProfileAsync()
        {
            var contentItem = await _contentManager.NewAsync("OrganizationProfile");

            contentItem.Alter<AddressPart>(
                nameof(AddressPart),
                a =>
                {
                    a.HouseNumber = "100";
                    a.BuildingName = "building name";
                    a.RoomNumber = "100/1";
                    a.Floor = 10;
                    a.Lane = "Lane";
                    a.Street = "Street";
                    a.Subdistrict = "Subdistrict";
                    a.District = "District";
                    a.Province = "Province";
                    a.ZipCode = "10300";
                }
            );

            contentItem.Alter<ContactInformationPart>(
                nameof(ContactInformationPart),
                c =>
                {
                    c.PhoneNumber = "0212345678";
                    c.EmailAddress = "user@gmail.com";
                }
            );

            contentItem.Alter<SocialNetworkPart>(
                nameof(SocialNetworkPart),
                s =>
                {
                    s.Facebook = "https://www.facebook.com/Replication-FAQ-105193241951452";
                    s.YouTube = "https://www.youtube.com/channel/UC5bQruOk_lEYkgajAB1CX-A";
                }
            );

            contentItem.Alter<AutoroutePart>(
                nameof(AutoroutePart),
                r => r.Path = "/contact-us"
            );

            await _contentManager.CreateAsync(contentItem, VersionOptions.Published);
        }

        private void UpdateBlogPostType()
        {
            var urlPattern = new[] {
                "{% assign category = ContentItem.Content.BlogPost.Category | taxonomy_terms | first %}",
                "/{{ 'categories' }}/{{ category | display_text | slugify }}/{{ ContentItem | display_text | slugify }}"
            };

            _contentDefinitionManager.AlterTypeDefinition(
                "BlogPost",
                type => type
                    .WithPart("MarkdownBodyPart", part => part.WithEditor("Wysiwyg"))
                    .WithPart(nameof(AutoroutePart), part => part.WithSettings(
                        new AutoroutePartSettings()
                        {
                            Pattern = string.Join("\n", urlPattern),
                            AllowCustomPath = true,
                            AllowUpdatePath = true,
                            //ManageContainedItemRoutes = true,
                        })
                    )
            );

            var contentPartDefinition = _contentDefinitionManager.GetPartDefinition("BlogPost");
            var field = contentPartDefinition.Fields.FirstOrDefault(x => x.Name == "Subtitle");
            _contentDefinitionManager.AlterPartDefinition(
                "BlogPost", // Same name as Content type to contain fields
                part => part
                    .RemoveField(field.Name)
                    .RemoveField("Image") // Banner Image
            );
        }

        private async Task CreateHomeBannerWidgetAsync()
        {
            const string widgetName = "HomeBannerWidget";
            _contentDefinitionManager.AlterTypeDefinition(
                widgetName,
                type => type
                    .WithPart(nameof(HomeBannerPart))
                    .Stereotype("Widget")
                    .Versionable(false) // no version
            );

            // Create a new content item, not save to database yet.
            var contentItem = await _contentManager.NewAsync(widgetName);
            contentItem.DisplayText = widgetName;

            // Only using Weld that creates actual part json data in a database
            contentItem.Weld<HomeBannerPart>(new HomeBannerPart());

            // Show only on home page
            var layerMetaData = new LayerMetadata()
            {
                RenderTitle = false,
                Zone = "Content",
                Layer = "Homepage",
                Position = 0
            };

            // Attach a layer Meta data to a widget content item.
            contentItem.Weld(layerMetaData);
            await _contentManager.CreateAsync(contentItem, VersionOptions.Published);
        }

        private async Task CreateMenuAsync()
        {
            // Create home menu item
            // var homeMenuItem = await _contentManager.NewAsync("LinkMenuItem");
            // homeMenuItem.DisplayText = "Home";
            // homeMenuItem.Alter<LinkMenuItemPart>(p => { p.Name = "Home"; p.Url = "~/"; });
            // await _contentManager.CreateAsync(homeMenuItem, VersionOptions.Published);

            // _session.Query()

            // Create about us menu item
            // var aboutUsMenuItem = await _contentManager.NewAsync("LinkMenuItem");
            // aboutUsMenuItem.DisplayText = "About us";
            // aboutUsMenuItem.Alter<LinkMenuItemPart>(p => { p.Name = "About us"; p.Url = "~/about-us"; });
            // await _contentManager.CreateAsync(aboutUsMenuItem, VersionOptions.Published);

            // Create contact us menu item
            var distributorMenuItem = await CreateMenuItem("Setup", "categories");
            var SnapshotMenuItem = await CreateMenuItem("Snapshot", "categories");
            var transactionalMenuItem = await CreateMenuItem("Transactional", "categories");
            var peerToPeerMenuItem = await CreateMenuItem("Peer to Peer", "categories");
            var mergeMenuItem = await CreateMenuItem("Merge", "categories");
            var syncServicesMenuItem = await CreateMenuItem("Sync Services", "categories");
            var alwaysOnMenuItem = await CreateMenuItem("Always On", "categories");
            var contactMenuItem = await CreateMenuItem("Contact us");

            //await _contentManager.CreateAsync(contactUsMenuItem, VersionOptions.Published);
            // C:\projects\OrchardCore\src\OrchardCore.Modules\OrchardCore.Alias\Razor\AliasPartRazorHelperExtensions.cs
            var contentItemId = await _contentHandleManager.GetContentItemIdAsync("alias:main-menu");
            var menuContentItem = await _contentManager.GetAsync(contentItemId);

            // // Create main menu
            // const string mainMenuName = "Main Menu";
            //var mainMenu =  menuContentItem.As<MenuPart>();
            // mainMenu.Alter<TitlePart>(p => p.Title = mainMenuName);
            // mainMenu.DisplayText = mainMenuName;
            // mainMenu.Alter<AliasPart>(p => p.Alias = "main-menu");
            // var menuItems = menuContentItem.As<MenuItemsListPart>().MenuItems;
            // var aboutMenuItem = menuItems.Single(x => x.DisplayText == "About");

            // menuContentItem.Alter<MenuItemsListPart>(p =>
            // {
            //     var aboutMenuItem = p.MenuItems.Single(x => x.DisplayText == "About");
            //     aboutMenuItem.DisplayText = "About Us";
            //     aboutMenuItem.As<LinkMenuItemPart>().Url = "~/about-us";
            // });

            menuContentItem.Alter<MenuItemsListPart>(
                p =>
                {
                    var aboutMenuItem = p.MenuItems.Single(x => x.DisplayText == "About");
                    p.MenuItems.Remove(aboutMenuItem);

                    p.MenuItems.AddRange(new[] {
                        distributorMenuItem,
                        SnapshotMenuItem,
                        transactionalMenuItem,
                        peerToPeerMenuItem,
                        mergeMenuItem,
                        syncServicesMenuItem,
                        alwaysOnMenuItem,
                        contactMenuItem
                    });
                }
            );

            // Update without a new version
            await _contentManager.UpdateAsync(menuContentItem);
        }

        private async Task<ContentItem> CreateMenuItem(string displayText, string basedUrl = null)
        {
            var contactUsMenuItem = await _contentManager.NewAsync("LinkMenuItem");
            contactUsMenuItem.DisplayText = displayText;

            var childUrl = pattern.Replace(displayText.Trim(), "-").ToLower();
            var urlSegments = new[] { basedUrl, childUrl }.Where(url => !string.IsNullOrEmpty(url));
            var resolvedUrl = $"~/{string.Join('/', urlSegments)}";
            contactUsMenuItem.Alter<LinkMenuItemPart>(p => p.Url = resolvedUrl);
            return contactUsMenuItem;
        }

        private void CreateBlockListPart()
        {
            _contentDefinitionManager.AlterPartDefinition(
                nameof(BlockListPart),
                part => part
                    .Attachable(true)
                    .WithDescription("Provide a block list part for a content item.")
            );
        }

        private void CreateHomeBannerPart()
        {
            _contentDefinitionManager.AlterPartDefinition(
                nameof(HomeBannerPart),
                part => part
                    .Attachable(true)
                    .WithDescription("Provide a home banner part for a content item.")
            );
        }

        private async Task CreateBlockListWidgetAsync()
        {
            const string widgetName = "BlockListWidget";
            _contentDefinitionManager.AlterTypeDefinition(
                widgetName,
                type => type
                    .WithPart(nameof(BlockListPart))
                    .Stereotype("Widget")
            );

            // Create a new content item, not save to database yet.
            var contentItem = await _contentManager.NewAsync(widgetName);
            contentItem.DisplayText = widgetName;
            contentItem.Weld<BlockListPart>(new BlockListPart());

            //var layerMetaData = contentItem.As<LayerMetadata>();
            var layerMetaData = new LayerMetadata()
            {
                RenderTitle = false,
                Zone = "Content", //=> BeforeContent
                Layer = "Homepage",
                Position = 2
            };

            // Attach Layer Meta data to a widget content item.
            contentItem.Weld(layerMetaData);
            await _contentManager.CreateAsync(contentItem, VersionOptions.Published);
        }

        public async Task RemoveExistingBlogPost()
        {
            var existingBlogPostContentItems = await _session
                .Query<ContentItem, ContentItemIndex>(q => q.ContentType == "BlogPost")
                .ListAsync();
            foreach (var item in existingBlogPostContentItems)
            {
                await _contentManager.RemoveAsync(item);
            }
        }

        public async Task CreateBlogPostTaxonomyAsync(string displayName, string termContentType, string[] terms, string iconClasses = null)
        {
            // Start by Taxonomy content item that constains some tag terms 
            var taxonomyContentItem = await _session
                .Query<ContentItem, ContentItemIndex>(q => q.ContentType == "Taxonomy")
                .Where(i => i.DisplayText == displayName)
                .FirstOrDefaultAsync();

            // Remove all tag terms from TaxonomyPart of Taxonomy ContentType
            // We won't see any record in ContentItemIndex because we don't save it by ContentManager.
            taxonomyContentItem.As<TaxonomyPart>().Terms.RemoveAll(_ => true);

            var createTermTasks =
                terms.Select(c => CreateBlogTaxonomyTermContentItem(taxonomyContentItem, termContentType, c, iconClasses));
            var categories = await Task.WhenAll(createTermTasks);
            taxonomyContentItem.Alter<TaxonomyPart>(part => part.Terms.AddRange(categories));
            _session.Save(taxonomyContentItem);
        }

        // Taxonomy term are actual Tag or Category content item
        // Category content type definition
        // https://github.com/OrchardCMS/OrchardCore/blob/main/src/OrchardCore.Themes/TheBlogTheme/Recipes/blog.recipe.json#L515-L556

        // Tag content type definition
        // https://github.com/OrchardCMS/OrchardCore/blob/main/src/OrchardCore.Themes/TheBlogTheme/Recipes/blog.recipe.json#L473-L514
        private async Task<ContentItem> CreateBlogTaxonomyTermContentItem(ContentItem taxonomyContentItem, string termContentType, string title, string iconClass = null)
        {
            var termContentItem = await _contentManager.NewAsync(termContentType);

            // Attach TermPart dynamically because it does not defined when creating Tag content type
            // taxonomyContentItem is a parent or a container which contains this term
            termContentItem.Weld<TermPart>();
            termContentItem.Alter<TermPart>(t => t.TaxonomyContentItemId = taxonomyContentItem.ContentItemId);

            // Alter other parts (TitlePart, PartName) which are defined in blog.recipe.json
            termContentItem.DisplayText = title;
            termContentItem.Alter<TitlePart>(t => t.Title = title);

            // Where default path get transformed
            // https://github.com/OrchardCMS/OrchardCore/blob/main/src/OrchardCore.Modules/OrchardCore.Autoroute/Handlers/AutoroutePartHandler.cs#L402-L403
            var url = $"{pattern.Replace(title.Trim(), "-").ToLower()}";
            termContentItem.Alter<AutoroutePart>(t => t.Path = url);

            // Not all term types have icon classes  
            if (!string.IsNullOrEmpty(iconClass))
            {
                // Fields are attached to a part which has the same name as type.
                // We don't have a strongly type version content part here
                // Defined alter part definition in 
                // https://github.com/OrchardCMS/OrchardCore/blob/main/src/OrchardCore.Themes/TheBlogTheme/Recipes/blog.recipe.json#L774-L794 
                termContentItem.Alter<ContentPart>(termContentType, p =>
                {
                    p.Alter<TextField>("Icon", f => f.Text = iconClass);
                });
            }
            return termContentItem;
        }

        private async Task CreateRecentBlogPostsWidget()
        {
            const string widgetName = "RecentBlogPostsWidget";
            _contentDefinitionManager.AlterTypeDefinition(
                widgetName,
                type => type
                    .WithPart(nameof(RecentBlogPostsPart))
                    .Stereotype("Widget")
                    .Versionable(false) // no version
            );

            // Create a new content item, not save to database yet.
            var contentItem = await _contentManager.NewAsync(widgetName);
            contentItem.DisplayText = widgetName;
            contentItem.Alter<RecentBlogPostsPart>(part => part.MaxResultCount = 5);

            // Show only on home page
            var layerMetaData = new LayerMetadata()
            {
                RenderTitle = false,
                Zone = "Content",
                Layer = "Homepage",
                Position = 1
            };

            // Attach a layer Meta data to a widget content item.
            contentItem.Weld(layerMetaData);
            await _contentManager.CreateAsync(contentItem, VersionOptions.Published);
        }

        private async Task RemoveFooterWidgetAsync()
        {
            // Known issue, existing raw html widget does not have Display text value
            var contentItem = await _session
                .Query<ContentItem, ContentItemIndex>(q => q.ContentType == "RawHtml")
                .FirstOrDefaultAsync();

            await _contentManager.RemoveAsync(contentItem);
        }
    }
}