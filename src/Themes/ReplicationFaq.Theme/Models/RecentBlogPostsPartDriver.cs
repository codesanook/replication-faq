using System.ComponentModel.DataAnnotations;
using OrchardCore.ContentManagement;

namespace ReplicationFaq.Theme.Models
{
    public class RecentBlogPostsPart : ContentPart
    {
        [Required]
        public int Count { get; set; }
    }
}