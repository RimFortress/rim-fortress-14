using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Input;

namespace Content.Client._RF.UserInterface.Controls.NpcJobs;

[Virtual]
public sealed class TextureSwitchButton : TextureButton
{
    [Dependency] private readonly IResourceCache _cache = default!;

    private int _index;
    private List<Texture> _textures = new();
    private List<string> _texturesPaths = new();

    public string TexturesCollection
    {
        set
        {
            _texturesPaths = value
                .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            _textures.Clear();

            foreach (var path in _texturesPaths)
            {
                _textures.Add(_cache.GetResource<TextureResource>(path));
            }

            Index = _index;
        }
    }

    public int Index
    {
        get => _index;
        set
        {
            if (value >= _textures.Count)
                _index = 0;
            else if (value < 0)
                _index = _textures.Count - 1;
            else
                _index = value;

            var texture = _textures[_index];
            TextureNormal = texture;
            OnTextureChanged?.Invoke(texture);
        }
    }

    public string? CurrentTexturePath => Index <= _texturesPaths.Count ? _texturesPaths[Index] : null;

    public event Action<Texture>? OnTextureChanged;

    public TextureSwitchButton()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (Disabled)
            return;

        if (args.Function == EngineKeyFunctions.UIClick)
            Index++;
        else if (args.Function == EngineKeyFunctions.UIRightClick)
            Index--;
    }

    public void SetTexture(Texture texture)
    {
        var index = _textures.IndexOf(texture);

        if (index == -1)
        {
            _textures.Add(texture);
            Index = _textures.Count - 1;
        }
        else
            Index = index;
    }
}

