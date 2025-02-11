using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icy.Assets;
using Microsoft.Xna.Framework;

namespace Icy.MonoGame.Assets
{
    internal class MonoGameAssetContextFactory(Game game) : IAssetContextFactory
    {
        private const string FileScheme = "file";
        private const string AssemblyResourceScheme = "icy-res";
        private const string AssetScheme = "icy-asset";

        public IAssetContext Create(Uri uri)
        {

        }
    }
}
