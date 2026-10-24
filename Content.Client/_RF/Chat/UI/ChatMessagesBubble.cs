
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Chat.UI;

public sealed partial class ChatMessagesBubble : RfBoxContainer
{
    private int _count;

    public int Count
    {
        get => _count;
        set
        {
            _count = value;
            Label.Text = value.ToString();

            Visible = value > 0;
        }
    }

    public ChatMessagesBubble()
    {
        InitializeComponent();
    }
}

