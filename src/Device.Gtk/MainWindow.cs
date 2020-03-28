using System;
using Grpc.Core;
using Gtk;
using OpenDeck.Protocol;
using UI = Gtk.Builder.ObjectAttribute;

namespace OpenDeck.Device.Gtk
{
    public class MainWindow : Window, IButtonEventSource, IButtonLabelSetter, IGridSizeProvider, IGridSizeSetter
    {
        private readonly DefaultDeviceService _srv;
        private readonly Server _server;

        [UI] private Grid _grid = null;
        private ButtonWrapper[,] _buttons;
        private (uint width, uint height) _size;

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetObject("MainWindow").Handle)
        {
            builder.Autoconnect(this);
            DeleteEvent += Window_DeleteEvent;

            SetGridSize(4, 3);
            (_server, _srv) = StartServer();
        }

        public event EventHandler<ButtonEventArgs> ButtonDown;
        public event EventHandler<ButtonEventArgs> ButtonUp;
        public event EventHandler<ButtonEventArgs> ButtonClick;

        public uint MaxLength => 20;
        public (uint width, uint height) MinGridSize => (1, 1);
        public (uint width, uint height) MaxGridSize => (10, 10);

        public void SetButtonLabel(uint x, uint y, string label) =>
            global::Gtk.Application.Invoke((sender, args) => _buttons[(int)x, (int)y].SetLabel(label));
        public (uint width, uint height) GetGridSize() => _size;

        public void SetGridSize(uint width, uint height)
        {
            global::Gtk.Application.Invoke((sender, args) =>
            {
                var oldButtons = _buttons;
                _buttons = new ButtonWrapper[width, height];

                var oldW = oldButtons?.GetLength(0) ?? 0;
                var oldH = oldButtons?.GetLength(1) ?? 0;

                // fill new buttons array
                ForAll(_buttons, (x, y, _) =>
                {
                    // new array is bigger - the button needs to be created
                    if (x >= oldW || y >= oldH)
                    {
                        _buttons[x, y] = CreateButton(x, y);
                        _grid.Attach(_buttons[x, y].Button, (int)x, (int)y, 1, 1);
                    }
                    // button already exists in old array - reuse
                    else
                    {
                        _buttons[x, y] = oldButtons[x, y];
                    }
                });

                // clean up old buttons
                if (oldButtons != null)
                {
                    ForAll(oldButtons, (x, y, btn) =>
                    {
                        // button wasn't reused - clean up
                        if (x >= width || y >= height)
                        {
                            _grid.Remove(btn.Button);
                            btn.Dispose();
                        }
                    });
                }

                _size = (width, height);
            });
        }

        private void ForAll(ButtonWrapper[,] buttons, Action<uint, uint, ButtonWrapper> op)
        {
            for (uint y = 0; y < buttons.GetLength(1); ++y)
                for (uint x = 0; x < buttons.GetLength(0); ++x)
                    op(x, y, buttons[x, y]);
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            _server.ShutdownAsync().Wait();
            Application.Quit();
        }

        private void ButtonDownHandler(uint x, uint y) => ButtonDown?.Invoke(this, new ButtonEventArgs(x, y));
        private void ButtonUpHandler(uint x, uint y) => ButtonUp?.Invoke(this, new ButtonEventArgs(x, y));
        private void ButtonClickedHandler(uint x, uint y) => ButtonClick?.Invoke(this, new ButtonEventArgs(x, y));
        private ButtonWrapper CreateButton(uint x, uint y) => new ButtonWrapper(x, y, ButtonDownHandler, ButtonUpHandler, ButtonClickedHandler);

        private (Server, DefaultDeviceService) StartServer(string id = "tmp-id", string typeId = "Gtk/0.0.1", string host = "127.0.0.1", int port = 8020)
        {
            var srv = new DefaultDeviceService(id, typeId, this, this, labelSetter: this, gridSizeSetter: this);
            var server = new Server();
            server.Services.Add(OpenDeck.Protocol.Device.BindService(srv));
            server.Ports.Add(new ServerPort(host, port, ServerCredentials.Insecure));
            server.Start();

            return (server, srv);
        }

        private class ButtonWrapper : IDisposable
        {
            private readonly Action<uint, uint> _handleDown;
            private readonly Action<uint, uint> _handleUp;
            private readonly Action<uint, uint> _handleClick;

            public ButtonWrapper(uint x, uint y, Action<uint, uint> handleDown, Action<uint, uint> handleUp, Action<uint, uint> handleClick)
            {
                X = x;
                Y = y;
                _handleDown = handleDown ?? throw new ArgumentNullException(nameof(handleDown));
                _handleUp = handleUp ?? throw new ArgumentNullException(nameof(handleUp));
                _handleClick = handleClick ?? throw new ArgumentNullException(nameof(handleClick));

                Button = new Button
                {
                    Visible = true,
                    CanFocus = true,
                    ReceivesDefault = true,
                };

                Button.Pressed += HandleDown;
                Button.Released += HandleUp;
                Button.Clicked += HandleClick;
            }

            public uint X { get; }
            public uint Y { get; }
            public Button Button { get; }

            public void SetLabel(string label) => Button.Label = label;
            public void SetImage(byte[] image) => throw new NotImplementedException();

            public void Dispose()
            {
                Button.Pressed -= HandleDown;
                Button.Released -= HandleUp;
                Button.Clicked -= HandleClick;
                Button.Dispose();
            }

            private void HandleDown(object sender, EventArgs e) => _handleDown(X, Y);
            private void HandleUp(object sender, EventArgs e) => _handleUp(X, Y);
            private void HandleClick(object sender, EventArgs e) => _handleClick(X, Y);
        }
    }
}
