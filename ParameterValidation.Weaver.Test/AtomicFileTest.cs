namespace Net.NowhereAtAll.ParameterValidation.Weaver.Test;

/// <summary>Replacing a file through a temporary one beside it, which never outlives the replacement.</summary>
public sealed class AtomicFileTest : IDisposable
{
    private const string TemporarySuffix = ".weaving";

    private readonly string _directory = Directory.CreateTempSubdirectory("ParameterValidation.Weaver.Test.").FullName;

    [Fact]
    public void Replace_WhenTheFileExists_ReplacesItsContents()
    {
        // Arrange
        string path = Path.Combine(this._directory, "Test.dll");
        File.WriteAllText(path, "original");
        using MemoryStream contents = new("replaced"u8.ToArray());

        // Act
        AtomicFile.Replace(path, contents);

        // Assert
        Assert.Equal("replaced", File.ReadAllText(path));
        Assert.False(File.Exists(path + TemporarySuffix));
    }

    [Fact]
    public void Replace_WhenTheFileCannotBeReplaced_LeavesNoTemporaryFile()
    {
        // Arrange - a directory can't be replaced by a file
        string path = Directory.CreateDirectory(Path.Combine(this._directory, "Test.dll")).FullName;
        byte[] replacement = "replaced"u8.ToArray();

        // Act
        Exception exception = Record.Exception(() => ReplaceWith(path, replacement))!;

        // Assert
        Assert.True(exception is IOException or UnauthorizedAccessException, exception.ToString());
        Assert.False(File.Exists(path + TemporarySuffix));
    }

    public void Dispose() => Directory.Delete(this._directory, recursive: true);

    private static void ReplaceWith(string path, byte[] replacement)
    {
        using MemoryStream contents = new(replacement);
        AtomicFile.Replace(path, contents);
    }
}
