namespace Pitech.XR.ContentDelivery
{
    public interface IContentDeliveryMetadataProvider
    {
        bool TryResolveLabMetadata(string labId, out string resolvedVersionId, out string runtimeCatalogUrl);
    }
}
