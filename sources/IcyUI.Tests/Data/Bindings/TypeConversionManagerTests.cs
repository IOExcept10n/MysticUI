// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using System.Drawing;
using System.Globalization;
using Xunit;

namespace Icy.Tests.Data.Bindings
{
    public class TypeConversionManagerTests
    {
        [Theory]
        [InlineData(3, typeof(int))]
        [InlineData(4, typeof(object))]
        [InlineData(5.2f, typeof(float))]
        [InlineData(3.14, typeof(double))]
        [InlineData(3.14, typeof(ValueType))]
        [InlineData(3.14, typeof(IParsable<double>))]
        public void Test_DirectConversion_ResultEquals(object argument, Type targetType)
        {
            // Arrange
            var converter = new TypeConversionManager();
            // Act
            var result = converter.Convert(argument, targetType);
            // Assert
            Assert.Equal(argument, result);
        }

        [Theory]
        [MemberData(nameof(GetTestConvertersData))]
        public void Test_IValueConversion_AsExpected(object argument, Type targetType, object expectedResult)
        {
            // Arrange
            var converter = new TypeConversionManager();
            converter.RegisterConverter(new TestDecimalConverter());
            converter.RegisterConverter(new TestPointConverter());
            converter.RegisterConverter(new TestRectangleConverter());

            // Act
            var result = converter.Convert(argument, targetType);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(1, typeof(float), 1f)]
        [InlineData(0.2, typeof(int), 0)]
        [InlineData(12.1, typeof(byte), (byte)12)]
        [InlineData(byte.MinValue, typeof(int), 0)]
        [InlineData(float.E, typeof(double), (double)float.E)]
        [InlineData((byte)14, typeof(short), (short)14)]
        public void Test_CastConversion_AsManual(object argument, Type targetType, object expectedResult)
        {
            // Arrange
            var converter = new TypeConversionManager();

            // Act
            var result = converter.Convert(argument, targetType);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GetPrimitiveTestsData))]
        public void Test_PrimitiveTypeConversion_AsConvertChangeType(object argument, Type targetType, object expectedResult)
        {
            // Arrange
            var converter = new TypeConversionManager();

            // Act
            var result = converter.Convert(argument, targetType);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GetStringRepresentations))]
        public void Test_ToStringConversion_AsToString(object argument, string expectedResult)
        {
            // Arrange
            var converter = new TypeConversionManager();

            // Act
            var result = converter.Convert(argument, typeof(string)) as string;

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GetParsingData))]
        public void Test_Parsing_AsTypeParse(string argument, Type targetType, object expectedResult)
        {
            // Arrange
            var converter = new TypeConversionManager();

            // Act
            var result = converter.Convert(argument, targetType);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GetTypeConversionData))]
        public void Test_TypeConversion_AsExpected(object argument, Type targetType, object expectedResult)
        {
            // Arrange
            var converter = new TypeConversionManager();

            // Act
            var result = converter.Convert(argument, targetType);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void Test_InvalidConversion_ThrowsFormatException()
        {
            // Arrange
            var converter = new TypeConversionManager();
            var invalidInput = "invalid";

            // Act & Assert
            Assert.Throws<FormatException>(() => converter.Convert(invalidInput, typeof(int)));
        }

        [Fact]
        public void Test_InvalidConversion_ThrowsInvalidCastException()
        {
            // Arrange
            var converter = new TypeConversionManager();
            var invalidInput = false;

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => converter.Convert(invalidInput, typeof(DateTime)));
        }

        public static TheoryData<object, Type, object> GetTypeConversionData()
        {
            return new TheoryData<object, Type, object>
            {
                { "Red", typeof(Color), Color.Red },
                { "3, 2", typeof(Point), new Point(3, 2) },
                { "2, 4, 10, 30", typeof(Rectangle), new Rectangle(2, 4, 10, 30) }
            };
        }

        public static TheoryData<string, Type, object> GetParsingData()
        {
            return new TheoryData<string, Type, object>
            {
                { "42", typeof(int), 42 },
                { "3.14", typeof(float), 3.14f },
                { "04/22/2024", typeof(DateTime), new DateTime(2024, 04, 22) },
                { "false", typeof(bool), false },
                { "Wednesday", typeof(DayOfWeek), DayOfWeek.Wednesday }
            };
        }

        public static TheoryData<object, string> GetStringRepresentations()
        {
            return new TheoryData<object, string>
            {
                { 123, "123" },
                { 12.34, "12.34" },
                { new DateTime(2020, 11, 23), new DateTime(2020, 11, 23).ToString(CultureInfo.InvariantCulture) },
                { true, "True" },
                { new int[2], "System.Int32[]" },
                { 1..3, "1..3" },
                { ConsoleColor.Gray, "Gray" }
            };
        }

        public static TheoryData<object, Type, object> GetPrimitiveTestsData()
        {
            var results = new TheoryData<object, Type, object>
            {
                { 42, typeof(bool), null! },
                { false, typeof(byte), null! },
                { new DateTime(2020, 01, 01), typeof(string), null! },
                { true, typeof(double), null! }
            };

            foreach (var item in results)
            {
                item[2] = Convert.ChangeType(item[0], (Type)item[1], CultureInfo.InvariantCulture);
            }

            return results;
        }

        public static TheoryData<object, Type, object> GetTestConvertersData()
        {
            return new TheoryData<object, Type, object>
            {
                { (2, 3), typeof(Point), new Point(2, 3) },
                { (0, 0), typeof(Point), new Point(0, 0) },
                { (16, -1), typeof(Point), new Point(16, -1) },
                { "1,2,3,4", typeof(Rectangle), new Rectangle(1, 2, 3, 4) },
                { "0,1,30,20", typeof(Rectangle), new Rectangle(0, 1, 30, 20) },
                { 6M, typeof(int), 6 },
                { 0.01M, typeof(int), 0 }
            };
        }

        private class TestPointConverter : IValueConverter<(int, int), Point>
        {
            public Point Convert((int, int) value)
            {
                return new(value.Item1, value.Item2);
            }
        }

        private class TestRectangleConverter : IValueConverter<string, Rectangle>
        {
            public Rectangle Convert(string value)
            {
                string[] parts = value.Split(',');
                int x = int.Parse(parts[0]),
                    y = int.Parse(parts[1]),
                    width = int.Parse(parts[2]),
                    height = int.Parse(parts[3]);
                return new(x, y, width, height);
            }
        }

        private class TestDecimalConverter : IValueConverter<decimal, int>
        {
            public int Convert(decimal value)
            {
                return (int)value;
            }
        }
    }
}
