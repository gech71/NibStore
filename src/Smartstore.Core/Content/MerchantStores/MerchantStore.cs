using System;
using System.ComponentModel.DataAnnotations.Schema;
using Smartstore.Core.Common;
using Smartstore.Core.Localization;

namespace Smartstore.Core.Content.MerchantStores
{
    [Table("MerchantStore")]
    public class MerchantStore : BaseEntity, ILocalizedEntity, IDisplayOrder
    {
        public string Name { get; set; }
        public int DisplayOrder { get; set; }
        public string Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string OpeningHours { get; set; }
        public TimeSpan? OpeningTime { get; set; }
        public TimeSpan? ClosingTime { get; set; }
        public bool Published { get; set; }
        public int MediaFileId { get; set; }
    }
}