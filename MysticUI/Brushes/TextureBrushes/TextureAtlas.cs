using Stride.Core.Mathematics;
using Stride.Graphics;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace MysticUI.Brushes.TextureBrushes
{
    /// <summary>
    /// Represents an atlas that can store multiple texture brushes inside one texture.
    /// </summary>
    public class TextureAtlas
    {
        /// <summary>
        /// Gets the source path to the texture of the atlas.
        /// </summary>
        public string ImageSource { get; init; } = null!;

        /// <summary>
        /// Gets the set of regions used in the atlas.
        /// </summary>
        public Dictionary<string, ImageBrush> Regions { get; } = new();

        /// <summary>
        /// Gets the source texture of the atlas.
        /// </summary>
        public Texture Texture { get; private set; } = null!;

        /// <summary>
        /// Gets the image brush by its name from the atlas.
        /// </summary>
        /// <param name="name">Name of the region.</param>
        /// <returns>Brush to the following region.</returns>
        public ImageBrush this[string name]
        {
            get => Regions[name];
            set
            {
                if (Regions.ContainsKey(name))
                    Regions[name] = value;
                else Regions.Add(name, value);
            }
        }

        /// <summary>
        /// Serializes the atlas into a XML.
        /// </summary>
        public string ToXml()
        {
            var doc = new XDocument();
            var root = new XElement(nameof(TextureAtlas));
            root.SetAttributeValue(nameof(ImageSource), ImageSource);
            doc.Add(root);

            foreach (var pair in Regions)
            {
                var region = pair.Value;

                var entry = LayoutSerializer.Default.Save(region);
                entry.SetAttributeValue(LayoutSerializer.KeyAttribute, pair.Key);
                root.Add(entry);
            }

            return doc.ToString();
        }

        /// <summary>
        /// Loads the atlas from the XML document.
        /// </summary>
        /// <param name="data">Document with the atlas definition.</param>
        /// <param name="textureLoader">Texture loading function.</param>
        /// <returns>Resulting atlas.</returns>
        public static TextureAtlas Load(string data, Func<string, Texture> textureLoader)
        {
            try
            {
                var xDoc = XDocument.Parse(data);
                return FromXml(xDoc.Root!, textureLoader);
            }
            catch
            {
                return FromGdx(data, textureLoader);
            }
        }

        /// <summary>
        /// Gets the atlas from the XML node.
        /// </summary>
        /// <param name="document">An element with the atlas definition.</param>
        /// <param name="textureLoader">Texture loading function.</param>
        /// <returns>Resulting atlas.</returns>
        /// <exception cref="ArgumentException"></exception>
        public static TextureAtlas FromXml(XElement document, Func<string, Texture> textureLoader)
        {
            XAttribute imageSource = document.Attribute(nameof(ImageSource)) ?? throw new ArgumentException("Image source for the atlas doesn't exist.");
            TextureAtlas result = new()
            {
                ImageSource = imageSource.Value,
                Texture = textureLoader(imageSource.Value)
            };
            foreach (XElement entry in document.Elements())
            {
                string key = entry.Attribute(LayoutSerializer.KeyAttribute)!.Value!;
                ImageBrush region = LayoutSerializer.Default.LoadLayout<ImageBrush>(entry);
                region.Texture = result.Texture;
                result[key] = region;
            }
            return result;
        }

#pragma warning disable

        public static TextureAtlas FromGdx(string data, Func<string, Texture> textureLoader)
        {
            // Code here was copied from the Myra Gdx.cs file: https://github.com/rds1983/Myra/blob/master/src/Myra/Graphics2D/TextureAtlases/Gdx.cs
            Texture? pageData = null;
            var spriteDatas = new Dictionary<string, GDXSpriteData>();
            using (var textReader = new StringReader(data))
            {
                GDXSpriteData? spriteData = null;
                while (true)
                {
                    var s = textReader.ReadLine();
                    if (s == null)
                    {
                        break;
                    }

                    s = s.Trim();
                    if (string.IsNullOrEmpty(s))
                    {
                        // New PageData
                        pageData = null;
                        continue;
                    }

                    if (pageData == null)
                    {
                        pageData = textureLoader(s) ?? throw new Exception($"Unable to resolve texture {s}");
                        continue;
                    }

                    if (!s.Contains(":"))
                    {
                        spriteData = new GDXSpriteData
                        {
                            Texture = pageData,
                            Name = s
                        };

                        spriteDatas[s] = spriteData;
                        continue;
                    }

                    var parts = s.Split(':');

                    var key = parts[0].Trim().ToLower();
                    var value = parts[1].Trim();

                    switch (key)
                    {
                        case "format":
                            break;

                        case "filter":
                            break;

                        case "repeat":
                            break;

                        case "rotate":
                            bool isRotated;
                            bool.TryParse(value, out isRotated);
                            spriteData.IsRotated = isRotated;
                            break;

                        case "xy":
                            parts = value.Split(',');
                            spriteData.SourceRectangle.X = int.Parse(parts[0].Trim());
                            spriteData.SourceRectangle.Y = int.Parse(parts[1].Trim());

                            break;

                        case "size":
                            if (spriteData == null)
                            {
                                continue;
                            }

                            parts = value.Split(',');
                            spriteData.SourceRectangle.Width = int.Parse(parts[0].Trim());
                            spriteData.SourceRectangle.Height = int.Parse(parts[1].Trim());

                            break;

                        case "orig":
                            parts = value.Split(',');
                            spriteData.OriginalSize = new Point(int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));

                            break;

                        case "offset":
                            parts = value.Split(',');
                            spriteData.Offset = new Point(int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));
                            break;

                        case "split":
                            parts = value.Split(',');
                            var split = new Thickness
                            {
                                Left = int.Parse(parts[0].Trim()),
                                Right = int.Parse(parts[1].Trim()),
                                Top = int.Parse(parts[2].Trim()),
                                Bottom = int.Parse(parts[3].Trim())
                            };

                            spriteData.Split = split;
                            break;
                    }
                }
            }

            var result = new TextureAtlas();
            var regions = result.Regions;
            foreach (var sd in spriteDatas)
            {
                var texture = sd.Value.Texture;
                var bounds = sd.Value.SourceRectangle;

                ImageBrush region;
                if (!sd.Value.Split.HasValue)
                {
                    region = new ImageBrush(texture, bounds);
                }
                else
                {
                    region = new NinePatchImageBrush(texture, bounds, sd.Value.Split.Value);
                }

                regions[sd.Key] = region;
            }

            return result;
        }

        private class GDXSpriteData
        {
            public Texture Texture { get; set; }
            public string Name { get; set; }
            public Rectangle SourceRectangle;
            public bool IsRotated { get; set; }
            public Thickness? Split;
            public Point OriginalSize;
            public Point Offset;
        }
    }

    public class TextureAtlasCollection : Collection<TextureAtlas>
    {
        public TextureAtlas Default
        {
            get => this[0];
            set => this[0] = value;
        }

        public ImageBrush? this[string key]
        {
            get
            {
                foreach (var atlas in this)
                {
                    if (atlas.Regions.TryGetValue(key, out ImageBrush? result)) return result;
                }
                return null;
            }
        }
    }

#pragma warning restore
}