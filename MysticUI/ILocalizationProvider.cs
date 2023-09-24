using System.Globalization;

namespace MysticUI
{
    /// <summary>
    /// Represents a service for value localization.
    /// </summary>
    public interface ILocalizationProvider
    {
        /// <summary>
        /// Localizes text into a target culture. It should use the <see cref="CultureInfo.CurrentUICulture"/> as the default mode to localize.
        /// </summary>
        /// <remarks>
        /// Localization should be performed in the following way:
        /// <list type="bullet">
        /// <item>
        /// If value can be localized, method should return localization result.
        /// </item>
        /// <item>
        /// If value can't be localized, it should return <paramref name="text"/> parameter value itself.
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="text">Text to localize.</param>
        /// <returns>Localized string.</returns>
        public string Localize(string text);
    }
}