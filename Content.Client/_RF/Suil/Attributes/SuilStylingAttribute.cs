using Content.Suil;
namespace Content.Client._RF.Suil;

/// <summary>
/// An attribute indicating that the UI class uses
/// SUIL properties and must be processed by <see cref="ISuilManager"/>.
/// </summary>
/// <remarks>
/// This attribute is automatically added to classes that use the
/// <see cref="SuilPropertyAttribute"/>; you do not need to add it manually.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SuilStylingAttribute : Attribute;
