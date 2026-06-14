using System;
using Pitech.XR.Core;

namespace Pitech.XR.ContentDelivery
{
    public interface IContentDeliveryService : IXRService
    {
        bool IsReady { get; }
        LaunchContext CurrentContext { get; }
        event Action<LaunchContext> OnLaunchContextResolved;
        void SetLaunchContext(LaunchContext context);
        bool TryGetCurrentContext(out LaunchContext context);
        bool TryReconcileAttempt(string launchRequestId, string canonicalAttemptId);
    }
}
