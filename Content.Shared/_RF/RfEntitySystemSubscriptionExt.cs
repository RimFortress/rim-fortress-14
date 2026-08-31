using Robust.Shared.Prototypes;

namespace Content.Shared._RF;

public static class RfEntitySystemSubscriptionExt
{
    extension(EntitySystem.Subscriptions subs)
    {
        /// <summary>
        /// Creates a subscription to reload prototypes of a specific type.
        /// </summary>
        /// <param name="protoMan">Prototypes manager.</param>
        /// <param name="onReloaded">An action that will be invoked when the prototypes are reloaded.</param>
        /// <typeparam name="T">Reloaded prototype type.</typeparam>
        public void ProtoReload<T>(IPrototypeManager protoMan, Action onReloaded) where T : IPrototype
        {
            protoMan.PrototypesReloaded += Handle;
            subs.RegisterUnsubscription(() => protoMan.PrototypesReloaded -= Handle);
            return;

            void Handle(PrototypesReloadedEventArgs args)
            {
                if (args.WasModified<T>())
                    onReloaded();
            }
        }

        /// <inheritdoc cref="ProtoReload{T}"/>
        public void ProtoReload<T1, T2>(IPrototypeManager protoMan, Action onReloaded)
            where T1 : IPrototype
            where T2 : IPrototype
        {
            protoMan.PrototypesReloaded += Handle;
            subs.RegisterUnsubscription(() => protoMan.PrototypesReloaded -= Handle);
            return;

            void Handle(PrototypesReloadedEventArgs args)
            {
                if (args.WasModified<T1>() || args.WasModified<T2>())
                    onReloaded();
            }
        }
    }
}
