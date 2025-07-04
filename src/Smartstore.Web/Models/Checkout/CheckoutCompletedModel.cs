﻿using Smartstore.Core.Checkout.Orders;

namespace Smartstore.Web.Models.Checkout
{
    public partial class CheckoutCompletedModel : ModelBase
    {
        public Order Order { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string ByGroundAddress { get; set; }
        public string ByGroundLatitude { get; set; }
        public string ByGroundLongitude { get; set; }
        public string OrderPin { get; set; }
    }
}
