namespace Pitech.XR.ContentDelivery
{
    public interface ILaunchContextProvider
    {
        bool TryBuildLaunchContext(AddressablesModuleConfig config, out LaunchContext context);
    }
}
