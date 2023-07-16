using System.Collections;

namespace FastText
{
    public class WordCollection : IEnumerable<string>
    {
        private readonly Queue<object> Queue_ = new();
        private bool HasCompletedAdding_ = true;
        private Barrier? StreamBarrier_ = null;
        private CancellationTokenSource CancellationTokenSource_ = new();

        public void Add(string word)
        {
            ReadOnlySpan<char> line = word.AsSpan().Trim();

            int i = 0;
            int start = 0;
            int limit = line.Length;
            while (i < limit)
            {
                if (!char.IsWhiteSpace(line[i]))
                {
                    i++;
                }
                else
                {
                    string w = line.Slice(start, i - start).ToString();
                    Queue_.Enqueue(w);
                }
            }
        }

        public void StartAdding()
        {
            HasCompletedAdding_ = false;
        }

        public void CompletedAdding()
        {
            HasCompletedAdding_ = true;
        }

        public void WaitForSignal()
        {
            CancellationTokenSource_.Cancel();
            StreamBarrier_ = new Barrier(2);
            StreamBarrier_.SignalAndWait();
            CancellationTokenSource_ = new();
        }

        public void Add(IEnumerable<string> words)
        {
            Queue_.Enqueue(words);
        }

        IEnumerator<string> IEnumerable<string>.GetEnumerator()
        {
            while (Queue_.TryDequeue(out object? current) || !HasCompletedAdding_)
            {
                // check for queue empty
                if (current is null)
                {
                    if (StreamBarrier_ is null)
                    {
                        try
                        {
                            Task.Delay(TimeSpan.FromSeconds(1), CancellationTokenSource_.Token);
                        }
                        catch { }
                    }
                    else
                    {
                        // signal for more data to be streamed
                        StreamBarrier_.SignalAndWait();

                        Queue_.TryDequeue(out current);
                    }
                }
                else if (current is string str)
                {
                    yield return str;
                }
                else if (current is IEnumerable<string> list)
                {
                    foreach (string enumStr in list)
                    {
                        yield return enumStr;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)this;
        }
    }
}
