using Mono.Cecil;

namespace Net.NowhereAtAll.Stipulate.Weaver.Test;

/// <summary>
/// The emitters' own refusals. A compiler only stores constants and members the weaver supports,
/// so these are reached through a hand-built model of an attribute, not through compiled source.
/// </summary>
public class AttributeEmitterTest
{
    private const string MethodName = "Check";

    [Fact]
    public void Emit_WhenAnArgumentIsOfAnUnsupportedType_ThrowsWeavingException()
    {
        // Arrange - decimal can't be an attribute argument
        using ModuleDefinition module = CreateModule();
        ArgumentValueEmitter emitter = new(
            new WellKnownReferences(module),
            CreateMethod(module),
            new InstructionBuffer()
        );
        CustomAttributeArgument argument = new(module.TypeSystem.Object, 1.5m);

        // Act
        WeavingException exception = Assert.Throws<WeavingException>(() => emitter.Emit(argument));

        // Assert
        Assert.Equal(WeavingErrorCodes.WeavingFailed, exception.Code);
        Assert.Equal(
            "Unsupported attribute argument of type 'System.Decimal' in 'System.Void Fake::Check()'.",
            exception.Message
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Emit_WhenANamedArgumentsMemberIsMissing_ThrowsWeavingException(bool isProperty)
    {
        // Arrange
        using ModuleDefinition module = CreateModule();
        WellKnownReferences references = new(module);
        InstructionBuffer buffer = new();
        AttributeConstructionEmitter emitter = new(
            references,
            new ArgumentValueEmitter(references, CreateMethod(module), buffer),
            buffer
        );
        TypeDefinition attributeType = module.Types.Single(static type => type.Name == "Fake");
        CustomAttribute attribute = new(new MethodReference(".ctor", module.TypeSystem.Void, attributeType));
        CustomAttributeNamedArgument missing = new("Missing", new CustomAttributeArgument(module.TypeSystem.Int32, 1));
        (isProperty ? attribute.Properties : attribute.Fields).Add(missing);

        // Act
        WeavingException exception = Assert.Throws<WeavingException>(() => emitter.Emit(attribute, attributeType));

        // Assert
        Assert.Equal("Could not find member 'Missing' on attribute 'Fake'.", exception.Message);
    }

    private static ModuleDefinition CreateModule()
    {
        ModuleDefinition module = ModuleDefinition.CreateModule("Fake", ModuleKind.Dll);
        module.Types.Add(new TypeDefinition(string.Empty, "Fake", TypeAttributes.Public));
        return module;
    }

    private static MethodDefinition CreateMethod(ModuleDefinition module)
    {
        MethodDefinition method = new(MethodName, MethodAttributes.Public, module.TypeSystem.Void);
        module.Types.Single(static type => type.Name == "Fake").Methods.Add(method);
        return method;
    }
}
