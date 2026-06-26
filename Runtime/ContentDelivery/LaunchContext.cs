using System;

namespace Pitech.XR.ContentDelivery
{
    public enum LaunchSource
    {
        ReactNativeBridge = 0,
        UnityMenu = 1,
        Direct = 2,
    }

    [Serializable]
    public sealed class LaunchContext
    {
        public string contractVersion = "1.1.0";   // UNCHANGED at B.1: the new tenant/user/locale fields are additive + empty (inert). Bumping this is NOT inert - LaunchContextReporter copies it into the emitted LaunchLifecyclePayload - so bump to 1.2.0 at G2, once the fields are populated AND the Web Portal / cloud aligns.
        public string launchRequestId = string.Empty;
        public string attemptId = string.Empty;
        public string idempotencyKey = string.Empty;
        public string labId = string.Empty;
        public string addressKey = string.Empty;
        public string resolvedVersionId = string.Empty;
        public string runtimeUrl = string.Empty;
        public bool launchedFromCache;
        public bool allowOfflineCacheLaunch;
        public bool allowOlderCachedSameLab;
        public bool networkRequiredIfCacheMiss;
        public LaunchSource source = LaunchSource.Direct;
        public string requestedAt = string.Empty;

        // ---- B.1 additions (additive; default empty so existing payloads round-trip) ----
        // Tenant + user id from auth at launch (map sec-11.5, RESOLVED 2026-06-23): tenant -> org
        // isolation; user -> the session report's user list. The report envelope is stamped tenant +
        // session + lab/version so the cloud asserts "report tenant == auth tenant, else reject" (RLS).
        // Role + attempt are created IN-SCENE, not here. CROSS-SURFACE: freezes at G2 (2026-06-29) -
        // the Web Portal / cloud side must align.
        public string tenantId = string.Empty;
        public string userId = string.Empty;

        // Per-client UI locale (map sec-12, WS B1.5 Step 1): NEVER networked - each client renders in
        // its own language. Resolved from the host/device at launch; empty = host default.
        public string locale = string.Empty;
    }

    public static class LaunchContextFactory
    {
        public static LaunchContext CreateUnityMenuContext(
            string labId,
            string resolvedVersionId,
            string runtimeUrl,
            AddressablesModuleConfig config)
        {
            AttemptIdentity identity = AttemptIdentityManager.CreateLocalFirst(labId);
            return new LaunchContext
            {
                launchRequestId = identity.launchRequestId,
                attemptId = identity.attemptId,
                idempotencyKey = identity.idempotencyKey,
                labId = Safe(labId),
                resolvedVersionId = Safe(resolvedVersionId),
                runtimeUrl = Safe(runtimeUrl),
                source = LaunchSource.UnityMenu,
                requestedAt = Timestamp.UtcNowIso8601(),
                allowOfflineCacheLaunch = config == null || config.allowOfflineCacheLaunch,
                allowOlderCachedSameLab = config == null || config.allowOlderCachedSameLab,
                networkRequiredIfCacheMiss = config == null || config.networkRequiredIfCacheMiss,
            };
        }

        public static LaunchContext CreateDirectContext(AddressablesModuleConfig config)
        {
            AttemptIdentity identity = AttemptIdentityManager.CreateLocalFirst("direct");
            return new LaunchContext
            {
                launchRequestId = identity.launchRequestId,
                attemptId = identity.attemptId,
                idempotencyKey = identity.idempotencyKey,
                labId = "direct",
                source = LaunchSource.Direct,
                requestedAt = Timestamp.UtcNowIso8601(),
                allowOfflineCacheLaunch = config == null || config.allowOfflineCacheLaunch,
                allowOlderCachedSameLab = config == null || config.allowOlderCachedSameLab,
                networkRequiredIfCacheMiss = config == null || config.networkRequiredIfCacheMiss,
            };
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
