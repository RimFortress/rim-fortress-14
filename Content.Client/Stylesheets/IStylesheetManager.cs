using Robust.Client.UserInterface;

namespace Content.Client.Stylesheets
{
    public interface IStylesheetManager
    {
        Stylesheet SheetNano { get; }
        Stylesheet SheetSpace { get; }
        Stylesheet SheetFortress { get; } // RimFortress

        void Initialize();
    }
}
