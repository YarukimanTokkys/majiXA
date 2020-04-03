namespace majiXA
{
    public interface IMaintenance
    {
        void OnCommand(ServerContext serverContext, byte[] data);
    }
}