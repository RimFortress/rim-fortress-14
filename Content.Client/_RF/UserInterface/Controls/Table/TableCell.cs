using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls.Table;

public sealed partial class TableCell : RfBoxContainer
{
    private readonly TableControl _table = default!;

    public int Column;
    public int Row;

    public TableCell()
    {
        InitializeComponent();
    }

    public TableCell(TableControl table, int  column, int row)
    {
        InitializeComponent();
        _table = table;
        Column = column;
        Row = row;
    }

    protected override void ChildAdded(Control child)
    {
        base.ChildAdded(child);
        _table.MarkLayoutDirty();
    }

    protected override void ChildRemoved(Control child)
    {
        base.ChildRemoved(child);
        _table.MarkLayoutDirty();
    }
}

