namespace Net.NowhereAtAll.Stipulate.Integration.Test;

/// <summary>How a program references a library that references Stipulate.</summary>
public enum LibraryReference
{
    /// <summary>A ProjectReference to the library.</summary>
    Project,

    /// <summary>
    /// A PackageReference to the packed library, whose dependency on Stipulate excludes its build
    /// and analyzer assets, as a PackageReference's defaults do.
    /// </summary>
    Package,
}
