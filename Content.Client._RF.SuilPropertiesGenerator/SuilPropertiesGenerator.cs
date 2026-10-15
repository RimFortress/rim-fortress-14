using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Robust.Roslyn.Shared;
using Robust.Roslyn.Shared.Helpers;

namespace Content.Client._RF.SuilPropertiesGenerator;

/// <summary>
/// Implements <c>UiElement.AutoSuilProperties()</c>/<c>AutoSuilShorthands()</c>
/// for every partial property annotated with <c>SuilPropertyAttribute</c> and/or
/// <c>SuilShorthandPropertyAttribute</c>. A property may carry more than one
/// <c>SuilShorthandPropertyAttribute</c> (it can belong to several shorthands
/// at once, e.g. a border-side color belonging to both "border" and
/// "border-top"). For a shorthand group where no member specifies a
/// <c>CustomParserMethod</c>, a greedy per-token expander is generated
/// automatically: for each unconsumed token it tries every not-yet-matched
/// longhand's registered value parser in declaration order until one
/// succeeds, resetting the stream on failure.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SuilPropertyGenerator : IIncrementalGenerator
{
    private const string SuilPropertyAttributeName = "Content.Suil.SuilPropertyAttribute";
    private const string SuilShorthandPropertyAttributeName = "Content.Suil.SuilShorthandAttribute";
    private const string SuilStylingAttributeName = "Content.Client._RF.Suil.SuilStylingAttribute";
    private const string SuilPropertyAccessorName = "Content.Client._RF.Suil.SuilStyleAccessor";
    private const string SuilShorthandInfoName = "Content.Client._RF.Suil.SuilShorthandInfo";
    private const string SuilManagerName = "Content.Suil.ISuilValues";
    private const string SuilValueStreamName = "Content.Suil.SuilValueStream";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var properties = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SuilPropertyAttributeName,
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (ctx, _) => TryGetMemberInfo(ctx))
            .Where(static it => it is not null)
            .Select(static (it, _) => it!.Value);

        var shorthands = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SuilShorthandPropertyAttributeName,
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (ctx, _) => TryGetShorthandMembers(ctx))
            .SelectMany(static (items, _) => items);

        var joined = properties.Collect()
            .Combine(shorthands.Collect())
            .SelectMany(static (pair, _) => Join(pair.Left, pair.Right));

        context.RegisterImplementationSourceOutput(joined, Generate);
    }

    /// <summary>
    /// Merges the two independently-collected candidate streams (plain
    /// properties and shorthand members) into one entry per partial class,
    /// so <see cref="Generate"/> can emit both AutoSuilProperties() and
    /// AutoSuilShorthands() together in a single generated file per class.
    /// </summary>
    private static List<ClassSuilInfo> Join(
        ImmutableArray<MemberCandidate> properties,
        ImmutableArray<ShorthandMemberCandidate> shorthandMembers)
    {
        var propsByClass = properties
            .GroupBy(m => m.PartialType, PartialTypeInfo.WithoutLocationEqualityComparer)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Member).ToImmutableArray(),
                PartialTypeInfo.WithoutLocationEqualityComparer);

        var shortsByClass = shorthandMembers
            .GroupBy(m => m.PartialType, PartialTypeInfo.WithoutLocationEqualityComparer)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Member).ToImmutableArray(),
                PartialTypeInfo.WithoutLocationEqualityComparer);

        var allTypes = new HashSet<PartialTypeInfo>(propsByClass.Keys, PartialTypeInfo.WithoutLocationEqualityComparer);
        allTypes.UnionWith(shortsByClass.Keys);

        var result = new List<ClassSuilInfo>(allTypes.Count);

        foreach (var type in allTypes)
        {
            propsByClass.TryGetValue(type, out var props);
            shortsByClass.TryGetValue(type, out var shorts);

            result.Add(new ClassSuilInfo(
                type,
                new EquatableArray<SuilPropertyMember>(props.IsDefault ? ImmutableArray<SuilPropertyMember>.Empty : props),
                new EquatableArray<ShorthandMember>(shorts.IsDefault ? ImmutableArray<ShorthandMember>.Empty : shorts)));
        }

        return result;
    }

    private static MemberCandidate? TryGetMemberInfo(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IPropertySymbol property ||
            ctx.TargetNode is not PropertyDeclarationSyntax { Parent: TypeDeclarationSyntax parentSyntax })
            return null;

        var attribute = property.GetAttributes()
            .First(a => a.AttributeClass?.ToDisplayString() == SuilPropertyAttributeName);

        var suilName = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string s
            ? s
            : ToKebabCase(property.Name);

        var parserFqn = attribute.ConstructorArguments.Length > 1
                        && attribute.ConstructorArguments[1].Value is ITypeSymbol parserType
            ? parserType.ToDisplayString()
            : null;

        var inherited = attribute.NamedArguments.Any(a => a is { Key: "Inherited", Value.Value: true });
        var clrTypeFqn = property.Type.ToDisplayString();

        var typeofFqn = property.Type is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated }
            ? property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString()
            : clrTypeFqn;

        var member = new SuilPropertyMember(property.Name, suilName, clrTypeFqn, typeofFqn, parserFqn, inherited);
        return new MemberCandidate(PartialTypeInfo.FromSymbol(property.ContainingType, parentSyntax), member);
    }

    /// <summary>
    /// A property may carry several <c>SuilShorthandPropertyAttribute</c>
    /// instances (AllowMultiple) — one <see cref="ShorthandMember"/> is
    /// produced per attribute instance, all sharing the same property's SUIL
    /// name/CLR type but potentially different shorthand groups.
    /// </summary>
    private static ImmutableArray<ShorthandMemberCandidate> TryGetShorthandMembers(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IPropertySymbol property ||
            ctx.TargetNode is not PropertyDeclarationSyntax { Parent: TypeDeclarationSyntax parentSyntax })
            return ImmutableArray<ShorthandMemberCandidate>.Empty;

        var partialType = PartialTypeInfo.FromSymbol(property.ContainingType, parentSyntax);
        var suilName = GetSuilName(property);
        var clrTypeFqn = property.Type.ToDisplayString();

        var builder = ImmutableArray.CreateBuilder<ShorthandMemberCandidate>();

        foreach (var attribute in ctx.Attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString() != SuilShorthandPropertyAttributeName)
                continue;

            if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not string shorthandName)
                continue;

            // Falls back to the generated greedy expander (CustomParserMethod = null)
            // if the referenced method doesn't exist / isn't a static 3-parameter
            // method — malformed usage is left for a companion analyzer to flag,
            // same convention as the missing-"partial" check below.
            var customParserMethod = attribute.ConstructorArguments.Length > 1
                                      && attribute.ConstructorArguments[1].Value is string m
                                      && HasValidCustomParserMethod(property.ContainingType, m)
                ? m
                : null;

            var member = new ShorthandMember(property.Name, suilName, clrTypeFqn, shorthandName, customParserMethod);
            builder.Add(new ShorthandMemberCandidate(partialType, member));
        }

        return builder.ToImmutable();
    }

    private static bool HasValidCustomParserMethod(INamedTypeSymbol containingType, string methodName)
    {
        foreach (var member in containingType.GetMembers(methodName))
        {
            if (member is IMethodSymbol { IsStatic: true, Parameters.Length: 3 })
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves a property's SUIL name the same way AutoSuilProperties() does —
    /// from its own SuilPropertyAttribute if present, otherwise kebab-cased
    /// from the C# name. A shorthand's longhand list must use SUIL names, not
    /// C# property names.
    /// </summary>
    private static string GetSuilName(IPropertySymbol property)
    {
        var propertyAttribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == SuilPropertyAttributeName);

        if (propertyAttribute is { ConstructorArguments.Length: > 0 } &&
            propertyAttribute.ConstructorArguments[0].Value is string s)
            return s;

        return ToKebabCase(property.Name);
    }

    private static void Generate(SourceProductionContext context, ClassSuilInfo info)
    {
        if (!info.PartialType.IsValid)
            return; // class itself isn't fully partial up its nesting chain — analyzer's job to flag

        var shorthandMembers = info.Shorthands.AsImmutableArray();

        // The properties of the classes don't need anything generated: the SUIL property tables have them all.
        if (shorthandMembers.IsEmpty)
            return;

        var builder = new StringBuilder("// <auto-generated/>\n#nullable enable\n\n");
        var indent = new IndentWriter(builder);

        info.PartialType.WriteHeader(ref indent, attributes: $"[global::{SuilStylingAttributeName}]");

        builder.AppendLine("{");
        builder.AppendLine();

        GenerateShorthands(builder, info, shorthandMembers);

        builder.AppendLine("}");

        info.PartialType.WriteFooter(builder);

        context.AddSource(info.PartialType.GetGeneratedFileName(), builder.ToString());
    }

    private static void GenerateProperties(StringBuilder builder, ClassSuilInfo info, ImmutableArray<SuilPropertyMember> members)
    {
        builder.AppendLine($"    public override System.Collections.Generic.Dictionary<string, {SuilPropertyAccessorName}> AutoSuilProperties()");
        builder.AppendLine("    {");
        builder.AppendLine("        return new()");
        builder.AppendLine("        {");

        foreach (var member in members)
        {
            var parserArg = member.ParserFqn is not null ? $"typeof({member.ParserFqn})" : "null";
            var inheritedArg = member.Inherited ? ", Inherited: true" : "";

            builder.AppendLine($"""
                            ["{member.SuilName}"] = new {SuilPropertyAccessorName}(
                                typeof({member.TypeofFqn}),
                                static (c, v) => (({info.PartialType.FullDisplayName})c).{member.PropertyName} = ({member.ClrTypeFqn})v,
                                static c => (({info.PartialType.FullDisplayName})c).{member.PropertyName}!,
                                {parserArg},
                                DefaultValue: this.{member.PropertyName}{inheritedArg}),
                """);
        }

        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void GenerateShorthands(StringBuilder builder, ClassSuilInfo info, ImmutableArray<ShorthandMember> members)
    {
        builder.AppendLine($"    public override System.Collections.Generic.Dictionary<string, {SuilShorthandInfoName}> AutoSuilShorthands()");
        builder.AppendLine("    {");
        builder.AppendLine("        return new()");
        builder.AppendLine("        {");

        foreach (var group in members.GroupBy(m => m.ShorthandName))
        {
            var groupMembers = group.ToList();
            var longhandsLiteral = string.Join(", ", groupMembers.Select(m => $"\"{m.SuilName}\""));

            // First non-null CustomParserMethod in the group wins if more than
            // one longhand specifies one — malformed/ambiguous usage, left for
            // a companion analyzer to flag rather than erroring here.
            var customParser = groupMembers.Select(m => m.CustomParserMethod).FirstOrDefault(p => p is not null);

            var parserExpr = customParser ?? GenerateGreedyExpander(groupMembers);

            builder.AppendLine($$"""
                                ["{{group.Key}}"] = new {{SuilShorthandInfoName}}(
                                    new string[] { {{longhandsLiteral}} },
                                    {{parserExpr}}),
                """);
        }

        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    /// <summary>
    /// Emits a lambda matching SuilShorthandParserDelegate's signature that
    /// greedily tries each not-yet-matched longhand's registered parser
    /// against the current token, in declaration order, until one succeeds —
    /// used when no group member specifies a CustomParserMethod. Each
    /// attempt resets the stream to its own pre-attempt position on failure,
    /// so a partially-consumed failed parse never corrupts the next attempt.
    /// </summary>
    private static string GenerateGreedyExpander(List<ShorthandMember> members)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"(ref {SuilValueStreamName} stream, {SuilManagerName} suil, System.Collections.Generic.Dictionary<string, object?> values) =>");
        sb.AppendLine("            {");
        sb.AppendLine($"                var remaining = new System.Collections.Generic.HashSet<string> {{ {string.Join(", ", members.Select(m => $"\"{m.SuilName}\""))} }};");
        sb.AppendLine("                while (!stream.AtEnd)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var outerMark = stream.Mark();");
        sb.AppendLine("                    var matched = false;");
        sb.AppendLine();

        for (var i = 0; i < members.Count; i++)
        {
            var m = members[i];
            sb.AppendLine($$"""
                                    if (!matched && remaining.Contains("{{m.SuilName}}"))
                                    {
                                        var innerMark{{i}} = stream.Mark();
                                        if (suil.TryParse<{{m.ClrTypeFqn}}>(ref stream, out var v{{i}}))
                                        {
                                            values["{{m.SuilName}}"] = v{{i}};
                                            remaining.Remove("{{m.SuilName}}");
                                            matched = true;
                                        }
                                        else
                                        {
                                            stream.Reset(innerMark{{i}});
                                        }
                                    }
                    """);
        }

        sb.AppendLine();
        sb.AppendLine("                    if (!matched)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        stream.Reset(outerMark);");
        sb.AppendLine("                        return false;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                return true;");
        sb.AppendLine("            }");

        return sb.ToString();
    }

    private static string ToKebabCase(string pascalCase)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < pascalCase.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascalCase[i]))
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(pascalCase[i]));
        }
        return sb.ToString();
    }

    private record struct MemberCandidate(PartialTypeInfo PartialType, SuilPropertyMember Member);
    private record struct ShorthandMemberCandidate(PartialTypeInfo PartialType, ShorthandMember Member);

    private record struct ClassSuilInfo(
        PartialTypeInfo PartialType,
        EquatableArray<SuilPropertyMember> Members,
        EquatableArray<ShorthandMember> Shorthands);

    private record struct SuilPropertyMember(
        string PropertyName,
        string SuilName,
        string ClrTypeFqn,
        string TypeofFqn,
        string? ParserFqn,
        bool Inherited);

    private record struct ShorthandMember(
        string PropertyName,
        string SuilName,
        string ClrTypeFqn,
        string ShorthandName,
        string? CustomParserMethod);
}
