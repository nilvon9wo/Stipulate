namespace ParameterValidation.Weaver;

/// <summary>Replaces a file by writing a temporary one beside it and moving that over it.</summary>
internal static class AtomicFile
{
    private const string TemporarySuffix = ".weaving";

    public static void Replace(string path, MemoryStream contents)
    {
        string temporaryPath = path + TemporarySuffix;
        try
        {
            File.WriteAllBytes(temporaryPath, contents.ToArray());
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
