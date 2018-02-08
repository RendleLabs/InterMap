using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace InterMap
{
    public class InterMapWriter : IDisposable
    {
        private readonly BlockingCollection<byte[]> _queue = new BlockingCollection<byte[]>(8192);
        private readonly string _name;
        private Thread _thread;
        private CancellationTokenSource _token;
        private MemoryMappedFile _file;
        private MemoryMappedViewStream _stream;

        public InterMapWriter(string name)
        {
            _name = name;
        }

        private void Start()
        {
            _token = new CancellationTokenSource();
            _thread = new Thread(Run);
            _thread.Start();
        }

        public void Stop()
        {
            _token.Cancel();
        }

        private void Run()
        {
            while (!_token.Token.IsCancellationRequested)
            {
                try
                {
                    var data = _queue.Take(_token.Token);
                    Write(data);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        public void Enqueue(byte[] data)
        {
            if (_thread == null)
            {
                Start();
            }
            _queue.Add(data);
        }

        private void Write(byte[] data)
        {
            if (_file == null)
            {
                _file = MemoryMappedFile.CreateOrOpen(_name, 32768, MemoryMappedFileAccess.ReadWrite);
                _stream = _file.CreateViewStream(0, 32768);
            }
            
            WaitToWrite();
            
            _stream.Position = 16;
            _stream.Write(data, 0, data.Length);
            _stream.Position = 8;
            var length = BitConverter.GetBytes(data.Length);
            _stream.Write(length, 0, 4);
            EnableRead();
        }

        private void EnableRead()
        {
            _stream.Position = 0;
            _stream.WriteByte(1);
        }

        private void WaitToWrite()
        {
            int sleep = 1;
            do
            {
                _stream.Position = 0;
                byte b = (byte)_stream.ReadByte();
                if (b == 0) return;
                Thread.Sleep(sleep);
                if (sleep < 64)
                {
                    sleep *= 2;
                }
            } while (!_token.Token.IsCancellationRequested);
        }

        public void Dispose()
        {
            _token?.Cancel();
            _token?.Dispose();
            _queue?.Dispose();
            _stream?.Dispose();
            _file?.Dispose();
        }
    }
}
