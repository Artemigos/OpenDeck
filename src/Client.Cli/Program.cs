using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using OpenDeck.Protocol;
using SixLabors.ImageSharp.Advanced;

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

            try
            {
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
                    else if (command.StartsWith("size "))
                    {
                        var arguments = command.Substring(command.IndexOf(' ') + 1);
                        var wStr = arguments.Substring(0, arguments.IndexOf(' '));
                        var w = uint.Parse(wStr);
                        arguments = arguments.Substring(arguments.IndexOf(' ') + 1);
                        var h = uint.Parse(arguments);

                        await cli.SetGridSizeAsync(new Size { Width = w, Height = h });
                    }
                    else if (command.StartsWith("image "))
                    {
                        var arguments = command.Substring(command.IndexOf(' ') + 1);
                        var xStr = arguments.Substring(0, arguments.IndexOf(' '));
                        var x = uint.Parse(xStr);
                        arguments = arguments.Substring(arguments.IndexOf(' ') + 1);
                        var yStr = arguments.Substring(0, arguments.IndexOf(' '));
                        var y = uint.Parse(yStr);
                        arguments = arguments.Substring(arguments.IndexOf(' ') + 1);
                        var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(arguments);
                        var data = MemoryMarshal.AsBytes(img.GetPixelSpan()).ToArray();

                        await cli.SetButtonImageAsync(new SetButtonImageRequest
                        {
                            Button = new ButtonPos { X = x, Y = y },
                            Image = new Image
                            {
                                Size = new Size { Width = (uint)img.Width, Height = (uint)img.Height },
                                PixelData = Google.Protobuf.ByteString.CopyFrom(data),
                                Format = Image.Types.Format.Rgba32
                            }
                        });
                    }
                    else if (command == "info")
                    {
                        var meta = await cli.GetMetaAsync(new Google.Protobuf.WellKnownTypes.Empty());
                        Console.WriteLine(meta.DeviceId);
                        Console.WriteLine(meta.DeviceTypeId);
                        Console.WriteLine($"{meta.GridSize.Width}x{meta.GridSize.Height}");

                        foreach (var feature in meta.Features)
                        {
                            Console.Write("+ ");
                            switch (feature.FeatureCase)
                            {
                                case Meta.Types.Feature.FeatureOneofCase.ButtonLabelFeature:
                                    Console.WriteLine($"label:{feature.ButtonLabelFeature.MaxLength}");
                                    break;
                                case Meta.Types.Feature.FeatureOneofCase.ButtonDisplayFeature:
                                    Console.WriteLine($"display:{feature.ButtonDisplayFeature.PreferredResolution.Width}x{feature.ButtonDisplayFeature.PreferredResolution.Height}");
                                    break;
                                case Meta.Types.Feature.FeatureOneofCase.CustomGridFeature:
                                    Console.WriteLine($"custom_grid:{feature.CustomGridFeature.MinSize.Width}x{feature.CustomGridFeature.MinSize.Height}:{feature.CustomGridFeature.MaxSize.Width}x{feature.CustomGridFeature.MaxSize.Height}");
                                    break;
                                case Meta.Types.Feature.FeatureOneofCase.CustomFeature:
                                    Console.WriteLine($"custom:{feature.CustomFeature.Name}");
                                    break;
                                default:
                                    Console.WriteLine("unknown feature");
                                    break;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("unknown command");
                    }
                }
            }
            finally
            {
                // cleanup
                cancel.Cancel();
                await t;
            }
        }
    }
}
