using Content.Client._RF.Suil;
using Content.Client._RF.UserInterface;
using Robust.Client.UserInterface;

namespace Content.Client._RF.UserInterface.Controls.Table;

public sealed partial class TableRow : RfBoxContainer
{
    private readonly TableControl _table = default!;
    private int _index;

    public int Index
    {
        get => _index;
        set
        {
            _index = value;

            foreach (var cell in Cells)
            {
                cell.Row = _index;
            }
        }
    }

    public List<TableCell> Cells { get; private set; } = new();

    public int Columns => Cells.Count;

    public TableRow()
    {
        InitializeComponent();
    }

    public TableRow(TableControl table, int index)
    {
        InitializeComponent();
        Index = index;
        _table = table;

        while (Columns < table.Columns)
        {
            InsertCell(Columns);
        }
    }

    public TableCell InsertCell(int column)
    {
        column = Math.Clamp(column, 0, Columns);

        var cell = new TableCell(_table, column, Index);

        cell.Row = Index;
        cell.Column = column;
        Cells.Insert(column, cell);

        for (var i = column + 1; i < Columns; i++)
        {
            Cells[i].Column++;
        }

        if (column == Columns)
        {
            if (Columns != 0)
                AddChild(Divider());

            AddChild(cell);
        }
        else
            Rebuild();

        return cell;
    }

    private static UiElement Divider()
    {
        var divider = new UiElement();
        divider.AddSuilStyleClass(SuilClass.LowDivider);
        return divider;
    }

    public void Rebuild()
    {
        RemoveAllChildren();

        for (var i = 0; i < Columns; i++)
        {
            if (i != 0)
                AddChild(Divider());

            AddChild(Cells[i]);
        }
    }
}

