using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A container that supports texture rendering within it using a shader
/// </summary>
[Virtual]
public class ShaderPanelContainer : Container
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public const string StylePropertyTexture = "panel-texture";
    public const string StylePropertyShader = "shader-id";

    public const string ShaderAreaSizeParameter = "area_size";

    private ShaderInstance? _shader;

    /// <summary>
    /// Shader prototype ID for rendering
    /// </summary>
    /// <remarks>
    /// Shader must have a vec2 texture_size parameter
    /// </remarks>
    public ProtoId<ShaderPrototype>? Shader
    {
        set => _shader = _prototype.TryIndex(value, out var proto)
            ? proto.InstanceUnique()
            : null;
    }

    public Texture? Texture { get; set; }

    public string TexturePath
    {
        set => Texture = _cache.GetTexture(value);
    }

    public ShaderPanelContainer()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void StylePropertiesChanged()
    {
        if (TryGetStyleProperty(StylePropertyShader, out ProtoId<ShaderPrototype>? shader))
            Shader = shader;

        if (TryGetStyleProperty(StylePropertyTexture, out Texture? texture))
            Texture = texture;

        base.StylePropertiesChanged();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        DrawTexture(handle, PixelSizeBox);
    }

    protected void DrawTexture(DrawingHandleScreen handle, UIBox2 box)
    {
        if (Texture == null)
            return;

        var prevShader = handle.GetShader();

        _shader?.SetParameter(ShaderAreaSizeParameter, box.Size);

        handle.UseShader(_shader);
        handle.DrawTextureRect(Texture, box, ModulateSelfOverride);
        handle.UseShader(prevShader);
    }
}
