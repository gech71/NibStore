using Smartstore.Core.Content.Media;
using System.ComponentModel.DataAnnotations;
namespace Smartstore.Admin.Models.MerchantStores
{
    [LocalizedDisplay("Admin.Catalog.MerchantStores.Fields.")]
    public class MerchantStoreModel : EntityModelBase
    {
        [LocalizedDisplay("*Name")]
        public string Name { get; set; }

        [LocalizedDisplay("Common.DisplayOrder")]
        public int DisplayOrder { get; set; }

        [LocalizedDisplay("*Address")]
        public string Address { get; set; }

        [LocalizedDisplay("*OpeningHours")]
        public string OpeningHours { get; set; }

        [LocalizedDisplay("*OpeningTime")]
        public string OpeningTime { get; set; }

        [LocalizedDisplay("*ClosingTime")]
        public string ClosingTime { get; set; }

        [LocalizedDisplay("*Published")]
        public bool Published { get; set; }

        [LocalizedDisplay("*Picture")]
        [UIHint("Media")]
        [AdditionalMetadata("album", "content")]
        public int? MediaFileId { get; set; }

        [LocalizedDisplay("*SearchMerchantStoreName")]
        public string SearchMerchantStoreName { get; set; }

        public string EditUrl { get; set; }

    }
}