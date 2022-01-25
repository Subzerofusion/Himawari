using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Drawing;
using System.IO;
using System.Windows.Threading;
using System.Threading;
using Point = System.Drawing.Point;
using System.Windows.Forms;
using Color = System.Drawing.Color;
using Application = System.Windows.Application;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Screen = System.Windows.Forms.Screen;

using Hardcodet.Wpf.TaskbarNotification;

namespace Himawari {
    public class TrayIconViewModel {

        Dictionary<State, Icon> available = new Dictionary<State, Icon>() {
            {State.Idle, Resource.baseline_public_white_ico },
            {State.Busy, Resource.twotone_public_white_ico }
        };
        Dictionary<State, Icon> unavailable = new Dictionary<State, Icon>() {
            {State.Idle, Resource.baseline_public_off_white_ico },
            {State.Busy, Resource.twotone_public_off_white_ico }
        };

        private App App { get { return (App)App.Current; } }
        private TaskbarIcon TrayIcon { get { return App.TrayIcon; } }

        private Task animation;
        CancellationTokenSource tokenSource;
        CancellationToken token;

        DateTime last = DateTime.Now;

        public ObservableProperty<string> CurrentStatus { get; set; } = new ObservableProperty<string>() { Value = "Ready" };

        private State _state;
        private State AppState {
            get { return _state; }
            set {
                if (value == State.Busy && animation == null) {
                    tokenSource = new CancellationTokenSource();
                    token = tokenSource.Token;
                    animation = new Task(() => {
                        while (true) {
                            Thread.Sleep(1000);
                            SetIcon(State.Busy, available);
                            Thread.Sleep(1000);
                            SetIcon(State.Idle, available);
                            if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();
                        }
                    }, tokenSource.Token);
                    animation.Start();
                } else if (value == State.Idle && animation != null) {
                    tokenSource?.Cancel();
                    animation = null;
                    tokenSource?.Dispose();
                    SetIcon(State.Idle, available);
                }
                _state = value;
            }
        }

        enum State {
            Idle, Busy
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e) {
            App.Dispatcher.Invoke(() => {if(AppState == State.Idle) CurrentStatus.Value = $"Ready (Last Image: {DateTime.Now - last} ago)"; });
        }

        public void ComposeWallPaper(Bitmap earth) {
            var screens = Wallpaper.GetScreens();

            // I know there are 4 screens, so I can do this
            // offset points are not cumulative
            // offsets compensate for bezels
            var offsets = new Dictionary<Screen, Point>() {
                { screens[0], new Point(-64, 0) },
                { screens[1], new Point(-18, 0) },
                { screens[2], new Point(64, 0) },
                { screens[3], new Point(18, 0) },
            };

            var space = Wallpaper.CalculateRectangle(screens);
            //earth = earth ?? new Bitmap(GetTempPath("test.png"));
            Bitmap wallpaper = new Bitmap(space.Width, space.Height, PixelFormat.Format32bppArgb);
            wallpaper.Fill(Color.Black);
            foreach (Screen screen in screens) {
                // earthPos is the centre of the planet
                // hard coded 1920 means middle of the two middle screens
                // hard coded -1700 places bottom of earth just above middle task bars
                Point earthPos = new Point(-screen.Bounds.Left + 1920 + offsets[screen].X, -screen.Bounds.Top - 850 + offsets[screen].Y);
                Point earthCentre = new Point(earth.Width / 2, 0);
                Point earthSize = new Point(earth.Width, earth.Height);

                using (Bitmap slice = Wallpaper.GetSlice(screen, earth, earthPos, earthCentre, earthSize)) {
                    wallpaper = Wallpaper.AddSlice(wallpaper, space, screen, slice);
                }
            }
            // wallpaper.Save(GetTempPath($"{time:yyMMdd_HHmmss}.png"));

            Wallpaper.Set(wallpaper, Wallpaper.Style.Span);
            wallpaper.Dispose();
        }

        public async Task FullChain(DateTime time, int zoom) {
            App.Dispatcher.Invoke(() => { CurrentStatus.Value = $"Beginning Scrape {DateTime.Now}"; });
            Bitmap earth = await Scraper.ComposeDisk(time, zoom, yStart: zoom / 2, progress: (msg) => {
                App.Dispatcher.Invoke(() => { CurrentStatus.Value = msg; });
            });
            App.Dispatcher.Invoke(() => { CurrentStatus.Value = "Scraping Done"; });
            ComposeWallPaper(earth);
            earth.Dispose();
            last = DateTime.Now;
            string imgTime = $"{time:HH:mm}";
            imgTime = imgTime.Substring(0, imgTime.Length - 1) + "0";
            App.Dispatcher.Invoke(() => { CurrentStatus.Value = $"Current Image: {imgTime}"; });
        }

        public ICommand GetMostRecentBackground {
            get {
                return new DelegateCommand() {
                    CommandAction = () => {
                        new Task(async () => {
                            if (AppState != State.Busy) {
                                AppState = State.Busy;
                                DateTime time = await Scraper.GetMostRecentTime();
                                await FullChain(time, 10);
                                AppState = State.Idle;
                            }
                        }).Start();
                    }
                };
            }
        }

        void SetIcon(State state, Dictionary<State, Icon> availability) {
            App.Dispatcher.Invoke(() => { TrayIcon.Icon = availability[state]; });
        }

        public ICommand Shutdown {
            get {
                return new DelegateCommand { CommandAction = () => Application.Current.Shutdown() };
            }
        }
    }
}
