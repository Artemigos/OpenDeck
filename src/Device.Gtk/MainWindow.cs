using System;
using Grpc.Core;
using Gtk;
using OpenDeck.Protocol;
using UI = Gtk.Builder.ObjectAttribute;

namespace OpenDeck.Device.Gtk
{
    public class MainWindow : Window, IButtonEventSource, IButtonLabelSetter, IGridSizeProvider
    {
        private readonly Button[,] _buttons;
        private readonly DefaultDeviceService _srv;
        private readonly Server _server;

        [UI] private Button _btn00 = null;
        [UI] private Button _btn10 = null;
        [UI] private Button _btn20 = null;
        [UI] private Button _btn30 = null;
        [UI] private Button _btn01 = null;
        [UI] private Button _btn11 = null;
        [UI] private Button _btn21 = null;
        [UI] private Button _btn31 = null;
        [UI] private Button _btn02 = null;
        [UI] private Button _btn12 = null;
        [UI] private Button _btn22 = null;
        [UI] private Button _btn32 = null;

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetObject("MainWindow").Handle)
        {
            builder.Autoconnect(this);

            _buttons = new Button[4, 3];
            _buttons[0, 0] = _btn00;
            _buttons[1, 0] = _btn10;
            _buttons[2, 0] = _btn20;
            _buttons[3, 0] = _btn30;
            _buttons[0, 1] = _btn01;
            _buttons[1, 1] = _btn11;
            _buttons[2, 1] = _btn21;
            _buttons[3, 1] = _btn31;
            _buttons[0, 2] = _btn02;
            _buttons[1, 2] = _btn12;
            _buttons[2, 2] = _btn22;
            _buttons[3, 2] = _btn32;

            DeleteEvent += Window_DeleteEvent;

            ForAllButtons((x, y, btn) => btn.Clicked += CreateButtonClickHandler(x, y));
            ForAllButtons((x, y, btn) => btn.Pressed += CreateButtonDownHandler(x, y));
            ForAllButtons((x, y, btn) => btn.Released += CreateButtonUpHandler(x, y));

            _srv = new DefaultDeviceService("tmp-id", "Gtk/0.0.1", this, this, labelSetter: this);
            _server = new Server();
            _server.Services.Add(OpenDeck.Protocol.Device.BindService(_srv));
            _server.Ports.Add(new ServerPort("127.0.0.1", 8020, ServerCredentials.Insecure));
            _server.Start();
        }

        public event EventHandler<ButtonEventArgs> ButtonDown;
        public event EventHandler<ButtonEventArgs> ButtonUp;
        public event EventHandler<ButtonEventArgs> ButtonClick;

        public uint MaxLength => 20;

        public void ForAllButtons(Action<uint, uint, Button> op)
        {
            for (uint y = 0; y < _buttons.GetLength(1); ++y)
                for (uint x = 0; x < _buttons.GetLength(0); ++x)
                    op(x, y, _buttons[x, y]);
        }

        public void SetButtonLabel(uint x, uint y, string label) => _buttons[(int)x, (int)y].Label = label;

        public (uint width, uint height) GetGridSize() => (4, 3);

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            _server.ShutdownAsync().Wait();
            Application.Quit();
        }

        private EventHandler CreateButtonDownHandler(uint x, uint y)
        {
            return ButtonDownHandler;

            void ButtonDownHandler(object sender, EventArgs a)
            {
                ButtonDown?.Invoke(this, new ButtonEventArgs(x, y));
            }
        }

        private EventHandler CreateButtonUpHandler(uint x, uint y)
        {
            return ButtonUpHandler;

            void ButtonUpHandler(object sender, EventArgs a)
            {
                ButtonUp?.Invoke(this, new ButtonEventArgs(x, y));
            }
        }

        private EventHandler CreateButtonClickHandler(uint x, uint y)
        {
            return ButtonClickedHandler;

            void ButtonClickedHandler(object sender, EventArgs a)
            {
                ButtonClick?.Invoke(this, new ButtonEventArgs(x, y));
            }
        }
    }
}
