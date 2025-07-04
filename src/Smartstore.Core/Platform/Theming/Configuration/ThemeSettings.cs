﻿using Smartstore.Core.Configuration;

namespace Smartstore.Core.Theming
{
    public class ThemeSettings : ISettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether
        /// asset bundling is enabled
        /// </summary>
        /// <value>
        /// 0: Auto (decide based on debug mode) > default
        /// 1: Disabled
        /// 2: Enabled
        /// </value>
        public int BundleOptimizationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether
        /// asset caching is enabled
        /// </summary>
        /// <value>
        /// 0 = Auto (decide based on debug mode)
        /// 1 = Disabled
        /// 2 = Enabled > default
        /// </value>
        public int AssetCachingEnabled { get; set; } = 2;

        /// <summary>
        /// Gets or sets a default store theme for desktops
        /// </summary>
        public string DefaultTheme { get; set; } = "Yellow";

        /// <summary>
        /// Gets or sets a value indicating whether customers are allowed to select a theme
        /// </summary>
        public bool AllowCustomerToSelectTheme { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether user's theme choice should be saved in a cookie
        /// </summary>
        /// <remarks>
        /// If <c>false</c>, user's theme choice is associated to the account, 
        /// which may be undesirable when, for example, multiple users share a guest account.
        /// </remarks>
        public bool SaveThemeChoiceInCookie { get; set; }

    }
}
