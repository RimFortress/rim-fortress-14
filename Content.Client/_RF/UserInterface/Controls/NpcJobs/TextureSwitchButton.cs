using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._RF.UserInterface.Controls.NpcJobs;

[Virtual]
public sealed class TextureSwitchButton : TextureButton
{
    private int _index;
    private List<string> _textures = new();

    public string TexturesCollection
    {
        set
        {
            _textures = value
                .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .ToList();

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

            var path = _textures[_index];
            TexturePath = path;
            OnTextureChanged?.Invoke(path);
        }
    }

    public string? CurrentTexturePath => Index <= _textures.Count ? _textures[Index] : null;

    public event Action<string>? OnTextureChanged;

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (Disabled)
            return;

        if (args.Function == EngineKeyFunctions.UIClick)
            Index++;
        else if (args.Function == EngineKeyFunctions.UIRightClick)
            Index--;
    }

    public void SetTexture(string texturePath)
    {
        var index = _textures.IndexOf(texturePath);

        if (index == -1)
        {
            _textures.Add(texturePath);
            Index =  _textures.Count - 1;
        }
        else
            Index =  index;
    }
}

