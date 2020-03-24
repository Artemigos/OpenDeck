using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OpenDeck.Protocol;

namespace OpenDeck.Device.Gtk
{
    public class DeviceServiceImpl : OpenDeck.Protocol.Device.DeviceBase, IDisposable
    {
        private readonly MainWindow _window;
        private readonly Task<Empty> EmptyTaskResult = Task.FromResult(new Empty());
        private readonly ConcurrentBag<BlockingCollection<Event>> _subscriptions = new ConcurrentBag<BlockingCollection<Event>>();

        public DeviceServiceImpl(MainWindow window)
        {
            _window = window;
        }

        public void PushDownEvent(int x, int y) => PushToAll(
                new Event
                {
                    ButtonDownEvent = new ButtonDownEvent { Button = new ButtonPos { X = (uint)x, Y = (uint)y } }
                });

        public void PushUpEvent(int x, int y) => PushToAll(
                new Event
                {
                    ButtonUpEvent = new ButtonUpEvent { Button = new ButtonPos { X = (uint)x, Y = (uint)y } }
                });

        public void PushClickEvent(int x, int y) => PushToAll(
                new Event
                {
                    ButtonClickEvent = new ButtonClickEvent { Button = new ButtonPos { X = (uint)x, Y = (uint)y } }
                });

        private void PushToAll(Event ev)
        {
            foreach (var coll in _subscriptions)
            {
                coll.Add(ev);
            }
        }

        public override Task<Meta> GetMeta(Empty request, ServerCallContext context) => Task.FromResult(
            new Meta
            {
                DeviceId = "tmp-id",
                DeviceTypeId = "Gtk/0.0.1",
                GridSize = new Size { Width = 4, Height = 3 },
                ProtocolVersion = 1,
                Features =
                {
                    new Meta.Types.Feature { ButtonLabelFeature = new Meta.Types.ButtonLabelFeature { MaxLength = 20 } }
                }
            });

        public override Task<Empty> SetButtonLabel(SetButtonLabelRequest request, ServerCallContext context)
        {
            _window.GetButton((int)request.Button.X, (int)request.Button.Y).Label = request.Label;
            return EmptyTaskResult;
        }

        public override async Task GetEventStream(Empty request, IServerStreamWriter<Event> responseStream, ServerCallContext context)
        {
            var coll = new BlockingCollection<Event>();
            _subscriptions.Add(coll);
            // TODO: this class needs to be disposable and the collections completed to clean up running requests

            try
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var ev = coll.Take(context.CancellationToken);
                    await responseStream.WriteAsync(ev);
                }
            }
            finally
            {
                // TODO: somehow remove the subscription in a thread-safe way
            }
        }

        public void Dispose()
        {
            foreach (var coll in _subscriptions)
            {
                coll.CompleteAdding();
            }
        }
    }
}
