using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Robust.Roslyn.Shared;
using Robust.Roslyn.Shared.Helpers;

namespace Content.Client._RF.CssPropertiesGenerator;

/// <summary>
/// Implements <c>CssControl.AutoCssProperties()</c>/<c>AutoCssShorthands()</c>
/// for every partial property annotated with <c>CssPropertyAttribute</c> and/or
/// <c>CssShorthandPropertyAttribute</c>. A property may carry more than one
/// <c>CssShorthandPropertyAttribute</c> (it can belong to several shorthands
/// at once, e.g. a border-side color belonging to both "border" and
/// "border-top"). For a shorthand group where no member specifies a
/// <c>CustomParserMethod</c>, a greedy per-token expander is generated
/// automatically: for each unconsumed token it tries every not-yet-matched
/// longhand's registered value parser in declaration order until one
/// succeeds, resetting the stream on failure.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class CssPropertyGenerator : IIncrementalGenerator
{
    private const string CssPropertyAttributeName = "Content.Client._RF.CSS.CssPropertyAttribute";
    private const string CssShorthandPropertyAttributeName = "Content.Client._RF.CSS.CssShorthandPropertyAttribute";
    private const string CssStylingAttributeName = "Content.Client._RF.CSS.CssStylingAttribute";
    private const string CssPropertyAccessorName = "Content.Client._RF.CSS.CssPropertyAccessor";
    private const string CssShorthandInfoName = "Content.Client._RF.CSS.CssShorthandInfo";
    private const string CssManagerName = "Content.Client._RF.CSS.ICssManager";
    private const string CssValueStreamName = "Content.Client._RF.CSS.CssValueStream";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var properties = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                CssPropertyAttributeName,
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (ctx, _) => TryGetMemberInfo(ctx))
            .Where(static it => it is not null)
            .Select(static (it, _) => it!.Value);

        var shorthands = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                CssShorthandPropertyAttributeName,
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
    /// so <see cref="Generate"/> can emit both AutoCssProperties() and
    /// AutoCssShorthands() together in a single generated file per class.
    /// </summary>
    private static List<ClassCssInfo> Join(
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

        var result = new List<ClassCssInfo>(allTypes.Count);

        foreach (var type in allTypes)
        {
            propsByClass.TryGetValue(type, out var props);
            shortsByClass.TryGetValue(type, out var shorts);

            result.Add(new ClassCssInfo(
                type,
                new EquatableArray<CssPropertyMember>(props.IsDefault ? ImmutableArray<CssPropertyMember>.Empty : props),
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
            .First(a => a.AttributeClass?.ToDisplayString() == CssPropertyAttributeName);

        var cssName = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string s
            ? s
            : ToKebabCase(property.Name);

        var parserFqn = attribute.ConstructorArguments.Length > 1
                        && attribute.ConstructorArguments[1].Value is ITypeSymbol parserType
            ? parserType.ToDisplayString()
            : null;

        var clrTypeFqn = property.Type.ToDisplayString();

        var typeofFqn = property.Type is { IsReferenceType: true, NullableAnnotation: NullableAnnotation.Annotated }
            ? property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString()
            : clrTypeFqn;

        var member = new CssPropertyMember(property.Name, cssName, clrTypeFqn, typeofFqn, parserFqn);
        return new MemberCandidate(PartialTypeInfo.FromSymbol(property.ContainingType, parentSyntax), member);
    }

    /// <summary>
    /// A property may carry several <c>CssShorthandPropertyAttribute</c>
    /// instances (AllowMultiple) — one <see cref="ShorthandMember"/> is
    /// produced per attribute instance, all sharing the same property's CSS
    /// name/CLR type but potentially different shorthand groups.
    /// </summary>
    private static ImmutableArray<ShorthandMemberCandidate> TryGetShorthandMembers(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IPropertySymbol property ||
            ctx.TargetNode is not PropertyDeclarationSyntax { Parent: TypeDeclarationSyntax parentSyntax })
            return ImmutableArray<ShorthandMemberCandidate>.Empty;

        var partialType = PartialTypeInfo.FromSymbol(property.ContainingType, parentSyntax);
        var cssName = GetCssName(property);
        var clrTypeFqn = property.Type.ToDisplayString();

        var builder = ImmutableArray.CreateBuilder<ShorthandMemberCandidate>();

        foreach (var attribute in ctx.Attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString() != CssShorthandPropertyAttributeName)
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

            var member = new ShorthandMember(property.Name, cssName, clrTypeFqn, shorthandName, customParserMethod);
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
    /// Resolves a property's CSS name the same way AutoCssProperties() does —
    /// from its own CssPropertyAttribute if present, otherwise kebab-cased
    /// from the C# name. A shorthand's longhand list must use CSS names, not
    /// C# property names.
    /// </summary>
    private static string GetCssName(IPropertySymbol property)
    {
        var propertyAttribute = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == CssPropertyAttributeName);

        if (propertyAttribute is { ConstructorArguments.Length: > 0 } &&
            propertyAttribute.ConstructorArguments[0].Value is string s)
            return s;

        return ToKebabCase(property.Name);
    }

    private static void Generate(SourceProductionContext context, ClassCssInfo info)
    {
        if (!info.PartialType.IsValid)
            return; // class itself isn't fully partial up its nesting chain — analyzer's job to flag

        var members = info.Members.AsImmutableArray();
        var shorthandMembers = info.Shorthands.AsImmutableArray();

        var builder = new StringBuilder("// <auto-generated/>\n#nullable enable\n\n");
        var indent = new IndentWriter(builder);

        info.PartialType.WriteHeader(ref indent, attributes: $"[global::{CssStylingAttributeName}]");

        builder.AppendLine("{");
        builder.AppendLine();

        GenerateProperties(builder, info, members);
        GenerateShorthands(builder, info, shorthandMembers);

        builder.AppendLine("}");

        info.PartialType.WriteFooter(builder);

        context.AddSource(info.PartialType.GetGeneratedFileName(), builder.ToString());
    }

    private static void GenerateProperties(StringBuilder builder, ClassCssInfo info, ImmutableArray<CssPropertyMember> members)
    {
        builder.AppendLine($"    public override System.Collections.Generic.Dictionary<string, {CssPropertyAccessorName}> AutoCssProperties()");
        builder.AppendLine("    {");
        builder.AppendLine("        return new()");
        builder.AppendLine("        {");

        foreach (var member in members)
        {
            var parserArg = member.ParserFqn is not null ? $"typeof({member.ParserFqn})" : "null";

            builder.AppendLine($"""
                            ["{member.CssName}"] = new {CssPropertyAccessorName}(
                                typeof({member.TypeofFqn}),
                                static (c, v) => (({info.PartialType.FullDisplayName})c).{member.PropertyName} = ({member.ClrTypeFqn})v,
                                static c => (({info.PartialType.FullDisplayName})c).{member.PropertyName}!,
                                {parserArg},
                                DefaultValue: this.{member.PropertyName}),
                """);
        }

        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void GenerateShorthands(StringBuilder builder, ClassCssInfo info, ImmutableArray<ShorthandMember> members)
    {
        builder.AppendLine($"    public override System.Collections.Generic.Dictionary<string, {CssShorthandInfoName}> AutoCssShorthands()");
        builder.AppendLine("    {");
        builder.AppendLine("        return new()");
        builder.AppendLine("        {");

        foreach (var group in members.GroupBy(m => m.ShorthandName))
        {
            var groupMembers = group.ToList();
            var longhandsLiteral = string.Join(", ", groupMembers.Select(m => $"\"{m.CssName}\""));

            // First non-null CustomParserMethod in the group wins if more than
            // one longhand specifies one — malformed/ambiguous usage, left for
            // a companion analyzer to flag rather than erroring here.
            var customParser = groupMembers.Select(m => m.CustomParserMethod).FirstOrDefault(p => p is not null);

            var parserExpr = customParser ?? GenerateGreedyExpander(groupMembers);

            builder.AppendLine($$"""
                                ["{{group.Key}}"] = new {{CssShorthandInfoName}}(
                                    new string[] { {{longhandsLiteral}} },
                                    {{parserExpr}}),
                """);
        }

        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    /// <summary>
    /// Emits a lambda matching CssShorthandParserDelegate's signature that
    /// greedily tries each not-yet-matched longhand's registered parser
    /// against the current token, in declaration order, until one succeeds —
    /// used when no group member specifies a CustomParserMethod. Each
    /// attempt resets the stream to its own pre-attempt position on failure,
    /// so a partially-consumed failed parse never corrupts the next attempt.
    /// </summary>
    private static string GenerateGreedyExpander(List<ShorthandMember> members)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"(ref {CssValueStreamName} stream, {CssManagerName} css, System.Collections.Generic.Dictionary<string, object?> values) =>");
        sb.AppendLine("            {");
        sb.AppendLine($"                var remaining = new System.Collections.Generic.HashSet<string> {{ {string.Join(", ", members.Select(m => $"\"{m.CssName}\""))} }};");
        sb.AppendLine("                while (!stream.AtEnd)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var outerMark = stream.Mark();");
        sb.AppendLine("                    var matched = false;");
        sb.AppendLine();

        for (var i = 0; i < members.Count; i++)
        {
            var m = members[i];
            sb.AppendLine($$"""
                                    if (!matched && remaining.Contains("{{m.CssName}}"))
                                    {
                                        var innerMark{{i}} = stream.Mark();
                                        if (css.TryParseValue<{{m.ClrTypeFqn}}>(ref stream, out var v{{i}}))
                                        {
                                            values["{{m.CssName}}"] = v{{i}};
                                            remaining.Remove("{{m.CssName}}");
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

    private record struct MemberCandidate(PartialTypeInfo PartialType, CssPropertyMember Member);
    private record struct ShorthandMemberCandidate(PartialTypeInfo PartialType, ShorthandMember Member);

    private record struct ClassCssInfo(
        PartialTypeInfo PartialType,
        EquatableArray<CssPropertyMember> Members,
        EquatableArray<ShorthandMember> Shorthands);

    private record struct CssPropertyMember(
        string PropertyName,
        string CssName,
        string ClrTypeFqn,
        string TypeofFqn,
        string? ParserFqn);

    private record struct ShorthandMember(
        string PropertyName,
        string CssName,
        string ClrTypeFqn,
        string ShorthandName,
        string? CustomParserMethod);
}
