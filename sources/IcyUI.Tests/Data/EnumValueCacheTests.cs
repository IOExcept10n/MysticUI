using Icy.Data;
using Xunit;

namespace Icy.Tests.Data
{
    public class EnumValueCacheTests
    {
        private enum Suit { Clubs, Diamonds, Hearts, Spades }

        [Fact]
        public void GetValues_ReturnsAllEnumValuesInDeclarationOrder()
        {
            Array values = EnumValueCache.GetValues(typeof(Suit));

            Assert.Equal(new object[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades }, values.Cast<object>());
        }

        [Fact]
        public void GetValues_CalledTwiceForSameType_ReturnsCachedInstance()
        {
            Array first = EnumValueCache.GetValues(typeof(Suit));
            Array second = EnumValueCache.GetValues(typeof(Suit));

            Assert.Same(first, second);
        }

        [Fact]
        public void GetValues_NonEnumType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => EnumValueCache.GetValues(typeof(string)));
        }
    }
}
