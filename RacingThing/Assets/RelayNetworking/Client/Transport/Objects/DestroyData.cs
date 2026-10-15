public readonly struct DestroyData
{
    public readonly uint NetworkId;

    internal DestroyData(uint networkId)
    {
        NetworkId = networkId;
    }
}