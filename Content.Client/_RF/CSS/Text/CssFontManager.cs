using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._RF.CSS.Text;

/// <inheritdoc cref="ICssFontManager"/>
public sealed partial class CssFontManager : ICssFontManager
{
    [Dependency] private IResourceCache _resources = null!;
    [Dependency] private IUserInterfaceManager _ui = null!;

    private readonly record struct FontKey(string Family, int Weight, FontSlant Slant, int Size);

    private readonly List<CssFontFace> _manualFaces = new();
    private List<CssFontFace> _cssFaces = new();
    private readonly Dictionary<string, List<CssFontFace>> _faces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string[]> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<FontKey, Font> _fonts = new();
    private readonly Dictionary<(ResPath Path, int Size), VectorFont> _vectorFonts = new();

    /// <inheritdoc/>
    public void SetFaces(IEnumerable<CssFontFace> faces)
    {
        _cssFaces = faces.ToList();
        Rebuild();
    }

    private void Rebuild()
    {
        _faces.Clear();
        _fonts.Clear();
        _vectorFonts.Clear();

        // CSS faces go last, so they win over the ones registered from code.
        foreach (var face in _manualFaces)
        {
            AddFace(face);
        }

        foreach (var face in _cssFaces)
        {
            AddFace(face);
        }
    }

    /// <inheritdoc/>
    public void RegisterFace(CssFontFace face)
    {
        _manualFaces.Add(face);
        Rebuild();
    }

    /// <inheritdoc/>
    public void RegisterAlias(string alias, params string[] families)
    {
        _aliases[alias] = families;
        _fonts.Clear();
    }

    private void AddFace(CssFontFace face)
    {
        if (!_faces.TryGetValue(face.Family, out var list))
            _faces[face.Family] = list = new List<CssFontFace>();

        list.Add(face);
    }

    /// <inheritdoc/>
    public Font Resolve(CssFontFamily family, FontWeight weight, FontSlant slant, float size)
    {
        var px = Math.Max(1, (int) MathF.Round(size));
        var key = new FontKey(family.Key, (int) weight, slant, px);

        if (_fonts.TryGetValue(key, out var cached))
            return cached;

        var fonts = new List<Font>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in family.Names)
        {
            AddFamily(name, (int) weight, slant, px, fonts, used, depth: 0);
        }

        var result = fonts.Count switch
        {
            0 => new DummyFont(),
            1 => fonts[0],
            _ => new StackedFont(fonts.ToArray()),
        };

        _fonts[key] = result;
        return result;
    }

    private void AddFamily(string name, int weight, FontSlant slant, int size, List<Font> fonts, HashSet<string> used, int depth)
    {
        if (!used.Add(name))
            return;

        if (_faces.TryGetValue(name, out var faces) && faces.Count > 0)
        {
            var face = PickFace(faces, weight, slant);

            if (!_vectorFonts.TryGetValue((face.Path, size), out var font))
            {
                var resource = _resources.GetResource<FontResource>(face.Path);
                _vectorFonts[(face.Path, size)] = font = new VectorFont(resource, size);
            }

            fonts.Add(font);
            return;
        }

        // Aliases may point to other aliases, but not forever.
        if (depth >= 4 || !_aliases.TryGetValue(name, out var targets))
            return;

        foreach (var target in targets)
        {
            AddFamily(target, weight, slant, size, fonts, used, depth + 1);
        }
    }

    // Simplified CSS font matching: style first (italic and oblique substitute each other, then normal),
    // then weight in the order the CSS Fonts spec prescribes.
    private static CssFontFace PickFace(List<CssFontFace> faces, int weight, FontSlant slant)
    {
        var pool = faces.Where(f => f.Slant == slant).ToList();

        if (pool.Count == 0 && slant != FontSlant.Normal)
        {
            var other = slant == FontSlant.Italic ? FontSlant.Oblique : FontSlant.Italic;
            pool = faces.Where(f => f.Slant == other).ToList();
        }

        if (pool.Count == 0)
            pool = faces.Where(f => f.Slant == FontSlant.Normal).ToList();

        if (pool.Count == 0)
            pool = faces;

        // On a tie the face registered later wins, as the last @font-face does in CSS.
        var best = pool[0];
        var bestRank = WeightRank(weight, best.Weight);

        for (var i = 1; i < pool.Count; i++)
        {
            var rank = WeightRank(weight, pool[i].Weight);

            if (rank > bestRank)
                continue;

            best = pool[i];
            bestRank = rank;
        }

        return best;
    }

    // Lower rank = better match.
    private static int WeightRank(int wanted, int actual) => wanted switch
    {
        // Between the wanted weight and 500 (ascending), then lighter (descending), then heavier (ascending).
        >= 400 and <= 500 when actual >= wanted && actual <= 500 => actual - wanted,
        >= 400 and <= 500 => actual < wanted ? 1000 + (wanted - actual) : 2000 + (actual - 500),
        < 400 => actual <= wanted ? wanted - actual : 1000 + (actual - wanted),
        _ => actual >= wanted ? actual - wanted : 1000 + (wanted - actual),
    };
}
