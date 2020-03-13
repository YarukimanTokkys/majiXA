namespace majiXA
{
    public interface IProcessing
    {
        void OnMessage(ServerContext serverContext, ConnectionInfo cInfo, byte[] data);
    }
}