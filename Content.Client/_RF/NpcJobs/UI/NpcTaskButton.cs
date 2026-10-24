using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Prototypes;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.NpcJobs.UI;

public sealed partial class NpcTaskButton : RfBoxContainer
{
    public NpcTaskButton()
    {
        InitializeComponent();
    }

    public NpcTaskButton(ProtoId<UtilityAiGoalPrototype> protoId)
    {
        InitializeComponent();
        var proto = IoCManager.Resolve<IPrototypeManager>().Index(protoId);

        Label.Text = Loc.GetString(proto.Name);
        Label.TextColor = proto.Color;
        Label.SuilSetHard("color");
    }
}

