using System;
using System.IO;
using Radish.Common.CoreTool;
using Xunit;

namespace Radish.Api.Tests.Common;

public class PathContainmentToolTest
{
    [Fact]
    public void IsSameOrDescendant_ShouldProtectChunkRootWithoutMatchingSiblingPrefix()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "radish-temp-policy");
        var chunkRoot = Path.Combine(tempRoot, "Chunks");

        Assert.True(PathContainmentTool.IsSameOrDescendant(chunkRoot, chunkRoot));
        Assert.True(PathContainmentTool.IsSameOrDescendant(
            chunkRoot,
            Path.Combine(chunkRoot, "session", "chunk_0")));
        Assert.False(PathContainmentTool.IsSameOrDescendant(
            chunkRoot,
            Path.Combine(tempRoot, "Chunks-old", "file.tmp")));
        Assert.False(PathContainmentTool.IsSameOrDescendant(
            chunkRoot,
            Path.Combine(tempRoot, "attachment.tmp")));
    }
}
