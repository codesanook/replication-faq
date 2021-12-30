using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;
using ReplicationFaq.Theme.Models;
using ReplicationFaq.Theme.ViewModels;
using YesSql;

namespace ReplicationFaq.Theme.Drivers
{
    public class RecentBlogPostsPartDisplayDriver : ContentPartDisplayDriver<RecentBlogPostsPart>
    {
        private readonly ILogger _logger;
        private readonly ISiteService _siteService;
        private readonly ISession _session;
        private readonly dynamic New;
        private readonly IContentItemDisplayManager _contentItemDisplayManager;
        private readonly IUpdateModelAccessor _updateModelAccessor;
        private readonly IContentManager _contentManager;

        public RecentBlogPostsPartDisplayDriver(
            ILogger<RecentBlogPostsPartDisplayDriver> logger,
            ISiteService siteService,
            ISession session,
            IShapeFactory shapeFactory,
            IUpdateModelAccessor updateModelAccessor,
            IContentItemDisplayManager contentItemDisplayManager,
            IContentManager contentManager
        )
        {
            _logger = logger;
            _siteService = siteService;
            _session = session;
            New = shapeFactory;
            _updateModelAccessor = updateModelAccessor;
            _contentItemDisplayManager = contentItemDisplayManager;
            _contentManager = contentManager;
        }

        public override async Task<IDisplayResult> DisplayAsync(RecentBlogPostsPart part, BuildPartDisplayContext context)
        {
            part.MaxResultCount = 5;
            var blogPostQuery = _session
                .Query<ContentItem>()
                .With<ContentItemIndex>(q => q.ContentType == "BlogPost" && q.Published && q.Latest)
                .OrderByDescending(index => index.CreatedUtc); // Start by in comming events

            var totalBlogPostCount = await blogPostQuery.CountAsync();
            var blogPosts = await blogPostQuery
                .Take(part.MaxResultCount)
                .ListAsync();

            var shapeTasks = blogPosts.Select(
                b => _contentItemDisplayManager.BuildDisplayAsync(b, _updateModelAccessor.ModelUpdater, "Summary")
            );

            var blogPostShapes = await Task.WhenAll(shapeTasks);
            var listShape = await New.List(); // Defined in src/OrchardCore/OrchardCore.DisplayManagement/Shapes/CoreShapes.cs
            foreach (var shape in blogPostShapes)
            {
                await listShape.AddAsync(shape); // Defined in src/OrchardCore/OrchardCore.DisplayManagement/Shapes/Shape.cs
            }

            var viewModel = new RecentBlogPostsViewModel() { ListShape = listShape, ShowViewMore = totalBlogPostCount > part.MaxResultCount };
            return View("RecentBlogPostsPart", viewModel).Location("Detail", "Content:0");
        }
    }
}