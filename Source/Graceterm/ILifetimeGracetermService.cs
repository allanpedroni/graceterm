namespace Graceterm
{
    public interface ILifetimeGracetermService
    {
        bool StopRequested { get; }
        void IncrementRequestCount();
        void DecrementRequestCount();
    }
}
