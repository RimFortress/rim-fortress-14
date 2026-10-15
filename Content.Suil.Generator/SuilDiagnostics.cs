using Microsoft.CodeAnalysis;

namespace Content.Suil.Generator;

internal static class SuilDiagnostics
{
    private const string Category = "Suil";

    private static DiagnosticDescriptor Error(string id, string title, string message)
        => new(id, title, message, Category, DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor ResourceRootMissing = Error(
        "SUIL001",
        "SUIL root is not set",
        "The build property SuilRoot is not set, so the .suil files can't be located (it is set by RimFortress.Suil.targets)");

    public static readonly DiagnosticDescriptor SyntaxError = Error(
        "SUIL003",
        "SUIL syntax error",
        "{0}");

    public static readonly DiagnosticDescriptor ExpansionError = Error(
        "SUIL004",
        "SUIL expansion error",
        "{0}");

    public static readonly DiagnosticDescriptor ClassNotPartial = Error(
        "SUIL006",
        "SUIL view class must be partial",
        "The class '{0}' is the class of a view, so it must be a partial non-generic class");

    public static readonly DiagnosticDescriptor RootTypeMismatch = Error(
        "SUIL007",
        "SUIL root type mismatch",
        "The class '{0}' must derive from '{1}', the type of the root element '{2}'");

    public static readonly DiagnosticDescriptor UnknownTag = Error(
        "SUIL008",
        "Unknown SUIL element type",
        "No control for the tag '{0}'. A tag is the name of the control class in kebab-case (SpriteView is sprite-view) or its custom name from [SuilTag]; the tag '_' must be registered with [assembly: SuilTag(\"_\", typeof(...))]");

    public static readonly DiagnosticDescriptor AmbiguousTag = Error(
        "SUIL009",
        "Ambiguous SUIL element type",
        "The tag '{0}' stands for several control types: {1}. Give one of them a custom name with [SuilTag]");

    public static readonly DiagnosticDescriptor UnknownProperty = Error(
        "SUIL010",
        "Unknown SUIL property",
        "'{0}' has no public settable property '{1}'");

    public static readonly DiagnosticDescriptor BadValue = Error(
        "SUIL011",
        "SUIL value can't be converted",
        "{0}");

    public static readonly DiagnosticDescriptor DuplicateId = Error(
        "SUIL012",
        "Duplicate SUIL id",
        "The id '{0}' is used twice in the file, every id becomes a field and must be unique");

    public static readonly DiagnosticDescriptor FieldClash = Error(
        "SUIL013",
        "SUIL id clashes with a member",
        "The id '{0}' becomes the field '{1}', but '{2}' already has a member with this name");

    public static readonly DiagnosticDescriptor NoDefaultConstructor = Error(
        "SUIL014",
        "SUIL element type has no default constructor",
        "'{0}' (the tag '{1}') has no public parameterless constructor");

    public static readonly DiagnosticDescriptor ControlNotFound = Error(
        "SUIL015",
        "Control type not found",
        "'Robust.Client.UserInterface.Control' is not referenced, SUIL views can only be built for UI controls");

    public static readonly DiagnosticDescriptor InvalidTag = Error(
        "SUIL017",
        "Invalid SUIL tag registration",
        "{0}");

    public static readonly DiagnosticDescriptor ViewClassNotFound = Error(
        "SUIL024",
        "Class of a view not found",
        "The view '@{0}' in '{1}' names no class of this project (write the name of a class, or its tail with the namespace)");

    public static readonly DiagnosticDescriptor AmbiguousViewClass = Error(
        "SUIL025",
        "Ambiguous class of a view",
        "The view '@{0}' could be any of: {1}. Write more of the namespace");

    public static readonly DiagnosticDescriptor DuplicateView = Error(
        "SUIL026",
        "Class has several views",
        "The class '{0}' already has a view at {1}");

    public static readonly DiagnosticDescriptor UnknownEvent = Error(
        "SUIL018",
        "Unknown SUIL event",
        "'{0}' has no public event '{1}' (the name of an event is the name of the member in kebab-case: OnPressed is on-pressed)");

    public static readonly DiagnosticDescriptor UnsupportedEvent = Error(
        "SUIL019",
        "Unsupported SUIL event",
        "The event '{0}' of '{1}' is {2}: only a delegate that returns nothing and takes nothing or one argument (Action, Action<T>) can be subscribed to");

    public static readonly DiagnosticDescriptor UnknownMethod = Error(
        "SUIL020",
        "Unknown method in a SUIL handler",
        "The class '{0}' has no method '{1}' that a handler can call (a method that returns nothing and takes nothing or the arguments of the event)");

    public static readonly DiagnosticDescriptor BadMethodSignature = Error(
        "SUIL021",
        "Wrong method in a SUIL handler",
        "The method '{0}.{1}' can't handle '{2}': it must take nothing or {3}");

    public static readonly DiagnosticDescriptor UnknownTarget = Error(
        "SUIL022",
        "Unknown target in a SUIL handler",
        "{0}");

    public static readonly DiagnosticDescriptor BadStatement = Error(
        "SUIL023",
        "Invalid statement in a SUIL handler",
        "{0}");
}
