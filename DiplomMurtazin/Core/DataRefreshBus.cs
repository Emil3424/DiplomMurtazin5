using System;

namespace DiplomMurtazin.Core
{
    public static class DataRefreshBus
    {
        public static event Action<int> ExternalChangesDetected;

        public static void PublishExternalChanges(int count)
        {
            ExternalChangesDetected?.Invoke(count);
        }
    }
}
