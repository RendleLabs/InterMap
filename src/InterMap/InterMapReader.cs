using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace InterMap
{
    public class InterMapReader : IObservable<byte[]>
    {
        private readonly string _name;
        private MemoryMappedFile _file;
        private MemoryMappedViewStream _stream;
        private IObserver<byte[]> _observer;
        private CancellationToken _token;
        private Thread _thread;
        private readonly byte[] _length = new byte[4];

        public InterMapReader(string name)
        {
            _name = name;
        }

        public IDisposable Subscribe(IObserver<byte[]> observer)
        {
            if (_observer != null)
            {
                throw new InvalidOperationException();
            }

            _observer = observer;
            _file = MemoryMappedFile.CreateOrOpen(_name, 32768, MemoryMappedFileAccess.ReadWrite);
            _stream = _file.CreateViewStream(0, 32768);
            _thread = new Thread(Run);
            var cts = new CancellationTokenSource();
            _token = cts.Token;
            _thread.Start();
            return new Subscription(cts);
        }

        private void Run()
        {
            while (!_token.IsCancellationRequested)
            {
                WaitToRead();
                _stream.Position = 8;
                _stream.Read(_length, 0, 4);
                int length = BitConverter.ToInt32(_length, 0);
                if (length > 0)
                {
                    var buffer = ReadBuffer(length);

                    EnableWrite();
                    _observer.OnNext(buffer);
                }
                else
                {
                    EnableWrite();
                }
            }
        }

        private byte[] ReadBuffer(int length)
        {
            _stream.Position = 16;
            int read = 0;
            var buffer = new byte[length];
            while (read < length)
            {
                read += _stream.Read(buffer, read, length - read);
            }

            return buffer;
        }

        private void EnableWrite()
        {
            _stream.Position = 0;
            _stream.WriteByte(0);
        }

        private void WaitToRead()
        {
            int sleep = 1;
            do
            {
                _stream.Position = 0;
                int ready = _stream.ReadByte();
                if (ready == 1)
                {
                    return;
                }

                Thread.Sleep(sleep);
                if (sleep < 64)
                {
                    sleep *= 2;
                }
            } while (!_token.IsCancellationRequested);
        }

        private class Subscription : IDisposable
        {
            private readonly CancellationTokenSource _cts;

            public Subscription(CancellationTokenSource cts)
            {
                _cts = cts;
            }

            public void Dispose()
            {
                _cts.Cancel();
            }
        }
    }
}