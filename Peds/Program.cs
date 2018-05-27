using Makaretu.Dns;
using System;
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

            Console.CancelKeyPress += async (s, e) =>
            {
                Console.WriteLine("CTRL-C");
                await StopAsync();
                e.Cancel = true;
            };

            await StartAsync();
            return await done.Task;
        }

        async Task StartAsync()
        {
            var resolver = new DotClient();

            server = new UdpServer { Resolver = resolver };
            await server.StartAsync();

            nic = new Nic();
            nic.SetDnsServer(server.Addresses);
        }

        async Task StopAsync()
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
