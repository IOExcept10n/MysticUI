using Icy.Data;
using Xunit;

namespace Icy.Tests.Data
{
    public class TypeConversionManagerTests
    {
        private static readonly Type[] NumericTypes =
            [typeof(int), typeof(long), typeof(short), typeof(byte), typeof(float), typeof(double), typeof(uint), typeof(ushort), typeof(sbyte)];

        [Fact]
        public void Convert_FromManyThreadsAtOnce_NeverCorruptsTheConverterCache()
        {
            // Regression: PropertyRegistry.Default shares one TypeConversionManager across every thread (e.g. animations
            // created on parallel test threads), and its converter cache was a plain Dictionary - concurrent first-time
            // inserts corrupted it ("Operations that change non-concurrent collections must have exclusive access").
            (object Value, Type Target)[] work = [.. NumericTypes.SelectMany(source => NumericTypes.Select(target => (System.Convert.ChangeType(1, source), target)))];

            for (int trial = 0; trial < 300; trial++)
            {
                var manager = new TypeConversionManager();
                Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = 8 }, item =>
                    Assert.Equal(1m, System.Convert.ToDecimal(manager.Convert(item.Value, item.Target))));
            }
        }
    }
}
