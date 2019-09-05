using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Akka.Actor;

namespace WinTail
{
    /// <summary>
    /// Monitors the file at <see cref="_filePath"/> for changes and sends
    /// file updates to console.
    /// </summary>
    public class TailActor : UntypedActor
    {
        #region Message types

        /// <summary>
        /// Signal that the file has changed, and we need to 
        /// read the next line of the file.
        /// </summary>
        public class FileWrite
        {
            public FileWrite(string fileName)
            {
                FileName = fileName;
            }

            public string FileName { get; private set; }
        }

        /// <summary>
        /// Signal that the OS had an error accessing the file.
        /// </summary>
        public class FileError
        {
            public FileError(string fileName, string reason)
            {
                FileName = fileName;
                Reason = reason;
            }

            public string FileName { get; private set; }

            public string Reason { get; private set; }
        }

        /// <summary>
        /// Signal to read the initial contents of the file at actor startup.
        /// </summary>
        public class InitialRead
        {
            public InitialRead(string fileName, string text)
            {
                FileName = fileName;
                Text = text;
            }

            public string FileName { get; private set; }
            public string Text { get; private set; }
        }

        #endregion

        private readonly string _filePath;
        private readonly string _fullFilePath;
        private long _previousLength = 0;
        private readonly IActorRef _reporterActor;
        private readonly FileObserver _observer;
//        private readonly Stream _fileStream;
//        private readonly StreamReader _fileStreamReader;

        public TailActor(IActorRef reporterActor, string filePath)
        {
            _reporterActor = reporterActor;
            _filePath = filePath;

            // start watching file for changes
            _observer = new FileObserver(Self, Path.GetFullPath(_filePath));
            _observer.Start();

            // open the file stream with shared read/write permissions
            // (so file can be written to while open)
            _fullFilePath = Path.GetFullPath(_filePath);
//            _fileStream = new FileStream(_fullFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
//            _fileStreamReader = new StreamReader(_fileStream, Encoding.UTF8);

            
            
            // read the initial contents of the file and send it to console as first msg
            var text = GetNewText();
            Self.Tell(new InitialRead(_filePath, text));
        }

        private string GetNewText()
        {
            using (var fs = new FileStream(_fullFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var fsr = new StreamReader(fs, Encoding.UTF8))
            {
                fs.Seek(_previousLength, SeekOrigin.Begin);
                _previousLength = fs.Length;
                return fsr.ReadToEnd();
            }
        }
        
        protected override void OnReceive(object message)
        {
            if (message is FileWrite)
            {
                // move file cursor forward
                // pull results from cursor to end of file and write to output
                // (this is assuming a log file type format that is append-only)
                var text = GetNewText();
                if (!string.IsNullOrEmpty(text))
                {
                    _reporterActor.Tell(text);
                }

            }
            else if (message is FileError fe)
            {
                _reporterActor.Tell($"Tail error: {fe.Reason}");
            }
            else if (message is InitialRead ir)
            {
                _reporterActor.Tell(ir.Text);
            }
        }
    }
}