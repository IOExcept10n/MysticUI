namespace MysticUI.Extensions.Content
{
    /// <summary>
    /// An interface which represents context to load assets.
    /// </summary>
    public interface IAssetContext
    {
        /// <summary>
        /// Path of the root directory for the loading context.
        /// </summary>
        public string RootPath { get; }
    }
}