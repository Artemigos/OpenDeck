using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace OpenDeck.Protocol
{
    public class DefaultDeviceService : OpenDeck.Protocol.Device.DeviceBase, IDisposable
    {
        private readonly Task<Empty> EmptyTaskResult = Task.FromResult(new Empty());
        private readonly ConcurrentBag<BlockingCollection<Event>> _subscriptions = new ConcurrentBag<BlockingCollection<Event>>();
        private readonly string _id;
        private readonly string _typeId;
        private readonly IButtonEventSource _eventSource;
        private readonly IGridSizeProvider _gridSizeProvider;
        private readonly IButtonLabelSetter _labelSetter;
        private readonly IButtonImageSetter _imageSetter;
        private readonly IGridSizeSetter _gridSizeSetter;

        public DefaultDeviceService(
            string id,
            string typeId,
            IButtonEventSource eventSource,
            IGridSizeProvider gridSizeProvider,
            IButtonLabelSetter labelSetter = null,
            IButtonImageSetter imageSetter = null,
            IGridSizeSetter gridSizeSetter = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("message", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(typeId))
            {
                throw new ArgumentException("message", nameof(typeId));
            }

            _id = id;
            _typeId = typeId;
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            _gridSizeProvider = gridSizeProvider ?? throw new ArgumentNullException(nameof(gridSizeProvider));
            _labelSetter = labelSetter;
            _imageSetter = imageSetter;
            _gridSizeSetter = gridSizeSetter;

            _eventSource.ButtonDown += OnButtonDown;
            _eventSource.ButtonUp += OnButtonUp;
            _eventSource.ButtonClick += OnButtonClick;
        }

        public override Task<Meta> GetMeta(Empty request, ServerCallContext context)
        {
            var gridSize = _gridSizeProvider.GetGridSize();

            var result = new Meta
            {
                ProtocolVersion = 1,
                DeviceId = _id,
                DeviceTypeId = _typeId,
                GridSize = new Size { Width = gridSize.width, Height = gridSize.height },
            };

            if (_labelSetter != null)
            {
                result.Features.Add(new Meta.Types.Feature
                {
                    ButtonLabelFeature = new Meta.Types.ButtonLabelFeature
                    {
                        MaxLength = _labelSetter.MaxLength
                    }
                });
            }

            if (_imageSetter != null)
            {
                result.Features.Add(new Meta.Types.Feature
                {
                    ButtonDisplayFeature = new Meta.Types.ButtonDisplayFeature
                    {
                        PreferredResolution = ToSize(_imageSetter.PreferredResolution)
                    }
                });
            }

            if (_gridSizeSetter != null)
            {
                result.Features.Add(new Meta.Types.Feature
                {
                    CustomGridFeature = new Meta.Types.CustomGridFeature
                    {
                        MinSize = ToSize(_gridSizeSetter.MinGridSize),
                        MaxSize = ToSize(_gridSizeSetter.MaxGridSize)
                    }
                });
            }

            return Task.FromResult(result);
        }

        public override Task<Empty> SetButtonLabel(SetButtonLabelRequest request, ServerCallContext context)
        {
            if (_labelSetter == null)
                throw new RpcException(new Status(StatusCode.Unimplemented, "The label feature is not supported on this device."));

            CheckButtonPos(request.Button);

            var label = request.Label;
            if (label.Length > _labelSetter.MaxLength)
                label = label.Substring((int)_labelSetter.MaxLength);
            _labelSetter.SetButtonLabel(request.Button.X, request.Button.Y, label);

            return EmptyTaskResult;
        }

        public override Task<Empty> SetButtonImage(SetButtonImageRequest request, ServerCallContext context)
        {
            if (_imageSetter == null)
                throw new RpcException(new Status(StatusCode.Unimplemented, "The image feature is not supported on this device."));

            CheckButtonPos(request.Button);

            _imageSetter.SetButtonImage(
                request.Button.X,
                request.Button.Y,
                request.Image.RgbPixelData.ToByteArray(),
                request.Image.Size.Width,
                request.Image.Size.Height);

            return EmptyTaskResult;
        }

        public override Task<Empty> SetGridSize(Size request, ServerCallContext context)
        {
            if (_gridSizeSetter == null)
                throw new RpcException(new Status(StatusCode.Unimplemented, "The custom grid feature is not supported on this device."));

            var width = Clamp(_gridSizeSetter.MinGridSize.width, _gridSizeSetter.MaxGridSize.width, request.Width);
            var height = Clamp(_gridSizeSetter.MinGridSize.height, _gridSizeSetter.MaxGridSize.height, request.Height);
            _gridSizeSetter.SetGridSize(width, height);

            return EmptyTaskResult;
        }

        public override async Task GetEventStream(Empty request, IServerStreamWriter<Event> responseStream, ServerCallContext context)
        {
            var coll = new BlockingCollection<Event>();
            _subscriptions.Add(coll);

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

            // TODO: somehow remove all subscriptions
        }

        private void OnButtonDown(object sender, ButtonEventArgs buttonEventArgs) => PushToAll(
            new Event
            {
                ButtonDownEvent = new ButtonDownEvent { Button = new ButtonPos { X = (uint)buttonEventArgs.X, Y = (uint)buttonEventArgs.Y } }
            });

        private void OnButtonUp(object sender, ButtonEventArgs buttonEventArgs) => PushToAll(
            new Event
            {
                ButtonUpEvent = new ButtonUpEvent { Button = new ButtonPos { X = (uint)buttonEventArgs.X, Y = (uint)buttonEventArgs.Y } }
            });

        private void OnButtonClick(object sender, ButtonEventArgs buttonEventArgs) => PushToAll(
            new Event
            {
                ButtonClickEvent = new ButtonClickEvent { Button = new ButtonPos { X = (uint)buttonEventArgs.X, Y = (uint)buttonEventArgs.Y } }
            });

        private void PushToAll(Event ev)
        {
            foreach (var coll in _subscriptions)
            {
                coll.Add(ev);
            }
        }

        private void CheckButtonPos(ButtonPos pos)
        {
            var (width, height) = _gridSizeProvider.GetGridSize();
            if (pos.X >= width || pos.Y >= height)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Button position out of grid size."));
        }

        private Size ToSize((uint width, uint height) size) => new Size { Width = size.width, Height = size.height };

        private uint Clamp(uint min, uint max, uint val) => Math.Min(max, Math.Max(min, val));
    }
}

