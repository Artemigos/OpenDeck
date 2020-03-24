using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using OpenDeck.Protocol;

namespace OpenDeck.Client.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // setup client
            var channel = new Channel("127.0.0.1", 8020, ChannelCredentials.Insecure);
            var cli = new Device.DeviceClient(channel);

            // setup event polling
            var q = new ConcurrentQueue<Event>();
            var cancel = new CancellationTokenSource();
            var t = Task.Run(async () =>
            {
                using var enumerator = cli.GetEventStream(new Google.Protobuf.WellKnownTypes.Empty(), cancellationToken: cancel.Token);

                while (!cancel.IsCancellationRequested && await enumerator.ResponseStream.MoveNext())
                {
                    q.Enqueue(enumerator.ResponseStream.Current);
                }
            });

            // run user commands
            while (true)
            {
                Console.Write("> ");
                var command = Console.ReadLine();

                if (command == "exit")
                {
                    break;
                }

                if (command == "flush")
                {
                    while (q.TryDequeue(out var ev))
                    {
                        Console.Write(ev.EventCase);
                        var pos = ev.ButtonClickEvent?.Button ?? ev.ButtonDownEvent?.Button ?? ev.ButtonUpEvent.Button;
                        Console.WriteLine($" {pos.X} {pos.Y}");
                    }
                }
                else if (command.StartsWith("label "))
                {
                    var arguments = command.Substring(command.IndexOf(' ') + 1);
                    var xStr = arguments.Substring(0, arguments.IndexOf(' '));
                    var x = uint.Parse(xStr);
                    arguments = arguments.Substring(arguments.IndexOf(' ') + 1);
                    var yStr = arguments.Substring(0, arguments.IndexOf(' '));
                    var y = uint.Parse(yStr);
                    arguments = arguments.Substring(arguments.IndexOf(' ') + 1);

                    await cli.SetButtonLabelAsync(new SetButtonLabelRequest { Button = new ButtonPos { X = x, Y = y }, Label = arguments });
                }
                else if (command == "info")
                {
                    var meta = await cli.GetMetaAsync(new Google.Protobuf.WellKnownTypes.Empty());
                    Console.WriteLine(meta.DeviceId);
                    Console.WriteLine(meta.DeviceTypeId);
                }
            }

            // cleanup
            cancel.Cancel();
            await t;
        }
    }
}
