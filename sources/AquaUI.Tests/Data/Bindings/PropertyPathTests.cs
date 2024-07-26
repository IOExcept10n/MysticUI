// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data.Bindings;

namespace AquaUI.Tests.Data.Bindings
{
    public class PropertyPathTests
    {
        public static TheoryData<object, string, object> GenerateMixedTestData()
        {
            return new()
            {
                { new string[] {"Hello", "Wonderful", "World"}, "this[0].Length", 5 },
                { new Uri("https://example.com/"), $"{nameof(Uri.Host)}.this[1]", 'x'},
                { new FileInfo("C:\\TestDirectory\\TestFile"), $"{nameof(FileInfo.Directory)}.{nameof(DirectoryInfo.Name)}[3]", 't' },
                { new List<string>() { "Hello", "this", "beautiful", "world!"}, "this[0].this[4]", 'o' }
            };
        }

        public static TheoryData<object, string, object> GeneratePropertyChainData()
        {
            return new()
            {
                { new ArgumentException("Test"), $"{nameof(Exception.Message)}.{nameof(string.Length)}", 4 },
                { new Uri("https://dot.net"), $"{nameof(Uri.Host)}.{nameof(string.Length)}", 7 },
                { new FileInfo("C:\\RandomDirectory\\OtherDirectory\\RandomFileName"), $"{nameof(FileInfo.Directory)}.{nameof(DirectoryInfo.Parent)}.{nameof(DirectoryInfo.Root)}.{nameof(DirectoryInfo.Exists)}", true },
            };
        }

        public static TheoryData<object, string, object> GenerateSimpleIndexerTestData()
        {
            return new()
            {
                { "Hello", "this[0]", 'H' },
                { new int[] {1, 2, 3}, "this[2]", 3 },
                { new List<int>() {1, 2, 3}, "this[1]", 2 },
                { new Dictionary<string, int>() { { "One", 1 }, { "Two", 2} }, "this[\"One\"]", 1 }
            };
        }

        public static TheoryData<object, string, object> GenerateSimplePathTestData()
        {
            return new()
            {
                { "Hello", nameof(string.Length), 5 },
                { new DateTime(2000, 6, 3), nameof(DateTime.Year), 2000 },
                { new DateTime(2000, 4, 1), nameof(DateTime.Day), 1 },
                { new TimeSpan(16, 12, 11), nameof(TimeSpan.TotalSeconds), (double)(16 * 3600 + 12 * 60 + 11) },
                { new int[] { 1, 2 }, nameof(Array.Length), 2 },
            };
        }

        public static TheoryData<Type, string> GenerateWrongIndexerNames()
        {
            return new()
            {
                { typeof(string), "this[Hello]" },
                { typeof(string), "this[1, 2]" },
                { typeof(int), "this[-100]" },
                { typeof(DateTime), "this[from now on...and forever..." },
                { typeof(string), "this[0][1]" },
                { typeof(List<string>), "this[0][0][1]" }
            };
        }

        public static TheoryData<Type, string> GenerateWrongPropertyNames()
        {
            return new()
            {
                { typeof(string), "Property" },
                { typeof(int), "Length" },
                { typeof(Uri), "Uri" },
                { typeof(double), "PositiveInfinity" },
                { typeof(DateTime), "Now" }, // This property is static so it won't work
            };
        }

        public static TheoryData<object, string, object> GenerateMutableObjects()
        {
            return new()
            {
                { new HttpClient(), nameof(HttpClient.BaseAddress), new Uri("https://example.com/") }
            };
        }

        [Theory]
        [MemberData(nameof(GenerateSimpleIndexerTestData))]
        public void Test_DynamicIndexerPath_GetsValue(object target, string path, object expectedResult)
        {
            // Arrange
            var pathInfo = new DynamicPropertyPath(path);

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GenerateMutableObjects))]
        public void Test_DynamicIndexerPath_SetsValue(object target, string path, object value)
        {
            // Arrange
            var pathInfo = new DynamicPropertyPath(path);

            // Act
            pathInfo.SetValue(target, value);

            // Assert
            var actual = pathInfo.GetValue(target);
            Assert.Equal(value, actual);
        }

        [Theory]
        [MemberData(nameof(GenerateMixedTestData))]
        public void Test_DynamicMixedPath_GetsValue(object target, string path, object expectedResult)
        {
            // Arrange
            var pathInfo = new DynamicPropertyPath(path);

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GeneratePropertyChainData))]
        public void Test_DynamicPropertyChainPath_GetsValue(object target, string path, object expectedResult)
        {
            // Arrange
            var pathInfo = new DynamicPropertyPath(path);

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GenerateWrongIndexerNames))]
        public void Test_Indexer_ThrowsWrongIndexerFormat(Type targetType, string indexerCall)
        {
            // Arrange & Act & Assert
            Assert.Throws<FormatException>(() => new PropertyPath(indexerCall, targetType));
        }

        [Theory]
        [MemberData(nameof(GenerateSimpleIndexerTestData))]
        public void Test_IndexerPath_GetsValue(object target, string path, object expectedResult)
        {
            // Arrange
            var pathInfo = new PropertyPath(path, target.GetType());

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GenerateMixedTestData))]
        public void Test_MixedPath_GetsValue(object target, string path, object expectedResult)
        {
            // Arrange
            var pathInfo = new PropertyPath(path, target.GetType());

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GeneratePropertyChainData))]
        public void Test_PropertyChainPath_GetsValue(object target, string path, object expectedResult)
        {
            // Arrange
            var pathInfo = new PropertyPath(path, target.GetType());

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GenerateWrongPropertyNames))]
        public void Test_SimpleDynamicPath_ReturnsNull(Type targetType, string propertyName)
        {
            // Arrange
            var pathInfo = new DynamicPropertyPath(propertyName);
            object testInstance = CreateTestInstance(targetType);

            // Act & Assert
            var result = pathInfo.GetValue(testInstance);

            // Assert
            // Somehow, the dynamic type traverser recognizes wrong paths as null.
            Assert.Null(result);
        }

        [Theory]
        [MemberData(nameof(GenerateSimplePathTestData))]
        public void Test_SimpleDynamicPropertyPath_GetsValue(object target, string path, object expectedResult)
        {
            // Arrange
            var pathInfo = new DynamicPropertyPath(path);

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GenerateWrongPropertyNames))]
        public void Test_SimplePath_ThrowsPropertyNotFound(Type targetType, string propertyName)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => new PropertyPath(propertyName, targetType));
        }

        [Theory]
        [MemberData(nameof(GenerateSimplePathTestData))]
        public void Test_SimplePropertyPath_GetsValue(object target, string path, object expectedResult)
        {
            // Arrange
            var pathInfo = new PropertyPath(path, target.GetType());

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [MemberData(nameof(GenerateMutableObjects))]
        public void Test_SimplePropertyPath_SetsValue(object target, string path, object value)
        {
            // Arrange
            var pathInfo = new PropertyPath(path, target.GetType());

            // Act
            pathInfo.SetValue(target, value);

            // Assert
            var actual = pathInfo.GetValue(target);
            Assert.Equal(value, actual);
        }

        [Theory]
        [MemberData(nameof(GenerateWrongIndexerNames))]
        public void Test_WrongDynamicIndexer_ReturnsNull(Type targetType, string indexerCall)
        {
            // Arrange
            var pathInfo = new DynamicPropertyPath(indexerCall);
            object testInstance = CreateTestInstance(targetType);

            // Act
            var result = pathInfo.GetValue(testInstance);

            Assert.Null(result);
        }

        [Theory]
        [MemberData(nameof(GenerateSimplePathTestData))]
        public void Test_CompiledSimplePath_GetsValue(object target, string path, object expectedValue)
        {
            // Arrange
            var pathInfo = new CompiledPropertyPath(path, target.GetType());

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [MemberData(nameof(GenerateMutableObjects))]
        public void Test_CompiledSimplePath_SetsValue(object target, string path, object value)
        {
            // Arrange
            var pathInfo = new CompiledPropertyPath(path, target.GetType());

            // Act
            pathInfo.SetValue(target, value);

            // Assert
            var actual = pathInfo.GetValue(target);
            Assert.Equal(value, actual);
        }

        [Theory]
        [MemberData(nameof(GenerateSimpleIndexerTestData))]
        public void Test_CompiledSimpleIndexer_GetsValue(object target, string path, object expectedValue)
        {
            // Arrange
            var pathInfo = new CompiledPropertyPath(path, target.GetType());

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [MemberData(nameof(GeneratePropertyChainData))]
        public void Test_CompiledPropertyChain_GetsValue(object target, string path, object expectedValue)
        {
            // Arrange
            var pathInfo = new CompiledPropertyPath(path, target.GetType());

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [MemberData(nameof(GenerateMixedTestData))]
        public void Test_CompiledMixedData_GetsValue(object target, string path, object expectedValue)
        {
            // Arrange
            var pathInfo = new CompiledPropertyPath(path, target.GetType());

            // Act
            var result = pathInfo.GetValue(target);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        private static object CreateTestInstance(Type targetType)
        {
            object testInstance;
            if (targetType == typeof(string))
                testInstance = string.Empty;
            else if (targetType == typeof(Uri))
                testInstance = new Uri("https://example.com");
            else
                testInstance = Activator.CreateInstance(targetType)!;
            return testInstance;
        }
    }
}