using System;
using System.Collections.Generic;
using System.Threading;

namespace ConsoleApp2
{
    class SyncToAsyncCaller
    {
        static void Delegate(string[] args)
        {
            // Create the delegate.
            var caller = new AsyncMethodCaller(AddStringToList);

            var stringList = new List<string>();
            // Initiate the asychronous call.
            IAsyncResult result = caller.BeginInvoke(3000, ref stringList, null, null);

            Console.Write("Working....");

            var spin = new ConsoleSpiner();
            while (result.IsCompleted == false)
            {
                spin.Turn();
                Thread.Sleep(100);
            }

            foreach (string str in stringList)
            {
                Console.WriteLine(str);
            }

            Console.WriteLine("Execution has finished!");
            Console.ReadLine();
        }

        public delegate List<string> AsyncMethodCaller(int callDuration, ref List<string> stringList);
        public static List<string> AddStringToList(int callDuration, ref List<string> stringList)
        {
            Console.WriteLine("Test method begins.");
            Thread.Sleep(callDuration);
            stringList.Add("1");
            stringList.Add("2");
            stringList.Add("3");
            stringList.Add("4");
            stringList.Add("5");
            stringList.Add("6");
            stringList.Add("7");
            stringList.Add("8");
            return stringList;
        }

    }

    public class ConsoleSpiner
    {
        int counter;
        public ConsoleSpiner()
        {
            counter = 0;
        }
        public void Turn()
        {
            counter++;
            switch (counter % 4)
            {
                case 0: Console.Write("/"); break;
                case 1: Console.Write("-"); break;
                case 2: Console.Write("\\"); break;
                case 3: Console.Write("|"); break;
            }
            Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
        }
    }

}
