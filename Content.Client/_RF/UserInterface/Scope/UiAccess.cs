using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._RF.UserInterface.Scope;

/// <summary>
/// The systems and the UI controllers from a control, without a field and a line in the constructor for each.
/// </summary>
public static class UiAccess
{
    extension(Control control)
    {
        /// <summary>
        /// The entity system of the type. Cheap enough to call where it is used, so a control keeps no field for it.
        /// </summary>
        public static T System<T>() where T : IEntitySystem
            => IoCManager.Resolve<IEntityManager>().System<T>();

        /// <summary>
        /// The UI controller of the type.
        /// </summary>
        public T Controller<T>() where T : UIController, new()
            => control.UserInterfaceManager.GetUIController<T>();
    }

    extension(UiScope scope)
    {
        /// <summary>
        /// Subscribes to an event of an entity system while the control is in the tree. The system is found when the control
        /// enters it.
        /// <code>Scope.ListenSystem&lt;ZoningSystem, Entity&lt;ZoneComponent&gt;&gt;((s, h) =&gt; s.OnZoneUpdated += h, (s, h) =&gt; s.OnZoneUpdated -= h, OnZoneUpdate);</code>
        /// </summary>
        public void ListenSystem<TSystem, T>(Action<TSystem, Action<T>> add,
            Action<TSystem, Action<T>> remove,
            Action<T> handler) where TSystem : IEntitySystem
        {
            scope.Listen(System<TSystem>, add, remove, handler);
        }

        /// <inheritdoc cref="ListenSystem{TSystem, T}"/>
        public void ListenSystem<TSystem>(Action<TSystem, Action> add,
            Action<TSystem, Action> remove,
            Action handler) where TSystem : IEntitySystem
        {
            scope.Listen(System<TSystem>, add, remove, handler);
        }

        /// <summary>
        /// Subscribes to an event of a UI controller while the control is in the tree. The controller is found when the control
        /// enters it.
        /// </summary>
        public void ListenController<TController, T>(Action<TController, Action<T>> add,
            Action<TController, Action<T>> remove,
            Action<T> handler) where TController : UIController, new()
        {
            scope.Listen(() => IoCManager.Resolve<IUserInterfaceManager>().GetUIController<TController>(),
                add,
                remove,
                handler);
        }

        /// <inheritdoc cref="ListenController{TController, T}"/>
        public void ListenController<TController>(Action<TController, Action> add,
            Action<TController, Action> remove,
            Action handler) where TController : UIController, new()
        {
            scope.Listen(() => IoCManager.Resolve<IUserInterfaceManager>().GetUIController<TController>(),
                add,
                remove,
                handler);
        }
    }
}
