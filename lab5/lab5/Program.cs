using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Task11
{
    static class Program
    {
        const int writerCount = 4, readerCount = 3, messageCount = 100000;
        const int nBuffer = 10; //to definite

        static bool isExecuted, isBufferFull,isBufferEmpty;
        static int currentReader, currentWriter;
        static string[] buffer;
        static int[] writeIndexCopy, readIndexCopy, writerPriority, readerPriority, countMessageWriter, countMessageReader;
        static double[] timeWriter, timeReader;
        static Task[] readerThreads, writerThreads;
        static void Main(string[] args)
        {
            isExecuted = isBufferFull = false;
            isBufferEmpty = true;
            currentReader = currentWriter = 0;

            timeWriter = new double[writerCount];
            timeReader = new double[readerCount];
            countMessageWriter = new int[writerCount];
            countMessageReader = new int[readerCount];
            writeIndexCopy = new int[writerCount];
            readIndexCopy = new int[readerCount];
            writerPriority = new int[writerCount];
            readerPriority = new int[readerCount];
            buffer = new string[nBuffer];

            for (int index = 0; index < readIndexCopy.Length; index++) readIndexCopy[index] = -1;
            for (int index = 0; index < writeIndexCopy.Length; index++) writeIndexCopy[index] = -1;

            Random random = new Random();

            for (int index = 0; index < writerCount; index++) writerPriority[index] = random.Next(writerCount);
            for (int index = 0; index < readerCount; index++) readerPriority[index] = random.Next(readerCount);

            Manager();

            Console.WriteLine($"Thread priorities are set randomly. Size of the ring buffer is: {nBuffer}.");
            Console.WriteLine($"Number of readers: {readerCount}. Number of writers: {writerCount}. Number of messages every writer: {messageCount}.");
            for (int index = 0; index < writerCount; index++) Console.WriteLine($"Writer #{index} recorded {countMessageWriter[index]} messages in {timeWriter[index]:f2} msec.");
            Console.WriteLine($"Total recorded messages is {countMessageWriter.Sum()}.");
            for (int index = 0; index < readerCount; index++) Console.WriteLine($"Reader #{index} read {countMessageReader[index]} messages in {timeReader[index]:f2} msec.");
            Console.WriteLine($"Total readed messages is {countMessageReader.Sum()}.");
        }
        static void ReaderThread(int currentReadereader, ManualResetEventSlim eventReadyRead, ManualResetEventSlim eventStartReading)
        {
            DateTime timerStart, timerStop;
            timerStart = DateTime.Now;
            var Messages = new List<string>();
            countMessageReader[currentReadereader] = 0;

            while (!isExecuted)
            {
                eventReadyRead.Set();
                eventStartReading.Wait();

                if (isExecuted && eventReadyRead.IsSet) break;

                int k = readIndexCopy[currentReadereader];
                Messages.Add(buffer[k]);
                countMessageReader[currentReadereader]++;
                isBufferFull = false;
                eventStartReading.Reset();
                readIndexCopy[currentReadereader] = -1;
            }
            timerStop = DateTime.Now;
            timeReader[currentReadereader] = new double();
            timeReader[currentReadereader] = (timerStop - timerStart).TotalMilliseconds;
        }
        static void WriterThread(int currentWriterriter, ManualResetEventSlim eventReadyWrite, ManualResetEventSlim eventStartWriting)
        {
            DateTime timerStart, timerStop;
            timerStart = DateTime.Now;
            countMessageWriter[currentWriterriter] = 0;
            string[] Messages = new string[messageCount];

            for (int index = 0; index < Messages.Length; index++) Messages[index] = $"{currentWriterriter}_{index}";

            int jindex = 0;

            while (jindex < Messages.Length)
            {
                eventReadyWrite.Set();
                eventStartWriting.Wait();

                int k = writeIndexCopy[currentWriterriter];
                buffer[k] = Messages[jindex++];
                countMessageWriter[currentWriterriter]++;
                isBufferEmpty = false;
                eventStartWriting.Reset();
                writeIndexCopy[currentWriterriter] = -1;
            }
            timerStop = DateTime.Now;
            timeWriter[currentWriterriter] = (timerStop - timerStart).TotalMilliseconds;
        }
        static void Manager()
        {
            ManualResetEventSlim[] eventStartReading, eventStartWriting;
            ManualResetEventSlim[] eventReadyRead, eventReadyWrite;
            eventReadyRead = new ManualResetEventSlim[readerCount];
            eventStartReading = new ManualResetEventSlim[readerCount];
            eventReadyWrite = new ManualResetEventSlim[writerCount];
            eventStartWriting = new ManualResetEventSlim[writerCount];
            readerThreads = new Task[readerCount];
            writerThreads = new Task[writerCount];

            for (int index = 0; index < readerThreads.Length; index++)
            {
                eventReadyRead[index] = new ManualResetEventSlim(false);
                eventStartReading[index] = new ManualResetEventSlim(false);
                int indexCopy = index;

                readerThreads[index] = new Task(() =>
                {
                    ReaderThread(indexCopy, eventReadyRead[indexCopy], eventStartReading[indexCopy]);
                });
                readerThreads[index].Start();
            }
            for (int index = 0; index < writerThreads.Length; index++)
            {
                eventReadyWrite[index] = new ManualResetEventSlim(false);
                eventStartWriting[index] = new ManualResetEventSlim(false);
                int indexCopy = index;

                writerThreads[index] = new Task(() =>
                {
                    WriterThread(indexCopy, eventReadyWrite[indexCopy], eventStartWriting[indexCopy]);
                });
                writerThreads[index].Start();
            }
            while (!isExecuted)
            {
                if (!isBufferFull && !readIndexCopy.Contains(currentWriter))
                {
                    int currentWriterriter = GetWriter(eventReadyWrite);
                    if (currentWriterriter != -1)
                    {
                        eventReadyWrite[currentWriterriter].Reset();
                        writeIndexCopy[currentWriterriter] = currentWriter;
                        eventStartWriting[currentWriterriter].Set();

                        currentWriter = (currentWriter + 1) % nBuffer;

                        if (currentWriter == currentReader) isBufferFull = true;
                    }
                }
                if (!isBufferEmpty && !writeIndexCopy.Contains(currentReader))
                {
                    int currentReadereader = GetReader(eventReadyRead);
                    if (currentReadereader != -1)
                    {
                        eventReadyRead[currentReadereader].Reset();
                        readIndexCopy[currentReadereader] = currentReader;
                        eventStartReading[currentReadereader].Set();

                        currentReader = (currentReader + 1) % nBuffer;

                        if (currentReader == currentWriter) isBufferEmpty = true;
                    }
                }
                if (writerThreads.All(thread => thread.IsCompleted) && isBufferEmpty) isExecuted = true;
            }
            foreach (var sr in eventStartReading.Where(e => !e.IsSet)) sr.Set();
            Task.WaitAll(readerThreads);
        }
        static int GetWriter(ManualResetEventSlim[] eventReadyWrite)
        {
            var ready = new List<int>();

            for (int index = 0; index < writerCount; index++) if (eventReadyWrite[index].IsSet) ready.Add(index);
            if (ready.Count == 0) return -1;
            return ready.OrderBy(index => writerPriority[index]).First();
        }
        static int GetReader(ManualResetEventSlim[] eventReadyRead)
        {
            var ready = new List<int>();

            for (int index = 0; index < readerCount; index++) if (eventReadyRead[index].IsSet) ready.Add(index);
            if (ready.Count == 0) return -1;
            return ready.OrderBy(index => readerPriority[index]).First();
        }
    }
}