using System;

namespace ChatExample1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var srv = new Server(8080);
            srv.Start();
            Console.ReadLine();
        }
    }
}
