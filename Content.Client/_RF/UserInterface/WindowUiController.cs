using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._RF.UserInterface;

public abstract class WindowUiController<T> : UIController where T : BaseWindow, new()
{
    protected T? Window;

    public void ToggleWindow()
    {
        if (Window == null || Window.Disposed)
            EnsureWindow();

        if (Window!.IsOpen)
            Window.Close();
        else
            Window.Open();
    }

    public virtual void OpenWindow()
    {
        if (Window == null || Window.Disposed)
            EnsureWindow();

        if (!Window!.IsOpen)
            Window.Open();
    }

    public virtual void CloseWindow()
    {
        Window?.Close();
    }

    protected virtual T EnsureWindow()
    {
        Window = UIManager.CreateWindow<T>();
        return Window;
    }
}
