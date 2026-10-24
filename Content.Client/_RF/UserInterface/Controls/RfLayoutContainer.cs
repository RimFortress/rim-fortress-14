using Content.Suil;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// The <see cref="LayoutContainer"/> as a content type: the hot reload of SUIL can create it, and it cannot create the
/// types of the engine.
/// </summary>
[Virtual]
[SuilTag("free-layout")]
public class RfLayoutContainer : LayoutContainer;
