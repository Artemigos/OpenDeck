using System;
using Grpc.Core;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

namespace OpenDeck.Device.Gtk
{
    public class MainWindow : Window
    {
        private readonly Button[,] _buttons;
        private readonly DeviceServiceImpl _srv;
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

            _srv = new DeviceServiceImpl(this);
            _server = new Server();
            _server.Services.Add(OpenDeck.Protocol.Device.BindService(_srv));
            _server.Ports.Add(new ServerPort("127.0.0.1", 8020, ServerCredentials.Insecure));
            _server.Start();
        }

        public void ForAllButtons(Action<int, int, Button> op)
        {
            for (int y = 0; y < _buttons.GetLength(1); ++y)
                for (int x = 0; x < _buttons.GetLength(0); ++x)
                    op(x, y, _buttons[x, y]);
        }

        public Button GetButton(int x, int y) => _buttons[x, y];

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            _server.ShutdownAsync().Wait();
            Application.Quit();
        }

        private EventHandler CreateButtonDownHandler(int x, int y)
        {
            return ButtonDown;

            void ButtonDown(object sender, EventArgs a)
            {
                _srv.PushDownEvent(x, y);
            }
        }

        private EventHandler CreateButtonUpHandler(int x, int y)
        {
            return ButtonUp;

            void ButtonUp(object sender, EventArgs a)
            {
                _srv.PushUpEvent(x, y);
            }
        }

        private EventHandler CreateButtonClickHandler(int x, int y)
        {
            return ButtonClicked;

            void ButtonClicked(object sender, EventArgs a)
            {
                _srv.PushClickEvent(x, y);
            }
        }
    }
}
