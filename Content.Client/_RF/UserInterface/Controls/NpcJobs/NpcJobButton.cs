using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.UserInterface.Controls.NpcJobs;

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
    }

    private void OnEditNamePressed()
    {
        LabelContainer.Visible = false;
        NameEdit.Visible = true;
        NameEdit.SelectionStart = 0;
        NameEdit.CursorPosition = NameEdit.Text.Length;
    }

    private void OnNameEntered()
    {
        LabelContainer.Visible = true;
        NameEdit.Visible = false;
        JobName = NameEdit.Text;
        OnDataChanged?.Invoke();
    }

    private void OnDeletePressed()
    {
        OnDeleted?.Invoke();
        Parent?.RemoveChild(this);
    }
}

