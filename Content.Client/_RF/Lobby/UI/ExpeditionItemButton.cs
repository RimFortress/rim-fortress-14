using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Lobby.UI;

public sealed partial class ExpeditionItemButton : RfBoxContainer
{
    [Dependency] private IPrototypeManager _protoManager = default!;

    public event Action<(int Old, int New)>? OnPointsSet;

    public int Count
    {
        get;
        set
        {
            field = value;
            Counter.SetValue(value);
        }
    }

    public int Cost { get; private set; }

    /// <summary>
    /// The name of the item as plain text, the text of <see cref="NameLabel"/> is a markup.
    /// </summary>
    public string ItemName { get; private set; } = string.Empty;

    public ExpeditionItemButton(EntProtoId protoId, int cost, int count)
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        Count = count;
        Cost = cost;

        if (!_protoManager.TryIndex(protoId, out var proto))
            return;

        View.SetPrototype(protoId);

        ItemName = proto.Name;
        NameLabel.SetPlainText(proto.Name);
        NameLabel.ToolTip = proto.Description;
        CostLabel.Text = $"{Loc.GetString("expedition-equipment-editor-points")}: {Cost}";
    }

    private void OnCountChanged((int Old, int New) args)
    {
        Count = args.New;
        OnPointsSet?.Invoke((args.Old * Cost, args.New * Cost));
    }
}
