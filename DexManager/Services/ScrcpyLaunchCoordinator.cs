using System;

namespace DexManager.Services
{
    public sealed class ScrcpyLaunchCoordinator
    {
        private readonly object _syncRoot = new object();

        public void RunExclusive(Action action)
        {
            if (action == null) throw new ArgumentNullException("action");
            lock (_syncRoot)
            {
                action();
            }
        }

        public T RunExclusive<T>(Func<T> action)
        {
            if (action == null) throw new ArgumentNullException("action");
            lock (_syncRoot)
            {
                return action();
            }
        }
    }
}
