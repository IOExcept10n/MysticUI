namespace AquaUI.Assets
{
    /// <summary>
    /// An interface which represents context to load assets.
    /// </summary>
    public interface IAssetContext
    {
        /// <summary>
        /// Gets path of the root directory for the loading context.
        /// </summary>
        public string RootPath { get; }
    }
}
