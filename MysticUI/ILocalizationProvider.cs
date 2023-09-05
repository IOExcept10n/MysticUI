using System.Globalization;

namespace MysticUI
{
    /// <summary>
    /// Represents a service for value localization.
    /// </summary>
    public interface ILocalizationProvider
    {
        /// <summary>
        /// Localizes text into a target culture.
        /// </summary>
        /// <remarks>
        /// Localization should be performed in the following way:
        /// <list type="bullet">
        /// <item>
        /// If value can be localized, method should return localization result.
        /// </item>
        /// <item>
        /// If value have localization in default culture but haven't got localization
        /// for given <paramref name="targetCulture"/>, it should return default localization.
        /// </item>
        /// <item>
        /// If value can't be localized, it should return <paramref name="text"/> parameter value itself.
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="text">Text to localize.</param>
        /// <param name="targetCulture">Target culture info to localize.</param>
        /// <returns>Localized string.</returns>
        public string Localize(string text, CultureInfo targetCulture);
    }
}