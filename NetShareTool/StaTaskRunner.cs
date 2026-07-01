namespace NetShareTool;

public static class StaTaskRunner
{
    public static Task Run(Action action)
    {
        return Run(() =>
        {
            action();
            return true;
        });
    }

    public static Task<T> Run<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>();

        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return completion.Task;
    }
}
