
using System;

namespace SearchHelper.Editor.Core
{
    interface IDataObserver
    {
        public event Action DataChanged;
    }

    public abstract class ObservableData : IDataObserver
    {
        public event Action DataChanged;
        public bool RequiresUpdate { get; private set; }

        protected void OnDataChanged()
        {
            RequiresUpdate = true;
            DataChanged?.Invoke();
        }

        public void CompleteUpdate()
        {
            RequiresUpdate = false;
        }
    }
}
