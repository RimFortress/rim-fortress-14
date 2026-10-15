using Content.Suil;
namespace Content.Client._RF.CSS;

/// <summary>
/// An attribute indicating that the UI class uses
/// CSS properties and must be processed by <see cref="ISuilManager"/>.
/// </summary>
/// <remarks>
/// This attribute is automatically added to classes that use the
/// <see cref="SuilPropertyAttribute"/>; you do not need to add it manually.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CssStylingAttribute : Attribute;
