namespace FastText
{
    public class TextStreamParser
    {
        private Stream stream_;
        public TextStreamParser(Stream stream)
        {
            stream_ = stream;
        }

        public WordCollection AsWordStream()
        {
            WordCollection w = new();
            w.StartAdding();

            Task.Run(() =>
            {
                TextReader r = new StreamReader(stream_);
                string? line;
                do
                {
                    line = r.ReadLine();
                    if (line is not null)
                    {
                        w.Add(line);
                        w.WaitForSignal();
                    }
                    else
                    {
                    }
                } while (stream_.CanRead);

                w.CompletedAdding();
            });

            return w;
        }
    }
}
