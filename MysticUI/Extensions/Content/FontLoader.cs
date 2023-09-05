using FontStashSharp;
using MysticUI.Brushes.TextureBrushes;

namespace MysticUI.Extensions.Content
{
    internal class FontLoader : IStringSerializer
    {
        private readonly IEnvironmentSettings settings = EnvironmentSettingsProvider.EnvironmentSettings;

        public bool CanParse(Type type)
        {
            return type.IsAssignableTo(typeof(SpriteFontBase));
        }

        public object Parse(Type targetType, string value)
        {
            if (value.Contains(".fnt", StringComparison.InvariantCultureIgnoreCase))
            {
                // Load static sprite font.
                return StaticSpriteFont.FromBMFont(settings.DefaultAssetsResolver.ReadFile(settings.DefaultAssets.DefaultAssetContext!, value), name =>
                {
                    var image = settings.DefaultAssetsResolver.LoadAsset<ImageBrush>(settings.DefaultAssets.DefaultAssetContext!, value);
                    return new TextureWithOffset(image.Texture, image.Bounds.Location);
                });
            }
            else
            {
                // Load dynamic sprite font.
                value = value.Trim(' ', '[', ']', '{', '}');
                int splitterIndex = value.IndexOf(',');
                string path = value[..splitterIndex];
                int size = int.Parse(value[(splitterIndex + 1)..]);
                var fontSystem = settings.DefaultAssetsResolver.LoadAsset<FontSystem>(settings.DefaultAssets.DefaultAssetContext!, path);
                return fontSystem.GetFont(size);
            }
        }

        public string Serialize(object value)
        {
            throw new NotSupportedException();
        }
    }
}