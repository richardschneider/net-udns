using Makaretu.Dns;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Peds
{
    partial class Program
    {
        TaskCompletionSource<int> done = new TaskCompletionSource<int>();
        UdpServer server;
        Nic nic;

        static int Main(string[] args)
        {
            try
            {
                return new Program().RunAsync(args).Result;
            }
            catch (AggregateException e)
            {
                foreach (var ex in e.InnerExceptions)
                {
                    Console.Error.WriteLine(ex.Message);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
            return -1;
        }

        async Task<int> RunAsync(string[] args)
        {
            Console.WriteLine("Hi!");

            // TODO: Process args

            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("CTRL-C");
                Stop();
                e.Cancel = true;
            };

            Start();
            return await done.Task;
        }

        void Start()
        {
            var resolver = new DotClient
            {
                ThrowResponseError = false
            };

            server = new UdpServer { Resolver = resolver };
            server.Start();

            nic = new Nic();
            nic.SetDnsServer(server.Addresses);
        }

        void Stop()
        {
            if (nic != null)
            {
                nic.Dispose();
                nic = null;
            }
            if (server != null)
            {
                server.Dispose();
                server = null;
            }

            done.SetCanceled();
        }
    }
}
