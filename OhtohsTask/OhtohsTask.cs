using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace OhtohsTask;

public class OhtohsTask
{
    private readonly object _lock = new object();

    private Exception? _exception;
    private bool _completed;
    private Action? _continuation;
    private ExecutionContext? _executionContext;

    private Exception? Exception
    {
        get { lock (_lock) return _exception; }
        set
        {
            lock (_lock)
            {
                if (_completed)
                    throw new InvalidOperationException("The task has already been completed.");
                _exception = value;
            }
        }
    }

    public bool Completed
    {
        get { lock (_lock) return _completed; }
        private set
        {
            lock (_lock)
            {
                if (_completed)
                    throw new InvalidOperationException("The task has already been completed.");
                _completed = value;
            }
        }
    }

    private void SetException(Exception ex) => Exception = ex;
    private void SetCompleted() => Completed = true;

    public static OhtohsTask Run(Action action)
    {
        var task = new OhtohsTask();

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                action();
                task.MarkCompleted();
            }
            catch (Exception e)
            {
                task.MarkFailed(e);
            }
        });

        return task;
    }

    private void MarkCompleted()
    {
        Completed = true;
        RunContinuation();
    }

    private void MarkFailed(Exception e)
    {
        Exception = e;
        Completed = true;
        RunContinuation();
    }

    private void RunContinuation()
    {
        if (_continuation is not null)
        {
            var continuation = _continuation;
            var context = _executionContext;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (context is not null)
                    ExecutionContext.Run(context, _ => continuation(), null);
                else
                    continuation();
            });
        }
    }

    public void Wait()
    {
        ManualResetEventSlim? resetEvent = null;

        lock (_lock)
        {
            if (!Completed)
            {
                resetEvent = new ManualResetEventSlim(false);
                ContinueWith(() => resetEvent.Set());
            }
        }

        resetEvent?.Wait();
        resetEvent?.Dispose();

        if (_exception is not null)
            ExceptionDispatchInfo.Throw(_exception);
    }

    public OhtohsTask ContinueWith(Action continuation)
    {
        var nextTask = new OhtohsTask();

        lock (_lock)
        {
            if (_completed)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        continuation();
                        nextTask.MarkCompleted();
                    }
                    catch (Exception e)
                    {
                        nextTask.MarkFailed(e);
                    }
                });
            }
            else
            {
                _continuation = () =>
                {
                    try
                    {
                        continuation();
                        nextTask.MarkCompleted();
                    }
                    catch (Exception e)
                    {
                        nextTask.MarkFailed(e);
                    }
                };

                _executionContext = ExecutionContext.Capture();
            }
        }

        return nextTask;
    }

    public OhtohsTaskAwaiter GetAwaiter() => new(this);

    public override string ToString() =>
        $"OhtohsTask (Completed={Completed}, Exception={_exception?.Message ?? "None"})";
}

public readonly struct OhtohsTaskAwaiter : INotifyCompletion
{
    private readonly OhtohsTask _task;

    internal OhtohsTaskAwaiter(OhtohsTask task) => _task = task;

    public void OnCompleted(Action continuation) => _task.ContinueWith(continuation);
    public void GetResult() => _task.Wait();
    public bool IsCompleted => _task.Completed;
}
