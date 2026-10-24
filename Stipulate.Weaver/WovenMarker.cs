using Mono.Cecil;

namespace Net.NowhereAtAll.Stipulate.Weaver;

/// <summary>
/// A type added to every woven assembly so that an incremental build, which skips compilation and
/// leaves the already-woven assembly in place, doesn't insert the checks a second time.
/// </summary>
internal static class WovenMarker
{
    public const string TypeName = "<StipulateWoven>";

    public static bool IsWoven(ModuleDefinition module) => module.Types.Any(static type => type.Name == TypeName);

    public static void Mark(ModuleDefinition module)
        => module.Types.Add(
            new TypeDefinition(
                string.Empty,
                TypeName,
                TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed,
                module.TypeSystem.Object
            )
        );
}
