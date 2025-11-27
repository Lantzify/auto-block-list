namespace AutoBlockList.Hubs
{
    public interface IAutoBlockListHubClient
    {
        void AddReport<TObject>(TObject item);
        void CurrentTask<TObject>(TObject item);
        void UpdateStep<TObject>(TObject item);
        void UpdateItem<TObject>(TObject item);
        void SetTitle<TObject>(TObject item);
        void SetSubTitle<TObject>(TObject item);
        void Done<TObject>(TObject item);
    }
}