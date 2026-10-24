using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.NpcJobs.UI;

public sealed partial class NpcJobButton : RfButton
{
    public event Action? OnDataChanged;
    public event Action? OnDeleted;

    public int JobId { get; }

    public string JobName
    {
        get => NameLabel.Text ?? string.Empty;
        set
        {
            NameLabel.Text = value;
            NameEdit.Text = value;
        }
    }

    public List<ProtoId<UtilityAiGoalPrototype>>? Goals;

    public NpcJobButton()
    {
        InitializeComponent();
        EnsureSetup();
    }

    public NpcJobButton(NpcJob job)
    {
        InitializeComponent();

        JobId = job.Id;
        JobName = job.Name;

        if (job.Icon != null)
            Icon.SetTexture(IoCManager.Resolve<IEntityManager>().System<SpriteSystem>().Frame0(job.Icon));

        EnsureSetup();
    }

    private void EnsureSetup()
    {
        Icon.OnTextureChanged += _ => OnDataChanged?.Invoke();

        NameEdit.OnTextEntered += _ =>
        {
            LabelContainer.Visible = true;
            NameEdit.Visible = false;
            JobName = NameEdit.Text;
            NotifyDataChanged();
        };

        EditNameButton.OnPressed += _ =>
        {
            LabelContainer.Visible = false;
            NameEdit.Visible = true;
            SelectName();
        };

        DeleteButton.OnPressed += _ => OnDeletePressed();
    }

    private void SelectName()
    {
        NameEdit.SelectionStart = 0;
        NameEdit.CursorPosition = NameEdit.Text.Length;
    }

    private void NotifyDataChanged() => OnDataChanged?.Invoke();

    private void OnDeletePressed()
    {
        OnDeleted?.Invoke();
        Parent?.RemoveChild(this);
    }
}

