namespace Graceterm
{
    public interface ILifetimeGracetermService //: IHostedService
    {
        bool StopRequested { get; }
        void IncrementRequestCount();
        void DecrementRequestCount();
    }

}
