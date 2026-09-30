using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading;

using UnityEngine;
using UnityEngine.Networking;

namespace BACKND.Database
{
    [AsyncMethodBuilder(typeof(BTaskMethodBuilder))]
    public class BTask : IEnumerator
    {
        protected bool isCompleted;
        protected Exception exception;
        protected Action continuation;
        protected CancellationToken cancellationToken;
        private CancellationTokenRegistration cancellationRegistration;

        public bool IsCompleted => isCompleted;
        public bool IsFaulted => exception != null;
        public bool IsCanceled => cancellationToken.IsCancellationRequested;

        public Exception GetException() => exception;

        public BTask()
        {
        }

        public BTask(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(OnCanceled);
            }
        }

        protected void ScheduleContinuation(Action action)
        {
            if (action == null)
            {
                return;
            }


#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                void EditorUpdate()
                {
                    UnityEditor.EditorApplication.update -= EditorUpdate;
                    action();
                }
                UnityEditor.EditorApplication.update += EditorUpdate;
                return;
            }
#endif

            DatabaseLoop.OnLateUpdate += ExecuteAction;

            void ExecuteAction()
            {
                DatabaseLoop.OnLateUpdate -= ExecuteAction;
                action();
            }
        }

        public void SetResult()
        {
            if (isCompleted) return;

            isCompleted = true;
            cancellationRegistration.Dispose();
            if (continuation != null)
            {
                ScheduleContinuation(continuation);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                }
#endif
            }
        }

        public void SetException(Exception ex)
        {
            if (isCompleted) return;

            exception = ex;
            isCompleted = true;
            cancellationRegistration.Dispose();
            if (continuation != null)
            {
                ScheduleContinuation(continuation);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                }
#endif
            }
        }

        protected virtual void OnCanceled()
        {
            SetException(new OperationCanceledException(cancellationToken));
        }

        public BTaskAwaiter GetAwaiter()
        {
            return new BTaskAwaiter(this);
        }

        internal void OnCompleted(Action continuation)
        {
            if (isCompleted)
            {
                ScheduleContinuation(continuation);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                }
#endif
                return;
            }
            this.continuation = continuation;
        }

        public BTask ContinueWith(Action<BTask> continuation)
        {
            var nextTask = new BTask();

            OnCompleted(() =>
            {
                try
                {
                    continuation(this);
                    nextTask.SetResult();
                }
                catch (Exception ex)
                {
                    nextTask.SetException(ex);
                }
            });

            return nextTask;
        }

        public BTask<TResult> ContinueWith<TResult>(Func<BTask, TResult> continuation)
        {
            var nextTask = new BTask<TResult>();

            OnCompleted(() =>
            {
                try
                {
                    if (IsFaulted)
                    {
                        nextTask.SetException(GetException());
                    }
                    else
                    {
                        var result = continuation(this);
                        nextTask.SetResult(result);
                    }
                }
                catch (Exception ex)
                {
                    nextTask.SetException(ex);
                }
            });

            return nextTask;
        }

        public object Current => null;

        public bool MoveNext()
        {
            return !IsCompleted;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public static BTask Yield()
        {
            var task = new BTask();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () => task.SetResult();
                return task;
            }
#endif

            task.ScheduleContinuation(() => task.SetResult());
            return task;
        }

        public static BTask NextFrame()
        {
            var task = new BTask();
            var currentFrameTime = Time.realtimeSinceStartup;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () => task.SetResult();
                return task;
            }
#endif

            void CheckFrame()
            {
                if (Time.realtimeSinceStartup > currentFrameTime)
                {
                    task.SetResult();
                }
                else
                {
                    task.ScheduleContinuation(CheckFrame);
                }
            }

            task.ScheduleContinuation(CheckFrame);
            return task;
        }

        public static BTask Delay(float seconds)
        {
            var task = new BTask();
            var targetTime = Time.realtimeSinceStartup + seconds;


#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                void EditorUpdate()
                {
                    if (Time.realtimeSinceStartup >= targetTime)
                    {
                        UnityEditor.EditorApplication.update -= EditorUpdate;
                        task.SetResult();
                    }
                }
                UnityEditor.EditorApplication.update += EditorUpdate;
                return task;
            }
#endif

            void CheckTime()
            {
                if (Time.realtimeSinceStartup >= targetTime)
                {
                    task.SetResult();
                }
                else
                {
                    task.ScheduleContinuation(CheckTime);
                }
            }

            task.ScheduleContinuation(CheckTime);
            return task;
        }

        public static BTask CompletedTask { get; } = CreateCompletedTask();

        private static BTask CreateCompletedTask()
        {
            var task = new BTask();
            task.SetResult();
            return task;
        }
        public static BTask FromException(Exception exception)
        {
            var task = new BTask();
            task.SetException(exception);
            return task;
        }

        public static BTask FromCanceled(CancellationToken cancellationToken)
        {
            var task = new BTask(cancellationToken);
            task.SetException(new OperationCanceledException(cancellationToken));
            return task;
        }

        public static BTask Run(Action action)
        {
            var task = new BTask();

            try
            {
                action();
                task.SetResult();
            }
            catch (Exception ex)
            {
                task.SetException(ex);
            }

            return task;
        }

        public static BTask<T> Run<T>(Func<T> function)
        {
            var task = new BTask<T>();

            try
            {
                var result = function();
                task.SetResult(result);
            }
            catch (Exception ex)
            {
                task.SetException(ex);
            }

            return task;
        }

        public static BTask WhenAll(params BTask[] tasks)
        {
            var resultTask = new BTask();

            if (tasks == null || tasks.Length == 0)
            {
                resultTask.SetResult();
                return resultTask;
            }

            var remainingCount = tasks.Length;
            Exception firstException = null;

            foreach (var task in tasks)
            {
                task.OnCompleted(() =>
                {
                    if (task.IsFaulted && firstException == null)
                    {
                        firstException = task.GetException();
                    }

                    if (Interlocked.Decrement(ref remainingCount) == 0)
                    {
                        if (firstException != null)
                        {
                            resultTask.SetException(firstException);
                        }
                        else
                        {
                            resultTask.SetResult();
                        }
                    }
                });
            }

            return resultTask;
        }

        public static BTask<T[]> WhenAll<T>(params BTask<T>[] tasks)
        {
            var resultTask = new BTask<T[]>();

            if (tasks == null || tasks.Length == 0)
            {
                resultTask.SetResult(new T[0]);
                return resultTask;
            }

            var results = new T[tasks.Length];
            var remainingCount = tasks.Length;
            Exception firstException = null;

            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                var task = tasks[i];

                task.OnCompleted(() =>
                {
                    if (task.IsFaulted)
                    {
                        if (firstException == null)
                        {
                            firstException = task.GetException();
                        }
                    }
                    else
                    {
                        results[index] = task.Result;
                    }

                    if (Interlocked.Decrement(ref remainingCount) == 0)
                    {
                        if (firstException != null)
                        {
                            resultTask.SetException(firstException);
                        }
                        else
                        {
                            resultTask.SetResult(results);
                        }
                    }
                });
            }

            return resultTask;
        }

        public static BTask<BTask> WhenAny(params BTask[] tasks)
        {
            var resultTask = new BTask<BTask>();

            if (tasks == null || tasks.Length == 0)
            {
                resultTask.SetException(new ArgumentException("tasks array is empty"));
                return resultTask;
            }

            var isCompleted = 0;

            foreach (var task in tasks)
            {
                task.OnCompleted(() =>
                {
                    if (Interlocked.CompareExchange(ref isCompleted, 1, 0) == 0)
                    {
                        resultTask.SetResult(task);
                    }
                });
            }

            return resultTask;
        }

    }

    [AsyncMethodBuilder(typeof(BTaskMethodBuilder<>))]
    public class BTask<T> : BTask, IEnumerator
    {
        private T result;

        public T Result
        {
            get
            {
                if (IsFaulted)
                    throw GetException();
                if (!IsCompleted)
                    throw new InvalidOperationException("Task not completed");
                return result;
            }
        }

        public BTask() : base()
        {
        }

        public BTask(CancellationToken cancellationToken) : base(cancellationToken)
        {
        }

        public void SetResult(T value)
        {
            if (isCompleted) return;

            result = value;
            base.SetResult();
        }

        public new BTaskAwaiter<T> GetAwaiter()
        {
            return new BTaskAwaiter<T>(this);
        }

        public static BTask<T> FromResult(T result)
        {
            var task = new BTask<T>();
            task.SetResult(result);
            return task;
        }

        public static new BTask<T> FromException(Exception exception)
        {
            var task = new BTask<T>();
            task.SetException(exception);
            return task;
        }

        public static new BTask<T> FromCanceled(CancellationToken cancellationToken)
        {
            var task = new BTask<T>(cancellationToken);
            task.SetException(new OperationCanceledException(cancellationToken));
            return task;
        }

        public BTask ContinueWith(Action<BTask<T>> continuation)
        {
            var nextTask = new BTask();

            OnCompleted(() =>
            {
                try
                {
                    continuation(this);
                    nextTask.SetResult();
                }
                catch (Exception ex)
                {
                    nextTask.SetException(ex);
                }
            });

            return nextTask;
        }

        public BTask<TResult> ContinueWith<TResult>(Func<BTask<T>, TResult> continuation)
        {
            var nextTask = new BTask<TResult>();

            OnCompleted(() =>
            {
                try
                {
                    if (IsFaulted)
                    {
                        nextTask.SetException(GetException());
                    }
                    else
                    {
                        var result = continuation(this);
                        nextTask.SetResult(result);
                    }
                }
                catch (Exception ex)
                {
                    nextTask.SetException(ex);
                }
            });

            return nextTask;
        }

        public BTask<TResult> Then<TResult>(Func<T, TResult> selector)
        {
            var nextTask = new BTask<TResult>();

            OnCompleted(() =>
            {
                try
                {
                    if (IsFaulted)
                    {
                        nextTask.SetException(GetException());
                    }
                    else
                    {
                        var result = selector(this.Result);
                        nextTask.SetResult(result);
                    }
                }
                catch (Exception ex)
                {
                    nextTask.SetException(ex);
                }
            });

            return nextTask;
        }

        public BTask<TResult> Then<TResult>(Func<T, BTask<TResult>> selector)
        {
            var nextTask = new BTask<TResult>();

            OnCompleted(() =>
            {
                try
                {
                    if (IsFaulted)
                    {
                        nextTask.SetException(GetException());
                    }
                    else
                    {
                        var innerTask = selector(this.Result);
                        innerTask.OnCompleted(() =>
                        {
                            if (innerTask.IsFaulted)
                            {
                                nextTask.SetException(innerTask.GetException());
                            }
                            else
                            {
                                nextTask.SetResult(innerTask.Result);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    nextTask.SetException(ex);
                }
            });

            return nextTask;
        }
    }

    public struct BTaskAwaiter : INotifyCompletion
    {
        private readonly BTask task;

        public BTaskAwaiter(BTask task)
        {
            this.task = task;
        }

        public bool IsCompleted => task.IsCompleted;

        public void GetResult()
        {
            if (task.IsFaulted)
            {
                throw task.GetException();
            }
        }

        public void OnCompleted(Action continuation)
        {
            task.OnCompleted(continuation);
        }
    }

    public struct BTaskAwaiter<T> : INotifyCompletion
    {
        private readonly BTask<T> task;

        public BTaskAwaiter(BTask<T> task)
        {
            this.task = task;
        }

        public bool IsCompleted => task.IsCompleted;

        public T GetResult()
        {
            return task.Result;
        }

        public void OnCompleted(Action continuation)
        {
            task.OnCompleted(continuation);
        }
    }

    public class BTaskMethodBuilder
    {
        private BTask _task;

        public static BTaskMethodBuilder Create()
        {
            return new BTaskMethodBuilder { _task = new BTask() };
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void SetResult()
        {
            _task.SetResult();
        }

        public void SetException(Exception exception)
        {
            _task.SetException(exception);
        }

        public BTask Task => _task;

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.OnCompleted(stateMachine.MoveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.OnCompleted(stateMachine.MoveNext);
        }
    }

    public class BTaskMethodBuilder<T>
    {
        private BTask<T> _task;

        public static BTaskMethodBuilder<T> Create()
        {
            return new BTaskMethodBuilder<T> { _task = new BTask<T>() };
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void SetResult(T result)
        {
            _task.SetResult(result);
        }

        public void SetException(Exception exception)
        {
            _task.SetException(exception);
        }

        public BTask<T> Task => _task;

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.OnCompleted(stateMachine.MoveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            awaiter.OnCompleted(stateMachine.MoveNext);
        }
    }

    public static class BTaskWebRequestExtensions
    {
        public static BTaskWebRequest GetWebRequestAsBTask(this UnityWebRequest request)
        {
            return new BTaskWebRequest(request);
        }

        public static BTaskWebRequest SendWebRequestAsBTask(this UnityWebRequest request, CancellationToken cancellationToken = default)
        {
            request.SendWebRequest();
            return new BTaskWebRequest(request, cancellationToken);
        }
    }

    public class BTaskWebRequest : BTask<UnityWebRequest>
    {
        private readonly UnityWebRequest webRequest;

        public BTaskWebRequest(UnityWebRequest webRequest, CancellationToken cancellationToken = default) : base(cancellationToken)
        {
            this.webRequest = webRequest;

            if (webRequest.isDone)
            {
                HandleCompletion();
            }
            else
            {
                StartPolling();
            }
        }

        protected override void OnCanceled()
        {
            webRequest?.Abort();
            base.OnCanceled();
        }

        private void StartPolling()
        {
            void CheckCompletion()
            {
                if (webRequest.isDone)
                {
                    HandleCompletion();
                }
                else if (!IsCanceled)
                {
                    ScheduleContinuation(CheckCompletion);
                }
            }

            ScheduleContinuation(CheckCompletion);
        }

        private void HandleCompletion()
        {
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                SetResult(webRequest);
            }
            else
            {
                var error = new UnityWebRequestException(
                    webRequest.error,
                    webRequest.responseCode,
                    webRequest.result
                );
                SetException(error);
            }
        }
    }

    public class UnityWebRequestException : Exception
    {
        public long ResponseCode { get; }
        public UnityWebRequest.Result Result { get; }

        public UnityWebRequestException(string message, long responseCode, UnityWebRequest.Result result)
            : base($"WebRequest failed: {message} (Code: {responseCode}, Result: {result})")
        {
            ResponseCode = responseCode;
            Result = result;
        }
    }

    public static class BTaskAsyncOperationExtensions
    {
        public static BTaskAsyncOperation ToBTask(this AsyncOperation operation)
        {
            return new BTaskAsyncOperation(operation);
        }
    }

    public class BTaskAsyncOperation : BTask<AsyncOperation>
    {
        private readonly AsyncOperation operation;

        public BTaskAsyncOperation(AsyncOperation operation) : base()
        {
            this.operation = operation;

            if (operation.isDone)
            {
                SetResult(operation);
            }
            else
            {
                operation.completed += OnCompleted;
            }
        }

        private void OnCompleted(AsyncOperation op)
        {
            SetResult(op);
        }
    }

    public class BTaskCompletionSource
    {
        private BTask _task;
        private bool _completed = false;
        private readonly object _lock = new object();

        public BTaskCompletionSource()
        {
            _task = new BTask();
        }

        public BTask Task => _task;

        public void SetResult()
        {
            lock (_lock)
            {
                if (_completed) return;
                _completed = true;
                _task.SetResult();
            }
        }

        public void SetException(System.Exception exception)
        {
            lock (_lock)
            {
                if (_completed) return;
                _completed = true;
                _task.SetException(exception);
            }
        }

        public void SetCanceled()
        {
            SetException(new System.OperationCanceledException());
        }
    }
}