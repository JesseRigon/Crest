using Grove.Services;
using Xunit;

namespace Grove.Infrastructure.Server.Tests;

public sealed class RecordNumberingTests
{
    [Fact]
    public void ANumberIsItsPrefixAndTheZeroPaddedSequence()
    {
        Assert.Equal("INV-00042", IRecordNumbering.Format("INV-", 42));
        Assert.Equal("Q-7", IRecordNumbering.Format("Q-", 7, digits: 1));
        Assert.Equal("123456", IRecordNumbering.Format(string.Empty, 123456));
    }
}
