using Content.Client._RF.UserInterface;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    /// <summary>
    /// Is the cursor over this control? A hovered control matches <c>:hover</c>.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool Hovered
    {
        get;
        private set
        {
            if (field == value)
                return;

            field = value;
            SetCssPseudoStyleClass(CssPseudoClass.Hover, value);
        }
    }

    /// <summary>
    /// Does this control have keyboard focus? A focused control matches <c>:focus</c>.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool Focused
    {
        get;
        private set
        {
            if (field == value)
                return;

            field = value;
            SetCssPseudoStyleClass(CssPseudoClass.Focus, value);
        }
    }

    /// <summary>
    /// Whether the control is disabled. A disabled control matches <c>:disabled</c>;
    /// controls that handle input must ignore it while this is <c>true</c>.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool Disabled
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            SetCssPseudoStyleClass(CssPseudoClass.Disabled, value);
            OnDisabledChanged();
        }
    }

    /// <summary>
    /// Whether the control is being pressed right now. An active control matches <c>:active</c>.
    /// Controls that react to a press are responsible for setting it.
    /// </summary>
    [PublicAPI, ViewVariables]
    public bool Active
    {
        get;
        protected set
        {
            if (field == value)
                return;

            field = value;
            SetCssPseudoStyleClass(CssPseudoClass.Active, value);
        }
    }

    /// <summary>
    /// Called after <see cref="Disabled"/> has changed.
    /// </summary>
    protected virtual void OnDisabledChanged() { }

    /// <summary>
    /// Adds or removes a pseudo-class depending on <paramref name="value"/>.
    /// </summary>
    [PublicAPI]
    public void SetCssPseudoStyleClass(string className, bool value)
    {
        if (value)
            AddCssPseudoStyleClass(className);
        else
            RemoveCssPseudoStyleClass(className);
    }
}
