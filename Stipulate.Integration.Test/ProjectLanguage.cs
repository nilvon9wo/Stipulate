namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>The .NET language a consumer project is written in.</summary>
public enum ProjectLanguage
{
    /// <summary>C#, in a .csproj: the language Stipulate is for.</summary>
    CSharp,

    /// <summary>Visual Basic, in a .vbproj.</summary>
    VisualBasic,

    /// <summary>F#, in a .fsproj, which lists its source files in the order they compile.</summary>
    FSharp,
}
